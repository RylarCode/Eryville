using System;
using System.Collections.Generic;

public enum ConnectionState
{
    Locked,    // its requirements are not met yet
    Available, // a chapter that can be played, or a quest that can be accepted
    Active,    // a quest being worked on
    Ready,     // a quest whose reward can be claimed
    Done       // a finished chapter, a completed quest
}

// One entry in a character's Connection: a Talk chapter or a quest that belongs to the character
public class ConnectionItem
{
    public StoryChapter Chapter;
    public Quest Quest;

    public int Order => Chapter != null ? Chapter.order : Quest.order;
    public string Title => Chapter != null ? Chapter.Title : Quest.Title;
    public string Key => Chapter != null ? "chapter:" + Chapter.Id : "quest:" + Quest.Id;
}

// A character's Connection: the story chapters (Trigger Talk, Talk To the character) and quests (Character set to the character) that
// belong to it, in order, with where each one is. Story characters are unique (Max Owned 1), so a character type is one person.
public static class Connection
{
    private static readonly StoryState noStory = new StoryState();

    public static List<ConnectionItem> ItemsFor(CharacterType type)
    {
        List<ConnectionItem> items = new List<ConnectionItem>();
        if (type == null) return items;
        if (StoryManager.Instance != null)
        {
            foreach (StoryChapter chapter in StoryManager.Instance.Chapters)
            {
                if (chapter.trigger == StoryTrigger.Talk && chapter.talkTo == type) items.Add(new ConnectionItem { Chapter = chapter });
            }
        }
        if (QuestManager.Instance != null)
        {
            foreach (Quest quest in QuestManager.Instance.Quests)
            {
                if (quest.character == type) items.Add(new ConnectionItem { Quest = quest });
            }
        }
        items.Sort((a, b) =>
        {
            int byOrder = a.Order.CompareTo(b.Order);
            return byOrder != 0 ? byOrder : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
        });
        return items;
    }

    public static bool HasAny(CharacterType type)
    {
        return ItemsFor(type).Count > 0;
    }

    public static ConnectionState StateOf(ConnectionItem item)
    {
        if (item.Chapter != null)
        {
            StoryManager story = StoryManager.Instance;
            if (story == null) return ConnectionState.Locked;
            if (story.IsDone(item.Chapter)) return ConnectionState.Done;
            return story.IsAvailable(item.Chapter) ? ConnectionState.Available : ConnectionState.Locked;
        }

        QuestManager quests = QuestManager.Instance;
        if (quests == null) return ConnectionState.Locked;
        switch (quests.StatusOf(item.Quest))
        {
            case QuestStatus.Available: return ConnectionState.Available;
            case QuestStatus.Active: return ConnectionState.Active;
            case QuestStatus.Ready: return ConnectionState.Ready;
            case QuestStatus.Complete: return ConnectionState.Done;
            default: return ConnectionState.Locked;
        }
    }

    // How many things wait for the player: chapters to play, quests to accept or claim
    public static int WaitingCount(CharacterType type)
    {
        int count = 0;
        foreach (ConnectionItem item in ItemsFor(type))
        {
            ConnectionState state = StateOf(item);
            if (state == ConnectionState.Available || state == ConnectionState.Ready) count++;
        }
        return count;
    }

    // The first locked entry (only that one is shown, so nothing further is given away)
    public static ConnectionItem NextLocked(CharacterType type)
    {
        foreach (ConnectionItem item in ItemsFor(type)) if (StateOf(item) == ConnectionState.Locked) return item;
        return null;
    }

    // What a locked entry still needs, as the player reads it ("Loyalty 40 (now 32)", "Finish the story chapter ...")
    public static List<string> Missing(ConnectionItem item)
    {
        List<string> missing = new List<string>();
        CharacterType subject = item.Chapter != null ? item.Chapter.talkTo : item.Quest.character;
        List<StoryCondition> requires = item.Chapter != null ? item.Chapter.requires : item.Quest.requires;
        StoryState state = StoryManager.Instance != null ? StoryManager.Instance.State : noStory;
        IStoryWorld world = StoryManager.Instance != null ? StoryManager.Instance.World : new UnityStoryWorld();

        CharacterType owner = item.Chapter != null ? item.Chapter.talkTo : item.Quest.character;
        if (owner != null && world != null && world.CharacterCount(owner) <= 0) missing.Add($"Have {owner.displayName}");
        foreach (StoryCondition condition in requires)
        {
            if (condition == null || condition.Test(state, world, subject)) continue;
            missing.Add(Describe(condition, state, world, subject));
        }
        if (missing.Count == 0 && item.Quest != null && item.Quest.category == QuestCategory.Main) missing.Add("The current main quest must be finished first");
        return missing;
    }

    private static string Describe(StoryCondition condition, StoryState state, IStoryWorld world, CharacterType subject)
    {
        condition.Measure(state, world, out int current, out int target, subject);
        switch (condition.kind)
        {
            case StoryCondition.Kind.Loyalty:
            {
                CharacterType who = condition.character != null ? condition.character : subject;
                int now = who != null && world != null ? world.Loyalty(who) : -1;
                string name = condition.character != null ? condition.character.displayName + "'s loyalty" : "Loyalty";
                return now >= 0 ? $"{name} {condition.amount} (now {now})" : $"{name} {condition.amount}";
            }
            case StoryCondition.Kind.ChapterDone:
            case StoryCondition.Kind.QuestDone:
                return condition.Friendly();
            case StoryCondition.Kind.HasBuilding:
            case StoryCondition.Kind.HasCharacter:
            case StoryCondition.Kind.HasItem:
                return target > 1 ? $"{condition.Friendly()} ({current}/{target})" : condition.Friendly();
            default:
                return "Something else must happen first"; // story values and the like: not spelled out, so nothing is given away
        }
    }
}
