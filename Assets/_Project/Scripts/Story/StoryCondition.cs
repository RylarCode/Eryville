using System;
using System.Collections.Generic;

// What the story can ask about the town. Implemented over the real managers by UnityStoryWorld; tests use a fake one.
public interface IStoryWorld
{
    int BuildingCount(BuildingType type);   // null: any building
    int CharacterCount(CharacterType type); // null: any character
    float ItemAmount(string item);
    void Give(string item, float amount);
    QuestStatus QuestStatus(string questId);
    void StartQuest(string questId);
    int Loyalty(CharacterType type);                 // the owned character's loyalty (0..100), or -1 if not owned
    void AddLoyalty(CharacterType type, int amount);
}

// One thing that must be true: a story value, a finished chapter, something the town has. Chapters list these in "Requires", scripts
// use them in "[if ...]". To make the story look at something new, add a Kind here and a case in Test.
// Some conditions are about a character (Loyalty): with no character picked they are about the *subject*, the character whose
// Connection the chapter or quest belongs to (a Talk chapter's Talk To, a quest's Character).
[Serializable]
public class StoryCondition
{
    public enum Kind { Value, ChapterDone, ChapterNotDone, HasBuilding, HasCharacter, HasItem, QuestDone, QuestActive, Loyalty }
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
    [UnityEngine.Tooltip("HasCharacter: empty means any character. Loyalty: whose (empty: the chapter's or quest's own character)")]
    public CharacterType character;
    [UnityEngine.Tooltip("HasItem")]
    public ItemType item;
    [UnityEngine.Tooltip("QuestDone (finished and claimed) / QuestActive (started, not yet claimed). Set Compare to 'Not Equal' to mean 'not'.")]
    public Quest quest;

    public string ChapterId => chapter != null ? chapter.Id : name;
    public string QuestId => quest != null ? quest.Id : name;

    // subject: the character the chapter or quest belongs to (for Loyalty with no character picked)
    public bool Test(StoryState state, IStoryWorld world, CharacterType subject = null)
    {
        switch (kind)
        {
            case Kind.Value: return Check(state.Get(name));
            case Kind.ChapterDone: return state.IsDone(ChapterId);
            case Kind.ChapterNotDone: return !state.IsDone(ChapterId);
            case Kind.HasBuilding: return world != null && Check(world.BuildingCount(building));
            case Kind.HasCharacter: return world != null && Check(world.CharacterCount(character));
            case Kind.HasItem: return world != null && Check((int)Math.Floor(world.ItemAmount(item != null ? item.Name : name)));
            case Kind.QuestDone: return world != null && (world.QuestStatus(QuestId) == global::QuestStatus.Complete) != (compare == Compare.NotEqual);
            case Kind.QuestActive:
            {
                QuestStatus status = world != null ? world.QuestStatus(QuestId) : global::QuestStatus.Locked;
                return (status == global::QuestStatus.Active || status == global::QuestStatus.Ready) != (compare == Compare.NotEqual);
            }
            case Kind.Loyalty:
            {
                int loyalty = LoyaltyOf(world, subject);
                return loyalty >= 0 && Check(loyalty);
            }
        }
        return false;
    }

    // Whose loyalty: the picked character, or the subject. -1 when there is nobody (not owned)
    private int LoyaltyOf(IStoryWorld world, CharacterType subject)
    {
        CharacterType who = character != null ? character : subject;
        return world != null && who != null ? world.Loyalty(who) : -1;
    }

    // How far along this condition is, for showing progress: "2 of 3". Counting conditions (at least N of something) give the count and N;
    // everything else is 0 or 1 of 1.
    public void Measure(StoryState state, IStoryWorld world, out int current, out int target, CharacterType subject = null)
    {
        int actual = -1;
        switch (kind)
        {
            case Kind.Value: actual = state.Get(name); break;
            case Kind.HasBuilding: actual = world != null ? world.BuildingCount(building) : 0; break;
            case Kind.HasCharacter: actual = world != null ? world.CharacterCount(character) : 0; break;
            case Kind.HasItem: actual = world != null ? (int)Math.Floor(world.ItemAmount(item != null ? item.Name : name)) : 0; break;
            case Kind.Loyalty: actual = Math.Max(0, LoyaltyOf(world, subject)); break;
        }
        if (actual >= 0 && compare == Compare.AtLeast && amount > 1)
        {
            target = amount;
            current = Math.Min(actual, amount);
            return;
        }
        target = 1;
        current = Test(state, world, subject) ? 1 : 0;
    }

    // A plain-language name for the condition ("Build a Gold Mine"), used when an objective has no label of its own
    public string Friendly()
    {
        switch (kind)
        {
            case Kind.HasBuilding:
            {
                string label = building != null ? building.buildingName : "building";
                return amount <= 1 ? $"Build a {label}" : $"Have {amount} {label} buildings";
            }
            case Kind.HasCharacter:
            {
                string label = character != null ? character.displayName : "character";
                return amount <= 1 ? $"Get a {label}" : $"Have {amount} {label} characters";
            }
            case Kind.HasItem: return $"Have {amount} {(item != null ? item.Name : name)}";
            case Kind.ChapterDone: return $"Finish the story chapter \"{(chapter != null ? chapter.Title : name)}\"";
            case Kind.QuestDone: return $"Complete the quest \"{(quest != null ? quest.Title : name)}\"";
            case Kind.Loyalty: return character != null ? $"{character.displayName}'s loyalty {amount}" : $"Loyalty {amount}";
            default: return Describe();
        }
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

    public static bool All(IEnumerable<StoryCondition> conditions, StoryState state, IStoryWorld world, CharacterType subject = null)
    {
        if (conditions == null) return true;
        foreach (StoryCondition condition in conditions) if (condition != null && !condition.Test(state, world, subject)) return false;
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
            case Kind.QuestDone: return $"{(compare == Compare.NotEqual ? "not " : "")}quest done {QuestId}";
            case Kind.QuestActive: return $"{(compare == Compare.NotEqual ? "not " : "")}quest active {QuestId}";
            case Kind.Loyalty: return $"loyalty{(character != null ? " (" + character.displayName + ")" : "")} {Op()} {amount}";
            default: return $"{(item != null ? item.Name : name)} {Op()} {amount}";
        }
    }
}
