package main

import (
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/http/cookiejar"
	"net/url"
	"os"
	"regexp"
	"strconv"
	"time"
)

type GameMode string

const (
	Endless        GameMode = "KFGameContent.KFGameInfo_Endless"
	Objective      GameMode = "KFGameContent.KFGameInfo_Objective"
	Survival       GameMode = "KFGameContent.KFGameInfo_Survival"
	VersusSurvival GameMode = "KFGameContent.KFGameInfo_VersusSurvival"
	WeeklySurvival GameMode = "KFGameContent.KFGameInfo_WeeklySurvival"
)

type Config struct {
	DesiredMap    string
	ServerAddress string
	Username      string
	Password      string
	GameMode      GameMode
	Servers       *[]Server
}

type Server struct {
	Name       string
	Port       int
	DesiredMap string
	GameMode   GameMode
	Disabled   bool
}

var Configuration Config

var EntryPoint *url.URL
var Client *http.Client
var LoginPage = "/ServerAdmin"
var ServerStatusPage = "/ServerAdmin/current/info"
var GamesummaryPage = "/ServerAdmin/current+gamesummary"

func main() {

	var cookieJar, _ = cookiejar.New(nil)
	Client = &http.Client{
		Jar: cookieJar,
		CheckRedirect: func(req *http.Request, via []*http.Request) error {
			return http.ErrUseLastResponse
		},
		Timeout: time.Duration(time.Duration.Seconds(20)),
	}

	// login("admin", "123")
	// fmt.Println(getServerData())

	loadConfig()
}

func loadConfig() {
	var data, readError = os.ReadFile("config.json")

	if readError != nil {
		panic(fmt.Sprintf("Error reading config: %s", readError))
	}

	var jsonError = json.Unmarshal(data, &Configuration)

	if jsonError != nil {
		panic(fmt.Sprintf("Error parsing config: %s", jsonError))
	}

	var entryPoint, urlParseError = url.Parse(Configuration.ServerAddress)
	if urlParseError != nil {
		panic(fmt.Sprintf("Error parsing config ServerAddress: %s\n", urlParseError))
	}
	EntryPoint = entryPoint

	var test, _ = json.MarshalIndent(Configuration, "", "    ")
	fmt.Println(string(test))
}

func isLoggedIn(address url.URL) bool {
	var response, responseError = Client.Get(EntryPoint.String() + ServerStatusPage)

	if responseError != nil {
		panic(responseError)
	}

	return response.StatusCode == http.StatusForbidden
}

func login(username string, password string) {
	var response, responseError = Client.Get(EntryPoint.String() + LoginPage)

	if responseError != nil {
		panic(responseError)
	}

	var responseContent, readError = io.ReadAll(response.Body)
	defer response.Body.Close()

	if readError != nil {
		panic(readError)
	}

	var sessionCookie = response.Cookies()[0]
	tokenRegex, _ := regexp.Compile("name=\"token\"\\svalue=\"(.+)\"")
	var token = tokenRegex.FindStringSubmatch(string(responseContent))[1]

	Client.Jar.SetCookies(EntryPoint, []*http.Cookie{sessionCookie})

	var requestBody = url.Values{}
	requestBody.Set("token", token)
	requestBody.Set("username", username)
	requestBody.Set("password", password)
	requestBody.Set("remember", "-1")
	requestBody.Set("password_hash", "")

	fmt.Println("Login request body: " + requestBody.Encode())

	response, responseError = Client.PostForm(EntryPoint.String()+LoginPage, requestBody)

	if responseError != nil {
		panic(fmt.Sprintf("Post to login failed: %s\n", responseError))
	}

	var responseCookies = response.Cookies()
	var authCookie *http.Cookie
	if len(responseCookies) > 0 {
		authCookie = responseCookies[0]
	}

	if authCookie == nil || (authCookie != nil && authCookie.Name != "authcred") {
		panic("Login failed. No auth cookie after call to log in")
	}

	Client.Jar.SetCookies(EntryPoint, []*http.Cookie{responseCookies[0]})
}

func getServerData() (int, string) {
	var response, responseError = Client.PostForm(EntryPoint.String()+GamesummaryPage, url.Values{"ajax": {"1"}})

	if responseError != nil || response.StatusCode != http.StatusOK {
		fmt.Printf("Call to gamesummary failed (status: %s | error: %s)\n", response.Status, responseError)
	}

	var responseContent, readError = io.ReadAll(response.Body)
	defer response.Body.Close()

	if readError != nil {
		fmt.Printf("Reading gamesummary response failed: %s\n", readError)
	}

	playerCountRegex, _ := regexp.Compile(`gs_players.+(\d{1})\/`)
	var playerCountString = playerCountRegex.FindStringSubmatch(string(responseContent))[1]

	mapRegex, _ := regexp.Compile("dd.+gs_map\">([\\w\\s]+)<")
	var mapName = mapRegex.FindStringSubmatch(string(responseContent))[1]

	assertStringNotEqual("", mapName)
	assertStringNotEqual("", playerCountString)

	var playerCount, parseError = strconv.Atoi(playerCountString)

	if parseError != nil {
		fmt.Printf("Parsing gamesummary response to get playercount: %s\n", parseError)
	}

	return playerCount, mapName
}

func assertStringEqual(expected string, actual string) {
	if actual != expected {
		var output = fmt.Sprintf("Expected '%s' but found '%s'", expected, actual)
		panic(output)
	}
}

func assertStringNotEqual(expected string, actual string) {
	if actual == expected {
		var output = fmt.Sprintf("Did not expect '%s' but found '%s'", expected, actual)
		panic(output)
	}
}

func assertNotNil(input any) {
	if input == nil {
		var output = fmt.Sprintf("Did not expect input to be nil (%s)", input)
		panic(output)
	}
}

func assertIntEqual(expected int, actual int) {
	if actual != expected {
		var output = fmt.Sprintf("Expected '%d' but found '%d'", expected, actual)
		panic(output)
	}
}

/*
func ValidateCmdLineArguments() map[string]string {

	// Declare our expected cmd line arguments, which gives us a pointer to the string in which they will be put once parsed. If not found then the second argument is the default value
	var ArgumentBaseDir *string = flag.String("baseDir", "", "The directory within which the application can enumerate files and folders")
	var ArgumentPort *uint = flag.Uint("port", 0, "The port that the application will listen to http requests on")
	var AllowedHost *string = flag.String("host", "http://localhost", "The value used for the 'Access-Control-Allow-Origin'-header")
	var ArgumentBasicAuthUser *string = flag.String("user", "tfrengler", "The HTTP Basic Auth username")
	var ArgumentBasicAuthPassword *string = flag.String("pass", "Nyu6FLKu&n6@Xw4r", "The HTTP Basic Auth password")
	flag.Parse()
}*/

/*

<response>
<gamesummary><![CDATA[
<div class="gs_mapimage"><img src="/images/maps/KF-BIOTICSLAB.jpg" alt="KF-BIOTICSLAB"/></div>
<dl class="gs_details">
  <dt class="gs_map">Map</dt>
  <dd class="gs_map">Biotics Lab</dd>
  <dt class="gs_players">Players</dt>
  <dd class="gs_players">0/6</dd>
  <dt class="gs_wave">Wave 0</dt>
  <dd class="gs_wave">0/0</dd>
</dl>
]]></gamesummary>
</response>

gs_players.+(\d{1})\/

dd.+gs_map">([\w\s]+)<

*/
