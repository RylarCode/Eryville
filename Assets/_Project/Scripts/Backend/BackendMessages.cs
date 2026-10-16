using System;
using System.Collections.Generic;

// What the game and a backend say to each other. Plain data with public fields, so it can be turned into
// JSON for a real server (JsonUtility) without any changes.

[Serializable]
public class BackendResult
{
    public bool ok = true;
    public string error = "";

    public static BackendResult Ok() { return new BackendResult(); }
    public static BackendResult Fail(string message) { return new BackendResult { ok = false, error = message }; }
}

// --- Recruitment ---

[Serializable]
public class PullRequest
{
    public string poolId;   // which pool (GachaPool.poolName)
    public int count;       // 1 or 10
}

[Serializable]
public class PullRollData
{
    public int typeId;          // CharacterType.typeId
    public int rarity;          // Rarity
    public bool pityApplied;
}

[Serializable]
public class PullResponse : BackendResult
{
    public List<PullRollData> rolls = new List<PullRollData>();
    public int lettersAfter;                 // the player's letters once this pull has been paid for
    public int totalPulls;
    public List<int> pity = new List<int>(); // the pity counters afterwards

    public static PullResponse Failed(string message) { return new PullResponse { ok = false, error = message }; }
}

[Serializable]
public class BuyLetterResponse : BackendResult
{
    public int lettersAfter;
    public float goldAfter;

    public static BuyLetterResponse Failed(string message) { return new BuyLetterResponse { ok = false, error = message }; }
}
