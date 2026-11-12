using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The game running: a town with its managers and UI (see Town), driven frame by frame
public static class GameTests
{
    public static void TownLife()
    {
        Town t = Town.Build();
        Building home = t.Place(t.Home, 0, 0);
        Building mine = t.Place(t.Mine, 8, 0);
        OwnedCharacter firsy = t.Hire(t.Firsy, 2, 2);
        t.cm.AssignWorker(firsy, mine);

        T.Section("a day: work, production, home");
        T.Check(t.clock.Current == TimeOfDay.Morning, "the day starts in the Morning");
        World.Run(12f);
        T.Check(mine.IsWorkerOnSite(firsy), "in the Morning the character walks to work and goes in");
        float gold = t.rm.Get(ResourceManager.Gold);
        World.Run(5f);
        T.Check(t.rm.Get(ResourceManager.Gold) > gold, "a staffed Gold Mine makes gold: " + gold + " -> " + t.rm.Get(ResourceManager.Gold));
        t.clock.Advance(); t.clock.Advance(); t.clock.Advance(); // Afternoon, Evening, Night
        T.Check(t.clock.Current == TimeOfDay.Night, "three phases later it is Night");
        World.Run(15f);
        T.Check(!mine.IsWorkerOnSite(firsy) && home.RestingInside == 1 && firsy.Avatar.CurrentActivity == Character.Activity.Resting, "at Night the character goes home and rests inside");

        T.Section("roads, moving, removing");
        T.Check(t.roads.Build(t.DirtRoad, new Vector2Int(4, 4)) && t.map.Roads.Any(r => r.Key == new Vector2Int(4, 4)), "a road is built on a free cell");
        T.Check(!t.roads.Build(t.DirtRoad, new Vector2Int(8, 0)), "not on a building");
        T.Check(t.bm.Move(mine, new Vector2Int(14, 0), Town.Center(14, 0, 3)) && t.bm.GetBuildingAt(new Vector2Int(14, 0)) == mine && t.bm.GetBuildingAt(new Vector2Int(8, 0)) == null, "a building moves, and its cells with it");
        t.bm.Remove(mine); World.EndFrame();
        T.Check(firsy.WorkBuilding == null && !t.bm.Buildings.Contains(mine), "removing a building frees its workers");

        T.Section("clicking the map");
        Building lumber = t.Place(t.Lumberyard, -8, 0);
        t.ClickWorld(lumber.transform.position);
        T.Check(t.detailsPanel.activeInHierarchy && t.detailsText.text.Contains("Lumberyard"), "clicking a building opens its details: " + t.detailsText.text.Split('\n')[0]);

        T.Section("wages and loyalty at payday");
        WageSystem wages = t.gm.gameObject.AddComponent<WageSystem>();
        World.Tick(0.02f);
        t.rm.SetResourceAmount(ResourceManager.Gold, 1000);
        firsy.Mood = 80f; float loyalty = firsy.Loyalty; Notify.Center.Reset();
        t.clock.Advance(); // Night -> Morning: a new day
        T.Check(wages.LastPaid > 0 && firsy.WagePaid, "a new Morning pays the wages");
        T.Check(firsy.Loyalty > loyalty && firsy.LastLoyaltyChange > 0f, "a paid, happy character becomes a little more loyal: " + loyalty + " -> " + firsy.Loyalty);
        T.Check(Notify.Center.History.Any(n => n.Text.StartsWith("Wages paid")) && Notify.Center.History.Any(n => n.Text == "A new day begins"), "and the player is told: " + string.Join(" | ", Notify.Center.History.Select(n => n.Text)));
        t.rm.SetResourceAmount(ResourceManager.Gold, 0); Notify.Center.Reset();
        t.clock.Advance(); t.clock.Advance(); t.clock.Advance(); t.clock.Advance();
        T.Check(!firsy.WagePaid && Notify.Center.History.Any(n => n.Kind == NotifyKind.Alert && n.Text.StartsWith("Could not pay")), "no gold: unpaid, and an alert");
        t.ui.ShowCharacterDetails(firsy);
        T.Check(t.detailsText.text.Contains("today)") && t.detailsText.text.Contains("UNPAID"), "the details show the loyalty change and the unpaid wage");
    }

    public static void SaveAndLoad()
    {
        Town t = Town.Build();
        Building home = t.Place(t.Home, 0, 0);
        Building mine = t.Place(t.Mine, 8, 0);
        OwnedCharacter firsy = t.Hire(t.Firsy, 2, 2);
        t.cm.AssignWorker(firsy, mine);
        firsy.Loyalty = 42f; firsy.Mood = 33f; firsy.DisplayName = "Firsy the Bold";
        t.roads.Build(t.DirtRoad, new Vector2Int(4, 4));
        t.rm.SetResourceAmount(ResourceManager.Gold, 777);

        T.Section("round trip");
        T.Check(SaveSystem.Save("test.json"), "saved");
        GameData data = JsonUtility.FromJson<GameData>(File.ReadAllText(Path.Combine(Application.persistentDataPath, "test.json")));
        T.Check(data.version == SaveSystem.CurrentVersion && data.buildings.Count == 2 && data.characters.Count == 1 && data.roads.Count == 1, "the file has everything, at version " + data.version);
        t.cm.ClearAll(); t.bm.ClearAll(); t.roads.ClearAll(); t.rm.SetResourceAmount(ResourceManager.Gold, 1); World.EndFrame();
        T.Check(SaveSystem.Load("test.json"), "loaded");
        World.EndFrame();
        OwnedCharacter back = t.cm.Roster.FirstOrDefault();
        T.Check(back != null && back.Id == firsy.Id && back.DisplayName == "Firsy the Bold" && back.Loyalty == 42f && back.Mood == 33f, "the character comes back as it was");
        T.Check(back.WorkBuilding != null && back.WorkBuilding.buildingType == t.Mine && back.HomeBuilding != null && back.HomeBuilding.buildingType == t.Home, "with its job and home");
        T.Check(t.rm.Get(ResourceManager.Gold) == 777f && t.map.Roads.Count() == 1 && t.bm.Buildings.Count == 2, "and the gold, the road, the buildings");

        T.Section("bad files");
        File.WriteAllText(Path.Combine(Application.persistentDataPath, "bad.json"), "not a save");
        T.Check(!SaveSystem.Load("bad.json") && t.cm.Roster.Count == 1, "garbage is refused and changes nothing");
        File.WriteAllText(Path.Combine(Application.persistentDataPath, "future.json"), "{\"version\":999}");
        T.Check(!SaveSystem.Load("future.json"), "a save from a newer game is refused");
        T.Check(!SaveSystem.Load("missing.json"), "a missing file is handled");
        File.WriteAllText(Path.Combine(Application.persistentDataPath, "old.json"), "{\"version\":2,\"resources\":[],\"buildings\":[],\"roads\":[],\"characters\":[{\"id\":\"x\",\"typeId\":1,\"displayName\":\"Old\",\"level\":1,\"loyalty\":40}]}");
        T.Check(SaveSystem.Load("old.json") && t.cm.Roster.Count == 1 && t.cm.Roster[0].Loyalty == 40f && t.cm.Roster[0].Mood == t.Firsy.mood.baseline, "an old save (version 2, no mood) loads with the starting mood");
    }

    public static void Menus()
    {
        Town t = Town.Build();
        T.Section("build menu");
        Transform cards = t.ui.buildingButtonContainer;
        T.Check(cards.name == "Content" && cards.parent.name == "Viewport" && cards.parent.parent.GetComponent<ScrollRect>() != null, "the build cards are in a scrolling list");
        T.Check(Town.Rows(cards).Count == t.placer.AllBuildings.Count + 2 && Town.Rows(cards).Any(c => c.name == "Gold MineButton"), "one card per building, a road, and Remove Road");
        t.rm.SetResourceAmount(ResourceManager.Gold, 1000);
        Town.Click(cards.Find("Gold MineButton"));
        T.Check(t.placer.IsPlacing && t.placer.buildingToPlace == t.Mine, "a card starts placing that building");
        Vector3 screen = t.camera.WorldToScreenPoint(Town.Center(3, 3, 3));
        t.actions.Gameplay.PointerPosition.Perform(new Vector2(screen.x, screen.y)); World.Tick(0.02f);
        T.Check(t.placer.ConfirmPlacement() && t.bm.Buildings.Any(b => b.buildingType == t.Mine) && t.rm.Get(ResourceManager.Gold) == 1000 - t.Mine.goldCost, "placing builds it where the pointer is and charges for it");
        t.placer.CancelPlacing();

        T.Section("menu buttons");
        Transform hire = t.canvas.Find("OpenHireButton");
        string[] order = { "OpenTownButton", "OpenRecruitButton", "OpenStoryButton", "OpenQuestButton" };
        for (int i = 0; i < order.Length; i++)
        {
            Transform b = t.canvas.Find(order[i]);
            T.Check(b != null && b.GetSiblingIndex() == hire.GetSiblingIndex() + i + 1, order[i] + " is in the row after Hire");
        }
        T.Check(Town.TextOf(t.canvas.Find("OpenStoryButton")) == "Story" && Town.TextOf(t.canvas.Find("OpenQuestButton")) == "Quests", "labelled Story and Quests");
        Transform view = t.canvas.Find("ViewButton"), menu = t.canvas.Find("OpenMenuButton");
        T.Check(view != null && view.GetSiblingIndex() == 0 && ((RectTransform)view).anchoredPosition.x < ((RectTransform)menu).anchoredPosition.x, "the View button sits behind the panels, left of the Menu button");
        Transform menuPanel = t.canvas.Find("MenuPanel");
        T.Check(menuPanel.Find("NotificationsButton") != null && menuPanel.Find("CheatsButton") != null, "the game menu has Notifications and Cheats below Quit");

        T.Section("town menu");
        OwnedCharacter firsy = t.Hire(t.Firsy, 1, 1);
        Town.Click(t.canvas.Find("OpenTownButton"));
        List<string> rows = t.PanelRows("TownPanel");
        T.Check(t.Panel("TownPanel").gameObject.activeInHierarchy && rows.Any(r => r.StartsWith("Firsy")), "the Town menu lists the characters: " + string.Join(" | ", rows));
        Town.Click(t.canvas.Find("OpenTownButton"));
        T.Check(!t.Panel("TownPanel").gameObject.activeInHierarchy, "the same button closes it");

        T.Section("jobs list");
        Building mine = t.bm.Buildings.First(b => b.buildingType == t.Mine);
        t.ui.ShowCharacterDetails(firsy);
        t.ui.GenerateAssignCharactertoBuildingButtons();
        World.EndFrame();
        T.Check(t.ListRows().Any(r => r == "Gold Mine (0/3)"), "the jobs list shows the free places: " + string.Join(" | ", t.ListRows()));
        Town.Click(Town.RowStartingWith(t.listContent.transform, "Gold Mine"));
        World.EndFrame();
        T.Check(firsy.WorkBuilding == mine && t.ListRows().Any(r => r == "Gold Mine (1/3)") && t.ListRows().Count == 1, "assigning updates the numbers at once");

        T.Section("phone layout");
        UILayout layout = UILayout.Instance;
        layout.ScreenSize = () => new Vector2(1080, 2340);
        layout.SafeArea = () => new Rect(0, 60, 1080, 2160);
        World.Tick(0.05f);
        RectTransform build = (RectTransform)t.canvas.Find("OpenBuildButton"), quests = (RectTransform)t.canvas.Find("OpenQuestButton");
        T.Check(layout.IsPortrait && build.anchorMin.x == 0f && T.Near(build.anchorMax.x, 1f / 6f) && quests.anchorMax.x == 1f, "a tall screen puts six buttons along the bottom");
        RectTransform toastArea = (RectTransform)t.canvas.Find("ToastArea");
        T.Check(toastArea != null && toastArea.anchorMin == new Vector2(0.5f, 1f) && toastArea.anchoredPosition.y < -200f, "toasts sit below the top row");
        foreach (string sheet in new[] { "TownPanel", "RecruitPanel", "StoryPanel", "QuestPanel", "NotificationPanel", "ConnectionPanel" })
            T.Check(((RectTransform)t.Panel(sheet)).anchorMax == new Vector2(1, 0), sheet + " is a sheet above the bar");
        layout.ScreenSize = () => new Vector2(1920, 1080);
        layout.SafeArea = () => new Rect(0, 0, 1920, 1080);
        World.Tick(0.05f);
        T.Check(!layout.IsPortrait && build.anchorMin == new Vector2(0.5f, 0.5f), "a wide screen puts it all back");
    }

    public static void Recruiting()
    {
        Town t = Town.Build();
        GachaSystem gacha = t.gm.gameObject.AddComponent<GachaSystem>();
        World.Tick(0.02f);
        T.Section("pulling with the local backend");
        t.rm.SetResourceAmount(ResourceManager.Letters, 12);
        gacha.SetRandom(() => 0.0);
        T.Check(!gacha.CanPull(1, out string noTavern) && noTavern.Contains("tavern"), "no Tavern yet: nobody can be recruited (" + noTavern + ")");
        t.Place(t.Tavern, -6, -6);
        List<PullResult> results = null;
        gacha.Pulled += r => results = r;
        gacha.Pull(1);
        T.Check(results != null && results.Count == 1 && results[0].IsNew && t.cm.Roster.Count == 1 && gacha.Letters == 11, "one pull: a character joins, a letter is spent");
        t.Firsy.maxOwned = 2; // so the ten-pull must give duplicates
        gacha.Pull(GachaSystem.TenPull);
        T.Check(results.Count == 10 && gacha.Letters == 11 - gacha.CostOf(GachaSystem.TenPull), "a ten-pull: ten results for " + gacha.CostOf(GachaSystem.TenPull) + " letters");
        T.Check(results.Count(r => r.IsNew && r.Type == t.Firsy) == 1 && results.Any(r => !r.IsNew && r.ConvertedToLoyalty && r.LoyaltyGained > 0f), "past the limit, repeats are duplicates (with no Awareness levels to raise, they give loyalty)");
        T.Check(!gacha.CanPull(GachaSystem.TenPull, out string reason) && reason.Length > 0, "not enough letters: refused, with a reason");
        T.Section("the recruit menu");
        Town.Click(t.canvas.Find("OpenRecruitButton"));
        List<string> header = t.Panel("RecruitPanel").Find("Header").children.SelectMany(r => r.children).Select(Town.TextOf).ToList();
        T.Check(header.Any(h => h.StartsWith("Letters:")) && header.Any(h => h.StartsWith("Guaranteed ") || h == "No pity"), "it shows the letters and the pity as 'Guaranteed ...': " + string.Join(" | ", header));
    }

    public static void Offline()
    {
        Town t = Town.Build();
        Building mine = t.Place(t.Mine, 8, 0);
        OwnedCharacter firsy = t.Hire(t.Firsy, 2, 2);
        t.cm.AssignWorker(firsy, mine);
        T.Section("time away");
        DateTime utc = t.clock.UtcNow;
        t.clock.LocalOffset = _ => utc.Date.AddHours(14) - utc; // it is 14:00 (Afternoon: work time) when the player comes back
        float gold = t.rm.Get(ResourceManager.Gold);
        Notify.Center.Reset();
        OfflineProgress.Report report = OfflineProgress.Apply(2 * 3600f);
        T.Check(t.rm.Get(ResourceManager.Gold) > gold && report.gold > 0f, "two hours away with a worker at the mine earns gold: +" + report.gold);
        T.Check(report.Summary().StartsWith("While you were away (2 h): +") && Notify.Center.History.Any(n => n.Text == report.Summary()), "and says so: " + report.Summary());
        OfflineProgress.Report capped = OfflineProgress.Apply(100 * 3600f);
        T.Check(capped.seconds <= 24 * 3600f, "time away counts for at most a day: " + capped.seconds / 3600f + " h");
    }
}
