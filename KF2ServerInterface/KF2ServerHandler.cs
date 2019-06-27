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

        #endregion

        /// <summary>Returns an instance of the handler for the KF2 server instances</summary>
        public KF2ServerHandler()
        {
            cookies = new CookieContainer();

            HttpClientHandler handler = new HttpClientHandler();
            handler.AllowAutoRedirect = false;
            handler.CookieContainer = cookies;

            client = new HttpClient(handler);
            client.Timeout = new TimeSpan(0,0,10);
        }

#region PUBLIC METHODS
        public async Task<bool> SetSessionID(string serverAddress, int port)
        {
            HttpResponseMessage serverResponse = await SendGetRequest(serverAddress, port, LOGIN_PAGE);
            string sessionID = GetHeaderValue(serverResponse.Headers, "Set-Cookie", "sessionid");

            if (sessionID.Length == 0)
                return false;

            cookies.Add(new Uri(serverAddress + ":" + port), new Cookie("sessionid", sessionID));
            return true;
        }

        public async Task<bool> IsServerResponding(string serverAddress, int port)
        {
            try
            {
                HttpResponseMessage serverResponse = await SendGetRequest(serverAddress, port, LOGIN_PAGE);
                if (serverResponse.IsSuccessStatusCode || serverResponse.StatusCode == HttpStatusCode.Redirect)
                    return true;
            }
            catch(TaskCanceledException error)
            {
                //Console.WriteLine(error.CancellationToken.IsCancellationRequested); Timeout has been reached
            }

            return false;
        }

        public async Task<bool> AreWeAuthenticated(string serverAddress, int port)
        {
            HttpResponseMessage response = await SendGetRequest(serverAddress, port, INFO_PAGE);
            string[] match = await GetContentBodyMatch(response.Content, "<form id=\"loginform\"");

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

            if (loginResponse.StatusCode != HttpStatusCode.Redirect)
                return false;

            string authToken = GetHeaderValue(loginResponse.Headers, "Set-Cookie", "authcred");

            if (authToken.Length == 0)
                return false;

            cookies.Add(new Uri(serverAddress + ":" + port), new Cookie("authcred", authToken));
            return true;
        }

        public async Task<int> GetPlayerCount(string address, int port)
        {
            HttpResponseMessage infoPageResponse = await this.SendGetRequest(address, port, INFO_PAGE);
            string responseBody = await infoPageResponse.Content.ReadAsStringAsync();

            Match playerCountSearch = new Regex("<dl id=\"currentRules\">[\\s\\S]+?<dd>(\\d)\\/6</dd>").Match(responseBody);

            if (!playerCountSearch.Success)
                return -1;

            int playerCount = Int16.Parse(playerCountSearch.Groups[1].Value);
            return playerCount;
        }

        public async Task<string> GetCurrentMap(string address, int port)
        {
            HttpResponseMessage httpResponse = await this.SendGetRequest(address, port, CHANGE_PAGE);
            if (httpResponse.StatusCode != HttpStatusCode.OK)
                return "";

            string responseBody = await httpResponse.Content.ReadAsStringAsync();
            Match currentMapSearch = new Regex("<select id=\"map\" name=\"map\">[\\s\\S]+?<option value=\"(.*)\" selected=\"selected\">").Match(responseBody);

            if (!currentMapSearch.Success)
                return "";

            return currentMapSearch.Groups[1].Value;
        }

        public async Task<bool> SwitchMap(string address, int port, string gamemode, string newMap, string configDir)
        {
            Dictionary<string, string> postData = new Dictionary<string, string>
            {
                { "gametype", gamemode },
                { "map", newMap },
                { "mutatorGroupCount", "0" },
                { "urlextra", $"?ConfigSubDir={configDir}" },
                { "action", "change" }
            };

            HttpResponseMessage httpResponse = await SendPostRequest(address, port, postData, CHANGE_PAGE);
            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            string responseBody = await httpResponse.Content.ReadAsStringAsync();
            Match changedMapSearch = new Regex("Changing the game. This could take a little while...").Match(responseBody);

            if (changedMapSearch.Success)
                return true;

            return false;
        }
#endregion

#region PRIVATE METHODS
        private async Task<HttpResponseMessage> SendGetRequest(string serverAddress, int port, string appendPath = "")
        {          
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Get, new Uri($"{serverAddress}:{port}{appendPath}"));
            HttpResponseMessage returnData;

            try {
                returnData = await client.SendAsync(httpRequest);
            }
            catch(HttpRequestException error) {
                returnData = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            };

            return returnData;
        }

        private async Task<HttpResponseMessage> SendPostRequest(string serverAddress, int port, Dictionary<string, string> postData, string appendPath = "")
        {
            HttpContent formData = new FormUrlEncodedContent(postData);
            HttpResponseMessage returnData;

            try
            {
                returnData = await client.PostAsync(new Uri($"{serverAddress}:{port}{appendPath}"), formData);
            }
            catch (HttpRequestException error)
            {
                returnData = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            };

            return returnData;
        }

        private string GetHeaderValue(HttpResponseHeaders headers, string headerName, string headerKey)
        {
            if (!headers.TryGetValues(headerName, out IEnumerable<string> headerOut))
                return "";

            string headerValues = string.Join(",", headerOut.ToArray());
            Match valueSearch = new Regex($"{headerKey}=\"(.*)\"").Match(headerValues);

            if (valueSearch.Success)
                return valueSearch.Groups[1].Value;

            return "";
        }

        private async Task<string[]> GetContentBodyMatch(HttpContent contentBody, string regexString)
        {
            string stringContent = await contentBody.ReadAsStringAsync();
            Match search = new Regex(regexString).Match(stringContent);

            if (!search.Success)
                return new string[0];
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
