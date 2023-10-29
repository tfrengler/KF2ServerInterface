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
	Servers       *[]Server
}

type Server struct {
	Name            string
	Port            int
	DesiredMap      string
	GameMode        GameMode
	Disabled        bool
	ConfigSubFolder string
}

var Configuration Config

var EntryPoint *url.URL
var Client *http.Client
var LoginPage = "/ServerAdmin/"
var ServerStatusPage = "/ServerAdmin/current/info"
var GamesummaryPage = "/ServerAdmin/current+gamesummary"
var ChangeMapPage = "/ServerAdmin/current/change"

func main() {

	var configError = loadConfig()
	if configError != nil {
		panic(configError)
	}

	var cookieJar, _ = cookiejar.New(nil)
	Client = &http.Client{
		Jar: cookieJar,
		CheckRedirect: func(req *http.Request, via []*http.Request) error {
			return http.ErrUseLastResponse
		},
		Timeout: 10 * time.Second,
	}

	for {
		fmt.Println("Waking up!")
		for _, currentServer := range *Configuration.Servers {

			fmt.Printf("Checking server '%s'\n", currentServer.Name)

			if currentServer.Disabled {
				fmt.Println("Server is disabled, skipping")
				continue
			}

			var ServerURL, ParseError = url.Parse(fmt.Sprintf("%s:%s", EntryPoint.String(), fmt.Sprint(currentServer.Port)))
			if ParseError != nil {
				panic(fmt.Sprintf("Unable to parse entry point with server port: %s", ParseError))
			}

			var isLoggedIn, Error = isLoggedIn(*ServerURL)
			if Error != nil {
				onCheckingServerError(Error)
				continue
			}

			if !isLoggedIn {
				fmt.Println("We have no authenticated session, need to log in")
				Error = login(*ServerURL, Configuration.Username, Configuration.Password)
				if Error != nil {
					onCheckingServerError(Error)
					continue
				}
				fmt.Println("Login successful")
			}

			playerCount, Error := getPlayerCount(*ServerURL)
			if Error != nil {
				onCheckingServerError(Error)
				continue
			}

			if playerCount > 0 {
				fmt.Printf("Server has players (%d), not checking map\n", playerCount)
				continue
			}

			fmt.Println("Server has no players, checking map")

			var DesiredMap string
			if currentServer.DesiredMap == "" {
				DesiredMap = fmt.Sprintf("KF-%s", Configuration.DesiredMap)
			} else {
				DesiredMap = fmt.Sprintf("KF-%s", currentServer.DesiredMap)
			}

			currentMap, Error := getCurrentMap(*ServerURL)
			if Error != nil {
				onCheckingServerError(Error)
				continue
			}

			if currentMap == DesiredMap {
				fmt.Printf("Server on desired map (%s)\n", currentMap)
				continue
			}

			var GameMode GameMode
			if currentServer.GameMode == GameMode_NONE {
				GameMode = Configuration.GameMode
			} else {
				GameMode = currentServer.GameMode
			}

			fmt.Printf("Changing map from '%s' to '%s' (%s)\n", currentMap, DesiredMap, GameMode)

			Error = changeMap(*ServerURL, GameMode, currentServer.ConfigSubFolder, DesiredMap)
			if Error != nil {
				onCheckingServerError(Error)
				continue
			}

			fmt.Println("Map change successful")
		}
		fmt.Printf("Done checking servers, sleeping (%d seconds)\n", Configuration.CheckInterval)
		time.Sleep(Configuration.CheckInterval * time.Second)
	}
}

func onCheckingServerError(error error) {
	fmt.Printf("Error while checking server:\n\t%s\n", error.Error())
}

func loadConfig() error {
	var data, readError = os.ReadFile("config.json")

	if readError != nil {
		return fmt.Errorf("failed to read config: %s", readError)
	}

	var jsonError = json.Unmarshal(data, &Configuration)

	if jsonError != nil {
		return fmt.Errorf("failed to parse config: %s", jsonError)
	}

	var entryPoint, urlParseError = url.Parse(Configuration.ServerAddress)
	if urlParseError != nil {
		return fmt.Errorf("failed to parse config ServerAddress: %s", urlParseError)
	}
	EntryPoint = entryPoint

	if Configuration.CheckInterval < 0 {
		Configuration.CheckInterval = 10 * time.Second
	}

	var ConfigStringified, _ = json.MarshalIndent(Configuration, "", "    ")
	fmt.Printf("Configuration loaded:%s\n\n", string(ConfigStringified))

	return nil
}

func isLoggedIn(address url.URL) (bool, error) {
	var serverStatusPage = address.String() + ServerStatusPage
	var response, responseError = Client.Get(serverStatusPage)

	if responseError != nil {
		return false, fmt.Errorf("error when determining whether we are logged in (%s): %s", address.String(), responseError.Error())
	}

	if response.StatusCode != http.StatusOK {
		return false, fmt.Errorf("error when determining whether we are logged in (%s): %s", serverStatusPage, response.Status)
	}

	var responseContent, readError = io.ReadAll(response.Body)
	defer response.Body.Close()

	if readError != nil {
		return false, fmt.Errorf("error when trying to parse server status-page response body (%s): %s", serverStatusPage, responseError)
	}

	loginFormRegex, _ := regexp.Compile("<form id=\"loginform\"")
	var loginFormPresent = loginFormRegex.Match(responseContent)

	return !loginFormPresent, nil
}

func login(address url.URL, username string, password string) error {
	var loginPage = address.String() + LoginPage
	var response, responseError = Client.Get(loginPage)

	if responseError != nil {
		return fmt.Errorf("error when trying to retrieve login-page (%s): %s", loginPage, responseError)
	}

	if response.StatusCode != http.StatusOK {
		return fmt.Errorf("error when trying to retrieve login-page (%s): %s", loginPage, response.Status)
	}

	var responseContent, readError = io.ReadAll(response.Body)
	defer response.Body.Close()

	if readError != nil {
		return fmt.Errorf("error when trying to parse login-page response body (%s): %s", loginPage, responseError)
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
		return fmt.Errorf("error when trying to log in (%s): %s", loginPage, responseError)
	}

	if !(response.StatusCode == http.StatusOK || response.StatusCode == http.StatusFound) {
		return fmt.Errorf("error when trying to log in (%s): %s", loginPage, response.Status)
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
		return fmt.Errorf("error when trying to log in as there's no auth-cookie in POST response (%s) (status: %s)", loginPage, response.Status)
	}

	return nil
}

func getPlayerCount(address url.URL) (int, error) {
	var gamesummaryPage = address.String() + GamesummaryPage
	var response, responseError = Client.PostForm(gamesummaryPage, url.Values{"ajax": {"1"}})

	if responseError != nil {
		return 0, fmt.Errorf("error calling gamesummary (%s): %s", gamesummaryPage, responseError)
	}

	if response.StatusCode != http.StatusOK {
		return 0, fmt.Errorf("error calling gamesummary  (%s): %s", gamesummaryPage, response.Status)
	}

	var responseContent, readError = io.ReadAll(response.Body)
	defer response.Body.Close()

	if readError != nil {
		return 0, fmt.Errorf("error reading gamesummary response body (%s): %s", gamesummaryPage, responseError)
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
		return "", fmt.Errorf("error calling change-page (%s): %s", changePage, responseError)
	}

	if response.StatusCode != http.StatusOK {
		return "", fmt.Errorf("error when trying to change map (%s): %s", changePage, response.Status)
	}

	var responseContent, readError = io.ReadAll(response.Body)
	defer response.Body.Close()

	if readError != nil {
		return "", fmt.Errorf("error reading change-page response body (%s): %s", changePage, responseError)
	}

	currentMapRegex, _ := regexp.Compile(`<select id="map" name="map">[\s\S]+?<option value="(.*)" selected="selected">`)
	var currentMapString = currentMapRegex.FindStringSubmatch(string(responseContent))[1]

	if currentMapString == "" {
		return "", fmt.Errorf("error parsing change-page because mapname is empty (%s)", changePage)
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
