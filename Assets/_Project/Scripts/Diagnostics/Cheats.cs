using UnityEngine;

// Testing tools: give yourself resources, spawn characters, change the time, reset the game. Everything here
// works on the game through its normal managers, so it behaves like the real thing. The Cheats menu in the
// game menu calls these. Only available in the editor and in Development Builds.
public static class Cheats
{
    public static readonly float[] GameSpeeds = { 1f, 2f, 5f, 10f };

    public static bool Available => Application.isEditor || Debug.isDebugBuild;

    // --- Time ---

    // The flat view and the outlines of characters behind buildings
    public static void ToggleFlatView() { if (CityView.Instance != null) CityView.Instance.Toggle(); }
    public static void ToggleSilhouettes() { if (CityView.Instance != null) CityView.Instance.Silhouettes = !CityView.Instance.Silhouettes; }

    // Lighting: switch the day / night tint off, or look at one hour of the day (every tap moves it 3 hours on; after the last it follows the clock again)
    public static void ToggleLighting()
    {
        if (DayNightCycle.Instance != null) DayNightCycle.Instance.LightingOn = !DayNightCycle.Instance.LightingOn;
    }

    public static void CyclePreviewHour()
    {
        DayNightCycle cycle = DayNightCycle.Instance;
        if (cycle == null) return;
        if (!cycle.PreviewHour.HasValue) cycle.PreviewHour = 0f;
        else if (cycle.PreviewHour.Value >= 21f) cycle.PreviewHour = null;
        else cycle.PreviewHour = cycle.PreviewHour.Value + 3f;
    }

    public static string DescribeLighting()
    {
        DayNightCycle cycle = DayNightCycle.Instance;
        if (cycle == null) return "Lighting: not running";
        string mode = cycle.PreviewHour.HasValue ? "previewing " : "clock ";
        return $"Lighting {(cycle.LightingOn ? "on" : "off")}: {mode}{cycle.Hour:00.0}h, {cycle.Stage}";
    }

    public static void NextTimeOfDay()
    {
        GameClock.Instance.Advance();
    }

    // Skips ahead to the start of the next day (which is when wages are paid)
    public static void SkipToMorning()
    {
        int guard = 0;
        do { GameClock.Instance.Advance(); } while (GameClock.Instance.Current != TimeOfDay.Morning && ++guard < 10);
    }

    // Cycles how fast the whole game runs (1x, 2x, 5x, 10x). Returns the new speed.
    public static float CycleGameSpeed()
    {
        int next = 0;
        for (int i = 0; i < GameSpeeds.Length; i++)
        {
            if (Mathf.Abs(Time.timeScale - GameSpeeds[i]) < 0.01f) { next = (i + 1) % GameSpeeds.Length; break; }
        }
        Time.timeScale = GameSpeeds[next];
        return Time.timeScale;
    }

    // Switches between the game's own clock and the real-time clock (remembered for next time)
    public static ClockMode ToggleClockMode()
    {
        GameClock clock = GameClock.Instance;
        clock.SetMode(clock.IsRealTime ? ClockMode.InGame : ClockMode.RealTime);
        return clock.Mode;
    }

    // Puts the real-time clock back to the real time after skipping around
    public static void ResetTimeOffset()
    {
        GameClock.Instance.ResetDebugOffset();
    }

    // Turns the automatic loading and saving (real time only) on or off
    public static bool ToggleAutomaticSession()
    {
        GameSession.Automatic = !(PlayerPrefs.GetInt(GameSession.AutoPreference, 1) == 1);
        return PlayerPrefs.GetInt(GameSession.AutoPreference, 1) == 1;
    }

    // Pretends the game was closed for this many hours and gives what that would have earned
    public static OfflineProgress.Report SimulateTimeAway(float hours)
    {
        return OfflineProgress.Apply(hours * 3600f);
    }

    public static string DescribeClock()
    {
        GameClock clock = GameClock.Instance;
        if (!clock.IsRealTime) return $"Clock: in-game, {clock.Current}, {clock.Elapsed:0}s into it";
        string offset = clock.DebugOffset > System.TimeSpan.Zero ? $"  (testing offset +{clock.DebugOffset.TotalHours:0.#} h)" : "";
        return $"Clock: real time {clock.LocalNow:HH:mm}, {clock.Current}, day {clock.DayNumber}{offset}";
    }

    public static void RunPayroll()
    {
        if (WageSystem.Instance != null) WageSystem.Instance.RunPayroll();
    }

    // --- Resources ---

    public static void AddGold(float amount) { ResourceManager.Instance.changeResourceAmount(ResourceManager.Gold, amount); }
    // 100 of every item the game knows (except gold, wood and letters, which have their own cheats), up to each limit
    public static void AddItems(float amount = 100f)
    {
        foreach (ItemType item in ItemCatalog.All)
        {
            if (item.category == ItemCategory.Currency || item.Name == ResourceManager.Wood) continue;
            ResourceManager.Instance.Add(item.Name, amount);
        }
        UIManager.Instance?.UpdateResourceUI(ResourceManager.Instance.resources);
    }

    // Sets everyone's satiety (100 = full, 10 = very hungry)
    public static void SetEveryoneSatiety(float satiety)
    {
        foreach (OwnedCharacter c in CharacterManager.Instance.Roster) { c.Satiety = satiety; if (satiety >= 100f) c.Hunger.LastMealKey = -1; }
    }

    // Everyone who wants to eat tries now, whatever the time (a meal window is not needed)
    public static void MealNow()
    {
        FoodSettings settings = FoodSettings.Current;
        foreach (OwnedCharacter c in CharacterManager.Instance.Roster)
        {
            if (c.Satiety >= settings.mealRestore * 2f) continue;
            MealResult meal = HungerRules.TryEat(c.Hunger, c.Type.favouriteFood, ResourceManager.Instance, 12f, GameClock.Instance.MealDay, settings);
            if (meal.Any && c.Avatar != null && meal.Main != null) c.Avatar.ShowEating(meal.Main, settings.eatSeconds);
            c.Mood += c.Hunger.PendingMood; c.Hunger.PendingMood = 0f;
        }
        UIManager.Instance?.UpdateResourceUI(ResourceManager.Instance.resources);
    }

    // Empties the stock of every item except gold, wood and letters (to see shortages)
    public static void EmptyItems()
    {
        foreach (ItemType item in ItemCatalog.All)
        {
            if (item.category == ItemCategory.Currency || item.Name == ResourceManager.Wood) continue;
            ResourceManager.Instance.SetResourceAmount(item.Name, 0f);
        }
    }

    public static void AddWood(float amount) { ResourceManager.Instance.changeResourceAmount(ResourceManager.Wood, amount); }
    // With a server that owns the letters, only the server can grant them (and only a development server will)
    public static void AddLetters(int amount)
    {
        IGameBackend backend = Backend.Current;
        if (!backend.ServerOwnsBalances)
        {
            ResourceManager.Instance.changeResourceAmount(ResourceManager.Letters, amount);
            return;
        }
        backend.DevAction(new DevRequest { action = "grantLetters", amount = amount }, response =>
        {
            if (response.ok) ResourceManager.Instance.SetResourceAmount(ResourceManager.Letters, response.lettersAfter);
            else Debug.LogWarning("The server did not grant letters: " + response.error);
        });
    }

    // Sets gold to exactly zero (to see what happens when wages cannot be paid)
    public static void ZeroGold()
    {
        ResourceManager.Instance.SetResourceAmount(ResourceManager.Gold, 0);
    }

    // --- Characters ---

    // Adds one character of the type to the roster whatever the limit on how many may be owned,
    // gives it a home if there is room, and puts it in front of the tavern (or in the middle of the view)
    public static OwnedCharacter SpawnCharacter(CharacterType type)
    {
        if (type == null) return null;

        OwnedCharacter owned = new OwnedCharacter(type);
        CharacterManager.Instance.Restore(owned);

        Building home = CharacterManager.Instance.FindHomeWithSpace();
        if (home != null) CharacterManager.Instance.AssignResident(owned, home);

        GachaSystem gacha = GachaSystem.Instance;
        Tavern tavern = gacha != null ? gacha.FindTavern() : null;
        Vector3 where = tavern != null ? tavern.SpawnPoint : ViewCentre();
        CharacterManager.Instance.SpawnAvatar(owned, where);
        return owned;
    }

    private static Vector3 ViewCentre()
    {
        Camera cam = Camera.main;
        return cam != null ? new Vector3(cam.transform.position.x, cam.transform.position.y, 0f) : Vector3.zero;
    }

    public static void SetEveryone(float mood, float loyalty)
    {
        foreach (OwnedCharacter owned in CharacterManager.Instance.Roster)
        {
            if (mood >= 0f) owned.Mood = mood;
            if (loyalty >= 0f) owned.Loyalty = loyalty;
        }
    }

    public static void DismissAllCharacters()
    {
        CharacterManager.Instance.ClearAll();
    }

    // --- World ---

    public static WorldState CycleWorldState()
    {
        int count = System.Enum.GetValues(typeof(WorldState)).Length;
        GameManager.Instance.worldState = (WorldState)(((int)GameManager.Instance.worldState + 1) % count);
        return GameManager.Instance.worldState;
    }

    public static void RemoveAllBuildings()
    {
        BuildingManager.Instance.ClearAll();
    }

    public static void RemoveAllRoads()
    {
        RoadManager.Instance.ClearAll();
    }

    public static void ResetRecruitment()
    {
        if (Backend.Current.ServerOwnsBalances)
        {
            Backend.Current.DevAction(new DevRequest { action = "resetPity" }, response =>
            {
                if (!response.ok) Debug.LogWarning("The server did not reset the pity: " + response.error);
            });
        }
        if (GachaSystem.Instance != null) GachaSystem.Instance.RestoreState(0, null);
    }

    // Which backend is in use, and who the player is on it
    public static string DescribeBackend()
    {
        IGameBackend backend = Backend.Current;
        FirebaseBackend online = backend as FirebaseBackend;
        string who = online != null && !string.IsNullOrEmpty(online.UserId) ? " as " + online.UserId.Substring(0, Mathf.Min(8, online.UserId.Length)) + "..." : "";
        return "Backend: " + backend.Name + who + (BackendSelector.FirebaseConfigured ? "" : " (no FirebaseConfig.json: Firebase is not set up)");
    }

    // Use the local backend even though Firebase is set up (or the other way round). Takes effect at the next start.
    public static bool ToggleBackendPreference()
    {
        BackendSelector.SetLocal(!BackendSelector.WantsLocal);
        return BackendSelector.WantsLocal;
    }

    // Makes this device a new player on the server at the next start
    public static void ForgetAccount()
    {
        FirebaseBackend online = Backend.Current as FirebaseBackend;
        if (online != null) online.Auth.ForgetAccount();
    }

    // --- Data ---

    // Back to a brand-new game: nobody, nothing built, starting resources, morning of the first day. Also deletes the save file,
    // so the old game cannot be loaded back by accident.
    public static void ResetGame()
    {
        if (StressTest.Instance != null) StressTest.Instance.Clear();
        if (UIManager.Instance != null) UIManager.Instance.CloseEverything();
        if (Backend.Current.ServerOwnsBalances)
        {
            // The server forgets this player's letters, pity and saves too (a development server only)
            Backend.Current.DevAction(new DevRequest { action = "reset" }, response =>
            {
                if (!response.ok) Debug.LogWarning("The server did not reset: " + response.error);
            });
        }

        CharacterManager.Instance.ClearAll();
        BuildingManager.Instance.ClearAll();
        RoadManager.Instance.ClearAll();
        ResourceManager.Instance.ResetResources();
        GameManager.Instance.worldState = WorldState.Peace;
        GameClock.Instance.SetTime(TimeOfDay.Morning, 0f); // (does nothing in real time)
        GameClock.Instance.ResetDebugOffset();
        if (WageSystem.Instance != null) WageSystem.Instance.LastPayrollDay = -1;
        ResetRecruitment();
        Time.timeScale = 1f;
        SaveSystem.Delete();
    }

    public static bool DeleteSaveFile()
    {
        return SaveSystem.Delete();
    }
}
