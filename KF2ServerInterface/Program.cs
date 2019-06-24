#define DEBUG

using System;
using System.Threading;
using System.Threading.Tasks;

namespace KF2ServerInterface
{
    class Program
    {
        const string GLOBAL_DESIRED_MAP = "KF-SteamFortress";
        const string SERVER_ADDRESS = "http://192.168.1.222";
        const int CHECK_INTERVAL = 300000;

        static void Main(string[] args)
        {
            KF2ServerHandler KF2Server = new KF2ServerHandler();
            KF2ServerHandler.Server[] servers = new KF2ServerHandler.Server[5];

            #region SERVER INSTANCES
            servers.SetValue(new KF2ServerHandler.Server
            {
                name = "PRIVATE",
                gamemode = "KFGameContent.KFGameInfo_Endless",
                address = new Uri(Program.SERVER_ADDRESS + ":8000/ServerAdmin/"),
                configDir = "Private",
                desiredMap = Program.GLOBAL_DESIRED_MAP
            }, 0);

            servers.SetValue(new KF2ServerHandler.Server
            {
                name = "HARD",
                gamemode = "KFGameContent.KFGameInfo_Survival",
                address = new Uri(Program.SERVER_ADDRESS + ":8001/ServerAdmin/"),
                configDir = "Hard",
                desiredMap = Program.GLOBAL_DESIRED_MAP
            }, 1);

            servers.SetValue(new KF2ServerHandler.Server
            {
                name = "SUICIDAL",
                gamemode = "KFGameContent.KFGameInfo_Survival",
                address = new Uri(Program.SERVER_ADDRESS + ":8002/ServerAdmin/"),
                configDir = "Suicidal",
                desiredMap = Program.GLOBAL_DESIRED_MAP
            }, 2);

            servers.SetValue(new KF2ServerHandler.Server
            {
                name = "HELL ON EARTH",
                gamemode = "KFGameContent.KFGameInfo_Survival",
                address = new Uri(Program.SERVER_ADDRESS + ":8003/ServerAdmin/"),
                configDir = "HoE",
                desiredMap = Program.GLOBAL_DESIRED_MAP
            }, 3);

            servers.SetValue(new KF2ServerHandler.Server
            {
                name = "WEEKLY",
                gamemode = "KFGameContent.KFGameInfo_Weekly",
                address = new Uri(Program.SERVER_ADDRESS + ":8004/ServerAdmin/"),
                configDir = "Weekly",
                desiredMap = Program.GLOBAL_DESIRED_MAP
            }, 4);
            #endregion

            while (true)
            {
                foreach (KF2ServerHandler.Server currentServer in servers)
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
                        /*
                        Task<bool> mapSwitchTask = KF2Server.SwitchMap(currentServer.desiredMap, currentServer);
                        while (!mapSwitchTask.IsCompleted) ;

                        if (mapSwitchTask.Result)
                            Console.WriteLine("Map switch successful");
                        */
                    }
                    else
                    {
                        Console.WriteLine($"Server has {playerCount} players on map {currentMap}, doing nothing" + Environment.NewLine);
                    }
                }

                Console.WriteLine($"Waiting... ({Program.CHECK_INTERVAL / 1000} seconds)");
                Console.WriteLine("-------------------------------------------------------------" + Environment.NewLine);
                Thread.Sleep(Program.CHECK_INTERVAL);
            }
        }
    }
}
