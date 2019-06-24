using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KF2ServerInterface
{
    public class KF2ServerHandler
    {
        #region PROPERTIES
        private readonly CookieContainer cookies;
        private readonly HttpClientHandler handler;
        private readonly HttpClient httpClient;

        public struct Server
        {
            public string name;
            public Uri address;
            public string gamemode;
            public string configDir;
            public string desiredMap;
        }

        public string[] maps { get; } = new string[]
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
            "KF-ZedLanding"
        };
        #endregion

        //CONSTRUCTOR
        public KF2ServerHandler()
        {
            this.cookies = new CookieContainer();
            this.handler = new HttpClientHandler();
            this.handler.AllowAutoRedirect = false;
            this.handler.CookieContainer = cookies;
            this.httpClient = new HttpClient(handler);
        }

        public async Task<string> GetLoginToken(Uri serverAddress)
        {
            HttpResponseMessage loginPageResponse = await this.SendGetRequest(serverAddress);
            if (loginPageResponse.StatusCode != HttpStatusCode.OK) return "";

            string sessionID = this.GetHeaderValue(loginPageResponse.Headers, "Set-Cookie", "sessionid");
            if (sessionID.Length == 0) return "";

            string responseBody = await loginPageResponse.Content.ReadAsStringAsync();

            Match tokenSearch = new Regex("name=\"token\" value=\"(.*)\"").Match(responseBody);
            if (!tokenSearch.Success) return "";

            this.cookies.Add(serverAddress, new Cookie("sessionid", sessionID));
            return tokenSearch.Groups[1].Value;
        }

        public async Task<string> Login(Uri serverAddress, string token, string username, string password)
        {
            Dictionary<string, string> postData = new Dictionary<string, string>
            {
                { "token", token },
                { "password_hash", "" },
                { "username", username },
                { "password", password },
                { "remember", "-1" }
            };

            HttpResponseMessage loginResponse = await this.SendPostRequest(serverAddress, postData);
            if (loginResponse.StatusCode != HttpStatusCode.OK) return "";
            string authToken = this.GetHeaderValue(loginResponse.Headers, "Set-Cookie", "authcred");

            if (authToken.Length == 0)
                return "";

            this.cookies.Add(serverAddress, new Cookie("authcred", authToken));
            return authToken;
        }

        private async void DumpResponseHeadersAndBody(HttpResponseMessage response)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
            {
                Console.WriteLine($"{header.Key}: {String.Join(" ", header.Value)}");
            }
            string responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseBody);
        }

        private void ClearSessionData(Uri address)
        {
            foreach (Cookie cookie in this.cookies.GetCookies(address))
                cookie.Expired = true;
        }

        public async Task<int> GetPlayerCount(Uri serverAddress)
        {
            HttpResponseMessage infoPageResponse = await this.SendGetRequest(new Uri(serverAddress + "current/info"));
            string responseBody = await infoPageResponse.Content.ReadAsStringAsync();

            Match playerCountSearch = new Regex("<dl id=\"currentRules\">[\\s\\S]+?<dd>(\\d)\\/6</dd>").Match(responseBody);

            if (!playerCountSearch.Success)
                return -1;

            int playerCount = Int16.Parse(playerCountSearch.Groups[1].Value);
            Console.WriteLine("Player count: " + playerCount);
            return playerCount;
        }

        public async Task<string> GetCurrentMap(Uri serverAddress)
        {
            HttpResponseMessage httpResponse = await this.SendGetRequest(new Uri(serverAddress + "current/change"));
            if (httpResponse.StatusCode != HttpStatusCode.OK)
                return "";

            string responseBody = await httpResponse.Content.ReadAsStringAsync();
            Match currentMapSearch = new Regex("<select id=\"map\" name=\"map\">[\\s\\S]+?<option value=\"(.*)\" selected=\"selected\">").Match(responseBody);

            if (!currentMapSearch.Success)
                return "";

            return currentMapSearch.Groups[1].Value;
        }

        public async Task<bool> SwitchMap(string mapName, Server serverInstance)
        {
            if (!this.maps.Contains(mapName)) return false;

            Dictionary<string, string> postData = new Dictionary<string, string>
            {
                { "gametype", serverInstance.gamemode },
                { "map", mapName },
                { "mutatorGroupCount", "0" },
                { "urlextra", $"?ConfigSubDir={serverInstance.configDir}" },
                { "action", "change" }
            };

            HttpResponseMessage httpResponse = await this.SendPostRequest(new Uri(serverInstance.address + "current/change"), postData);
            if (httpResponse.StatusCode != HttpStatusCode.OK) return false;

            string newSessionID = this.GetHeaderValue(httpResponse.Headers, "Set-Cookie", "sessionid");

            if (newSessionID.Length > 0)
            {
                this.ClearSessionData(serverInstance.address);
                this.cookies.Add(serverInstance.address, new Cookie("sessionid", newSessionID));
            }

            string responseBody = await httpResponse.Content.ReadAsStringAsync();
            Match changedMapSearch = new Regex("Changing the game. This could take a little while...").Match(responseBody);

            if (changedMapSearch.Success)
                return true;

            return false;
        }

        private async Task<HttpResponseMessage> SendGetRequest(Uri address)
        {
            HttpResponseMessage httpResponse;

            try { 
                httpResponse = await httpClient.GetAsync(address);
                return httpResponse;
            }
            catch(HttpRequestException error) {
                Console.WriteLine($"ERROR: Sending GET request to {address} failed");
                Console.WriteLine(error.Message);
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            };
        }

        private async Task<HttpResponseMessage> SendPostRequest(Uri address, Dictionary<string, string> postData)
        {
            HttpResponseMessage httpResponse;
            HttpContent formData = new FormUrlEncodedContent(postData);
            try
            {
                httpResponse = await httpClient.PostAsync(address, formData);
                return httpResponse;
            }
            catch(HttpRequestException error)
            {
                Console.WriteLine($"ERROR: Sending POST request to {address} failed");
                Console.WriteLine(error.Message);
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            };
        }

        private string GetHeaderValue(HttpResponseHeaders headers, string headerName, string headerKey)
        {
            if (!headers.TryGetValues(headerName, out var headerOut))
                return "";

            string headerValues = String.Join(",", headerOut.ToArray());
            Match valueSearch = new Regex($"{headerKey}=\"(.*)\"").Match(headerValues);

            if (valueSearch.Success)
                return valueSearch.Groups[1].Value;

            return "";
        }
    }
}
