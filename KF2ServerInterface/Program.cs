#define DEBUG

using System;
using System.Threading;
using System.Threading.Tasks;

namespace KF2ServerInterface
{
    class Program
    {
        public const string GLOBAL_DESIRED_MAP = "KF-SteamFortress";
        public const string SERVER_ADDRESS = "http://80.101.134.182"; //192.168.1.222
        private const int CHECK_INTERVAL = 300000;

        static void Main(string[] args)
        {
            KF2ServerHandler KF2Server = new KF2ServerHandler();
            KF2ServerInstance[] servers = new KF2ServerInstance[6];

            #region SERVER INSTANCES
            servers.SetValue(new KF2ServerInstance("PRIVATE", 8000, "Endless", "Private", Program.GLOBAL_DESIRED_MAP), 0);
            servers.SetValue(new KF2ServerInstance("HARD", 8001, "Survival", "Hard", Program.GLOBAL_DESIRED_MAP), 1);
            servers.SetValue(new KF2ServerInstance("SUICIDAL", 8002, "Survival", "Suicidal", Program.GLOBAL_DESIRED_MAP), 2);
            servers.SetValue(new KF2ServerInstance("HELL ON EARTH", 8003, "Survival", "HoE", Program.GLOBAL_DESIRED_MAP), 3);
            servers.SetValue(new KF2ServerInstance("WEEKLY", 8004, "WeeklySurvival", "Weekly", Program.GLOBAL_DESIRED_MAP), 4);
            servers.SetValue(new KF2ServerInstance("OBJECTIVE", 8005, "Objective", "ObjectiveHard", Program.GLOBAL_DESIRED_MAP), 5);
            #endregion

            Task<bool> serverUp = KF2Server.IsServerResponding(SERVER_ADDRESS, servers[3].Port);
            while (!serverUp.IsCompleted) ;
            Console.WriteLine("IS SERVER RESPONDING:" + serverUp.Result);

            Task<bool> haveSession = KF2Server.AreWeAuthenticated(SERVER_ADDRESS, servers[3].Port);
            while (!haveSession.IsCompleted) ;
            Console.WriteLine("ARE WE AUTHENTICATED: " + haveSession.Result);

            Task<string> sessionID = KF2Server.GetSessionID(SERVER_ADDRESS, servers[3].Port);
            while (!sessionID.IsCompleted) ;
            Console.WriteLine($"DO WE HAVE A NEW SESSION ID: {sessionID.Result.Length > 0}");

            Task<bool> login = KF2Server.Login(SERVER_ADDRESS, servers[3].Port, "admin", "ThomaS_86954494?");
            while (!login.IsCompleted) ;
            Console.WriteLine($"LOGGED IN: {login.Result}");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            /*
            while (true)
            {
                foreach (KF2ServerHandler.ServerInstance currentServer in servers)
                {
                    Console.WriteLine($"---------------------CHECKING SERVER: {currentServer.name}");

                    Console.WriteLine("Getting login token");
                    Task<string> tokenTask = KF2Server.GetLoginToken(currentServer.address);
                    while (!tokenTask.IsCompleted) ;

                    string loginToken = tokenTask.Result;

                    if (loginToken.Length == 0)
                    {
                        Console.WriteLine("ERROR: No login token acquired" + Environment.NewLine);
                        continue;
                    }

                    Console.WriteLine($"SUCCESS: Login token acquired ({loginToken})");

                    Console.WriteLine("Logging in");
                    Task<string> loginTask = KF2Server.Login(currentServer.address, loginToken, "admin", "ThomaS_86954494?");
                    while (!loginTask.IsCompleted) ;

                    string authToken = loginTask.Result;

                    if (authToken.Length == 0)
                    {
                        Console.WriteLine("ERROR: No auth token acquired" + Environment.NewLine);
                        continue;
                    }

                    Console.WriteLine($"SUCCESS: Logged in ({authToken})");

                    Task<int> getPlayerCountTask = KF2Server.GetPlayerCount(currentServer.address);
                    while (!getPlayerCountTask.IsCompleted) ;
                    int playerCount = getPlayerCountTask.Result;

                    Task<string> getCurrentMapTask = KF2Server.GetCurrentMap(currentServer.address);
                    while (!getCurrentMapTask.IsCompleted) ;
                    string currentMap = getCurrentMapTask.Result;

                    if (playerCount == 0 && currentMap != currentServer.desiredMap)
                    {
                        Console.WriteLine($"Server is empty and not on the right map ({currentMap}). Switching to: {currentServer.desiredMap}" + Environment.NewLine);
                        
                        Task<bool> mapSwitchTask = KF2Server.SwitchMap(currentServer.desiredMap, currentServer);
                        while (!mapSwitchTask.IsCompleted) ;

                        if (mapSwitchTask.Result)
                            Console.WriteLine("Map switch successful");
                        
                    }
                    else
                    {
                        Console.WriteLine($"Server has {playerCount} players on map {currentMap}, doing nothing" + Environment.NewLine);
                    }
                }
                Console.WriteLine($"Waiting... ({Program.CHECK_INTERVAL / 1000} seconds)");
                Console.WriteLine("-------------------------------------------------------------" + Environment.NewLine);
                Thread.Sleep(Program.CHECK_INTERVAL);
            }*/
        }
    }
}
