using System;
using System.IO;
using System.Xml;

namespace KF2ServerInterface
{
    public class Config
    {
        #region STATIC PROPERTIES

        public static string FILE_NAME = "Config.xml";

        #endregion

        #region INSTANCE PROPERTIES

        public string DesiredMap { get; }
        public string ServerAddress { get; }
        public TimeSpan ServerCheckInterval { get; }
        public int ServerUnreponsiveThreshold { get; }
        public string UserName { get; }
        public string Password { get; }
        public ServerInstanceConfig[] Servers { get; }

        #endregion

        #region DATA STRUCTURES

        public class GlobalConfig
        {
            public string DesiredMap { get; set; }
            public string ServerAddress { get; set; }
            public TimeSpan ServerCheckInterval { get; set; }
            public int ServerUnreponsiveThreshold { get; set; }
            public string UserName { get; set; }
            public string Password { get; set; }
            public ServerInstanceConfig[] Servers { get; set; }
        }

        public class ServerInstanceConfig
        {
            public string Name { get; set; }
            public int Port { get; set; }
            public string Gamemode { get; set; }
            public string ConfigDir { get; set; }
            public string DesiredMap { get; set; }
            public bool Disabled { get; set; }
        }

        public struct ConfigLoadResult
        {
            public bool Success { get; set; }
            public Config Result { get; set; }
            public string Error { get; set; }
        }

        #endregion

        #region CONSTRUCTORS

        public Config(GlobalConfig globalSettings, ServerInstanceConfig[] servers)
        {
            DesiredMap = (globalSettings.DesiredMap.Length > 0 ? globalSettings.DesiredMap : "kf-bioticslab");
            ServerAddress = (globalSettings.ServerAddress.Length > 0 ? globalSettings.ServerAddress : "http://127.0.0.1");
            ServerCheckInterval = globalSettings.ServerCheckInterval;
            ServerUnreponsiveThreshold = (globalSettings.ServerUnreponsiveThreshold > 0 ? globalSettings.ServerUnreponsiveThreshold : 3);
            UserName = (globalSettings.UserName.Length > 0 ? globalSettings.UserName : "UNKNOWN");
            Password = (globalSettings.Password.Length > 0 ? globalSettings.Password : "UNKNOWN");
            Servers = servers;
        }

        /// <summary>
        /// Default constructor, for when errors occur during parsing / validating the config file
        /// </summary>
        public Config()
        {
            DesiredMap = "ERROR";
            ServerAddress = "ERROR";
            ServerCheckInterval = new TimeSpan(0,0,0);
            ServerUnreponsiveThreshold = 0;
            UserName = "ERROR";
            Password = "ERROR";
            Servers = null;
        }

        #endregion

        #region STATIC METHODS

        public static ConfigLoadResult GetConfiguration(string fileName)
        {
            ConfigLoadResult returnData = new ConfigLoadResult();

            if (!File.Exists(fileName))
            {
                returnData.Result = new Config();
                returnData.Success = false;
                returnData.Error = $"Config file does not exist: {fileName}";

                return returnData;
            }

            XmlDocument configContents = new XmlDocument();

            try
            {
                configContents.Load(fileName);
            }
            catch(AggregateException error)
            {
                returnData.Result = new Config();
                returnData.Success = false;
                returnData.Error = $"Error opening/parsing config file: {error.Message}";

                Logger.Log($"Error opening/parsing config file: {error.Message}", Logger.LogType.ERROR);

                return returnData;
            }

            GlobalConfig GlobalSettings;

            try
            {
                GlobalSettings = GetGlobalConfig(configContents);
            }
            catch(Exception error)
            {
                returnData.Result = new Config();
                returnData.Success = false;
                returnData.Error = $"Error parsing global-settings: {error.Message}";

                Logger.Log($"Error parsing global-settings: {error.Message}", Logger.LogType.ERROR);
                Logger.Log(error.StackTrace, Logger.LogType.ERROR);

                return returnData;
            }

            ServerInstanceConfig[] ServerConfigs;

            try
            {
                ServerConfigs = GetServerInstanceConfig(configContents);
            }
            catch (Exception error)
            {
                returnData.Result = new Config();
                returnData.Success = false;
                returnData.Error = $"Error parsing server instance-settings: {error.Message}";

                Logger.Log($"Error parsing server instance-settings: {error.Message}", Logger.LogType.ERROR);
                Logger.Log(error.StackTrace, Logger.LogType.ERROR);

                return returnData;
            }

            // Defaulting to the global desired map if a map is not defined by the instance-settings itself
            foreach (ServerInstanceConfig CurrentServer in ServerConfigs)
                if (CurrentServer.DesiredMap == null || (CurrentServer.DesiredMap is string && CurrentServer.DesiredMap.Length == 0))
                    CurrentServer.DesiredMap = GlobalSettings.DesiredMap;

            returnData.Success = true;
            returnData.Result = new Config(GlobalSettings, ServerConfigs);

            return returnData;
        }

        #endregion

        private static GlobalConfig GetGlobalConfig(XmlDocument configContents)
        {
            XmlNodeList ElementSearch = configContents.GetElementsByTagName("Global");
            if (ElementSearch.Count == 0)
                throw new Exception("Error loading configuration: the Global-element is missing");

            GlobalConfig GlobalSettings = new GlobalConfig();

            foreach (XmlNode GlobalSetting in ElementSearch.Item(0).ChildNodes)
            {
                switch (GlobalSetting.Name)
                {
                    case "DesiredMap":
                        GlobalSettings.DesiredMap = GlobalSetting.InnerText;
                        break;
                    case "ServerAddress":
                        GlobalSettings.ServerAddress = GlobalSetting.InnerText;
                        break;
                    case "ServerCheckInterval":
                        GlobalSettings.ServerCheckInterval = new TimeSpan(0, 0, int.Parse(GlobalSetting.InnerText));
                        break;
                    case "ServerUnreponsiveThreshold":
                        GlobalSettings.ServerUnreponsiveThreshold = int.Parse(GlobalSetting.InnerText);
                        break;
                    case "UserName":
                        GlobalSettings.UserName = GlobalSetting.InnerText;
                        break;
                    case "Password":
                        GlobalSettings.Password = GlobalSetting.InnerText;
                        break;

                    default: break;
                }
            }

            return GlobalSettings;
        }

        private static ServerInstanceConfig[] GetServerInstanceConfig(XmlDocument configContents)
        {
            XmlNodeList ElementSearch = configContents.GetElementsByTagName("KF2Servers");
            if (ElementSearch.Count == 0)
                throw new Exception("Error loading configuration: the KF2Servers-element is missing");

            XmlNodeList KF2ServerInstanceElements = ElementSearch.Item(0).SelectNodes("KF2ServerInstance");

            if (KF2ServerInstanceElements.Count == 0)
                throw new Exception("Error loading configuration: the KF2Servers-element does not contain any KF2ServerInstance-elements");

            ServerInstanceConfig[] ServerConfigs = new ServerInstanceConfig[KF2ServerInstanceElements.Count];
            int Index = 0;

            foreach (XmlNode KF2ServerInstance in KF2ServerInstanceElements)
            {
                ServerConfigs[Index] = new ServerInstanceConfig();

                foreach (XmlNode InstanceSetting in KF2ServerInstance.ChildNodes)
                {
                    switch (InstanceSetting.Name)
                    {
                        case "Name":
                            ServerConfigs[Index].Name = InstanceSetting.InnerText;
                            break;
                        case "Port":
                            ServerConfigs[Index].Port = int.Parse(InstanceSetting.InnerText);
                            break;
                        case "Gamemode":
                            ServerConfigs[Index].Gamemode = InstanceSetting.InnerText;
                            break;
                        case "ConfigDir":
                            ServerConfigs[Index].ConfigDir = InstanceSetting.InnerText;
                            break;
                        case "DesiredMap":
                            ServerConfigs[Index].DesiredMap = InstanceSetting.InnerText;
                            break;
                        case "Disabled":
                            ServerConfigs[Index].Disabled = Convert.ToBoolean(InstanceSetting.InnerText);
                            break;

                        default: break;
                    }
                }

                Index++;
            }

            return ServerConfigs;
        }
    }
}
