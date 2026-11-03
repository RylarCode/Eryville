using System;
using System.Collections.Generic;

// One change a chapter makes: add to a story value, set it, or give the player some of an item (gold, wood, letters, wheat...).
// Used for a chapter's rewards, for what a choice does, and (written as "@add kindness 1") inside a script.
[Serializable]
public class StoryEffect
{
    public enum Kind { Add, Set, Give, StartQuest, Loyalty, Buff }

    public Kind kind = Kind.Add;
    public string name;     // the story value, the item for Give, or the quest id for StartQuest (when no quest is picked)
    public int amount = 1;
    [UnityEngine.Tooltip("StartQuest: the quest to start")]
    public Quest quest;
    [UnityEngine.Tooltip("Loyalty / Buff: whose (empty: the chapter's or quest's own character)")]
    public CharacterType character;
    [UnityEngine.Tooltip("Buff: which stat, and its multiplier (1.1 = +10%; below 1 for DailyWage / MoodDrain is better). Lasts as long as the chapter stays finished or the quest complete.")]
    public BuffStat stat = BuffStat.Productivity;
    public float multiplier = 1.1f;

    public string QuestId => quest != null ? quest.Id : name;

    public StoryEffect() { }

    public StoryEffect(Kind kind, string name, int amount)
    {
        this.kind = kind;
        this.name = name;
        this.amount = amount;
    }

    // Applies it to the values. A Give, StartQuest or Loyalty is not applied here: it is added to gives, for whoever owns the stock / the
    // quests / the characters to do. A Buff is never applied: it holds while what gave it stays finished.
    public void Apply(StoryState state, List<StoryEffect> gives)
    {
        switch (kind)
        {
            case Kind.Add: state.Add(name, amount); break;
            case Kind.Set: state.Set(name, amount); break;
            case Kind.Give:
            case Kind.StartQuest:
            case Kind.Loyalty: if (gives != null) gives.Add(this); break;
            case Kind.Buff: break; // not applied: a buff holds while what gave it is finished (see ConnectionBuffs)
        }
    }

    public string Describe()
    {
        switch (kind)
        {
            case Kind.Add: return $"{name} {(amount >= 0 ? "+" : "")}{amount}";
            case Kind.Set: return $"{name} = {amount}";
            case Kind.StartQuest: return $"start quest {QuestId}";
            case Kind.Loyalty: return $"loyalty{(character != null ? " (" + character.displayName + ")" : "")} {(amount >= 0 ? "+" : "")}{amount}";
            case Kind.Buff: return $"{stat} x{multiplier:0.##}{(character != null ? " (" + character.displayName + ")" : "")}";
            default: return $"give {amount} {name}";
        }
    }
}
