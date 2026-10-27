using System;

public enum QuestCategory
{
    Main, // the story's thread: one at a time, in order
    Side  // optional: as many at once as are available
}

public enum QuestStart
{
    Automatic, // starts by itself once its requirements are met
    Offered    // waits (shown as available) until the player accepts it, or a story chapter starts it
}

// Where a quest is. Locked: not available yet. Available: offered, waiting to be accepted. Active: being worked on.
// Ready: every objective is done, waiting for the player to claim the reward. Complete: reward claimed.
// (Saved by number: only ever add at the end.)
public enum QuestStatus
{
    Locked,
    Available,
    Active,
    Ready,
    Complete
}

[Serializable]
public class QuestSaveData
{
    public string id;
    public int status; // QuestStatus
}
