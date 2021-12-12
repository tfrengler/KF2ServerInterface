using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace KF2ServerInterface
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Contains("-logToFile"))
                Logger.LogToFile = true;
            else
                Logger.LogToFile = false;

            Config.ConfigLoadResult ConfigLoad = Config.GetConfiguration();
            if (!ConfigLoad.Success)
            {
                Logger.Log(ConfigLoad.Error, Logger.LogType.ERROR);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();

                return;
            }

            Config Configuration = ConfigLoad.Result;

            Console.WriteLine("Configuration loaded:" + Environment.NewLine);

            Console.WriteLine($"Desired map: {Configuration.DesiredMap}");
            Console.WriteLine($"Global server address: {Configuration.ServerAddress}");
            Console.WriteLine($"Server check interval: {Configuration.ServerCheckInterval}");
            Console.WriteLine($"Server unreponsive threshold: {Configuration.ServerUnreponsiveThreshold}");
            Console.WriteLine($"User name: {Configuration.UserName}");
            Console.WriteLine($"Password: {Configuration.Password}");
            Console.WriteLine($"Servers: {Configuration.Servers.Length}" + Environment.NewLine);

            KF2ServerHandler serverHandler = new KF2ServerHandler();
            KF2ServerInstance[] servers = new KF2ServerInstance[Configuration.Servers.Length];

            int Index = 0;
            foreach (Config.ServerInstanceConfig currentServerConfig in Configuration.Servers)
            {
                servers.SetValue(
                    new KF2ServerInstance(
                        currentServerConfig.Name,
                        currentServerConfig.Port,
                        currentServerConfig.DesiredMap,
                        currentServerConfig.Disabled
                    ),
                    Index
                );
                Index++;
            }

            Dictionary<string, int> stalledServerStatus = new Dictionary<string, int>();

            foreach (KF2ServerInstance currentServer in servers)
                stalledServerStatus.Add(currentServer.Name, 0);

            while (true)
            {
                Console.Clear();
                Logger.Log("Waking up, going to check servers" + Environment.NewLine);

                foreach (KF2ServerInstance currentServer in servers)
                {
                    Logger.Log($"Name: {currentServer.Name} | Address: {Configuration.ServerAddress}:{currentServer.Port} | Desired map: {currentServer.DesiredMap}");

                    if (currentServer.Disabled)
                    {
                        Logger.Log("Server is disabled, not checking" + Environment.NewLine);
                        continue;
                    }

                    if (currentServer.Down)
                    {
                        Logger.Log("Server is down, not checking" + Environment.NewLine, Logger.LogType.WARNING);
                        continue;
                    }

                    bool isServerUp = await serverHandler.IsServerResponding(Configuration.ServerAddress, currentServer.Port);

                    if (!isServerUp)
                    {
                        if (stalledServerStatus[currentServer.Name] >= Configuration.ServerUnreponsiveThreshold)
                        {
                            Logger.Log($"Server is unresponsive after {Configuration.ServerUnreponsiveThreshold} attempts. Marking as down" + Environment.NewLine, Logger.LogType.WARNING);
                            currentServer.Down = true;
                            continue;
                        }
                        else
                            stalledServerStatus[currentServer.Name]++;

                        Logger.Log($"Server is not responding ({stalledServerStatus[currentServer.Name]}). It is either down, or switching maps" + Environment.NewLine, Logger.LogType.WARNING);
                        continue;
                    }

                    stalledServerStatus[currentServer.Name] = 0;
                    bool isAuthenticated = await serverHandler.AreWeAuthenticated(Configuration.ServerAddress, currentServer.Port);

                    if (!isAuthenticated)
                    {
                        Logger.Log("We are not authenticated");

                        bool newSession = await serverHandler.SetSessionID(Configuration.ServerAddress, currentServer.Port);
                        Logger.Log($"New sessionid acquired: {newSession}");

                        Logger.Log("Looking for login token");
                        string loginToken = await serverHandler.GetLoginToken(Configuration.ServerAddress, currentServer.Port);

                        if (loginToken.Length == 0)
                        {
                            Logger.Log("Failed to fetch login token" + Environment.NewLine, Logger.LogType.ERROR);
                            continue;
                        }

                        Logger.Log($"Logging in (TOKEN: {loginToken})");

                        bool loginSuccess = await serverHandler.Login(Configuration.ServerAddress, currentServer.Port, loginToken, Configuration.UserName, Configuration.Password);
                        Logger.Log($"Logged in: {loginSuccess}");

                        if (!loginSuccess)
                        {
                            Logger.Log("Login failed" + Environment.NewLine, Logger.LogType.ERROR);
                            continue;
                        }
                    }
                    else
                        Logger.Log("We are already authenticated, no need to log in");

                    short playerCount = await serverHandler.GetPlayerCount(Configuration.ServerAddress, currentServer.Port);
                    string currentMap = await serverHandler.GetCurrentMap(Configuration.ServerAddress, currentServer.Port);

                    if (playerCount > 0)
                    {
                        Logger.Log($"Server has active players ({playerCount}) on map {currentMap}, leaving it alone" + Environment.NewLine);
                        continue;
                    }

                    if (playerCount == 0 && currentMap != currentServer.DesiredMap)
                    {
                        var GetGameModeAndExtraConfig = await serverHandler.GetGameModeAndExtraConfig(Configuration.ServerAddress, currentServer.Port);
                        if (GetGameModeAndExtraConfig == null)
                            continue;

                        Logger.Log($"Server is empty and on the wrong map ({currentMap}), changing to {currentServer.DesiredMap} instead (MODE: {GetGameModeAndExtraConfig.Item1} | URL EXTRA: {GetGameModeAndExtraConfig.Item2})");
                        bool mapSwitched = await serverHandler.SwitchMap(Configuration.ServerAddress, currentServer.Port, $"KFGameContent.KFGameInfo_{GetGameModeAndExtraConfig.Item1}", currentServer.DesiredMap, GetGameModeAndExtraConfig.Item2);

                        if (mapSwitched)
                            Logger.Log("Map switch successful" + Environment.NewLine);
                        else
                            Logger.Log("Map switch not succesful" + Environment.NewLine, Logger.LogType.ERROR);

                        continue;
                    }

                    Logger.Log("Server is empty but on the right map" + Environment.NewLine);
                }

                Logger.Log($"Server checking done, going back to sleep ({Configuration.ServerCheckInterval})");
                await Task.Delay(Configuration.ServerCheckInterval);
            }

        }
    }
}
