using System;
using System.Collections.Generic;

// One change a chapter makes: add to a story value, set it, or give the player some of an item (gold, wood, letters, wheat...).
// Used for a chapter's rewards, for what a choice does, and (written as "@add kindness 1") inside a script.
[Serializable]
public class StoryEffect
{
    public enum Kind { Add, Set, Give, StartQuest }

    public Kind kind = Kind.Add;
    public string name;     // the story value, the item for Give, or the quest id for StartQuest (when no quest is picked)
    public int amount = 1;
    [UnityEngine.Tooltip("StartQuest: the quest to start")]
    public Quest quest;

    public string QuestId => quest != null ? quest.Id : name;

    public StoryEffect() { }

    public StoryEffect(Kind kind, string name, int amount)
    {
        this.kind = kind;
        this.name = name;
        this.amount = amount;
    }

    // Applies it to the values. A Give (and a StartQuest) is not applied here: it is added to gives, for whoever owns the stock / the quests to do.
    public void Apply(StoryState state, List<StoryEffect> gives)
    {
        switch (kind)
        {
            case Kind.Add: state.Add(name, amount); break;
            case Kind.Set: state.Set(name, amount); break;
            case Kind.Give:
            case Kind.StartQuest: if (gives != null) gives.Add(this); break;
        }
    }

    public string Describe()
    {
        switch (kind)
        {
            case Kind.Add: return $"{name} {(amount >= 0 ? "+" : "")}{amount}";
            case Kind.Set: return $"{name} = {amount}";
            case Kind.StartQuest: return $"start quest {QuestId}";
            default: return $"give {amount} {name}";
        }
    }
}
