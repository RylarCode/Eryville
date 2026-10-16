using UnityEngine;

// Testing tools: give yourself resources, spawn characters, change the time, reset the game. Everything here
// works on the game through its normal managers, so it behaves like the real thing. The Cheats menu in the
// game menu calls these. Only available in the editor and in Development Builds.
public static class Cheats
{
    public static readonly float[] GameSpeeds = { 1f, 2f, 5f, 10f };

    public static bool Available => Application.isEditor || Debug.isDebugBuild;

    // --- Time ---

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

    public static void RunPayroll()
    {
        if (WageSystem.Instance != null) WageSystem.Instance.RunPayroll();
    }

    // --- Resources ---

    public static void AddGold(float amount) { ResourceManager.Instance.changeResourceAmount(ResourceManager.Gold, amount); }
    public static void AddWood(float amount) { ResourceManager.Instance.changeResourceAmount(ResourceManager.Wood, amount); }
    public static void AddLetters(int amount) { ResourceManager.Instance.changeResourceAmount(ResourceManager.Letters, amount); }

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
        if (GachaSystem.Instance != null) GachaSystem.Instance.RestoreState(0, null);
    }

    // --- Data ---

    // Back to a brand-new game: nobody, nothing built, starting resources, morning of the first day. Also deletes the save file,
    // so the old game cannot be loaded back by accident.
    public static void ResetGame()
    {
        if (StressTest.Instance != null) StressTest.Instance.Clear();
        if (UIManager.Instance != null) UIManager.Instance.CloseEverything();

        CharacterManager.Instance.ClearAll();
        BuildingManager.Instance.ClearAll();
        RoadManager.Instance.ClearAll();
        ResourceManager.Instance.ResetResources();
        GameManager.Instance.worldState = WorldState.Peace;
        GameClock.Instance.SetTime(TimeOfDay.Morning, 0f);
        ResetRecruitment();
        Time.timeScale = 1f;
        SaveSystem.Delete();
    }

    public static bool DeleteSaveFile()
    {
        return SaveSystem.Delete();
    }
}
