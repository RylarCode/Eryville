using System;
using System.Collections.Generic;

// One change a chapter makes: add to a story value, set it, or give the player some of an item (gold, wood, letters, wheat...).
// Used for a chapter's rewards, for what a choice does, and (written as "@add kindness 1") inside a script.
[Serializable]
public class StoryEffect
{
    public enum Kind { Add, Set, Give }

    public Kind kind = Kind.Add;
    public string name;     // the story value, or the item for Give
    public int amount = 1;

    public StoryEffect() { }

    public StoryEffect(Kind kind, string name, int amount)
    {
        this.kind = kind;
        this.name = name;
        this.amount = amount;
    }

    // Applies it to the values. A Give is not applied here: it is added to gives, for whoever owns the stock to do.
    public void Apply(StoryState state, List<StoryEffect> gives)
    {
        switch (kind)
        {
            case Kind.Add: state.Add(name, amount); break;
            case Kind.Set: state.Set(name, amount); break;
            case Kind.Give: if (gives != null) gives.Add(this); break;
        }
    }

    public string Describe()
    {
        switch (kind)
        {
            case Kind.Add: return $"{name} {(amount >= 0 ? "+" : "")}{amount}";
            case Kind.Set: return $"{name} = {amount}";
            default: return $"give {amount} {name}";
        }
    }
}
