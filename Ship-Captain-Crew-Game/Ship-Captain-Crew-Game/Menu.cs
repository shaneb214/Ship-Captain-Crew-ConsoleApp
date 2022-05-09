﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ship_Captain_Crew_Game
{
    //Numbered menu with options base class.

    public class Menu
    {
        public string Title;
        public Dictionary<int, MenuOption> optionCallbackDict = new Dictionary<int, MenuOption>();

        public Menu(string title,params MenuOption[] menuOptions)
        {
            Title = title;

            for (int i = 0; i < menuOptions.Length; i++)
            {
                optionCallbackDict.Add(i + 1, menuOptions[i]);
            }
        }

        public void DisplayMenu(bool showTitle = false)
        {
            if (showTitle)
                Console.WriteLine(Title + "\n");

            int enteredOption = -1;
            int lowestOption = optionCallbackDict.Keys.ElementAt(0);
            int highestOption = optionCallbackDict.Keys.ElementAt(optionCallbackDict.Count - 1);

            while (enteredOption < lowestOption || enteredOption > highestOption)
            {
                DisplayOptions();

                Console.WriteLine("Enter option: ");
                bool isNumber = int.TryParse(Console.ReadLine(), out enteredOption);
                if (isNumber && enteredOption >= lowestOption && enteredOption <= highestOption)
                    InterpretInput(enteredOption);
                else
                    Console.WriteLine("Incorrect input try again. \n");
            }
        }

        private void DisplayOptions()
        {
            foreach (KeyValuePair<int, MenuOption> entry in optionCallbackDict)
            {
                Console.WriteLine($"{entry.Key}) {entry.Value.Description}");
            }
        }

        public void InterpretInput(int input)
        {
            optionCallbackDict[input].Callback?.Invoke();
        }
    }

    public struct MenuOption
    {
        public string Description;
        public Action Callback;

        public MenuOption(string description, Action callback)
        {
            Description = description;
            Callback = callback;
        }
    }
}