using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace KF2ServerInterface
{
    class Program
    {

        static async Task Main(string[] args)
        {

            Config.ConfigLoadResult ConfigLoad = Config.GetConfiguration(Config.FILE_NAME);
            if (!ConfigLoad.Success)
            {
                Logger.Log(ConfigLoad.Error, Logger.LogType.ERROR);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
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
                        currentServerConfig.Gamemode,
                        currentServerConfig.ConfigDir,
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
                Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Waking up, going to check servers" + Environment.NewLine);

                foreach (KF2ServerInstance currentServer in servers)
                {
                    Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Name: {currentServer.Name} | Address: {Configuration.ServerAddress}:{currentServer.Port} | Gamemode: {currentServer.Gamemode} | Desired map: {currentServer.DesiredMap}");

                    if (currentServer.Disabled)
                    {
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Server is disabled, not checking" + Environment.NewLine);
                        continue;
                    }

                    if (currentServer.Down)
                    {
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} WARNING - Server is down, not checking" + Environment.NewLine);
                        continue;
                    }

                    bool isServerUp = await serverHandler.IsServerResponding(Configuration.ServerAddress, currentServer.Port);

                    if (!isServerUp)
                    {
                        if (stalledServerStatus[currentServer.Name] >= Configuration.ServerUnreponsiveThreshold)
                        {
                            Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} WARNING - Server is unresponsive after {Configuration.ServerUnreponsiveThreshold} attempts. Marking as down" + Environment.NewLine);
                            currentServer.Down = true;
                            continue;
                        }
                        else
                            stalledServerStatus[currentServer.Name]++;

                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} WARNING - Server is not responding ({stalledServerStatus[currentServer.Name]}). It is either down, or switching maps" + Environment.NewLine);
                        continue;
                    }

                    stalledServerStatus[currentServer.Name] = 0;
                    bool isAuthenticated = await serverHandler.AreWeAuthenticated(Configuration.ServerAddress, currentServer.Port);

                    if (!isAuthenticated)
                    {
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} We are not authenticated");

                        bool newSession = await serverHandler.SetSessionID(Configuration.ServerAddress, currentServer.Port);
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} New sessionid acquired: {newSession}");

                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Looking for login token");
                        string loginToken = await serverHandler.GetLoginToken(Configuration.ServerAddress, currentServer.Port);

                        if (loginToken.Length == 0)
                        {
                            Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} ERROR - Failed to fetch login token" + Environment.NewLine);
                            continue;
                        }

                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Logging in (TOKEN: {loginToken})");

                        bool loginSuccess = await serverHandler.Login(Configuration.ServerAddress, currentServer.Port, loginToken, Configuration.UserName, Configuration.Password);
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Logged in: {loginSuccess}");

                        if (!loginSuccess)
                        {
                            Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} ERROR - Login failed" + Environment.NewLine);
                            continue;
                        }
                    }
                    else
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} We are already authenticated, no need to log in");

                    int playerCount = await serverHandler.GetPlayerCount(Configuration.ServerAddress, currentServer.Port);
                    string currentMap = await serverHandler.GetCurrentMap(Configuration.ServerAddress, currentServer.Port);

                    if (playerCount > 0)
                    {
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Server has active players ({playerCount}) on map {currentMap}, not switching" + Environment.NewLine);
                        continue;
                    }

                    if (playerCount == 0 && currentMap != currentServer.DesiredMap)
                    {
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Server is empty and on the wrong map ({currentMap}), changing to {currentServer.DesiredMap} instead");
                        bool mapSwitched = await serverHandler.SwitchMap(Configuration.ServerAddress, currentServer.Port, currentServer.Gamemode, currentServer.DesiredMap, currentServer.ConfigDir);

                        if (mapSwitched)
                            Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Map switch successful" + Environment.NewLine);
                        else
                            Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} ERROR - Map switch not succesful" + Environment.NewLine);

                        continue;
                    }

                    Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Server is empty but on the right map" + Environment.NewLine);
                }

                Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Server checking done, going back to sleep ({Configuration.ServerCheckInterval})");
                Thread.Sleep(Configuration.ServerCheckInterval);
            }
            
        }
    }
}
