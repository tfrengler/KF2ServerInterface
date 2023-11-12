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
	GameMode_NONE           GameMode = ""
	GameMode_Endless        GameMode = "KFGameContent.KFGameInfo_Endless"
	GameMode_Objective      GameMode = "KFGameContent.KFGameInfo_Objective"
	GameMode_Survival       GameMode = "KFGameContent.KFGameInfo_Survival"
	GameMode_VersusSurvival GameMode = "KFGameContent.KFGameInfo_VersusSurvival"
	GameMode_WeeklySurvival GameMode = "KFGameContent.KFGameInfo_WeeklySurvival"
)

type Config struct {
	DesiredMap    string
	ServerAddress string
	Username      string
	Password      string
	GameMode      GameMode
	CheckInterval time.Duration
	KillOnEmpty   bool
	KillAfter     int
	Servers       *[]*Server
}

type Server struct {
	Name             string
	Port             int
	DesiredMap       string
	GameMode         GameMode
	Inactive         bool
	ConfigSubFolder  string
	UnreachableCount int
}

var Configuration Config

var EntryPoint *url.URL
var Client *http.Client
var LoginPage = "/ServerAdmin/"
var ServerStatusPage = "/ServerAdmin/current/info"
var GamesummaryPage = "/ServerAdmin/current+gamesummary"
var ChangeMapPage = "/ServerAdmin/current/change"
var ConsolePage = "/ServerAdmin/console"
var InactiveServers = 0
var TimeStarted time.Time
var KillThresholdOffset = 0

func main() {

	TimeStarted = time.Now().Local()
	loadConfig()

	var cookieJar, _ = cookiejar.New(nil)
	Client = &http.Client{
		Jar: cookieJar,
		CheckRedirect: func(req *http.Request, via []*http.Request) error {
			return http.ErrUseLastResponse
		},
		Timeout: 10 * time.Second,
	}

	for {
		printLine("Waking up")
		printLine(fmt.Sprintf("%d servers out of %d are inactive", InactiveServers, len(*Configuration.Servers)))

		for _, currentServer := range *Configuration.Servers {

			if InactiveServers >= len(*Configuration.Servers) {
				printLine("All servers are inactive, exiting program")
				os.Exit(0)
			}

			fmt.Println("----------------------------------------------")
			printLine(fmt.Sprintf("Checking server '%s'", currentServer.Name))

			if currentServer.Inactive {
				printLine("Server is inactive, skipping")
				continue
			}

			if currentServer.UnreachableCount == 3 {
				printLine("WARN: Server is considered unreachable after 3 failed connection attempts, marking as inactive")
				currentServer.Inactive = true
				InactiveServers++
				continue
			}

			var ServerURL, ParseError = url.Parse(fmt.Sprintf("%s:%s", EntryPoint.String(), fmt.Sprint(currentServer.Port)))
			if ParseError != nil {
				panic(fmt.Sprintf("Unable to parse entry point with server port: %s", ParseError))
			}

			var isLoggedIn, Error = isLoggedIn(*ServerURL)
			if Error != nil {
				onCheckingServerError(Error)
				currentServer.UnreachableCount++
				continue
			}

			currentServer.UnreachableCount = 0

			if !isLoggedIn {
				printLine("We have no authenticated session, need to log in")
				Error = login(*ServerURL, Configuration.Username, Configuration.Password)
				if Error != nil {
					onCheckingServerError(Error)
					continue
				}
				printLine("Login successful")
			}

			playerCount, Error := getPlayerCount(*ServerURL)
			if Error != nil {
				onCheckingServerError(Error)
				continue
			}

			if playerCount > 0 {
				printLine(fmt.Sprintf("Server has players (%d), skipping other checks", playerCount))
				continue
			}

			var Now = time.Now().Local()
			var KillServer = false

			if playerCount == 0 {

				if Configuration.KillOnEmpty {
					KillServer = true
				} else if Configuration.KillAfter != -1 {
					// We need to do this potential offset check because if we spill over to the next day our server might not be killed as expected
					// If it was started at 18 and you want it to be shut down after 20 but there are still players at 02:00 at night then the comparison
					// fails (because 2 is not greater than 20). Hence the need to add 24 to the check once the check no longer happens on the same day
					// the server was started.
					var ComparisonOffset = 0
					if Now.Day() != TimeStarted.Day() {
						ComparisonOffset = 24
					}
					KillServer = (Now.Hour() + ComparisonOffset) > (Configuration.KillAfter + KillThresholdOffset)
				}
			}

			if KillServer {

				printLine("Server has no players and shutdown conditions are met, killing server")
				Error = shutdown(*ServerURL)
				if Error != nil {
					onCheckingServerError(Error)
					continue
				}
				currentServer.Inactive = true
				InactiveServers++
				printLine("Server shutdown successful, marked as inactive")
				continue
			}

			printLine("Server has no players, checking map")

			currentMap, Error := getCurrentMap(*ServerURL)
			if Error != nil {
				onCheckingServerError(Error)
				continue
			}

			if currentMap == currentServer.DesiredMap {
				printLine(fmt.Sprintf("Server on desired map (%s)", currentMap))
				continue
			}

			printLine(fmt.Sprintf("Changing map from '%s' to '%s' (gamemode: %s)", currentMap, currentServer.DesiredMap, currentServer.GameMode))

			Error = changeMap(*ServerURL, currentServer.GameMode, currentServer.ConfigSubFolder, currentServer.DesiredMap)
			if Error != nil {
				onCheckingServerError(Error)
				continue
			}

			printLine("Map change successful")
		}

		printLine(fmt.Sprintf("Done checking servers, sleeping (%f seconds)\n", time.Duration.Seconds(Configuration.CheckInterval)))
		time.Sleep(Configuration.CheckInterval)
	}
}

func onCheckingServerError(error error) {
	printLine(fmt.Sprintf("Error while checking server:\n\t%s", error.Error()))
}

func loadConfig() {
	var data, readError = os.ReadFile("config.json")

	if readError != nil {
		panic(fmt.Sprintf("failed to read config: %s", readError))
	}

	var jsonError = json.Unmarshal(data, &Configuration)

	if jsonError != nil {
		panic(fmt.Sprintf("failed to parse config: %s", jsonError))
	}

	if len(Configuration.DesiredMap) == 0 {
		panic("error validating config: item 'DesiredMap' is empty")
	}

	if len(Configuration.Username) == 0 {
		panic("error validating config: item 'Username' is empty")
	}

	if len(Configuration.Password) == 0 {
		panic("error validating config: item 'Password' is empty")
	}

	if Configuration.GameMode == GameMode_NONE {
		panic("error validating config: item 'GameMode' is empty")
	}

	var entryPoint, urlParseError = url.Parse(Configuration.ServerAddress)
	if urlParseError != nil {
		panic(fmt.Sprintf("failed to parse config item 'ServerAddress': %s", urlParseError))
	}
	EntryPoint = entryPoint

	if Configuration.CheckInterval < 60 {
		fmt.Printf("WARN: Configuration item 'CheckInterval' is invalid (%d). Minimum is 60 seconds. Defaulting instead to 10 minutes\n", Configuration.CheckInterval)
		Configuration.CheckInterval = 10 * time.Minute
	} else {
		Configuration.CheckInterval = Configuration.CheckInterval * time.Second
	}

	if Configuration.KillAfter > 23 || Configuration.KillAfter < -1 {
		fmt.Printf("WARN: Configuration item 'KillAfter' is invalid (%d), setting to -1 (disabled)\n", Configuration.KillAfter)
		Configuration.KillAfter = -1
	}

	if Configuration.KillAfter > -1 && Configuration.KillOnEmpty {
		fmt.Println("WARN: Configuration item 'KillAfter' and 'KillOnEmpty' are both defined. 'KillAfter' will be disabled")
		Configuration.KillAfter = -1

		// Making it so that if started the server after noon, and killAfter is between midnight and noon, then we add a 24 offset
		// so we can spill over into the next day. That way we can start a server and tell it to be shut down at 3, meaning 03:00
		// the following night, otherwise it will be shut down immediately, because anywhere between noon and midnight today (12-23) is greater than 3
	} else if (Configuration.KillAfter >= 0 || Configuration.KillAfter <= 12) && TimeStarted.Hour() >= 12 {
		KillThresholdOffset = 24
	}

	Configuration.GameMode = validateAndGetGameMode(string(Configuration.GameMode))

	for _, currentServer := range *Configuration.Servers {

		if len(currentServer.Name) == 0 {
			panic("error validating config: item 'Name' is empty")
		}

		if currentServer.Inactive {
			InactiveServers++
			continue
		}

		if currentServer.GameMode == GameMode_NONE {
			currentServer.GameMode = Configuration.GameMode
		} else {
			currentServer.GameMode = validateAndGetGameMode(string(currentServer.GameMode))
		}

		if currentServer.DesiredMap == "" {
			currentServer.DesiredMap = fmt.Sprintf("KF-%s", Configuration.DesiredMap)
		} else {
			currentServer.DesiredMap = fmt.Sprintf("KF-%s", currentServer.DesiredMap)
		}
	}

	var ConfigStringified, _ = json.MarshalIndent(Configuration, "", "    ")
	fmt.Printf("Configuration loaded:%s\n\n", string(ConfigStringified))
}

func validateAndGetGameMode(gamemode string) GameMode {
	switch gamemode {
	case "S":
		return GameMode_Survival
	case "E":
		return GameMode_Endless
	case "O":
		return GameMode_Objective
	case "W":
		return GameMode_WeeklySurvival
	case "WS":
		return GameMode_WeeklySurvival
	default:
		panic(fmt.Sprintf("error validating gamemode, as '%s' is not a valid option", gamemode))
	}
}

func isLoggedIn(address url.URL) (bool, error) {
	var serverStatusPage = address.String() + ServerStatusPage
	var response, responseError = Client.Get(serverStatusPage)

	if responseError != nil {
		return false, fmt.Errorf("error when determining whether we are logged in: %s", responseError.Error())
	}

	if response.StatusCode != http.StatusOK {
		return false, fmt.Errorf("error when determining whether we are logged in: %s", response.Status)
	}

	var responseContent, readError = io.ReadAll(response.Body)
	defer response.Body.Close()

	if readError != nil {
		return false, fmt.Errorf("error when trying to parse server status-page response body: %s", responseError)
	}

	loginFormRegex, _ := regexp.Compile("<form id=\"loginform\"")
	var loginFormPresent = loginFormRegex.Match(responseContent)

	return !loginFormPresent, nil
}

func login(address url.URL, username string, password string) error {
	var loginPage = address.String() + LoginPage
	var response, responseError = Client.Get(loginPage)

	if responseError != nil {
		return fmt.Errorf("error when trying to retrieve login-page: %s", responseError)
	}

	if response.StatusCode != http.StatusOK {
		return fmt.Errorf("error when trying to retrieve login-page: %s", response.Status)
	}

	var responseContent, readError = io.ReadAll(response.Body)
	defer response.Body.Close()

	if readError != nil {
		return fmt.Errorf("error when trying to parse login-page response body: %s", responseError)
	}

	tokenRegex, _ := regexp.Compile("name=\"token\"\\svalue=\"(.+)\"")
	var tokenMatches = tokenRegex.FindStringSubmatch(string(responseContent))

	if len(tokenMatches) == 0 {
		panic("No login token :(")
	}
	var loginToken = tokenMatches[1]

	var requestBody = url.Values{}
	requestBody.Set("token", loginToken)
	requestBody.Set("username", username)
	requestBody.Set("password", password)
	requestBody.Set("remember", "-1")
	requestBody.Set("password_hash", "")

	response, responseError = Client.PostForm(loginPage, requestBody)

	if responseError != nil {
		return fmt.Errorf("error when trying to log in: %s", responseError)
	}

	if !(response.StatusCode == http.StatusOK || response.StatusCode == http.StatusFound) {
		return fmt.Errorf("error when trying to log in: %s", response.Status)
	}

	var responseCookies = response.Cookies()
	var authCookie *http.Cookie
	for _, currentCookie := range responseCookies {
		if currentCookie.Name == "authcred" {
			authCookie = currentCookie
			break
		}
	}

	if authCookie == nil {
		return fmt.Errorf("error when trying to log in as there's no auth-cookie in POST response (status: %s)", response.Status)
	}

	return nil
}

func getPlayerCount(address url.URL) (int, error) {
	var gamesummaryPage = address.String() + GamesummaryPage
	var response, responseError = Client.PostForm(gamesummaryPage, url.Values{"ajax": {"1"}})

	if responseError != nil {
		return 0, fmt.Errorf("error calling gamesummary: %s", responseError)
	}

	if response.StatusCode != http.StatusOK {
		return 0, fmt.Errorf("error calling gamesummary: %s", response.Status)
	}

	var responseContent, readError = io.ReadAll(response.Body)
	defer response.Body.Close()

	if readError != nil {
		return 0, fmt.Errorf("error reading gamesummary response body: %s", responseError)
	}

	playerCountRegex, _ := regexp.Compile(`gs_players.+(\d{1})\/`)
	var playerCountString = playerCountRegex.FindStringSubmatch(string(responseContent))[1]

	var playerCount, parseError = strconv.Atoi(playerCountString)

	if parseError != nil {
		panic(fmt.Sprintf("error parsing gamesummary (%s): %s", gamesummaryPage, responseError))
	}

	return playerCount, nil
}

func getCurrentMap(address url.URL) (string, error) {
	var changePage = address.String() + ChangeMapPage
	var response, responseError = Client.Get(changePage)

	if responseError != nil {
		return "", fmt.Errorf("error calling change-page: %s", responseError)
	}

	if response.StatusCode != http.StatusOK {
		return "", fmt.Errorf("error calling change-page: %s", response.Status)
	}

	var responseContent, readError = io.ReadAll(response.Body)
	defer response.Body.Close()

	if readError != nil {
		return "", fmt.Errorf("error reading change-page response body: %s", responseError)
	}

	currentMapRegex, _ := regexp.Compile(`<select id="map" name="map">[\s\S]+?<option value="(.*)" selected="selected">`)
	var currentMapString = currentMapRegex.FindStringSubmatch(string(responseContent))[1]

	if currentMapString == "" {
		panic("error parsing change-page because mapname regex turned up empty")
	}

	return currentMapString, nil
}

func changeMap(address url.URL, gameMode GameMode, configSubDir string, desiredMap string) error {
	var changeMapPage = address.String() + ChangeMapPage

	var requestBody = url.Values{}
	requestBody.Set("gametype", string(gameMode))
	requestBody.Set("map", desiredMap)
	requestBody.Set("urlextra", fmt.Sprintf("?ConfigSubDir=%s", configSubDir))
	requestBody.Set("mutatorGroupCount", "0")
	requestBody.Set("action", "change")

	var response, responseError = Client.PostForm(changeMapPage, requestBody)

	if responseError != nil {
		return fmt.Errorf("error when trying to change map (%s): %s", changeMapPage, responseError)
	}

	if response.StatusCode != http.StatusOK {
		return fmt.Errorf("error when trying to change map (%s): %s", changeMapPage, response.Status)
	}

	return nil
}

func shutdown(address url.URL) error {
	var consolePage = address.String() + ConsolePage

	var requestBody = url.Values{}
	requestBody.Set("command", "exit")

	var response, responseError = Client.PostForm(consolePage, requestBody)

	if responseError != nil {
		return fmt.Errorf("error when trying to shut down server: %s", responseError)
	}

	if response.StatusCode != http.StatusOK {
		return fmt.Errorf("error when trying to shut down server: %s", response.Status)
	}

	return nil
}

func printLine(message string) {
	fmt.Printf("[%s] - %s\n", time.Now().Format(time.TimeOnly), message)
}
