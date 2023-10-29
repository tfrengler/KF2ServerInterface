/* PLACE IN: gamedir/KGFame/Config/ and run to auto (re)generate config-folder and -files, as well as start up scripts */

using System.Threading;

public string GetGameIni(byte Difficulty, string ServerFullName, string ServerShortName) {
    return $@"[Configuration]
BasedOn=..\%GAME%Game\Config\LinuxServer-KFGame.ini

[Engine.GameInfo]
MaxPlayers=6
GameDifficulty={Difficulty}.0
bChangeLevels=True
MaxSpectators=1
MaxIdleTime=180.000000
TotalNetBandwidth=90000
MaxDynamicBandwidth=15000
MinDynamicBandwidth=6000
KickVotePercentage=0.600000
TimeBetweenFailedVotes=10.0
VoteTime=60.0

[Engine.GameReplicationInfo]
ServerName={ServerFullName}
ShortName={ServerShortName}

[Engine.AccessControl]
AdminPassword=ThomaS_86954494?
GamePassword=

[KFGame.KFGameInfo]
GameLength=2
MinNetPlayers=1
bWaitForNetPlayers=true
EndOfGameDelay=60
FriendlyFireScale=0.000000
KickVotePercentage=0.600000
TimeBetweenFailedVotes=10.000000
VoteTime=30.0
MapVoteDuration=60.000000
ServerMOTD=Private server hosted in The Netherlands. No regular up and down times. Have fun and be awesome to each other!
bDisableKickVote=False
bDisableVOIP=True
BadPingThreshold=150
MinNetPlayers=1
ReadyUpDelay=30
GameStartDelay=10
EndOfGameDelay=60
";
}

public string GetEngineIni(uint ListenPort, uint QueryPort, uint WebAdminPort)
{
    return $@"[Configuration]
BasedOn=..\%GAME%Game\Config\LinuxServer-KFEngine.ini

[IpDrv.TcpNetDriver]
MaxClientRate=15000
MaxInternetClientRate=15000
NetServerMaxTickRate=66
LanServerMaxTickRate=66

[IPDrv.WebServer]
ListenPort={WebAdminPort}
bEnabled=True

[URL]
Port={ListenPort}
PeerPort={QueryPort}

[Engine.GameEngine]
bUsedForTakeover=False

[OnlineSubsystemSteamworks.OnlineSubsystemSteamworks]
bUseVAC=true
Region=3";
}

/* Inheriting from Config/DefaultWeb.ini does not seem to work. This is a full copy of DefaultWeb anno Aug 2020 */
public string GetWebIni(uint WebAdminPort)
{
    return $@"[IpDrv.WebConnection]
MaxValueLength=4096
MaxLineLength=4096

[IpDrv.WebServer]
Applications[0]=WebAdmin.KF2ServerAdmin
Applications[1]=WebAdmin.KF2ImageServer
ApplicationPaths[0]=/ServerAdmin
ApplicationPaths[1]=/images
ListenPort={WebAdminPort}
MaxConnections=18
ExpirationSeconds=86400
bEnabled=True

[IpDrv.WebResponse]
IncludePath=/KFGame/Web
";
}

public string GetStartScript(GameMode gameMode, uint QueryPort, uint GamePort, string ConfigDirName)
{
    string GameModeString;

    switch(gameMode)
    {
        case GameMode.SURVIVAL:
            GameModeString = "";
            break;

        case GameMode.ENDLESS:
            GameModeString = "?Game=KFGameContent.KFGameInfo_Endless";
            break;

        case GameMode.OBJECTIVE:
            GameModeString = "?Game=KFGameContent.KFGameInfo_Objective";
            break;

        case GameMode.WEEKLY:
            GameModeString = "?Game=KFGameContent.KFGameInfo_WeeklySurvival";
            break;

        default:
            throw new ArgumentException("GameMode is invalid");
    }

    return $@"#!/bin/bash

echo 'Starting KF2 instance - {ConfigDirName}. What level do you want to start with?'
read level

./Binaries/Win64/KFGameSteamServer.bin.x86_64 $level{GameModeString}?Port={GamePort}?QueryPort={QueryPort}?ConfigSubDir={ConfigDirName}";
}

struct ServerInfo {
    public GameMode Mode;
    public byte Difficulty;
    public string ServerFullName;
    public string ServerShortName;
    public uint ListenPort;
    public uint QueryPort;
    public uint WebAdminPort;
    public string ConfigDirName;
};

public enum GameMode {
    SURVIVAL,
    ENDLESS,
    OBJECTIVE,
    WEEKLY
};

/*
0 = Normal
1 = Hard
2 = Suicidal
3 = Hell on Earth
*/

var Servers = new ServerInfo[]
{
    //PRIVATE:
    new ServerInfo {
        Mode=GameMode.SURVIVAL,
        Difficulty=2,
        ServerFullName="#Rock, Dosh, Shotgun PRIVATE",
        ServerShortName="RDSP",
        ListenPort=27080,
        QueryPort=27082,
        WebAdminPort=8000,
        ConfigDirName="Private",
    },
    //HARD:
    new ServerInfo {
        Mode=GameMode.SURVIVAL,
        Difficulty=1,
        ServerFullName="#Rock, Dosh, Shotgun 01",
        ServerShortName="RDS1",
        ListenPort=27084,
        QueryPort=27086,
        WebAdminPort=8001,
        ConfigDirName="Hard"
    },
    //SUICIDAL:
    new ServerInfo {
        Mode=GameMode.SURVIVAL,
        Difficulty=2,
        ServerFullName="#Rock, Dosh, Shotgun 02",
        ServerShortName="RDS2",
        ListenPort=27088,
        QueryPort=27090,
        WebAdminPort=8002,
        ConfigDirName="Suicidal"
    },
    //ENDLESS (old HoE server):
    new ServerInfo {
        Mode=GameMode.ENDLESS, // Endless has no standard difficulty, as the game ramps up during waves
        Difficulty=0,
        ServerFullName="#Rock, Dosh, Shotgun 03",
        ServerShortName="RDS3",
        ListenPort=27092,
        QueryPort=27094,
        WebAdminPort=8003,
        ConfigDirName="Endless"
    },
    //WEEKLY:
    new ServerInfo {
        Mode=GameMode.WEEKLY,
        Difficulty=0, // Weekly has dynamic difficulty, depending on the outbreak type
        ServerFullName="#Rock, Dosh, Shotgun 04",
        ServerShortName="RDS4",
        ListenPort=27096,
        QueryPort=27098,
        WebAdminPort=8004,
        ConfigDirName="Weekly"
    },
    //OBJECTIVE:
    new ServerInfo {
        Mode=GameMode.OBJECTIVE,
        Difficulty=1,
        ServerFullName="#Rock, Dosh, Shotgun 05",
        ServerShortName="RDS5",
        ListenPort=27076,
        QueryPort=27078,
        WebAdminPort=8005,
        ConfigDirName="Objective"
    }
};

// This should be {gameRoot}/KFGame/Config/
var RootDir = new DirectoryInfo(Environment.CurrentDirectory);
if (RootDir.Name != "Config")
    throw new Exception($"Script does not appear to be run from within the Config-folder ({RootDir.FullName})");

foreach(var CurrentServer in Servers)
{
    WriteLine($"-----------------------------------{Environment.NewLine}Processing server: " + CurrentServer.ConfigDirName);
    var WorkingDir = new DirectoryInfo(RootDir + "/" + CurrentServer.ConfigDirName);

    WriteLine($"Re-creating config dir");
    if (WorkingDir.Exists) {
        WriteLine("Deleted old dir");
        WorkingDir.Delete(true);
    }

    Thread.Sleep(500);
    WorkingDir.Refresh();
    WorkingDir.Create();

    WriteLine("Creating ini files");
    File.WriteAllText(WorkingDir.FullName + "/LinuxServer-KFGame.ini", GetGameIni(CurrentServer.Difficulty, CurrentServer.ServerFullName, CurrentServer.ServerShortName));
    File.WriteAllText(WorkingDir.FullName + "/LinuxServer-KFEngine.ini", GetEngineIni(CurrentServer.ListenPort, CurrentServer.QueryPort, CurrentServer.WebAdminPort));
    File.WriteAllText(WorkingDir.FullName + "/KFWeb.ini", GetWebIni(CurrentServer.WebAdminPort));

    WriteLine("Creating server start script file");
    var KF2RootDir = RootDir.Parent.Parent;
    string StartServerScriptFile = "Server" + CurrentServer.ConfigDirName + ".sh";
    File.WriteAllText(KF2RootDir.FullName + "/" + StartServerScriptFile, GetStartScript(CurrentServer.Mode, CurrentServer.QueryPort, CurrentServer.ListenPort, CurrentServer.ConfigDirName));

    WriteLine("Server setup done" + Environment.NewLine);
}

WriteLine("All done!");