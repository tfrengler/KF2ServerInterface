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

            Task<bool> IsServerUp = KF2Server.IsServerResponding(SERVER_ADDRESS, servers[3].Port);
            while (!IsServerUp.IsCompleted) ;
            Console.WriteLine("IS SERVER RESPONDING: " + IsServerUp.Result);

            Task<bool> Authenticated = KF2Server.AreWeAuthenticated(SERVER_ADDRESS, servers[3].Port);
            while (!Authenticated.IsCompleted) ;
            Console.WriteLine("ARE WE AUTHENTICATED: " + Authenticated.Result);
            /*
            Task<string> sessionID = KF2Server.GetSessionID(SERVER_ADDRESS, servers[3].Port);
            while (!sessionID.IsCompleted) ;
            Console.WriteLine($"DO WE HAVE A NEW SESSION ID: {sessionID.Result.Length > 0}");

            Task<string> token = KF2Server.GetLoginToken(SERVER_ADDRESS, servers[3].Port);
            while (!token.IsCompleted) ;
            Console.WriteLine($"LOGIN TOKEN: {token.Result}");

            Task<bool> login = KF2Server.Login(SERVER_ADDRESS, servers[3].Port, token.Result, "admin", "ThomaS_86954494?");
            while (!login.IsCompleted) ;
            Console.WriteLine($"LOGGED IN: {login.Result}");
            */
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
