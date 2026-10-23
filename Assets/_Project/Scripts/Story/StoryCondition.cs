using System;
using System.Collections.Generic;

// What the story can ask about the town. Implemented over the real managers by UnityStoryWorld; tests use a fake one.
public interface IStoryWorld
{
    int BuildingCount(BuildingType type);   // null: any building
    int CharacterCount(CharacterType type); // null: any character
    float ItemAmount(string item);
    void Give(string item, float amount);
}

// One thing that must be true: a story value, a finished chapter, something the town has. Chapters list these in "Requires", scripts
// use them in "[if ...]". To make the story look at something new, add a Kind here and a case in Test.
[Serializable]
public class StoryCondition
{
    public enum Kind { Value, ChapterDone, ChapterNotDone, HasBuilding, HasCharacter, HasItem }
    public enum Compare { AtLeast, AtMost, Exactly, NotEqual }

    public Kind kind = Kind.Value;
    [UnityEngine.Tooltip("Value: the story value's name. Chapter kinds: the chapter's id (when no chapter is picked). HasItem: the item's name (when no item is picked).")]
    public string name;
    public Compare compare = Compare.AtLeast;
    public int amount = 1;
    [UnityEngine.Tooltip("ChapterDone / ChapterNotDone")]
    public StoryChapter chapter;
    [UnityEngine.Tooltip("HasBuilding: empty means any building")]
    public BuildingType building;
    [UnityEngine.Tooltip("HasCharacter: empty means any character")]
    public CharacterType character;
    [UnityEngine.Tooltip("HasItem")]
    public ItemType item;

    public string ChapterId => chapter != null ? chapter.Id : name;

    public bool Test(StoryState state, IStoryWorld world)
    {
        switch (kind)
        {
            case Kind.Value: return Check(state.Get(name));
            case Kind.ChapterDone: return state.IsDone(ChapterId);
            case Kind.ChapterNotDone: return !state.IsDone(ChapterId);
            case Kind.HasBuilding: return world != null && Check(world.BuildingCount(building));
            case Kind.HasCharacter: return world != null && Check(world.CharacterCount(character));
            case Kind.HasItem: return world != null && Check((int)Math.Floor(world.ItemAmount(item != null ? item.Name : name)));
        }
        return false;
    }

    private bool Check(int actual)
    {
        switch (compare)
        {
            case Compare.AtLeast: return actual >= amount;
            case Compare.AtMost: return actual <= amount;
            case Compare.Exactly: return actual == amount;
            default: return actual != amount;
        }
    }

    public static bool All(IEnumerable<StoryCondition> conditions, StoryState state, IStoryWorld world)
    {
        if (conditions == null) return true;
        foreach (StoryCondition condition in conditions) if (condition != null && !condition.Test(state, world)) return false;
        return true;
    }

    private string Op()
    {
        switch (compare)
        {
            case Compare.AtLeast: return ">=";
            case Compare.AtMost: return "<=";
            case Compare.Exactly: return "==";
            default: return "!=";
        }
    }

    public string Describe()
    {
        switch (kind)
        {
            case Kind.Value: return $"{name} {Op()} {amount}";
            case Kind.ChapterDone: return $"done {ChapterId}";
            case Kind.ChapterNotDone: return $"not done {ChapterId}";
            case Kind.HasBuilding: return $"buildings ({(building != null ? building.buildingName : "any")}) {Op()} {amount}";
            case Kind.HasCharacter: return $"characters ({(character != null ? character.displayName : "any")}) {Op()} {amount}";
            default: return $"{(item != null ? item.Name : name)} {Op()} {amount}";
        }
    }
}
