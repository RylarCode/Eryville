using System;
using System.Collections.Generic;

// What the game and a backend say to each other. Plain data with public fields, so it can be turned into
// JSON for a real server (JsonUtility) without any changes.

[Serializable]
public class BackendResult
{
    public bool ok = true;
    public string error = "";
    public bool offline;   // true when the server could not be reached at all (as opposed to refusing)

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
public class BuyLetterRequest
{
    public float clientGold;   // the gold the game says the player has (the server cannot see it, so it bounds it)
}

[Serializable]
public class BuyLetterResponse : BackendResult
{
    public int lettersAfter;
    public float goldAfter;

    public static BuyLetterResponse Failed(string message) { return new BuyLetterResponse { ok = false, error = message }; }
}

// A finished quest's letters. The server pays them (once, and only the amount it has listed for that quest id in
// firebase/functions/src/config.ts); everything else a quest gives is the game's own business.
[Serializable]
public class ClaimQuestRequest
{
    public string questId;
}

[Serializable]
public class ClaimQuestResponse : BackendResult
{
    public int lettersAfter;
    public int lettersGranted;      // 0 when it had been paid before
    public bool alreadyClaimed;

    public static ClaimQuestResponse Failed(string message) { return new ClaimQuestResponse { ok = false, error = message }; }
}

// --- The player's record on the server ---

[Serializable]
public class PlayerStateResponse : BackendResult
{
    public int letters;
    public int totalPulls;
    public List<int> pity = new List<int>();
    public long serverMs;       // the server's clock, in milliseconds since 1970 (UTC)
}

[Serializable]
public class TimeResponse : BackendResult
{
    public long serverMs;
}

// A saved game on the server. (The save itself is text: the same JSON the game writes to a file.)
[Serializable]
public class SaveGameRequest
{
    public string slot;
    public string json;
}

[Serializable]
public class SaveGameResponse : BackendResult
{
    public long savedAtMs;
}

[Serializable]
public class LoadGameRequest
{
    public string slot;
    public bool includeJson = true;
}

[Serializable]
public class LoadGameResponse : BackendResult
{
    public bool exists;
    public string json = "";
    public long savedAtMs;
    public int letters;
    public int totalPulls;
    public List<int> pity = new List<int>();
    public long serverMs;
}

[Serializable]
public class SlotRequest
{
    public string slot;
}

// Testing actions (grant letters, reset); only development servers accept them
[Serializable]
public class DevRequest
{
    public string action;   // "grantLetters", "resetPity", "reset"
    public int amount;
}

[Serializable]
public class DevResponse : BackendResult
{
    public int lettersAfter;
}
