#define DEBUG

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
        private readonly HttpClient client;

        private const string LOGIN_PAGE = "/ServerAdmin/";
        private const string CHANGE_PAGE = "/ServerAdmin/current/change";
        private const string INFO_PAGE = "/ServerAdmin/current/info";

        public string SessionID { get; private set; } = "";
        public string AuthToken { get; private set; } = "";

        #endregion

        /// <summary>Returns an instance of the handler for the KF2 server instances</summary>
        public KF2ServerHandler()
        {
            this.cookies = new CookieContainer();
            HttpClientHandler handler = new HttpClientHandler();
            handler.AllowAutoRedirect = false;
            handler.CookieContainer = cookies;
            this.client = new HttpClient(handler);
        }

        #region PUBLIC METHODS
        public async Task<string> GetSessionID(string serverAddress, int port)
        {
            HttpResponseMessage serverResponse = await SendGetRequest(serverAddress, port, LOGIN_PAGE);
            string sessionID = GetHeaderValue(serverResponse.Headers, "Set-Cookie", "sessionid");

            if (sessionID.Length > 0)
                return sessionID;

            return "";
        }

        public async Task<bool> IsServerResponding(string serverAddress, int port)
        {
            HttpResponseMessage serverResponse = await SendGetRequest(serverAddress, port, LOGIN_PAGE);
            if (serverResponse.IsSuccessStatusCode || serverResponse.StatusCode == HttpStatusCode.Redirect)
                return true;

            return false;
        }

        public async Task<bool> AreWeAuthenticated(string serverAddress, int port)
        {
            HttpResponseMessage response = await SendGetRequest(serverAddress, port, INFO_PAGE);
            string[] match = await GetContentBodyMatch(response.Content, "<form id=\"loginform\"");

#if DEBUG
            Logger.DumpHttpHeaders(response);
            Logger.DumpResponseContent(response.Content);
            Logger.LogToFile("STATUS CODE: " + response.StatusCode.ToString());
#endif

            if (match.Length == 0)
                return true;

            return false;
        }

        public async Task<string> GetLoginToken(string serverAddress, int port)
        {
            HttpResponseMessage loginPageResponse = await this.SendGetRequest(serverAddress, port, LOGIN_PAGE);
            if (loginPageResponse.StatusCode != HttpStatusCode.OK) return "";

            string[] loginToken = await GetContentBodyMatch(loginPageResponse.Content, "name=\"token\" value=\"(.*)\"");

            if (loginToken.Length > 0)
                return loginToken[1];

            return "";
        }

        public async Task<bool> Login(string serverAddress, int port, string token, string username, string password)
        {
            Dictionary<string, string> postData = new Dictionary<string, string>
            {
                { "token", token },
                { "password_hash", "" },
                { "username", username },
                { "password", password },
                { "remember", "-1" }
            };

            HttpResponseMessage loginResponse = await SendPostRequest(serverAddress, port, postData, LOGIN_PAGE);
#if DEBUG
            Logger.DumpHttpHeaders(loginResponse);
            Logger.DumpResponseContent(loginResponse.Content);
            Logger.LogToFile("STATUS CODE: " + loginResponse.StatusCode.ToString());
#endif
            if (loginResponse.StatusCode != HttpStatusCode.Redirect)
            {
                Logger.LogToFile($"Login(): Status code was not Redirect as expected but rather {loginResponse.StatusCode.ToString()}");
                return false;
            }
            string authToken = this.GetHeaderValue(loginResponse.Headers, "Set-Cookie", "authcred");

            if (authToken.Length == 0)
            {
                Logger.LogToFile($"Login(): No authcred cookie received after login attempt");
                return false;
            }

            cookies.Add(new Uri(serverAddress + ":" + port), new Cookie("authcred", authToken));
            Logger.LogToFile($"Login(): Login successful. Authcred received: {authToken}");
            return true;
        }
        /*
        private void DumpCookies(Uri serverAddress)
        {
            Console.WriteLine("COOKIE DUMP:--------------------------------");

            foreach (Cookie cookie in this.cookies.GetCookies(serverAddress))
                Console.WriteLine($"{cookie.Name}: {cookie.Value}");

            Console.WriteLine("END DUMP:--------------------------------");
        }

        

        public void RotateSession(string serverAddress, string newSessionID)
        {
            foreach (Cookie cookie in this.cookies.GetCookies(new Uri(serverAddress)))
                cookie.Expired = true;

            this.cookies.Add(new Uri(serverAddress), new Cookie("sessionid", newSessionID));
        }
        /*
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

        public async Task<bool> SwitchMap(string mapName, KF2ServerInstance server)
        {
            Dictionary<string, string> postData = new Dictionary<string, string>
            {
                { "gametype", server.gamemode },
                { "map", mapName },
                { "mutatorGroupCount", "0" },
                { "urlextra", $"?ConfigSubDir={server.configDir}" },
                { "action", "change" }
            };

            HttpResponseMessage httpResponse = await this.SendPostRequest(new Uri(server.address + "current/change"), postData);
            if (httpResponse.StatusCode != HttpStatusCode.OK) return false;

            string newSessionID = this.GetHeaderValue(httpResponse.Headers, "Set-Cookie", "sessionid");

            if (newSessionID.Length > 0)
            {
                this.ClearSessionData(server.address);
                this.cookies.Add(server.address, new Cookie("sessionid", newSessionID));
            }

            string responseBody = await httpResponse.Content.ReadAsStringAsync();
            Match changedMapSearch = new Regex("Changing the game. This could take a little while...").Match(responseBody);

            if (changedMapSearch.Success)
                return true;

            return false;
        }
        */
#endregion

#region PRIVATE METHODS
        private async Task<HttpResponseMessage> SendGetRequest(string serverAddress, int port, string appendPath = "")
        {
            Console.WriteLine($"Sending GET request to: {serverAddress}:{port}{appendPath}");

            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Get, new Uri($"{serverAddress}:{port}{appendPath}"));
            //httpRequest.Headers.Add("host", "KF2ServerHandler");
            HttpResponseMessage returnData;

            try {
                returnData = await client.SendAsync(httpRequest);
            }
            catch(HttpRequestException error) {
                Console.WriteLine($"ERROR: Sending GET request to {serverAddress}:{port}{appendPath} || {error.Message}");
                returnData = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            };

            return returnData;
        }

        private async Task<HttpResponseMessage> SendPostRequest(string serverAddress, int port, Dictionary<string, string> postData, string appendPath = "")
        {
            HttpContent formData = new FormUrlEncodedContent(postData);
            //formData.Headers.Add("host", "KF2ServerHandler");
            HttpResponseMessage returnData;

            try
            {
                returnData = await client.PostAsync(new Uri($"{serverAddress}:{port}{appendPath}"), formData);
            }
            catch (HttpRequestException error)
            {
                Console.WriteLine($"ERROR: Sending POST request to {serverAddress}:{port}{appendPath} || {error.Message}");
                returnData = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            };

            return returnData;
        }

        private string GetHeaderValue(HttpResponseHeaders headers, string headerName, string headerKey)
        {
            if (!headers.TryGetValues(headerName, out IEnumerable<string> headerOut))
            {
                Console.WriteLine($"ERROR: No such header exists in the response header collection: {headerName}");
                return "";
            }

            string headerValues = string.Join(",", headerOut.ToArray());
            Match valueSearch = new Regex($"{headerKey}=\"(.*)\"").Match(headerValues);

            if (valueSearch.Success)
                return valueSearch.Groups[1].Value;

            Console.WriteLine($"ERROR: No key called {headerKey} exists in header {headerName}");
            return "";
        }

        private async Task<string[]> GetContentBodyMatch(HttpContent contentBody, string regexString)
        {
            string stringContent = await contentBody.ReadAsStringAsync();
            Match search = new Regex(regexString).Match(stringContent);

            if (!search.Success)
            {
                Console.WriteLine($"No match on regex string: {regexString}");
                return new string[0];
            }
            else
            {
                string[] returnData = new string[search.Groups.Count];
                for (int index = 0; index < search.Groups.Count; index++)
                    returnData[index] = search.Groups[index].Value;

                return returnData;
            }
        }
#endregion
    }
}
