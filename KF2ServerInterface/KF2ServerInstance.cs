﻿using System;
using System.Linq;

namespace KF2ServerInterface
{
    class KF2ServerInstance
    {
        public string Name { get; }
        public int Port { get; }
        public string Gamemode { get; }
        public string ConfigDir { get; }
        public string DesiredMap { get; }
        public bool Down { get; set; }
        public bool Disabled { get; set; }

        static public string[] GameModes { get; } = new string[]
        {
            "Endless",
            "Survival",
            "Objective",
            "WeeklySurvival"
        };

        static public string[] Maps { get; } = new string[]
        {
            "KF-Airship",
            "KF-BioticsLab",
            "KF-BlackForest",
            "KF-BurningParis",
            "KF-Catacombs",
            "KF-ContainmentStation",
            "KF-DieSector",
            "KF-EvacuationPoint",
            "KF-Farmhouse",
            "KF-HostileGrounds",
            "KF-InfernalRealm",
            "KF-KrampusLair",
            "KF-Lockdown",
            "KF-MonsterBall",
            "KF-Nightmare",
            "KF-Nuked",
            "KF-Outpost",
            "KF-PowerCore_Holdout",
            "KF-Prison",
            "KF-SantasWorkshop",
            "KF-ShoppingSpree",
            "KF-Spillway",
            "KF-SteamFortress",
            "KF-TheDescent",
            "KF-TragicKingdom",
            "KF-VolterManor",
            "KF-ZedLanding",
            "KF-AshwoodAsylum",
            "KF-Sanitarium",
            "KF-Biolapse",
            "KF-Desolation"
        };

        public KF2ServerInstance(string name, int port, string gamemode, string configDir, string desiredMap, bool disabled)
        {
            //if (!Maps.Contains(desiredMap))
            //    throw new Exception($"The map you passed is not a valid KF2 map: {desiredMap}");

            if (!GameModes.Contains(gamemode))
                throw new Exception($"The gamemode you passed is not valid: {gamemode}");

            Name = name;
            Port = port;
            Gamemode = "KFGameContent.KFGameInfo_" + gamemode;
            ConfigDir = configDir;
            DesiredMap = desiredMap;
            Disabled = disabled;
        }
    }
}
