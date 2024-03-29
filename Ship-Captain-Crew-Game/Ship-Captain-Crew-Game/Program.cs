﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;

//https://www.dicegamedepot.com/ship-captain-and-crew-dice-game-rules/

namespace Ship_Captain_Crew_Game
{
    internal class Program
    {
        //Data used during each players turn.
        [ThreadStatic()]
        private static Turn turn;
        [ThreadStatic()]
        private static Ship ship;

        private static DiceRoll diceRoll;

        private static IsolatedStorageManager isolatedStorageManager;

        //Menus.
        private static Menu mainMenu;
        private static Menu settingsMenu;

        //Threads.
        private static object locker = new object();
        private static List<Thread> playerThreads = new List<Thread>();

        private static GameManager gameManager;

        static void Main(string[] args)
        {
            //Read settings from Isolated Storage (if any) and save in memory.
            ReadSettings();

            //Create menus and show main menu.
            CreateMenus();
            mainMenu.DisplayMenu(showTitle: true);

            Console.WriteLine("End of main");
            Console.ReadKey();
        }

        private static void CreateMenus()
        {
            mainMenu = new Menu("Welcome to Ship, Captain and Crew!",

                new MenuOption("Play", StartGame),
                new MenuOption("Settings", OnSettingsSelected),
                new MenuOption("Quit", () => Environment.Exit(0)));

            settingsMenu = new Menu("Settings: ",

                new MenuOption("Select Number of Players", OnSelectNumberOfPlayersSelected),
                new MenuOption("Bow to Stern", OnBowToSternSelected),
                new MenuOption("Return", OnReturnFromSettingsSelected));
        }

        #region Menu Options Being Selected
        private static void OnSettingsSelected()
        {
            Console.Clear();
            settingsMenu.DisplayMenu(showTitle: true);
        }
        private static void OnReturnFromSettingsSelected()
        {
            Console.Clear();
            mainMenu.DisplayMenu(showTitle: true);
        }

        private static void OnSelectNumberOfPlayersSelected()
        {
            Console.Clear();
            Console.WriteLine($"Enter the number of players to play: (Min {GameSettings.MIN_PLAYERS_CAN_PLAY}, Max {GameSettings.MAX_PLAYERS_CAN_PLAY})");
            GameSettings.Instance.NumPlayers = UserInputManager.AskForNumberWithinRange(GameSettings.MIN_PLAYERS_CAN_PLAY, GameSettings.MAX_PLAYERS_CAN_PLAY);
            SaveSettingsToIsolatedStorage();
            Console.Clear();
        }

        private static void OnBowToSternSelected()
        {
            Console.Clear();
            Console.WriteLine("In Bow to Stern, 1, 2, and 3 are the ship, captain and crew instead of 6, 5 and 4.");
            Console.WriteLine($"This option is currently {GameSettings.Instance.GetBowToSternStatus}.");
            Console.WriteLine("Do you want to enable Bow to Stern? (y/n)");
            GameSettings.Instance.BowToStern = UserInputManager.AskForBooleanValue();
            SaveSettingsToIsolatedStorage();
            Console.Clear();
        }
        #endregion

        #region Saving / Reading Settings 
        private static void ReadSettings()
        {
            isolatedStorageManager = new IsolatedStorageManager(GameSettings.ISOLATED_STORAGE_FOLDER_NAME,GameSettings.ISOLATED_STORAGE_FILE_NAME);

            //Check if file is in isolated storage.
            Thread checkFolderExistsIsolatedStorage = new Thread(new ThreadStart(isolatedStorageManager.CheckDirectoryExists));
            checkFolderExistsIsolatedStorage.Start();
            checkFolderExistsIsolatedStorage.Join();

            bool jsonFileExists = isolatedStorageManager.jsonFileExists;
            string json = string.Empty;

            if (jsonFileExists) //If file existed before - read json.
            {
                Thread readFromIsolatedStorage = new Thread(new ThreadStart(isolatedStorageManager.readFromStorage));
                readFromIsolatedStorage.Start();
                readFromIsolatedStorage.Join();

                //Check if json is valid.
                json = isolatedStorageManager.json;
                if (!string.IsNullOrEmpty(json))
                {
                    SettingsDTO settings = JsonConvert.DeserializeObject<SettingsDTO>(json);
                    GameSettings.Instance.UpdateSettingsFromJSON(settings);
                }
            }
            else //Json didn't exist - write default settings.
            {
                GameSettings.Instance.SetDefaultSettings();
                SaveDefaultSettingsToIsolatedStorage();
            }
        }

        private static void SaveSettingsToIsolatedStorage()
        {
            Thread writeToIsolatedStorage = new Thread(new ParameterizedThreadStart(isolatedStorageManager.writeToStorage));
            SettingsDTO settingsDTO = new SettingsDTO(GameSettings.Instance.BowToStern, GameSettings.Instance.NumPlayers);
            string json = JsonConvert.SerializeObject(settingsDTO).ToString();
            writeToIsolatedStorage.Start(json);
        }

        private static void SaveDefaultSettingsToIsolatedStorage()
        {
            Thread writeToIsolatedStorage = new Thread(new ParameterizedThreadStart(isolatedStorageManager.writeToStorage));
            SettingsDTO settingsDTO = new SettingsDTO(GameSettings.BOW_TO_STERN_DEFAULT_SETTING, GameSettings.MIN_PLAYERS_CAN_PLAY);
            string json = JsonConvert.SerializeObject(settingsDTO).ToString();
            writeToIsolatedStorage.Start(json);
        }
        #endregion

        private static void StartGame()
        {
            Console.Clear();

            gameManager = new GameManager();

            CreatePlayerThreads(GameSettings.Instance.NumPlayers);
            playerThreads.ForEach(thread => thread.Start());
            playerThreads.ForEach(thread => thread.Join());

            //After round / game is over.
            playerThreads.Clear();


            gameManager.DisplayPlayerScores();

            //Determine who won.
            if (gameManager.ArePlayersDrawn())
            {
                gameManager.DisplayTiedPlayers();
                DisplayPressAnyKeyReturnToMenuPrompt();
                Console.Clear();
            }
            else
            {          
                gameManager.DisplayWinningPlayer();
                DisplayPressAnyKeyReturnToMenuPrompt();
                Console.Clear();
            }
        }

        private static void CreatePlayerThreads(int numberOfPlayers) 
        {
            for (int i = 0; i < numberOfPlayers; i++)
            {
                Thread thread = new Thread(PlayTurn);
                thread.Name = $"Player {i + 1}";

                playerThreads.Add(thread);
            }
        }

        private static void PlayTurn()
        {
            Monitor.Enter(locker);
            try
            {
                turn = new Turn();
                ship = new Ship();

                while (!turn.HasEnded && turn.HasRollsRemaining)
                {
                    Console.WriteLine($"{Thread.CurrentThread.Name}'s Turn! \n");

                    if (turn.HasRollsRemaining)
                    {
                        if (ship.HasAllShipFeatures) //Give option to roll if they have all ship features.
                        {
                            AskForRollCargoUpdate(turn, ship);
                        }
                        else //Otherwise ask to roll dice to try and get needed ship features.
                        {
                            //Show how many turns are left.
                            turn.DisplayRollsRemaining();

                            //Roll.
                            AskToRollDiceToGetAllShipFeatures();
                            diceRoll = GenerateDiceRoll(turn.NumDiceAvailable, ref turn.RollsRemaining);

                            //Update ship.
                            ShipUpdateResponse shipUpdate = UpdateShip(ship, diceRoll);

                            //Display ship status.
                            ship.DisplayShipStatus();

                            //If ship was updated. Tell player and reduce dices that can be thrown.
                            if (shipUpdate.FeaturesWereUpdated)
                            {
                                Console.WriteLine(shipUpdate.Message);
                                turn.NumDiceAvailable -= shipUpdate.NumFeaturesUpdated;
                            }

                            if (ship.HasAllShipFeatures)
                            {
                                //Find cargo value based on dice left on "table".
                                ship.SetCargoValue(diceRoll);
                                //Display this value.
                                ship.DisplayCargoValue();
                            }
                        }
                    }
                    else
                    {
                        //Show them cargo value. 
                        if (!ship.HasAllShipFeatures)
                            ship.DisplayCargoValue();

                        turn.End();
                    }

                    Monitor.Pulse(locker);
                    Monitor.Wait(locker);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                gameManager.AddPlayerScore(new PlayerScore(ship.CargoValue, Thread.CurrentThread.Name));
                Monitor.Pulse(locker);
                Monitor.Exit(locker);
            }
        }

        private static void AskForRollCargoUpdate(Turn turn, Ship ship)
        {
            turn.DisplayRollsRemaining();

            string input = string.Empty;

            ship.DisplayCargoValue();
            Console.WriteLine("Do you wish to roll to get a better cargo? (y/n)");
            bool userWantsToRollAgain = UserInputManager.AskForBooleanValue();
            if(userWantsToRollAgain)
            {
                //Roll dice for better cargo score attempt.
                DiceRoll currentDiceRoll = GenerateDiceRoll(turn.NumDiceAvailable, ref turn.RollsRemaining);
                ship.SetCargoValue(currentDiceRoll);
                ship.DisplayCargoValue();
            }
            else
                turn.End();
        }

        private static DiceRoll GenerateDiceRoll(int numDice,ref int rollsRemaining)
        {
            DiceRoll diceRoll = new DiceRoll(numDice);

            //Roll Dice.
            diceRoll.RollDice();
            diceRoll.DisplayDiceValues();

            rollsRemaining--;

            return diceRoll;
        }
        public static ShipUpdateResponse UpdateShip(Ship ship,DiceRoll diceRoll)
        {
            ShipUpdateResponse updateResponse = ship.UpdateShipFeatures(diceRoll);
            return updateResponse;
        }

        private static void AskToRollDiceToGetAllShipFeatures()
        {
            Console.WriteLine("Enter y to roll dice.");
            string input = string.Empty;
            while (input != UserInputManager.YES_INPUT)
            {
                input = Console.ReadLine().ToLower();
            }
        }
        private static void DisplayPressAnyKeyReturnToMenuPrompt()
        {
            Console.WriteLine("Press any key to return to menu.");
            Console.ReadKey();
        }
    }
}