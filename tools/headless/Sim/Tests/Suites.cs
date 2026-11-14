using System;
using System.Collections.Generic;

// Every suite, in the order they run
public static partial class Suites
{
    public static readonly List<(string, Action)> All = new List<(string, Action)>
    {
        ("Engine stand-ins", EngineTests.Run),
        ("Pathfinding", LogicTests.Pathfinding),
        ("Production", LogicTests.Production),
        ("Hunger rules", LogicTests.Hunger),
        ("Gacha odds and pity", LogicTests.Gacha),
        ("Times of day and lighting", LogicTests.TimeAndLight),
        ("Building layout", LogicTests.Layout),
        ("Story scripts", StoryLogicTests.Script),
        ("Story player and conditions", StoryLogicTests.Player),
        ("Notification rules", StoryLogicTests.Notifications),
        ("Quest and loyalty rules", StoryLogicTests.QuestsAndLoyalty),
        ("Town: buildings, characters, jobs", TownTests.Run),
        ("Town life: a day, roads, payday", GameTests.TownLife),
        ("Saving and loading", GameTests.SaveAndLoad),
        ("Menus and layout", GameTests.Menus),
        ("Recruiting", GameTests.Recruiting),
        ("Time away (offline)", GameTests.Offline),
        ("Story in the game", FeatureTests.StoryInGame),
        ("Quests in the game", FeatureTests.QuestsInGame),
        ("Connection", FeatureTests.Connection),
        ("Toasts and history", FeatureTests.Toasts),
        ("Firebase client", FeatureTests.FirebaseClient),
        ("Map regions: the rules", MapTests.Rules),
        ("Map regions: the shipped sample", MapTests.SampleContent),
        ("Map regions in the game", MapTests.InTheGame),
        ("Saving the Map", MapTests.SavingTheMap),
        ("Project files", ProjectFileTests.Run),
    };
}
