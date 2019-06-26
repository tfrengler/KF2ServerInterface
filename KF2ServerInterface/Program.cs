using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace KF2ServerInterface
{
    class Program
    {
        public const string GLOBAL_DESIRED_MAP = "KF-SteamFortress";
        public const string SERVER_ADDRESS = "http://80.101.134.182"; //192.168.1.222
        private static readonly TimeSpan CHECK_INTERVAL = new TimeSpan(0,5,0);
        private const int STALLED_THRESHOLD = 3;
        private const string USER = "admin";
        private const string PASSWORD = "ThomaS_86954494?";

        static async Task Main(string[] args)
        {
            KF2ServerHandler serverHandler = new KF2ServerHandler();
            KF2ServerInstance[] servers = new KF2ServerInstance[6];

            Dictionary<string, int> stalledServerStatus = new Dictionary<string, int>();
            
            #region SERVER INSTANCES
            servers.SetValue(new KF2ServerInstance("PRIVATE", 8000, "Endless", "Private", GLOBAL_DESIRED_MAP), 0);
            servers.SetValue(new KF2ServerInstance("HARD", 8001, "Survival", "Hard", GLOBAL_DESIRED_MAP), 1);
            servers.SetValue(new KF2ServerInstance("SUICIDAL", 8002, "Survival", "Suicidal", GLOBAL_DESIRED_MAP), 2);
            servers.SetValue(new KF2ServerInstance("HELL ON EARTH", 8003, "Survival", "HoE", GLOBAL_DESIRED_MAP), 3);
            servers.SetValue(new KF2ServerInstance("WEEKLY", 8004, "WeeklySurvival", "Weekly", GLOBAL_DESIRED_MAP), 4);
            servers.SetValue(new KF2ServerInstance("OBJECTIVE", 8005, "Objective", "ObjectiveHard", GLOBAL_DESIRED_MAP), 5);
            #endregion

            foreach (KF2ServerInstance currentServer in servers)
                stalledServerStatus.Add(currentServer.Name, 0);

            while (true)
            {
                Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Waking up, going to check servers" + Environment.NewLine);

                foreach (KF2ServerInstance currentServer in servers)
                {
                    Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Name: {currentServer.Name} | Address: {SERVER_ADDRESS}:{currentServer.Port} | Gamemode: {currentServer.Gamemode} | Desired map: {currentServer.DesiredMap}");

                    if (currentServer.Down)
                    {
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} WARNING - Server is down, not checking" + Environment.NewLine);
                        continue;
                    }

                    bool isServerUp = await serverHandler.IsServerResponding(SERVER_ADDRESS, currentServer.Port);

                    if (!isServerUp)
                    {
                        if (stalledServerStatus[currentServer.Name] >= STALLED_THRESHOLD)
                        {
                            Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} WARNING - Server is unresponsive after {STALLED_THRESHOLD} attempts. Marking as down" + Environment.NewLine);
                            currentServer.Down = true;
                            continue;
                        }
                        else
                            stalledServerStatus[currentServer.Name]++;

                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} WARNING - Server is not responding ({stalledServerStatus[currentServer.Name]}). It is either down, or switching maps" + Environment.NewLine);
                        continue;
                    }

                    stalledServerStatus[currentServer.Name] = 0;
                    bool isAuthenticated = await serverHandler.AreWeAuthenticated(SERVER_ADDRESS, currentServer.Port);

                    if (!isAuthenticated)
                    {
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} We are not authenticated");

                        bool newSession = await serverHandler.SetSessionID(SERVER_ADDRESS, currentServer.Port);
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} New sessionid acquired: {newSession}");

                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Looking for login token");
                        string loginToken = await serverHandler.GetLoginToken(SERVER_ADDRESS, currentServer.Port);

                        if (loginToken.Length == 0)
                        {
                            Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} ERROR - Failed to fetch login token" + Environment.NewLine);
                            continue;
                        }

                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Logging in (TOKEN: {loginToken})");

                        bool loginSuccess = await serverHandler.Login(SERVER_ADDRESS, currentServer.Port, loginToken, USER, PASSWORD);
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Logged in: {loginSuccess}");

                        if (!loginSuccess)
                        {
                            Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} ERROR - Login failed" + Environment.NewLine);
                            continue;
                        }
                    }
                    else
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} We are already authenticated, no need to log in");

                    int playerCount = await serverHandler.GetPlayerCount(SERVER_ADDRESS, currentServer.Port);
                    string currentMap = await serverHandler.GetCurrentMap(SERVER_ADDRESS, currentServer.Port);

                    if (playerCount > 0)
                    {
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Server has active players ({playerCount}) on map {currentMap}, not switching" + Environment.NewLine);
                        continue;
                    }

                    if (playerCount == 0 && currentMap != currentServer.DesiredMap)
                    {
                        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Server is empty and on the wrong map ({currentMap}), changing to {currentServer.DesiredMap} instead");
                        bool mapSwitched = await serverHandler.SwitchMap(SERVER_ADDRESS, currentServer.Port, currentServer.Gamemode, currentServer.DesiredMap, currentServer.ConfigDir);

                        if (mapSwitched)
                            Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Map switch successful" + Environment.NewLine);
                        else
                            Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} ERROR - Map switch not succesful" + Environment.NewLine);

                        continue;
                    }

                    Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Server is empty but on the right map" + Environment.NewLine);
                }

                Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]:")} Server checking done, going back to sleep ({CHECK_INTERVAL})");
                Thread.Sleep(CHECK_INTERVAL);
            }
        }
    }
}
