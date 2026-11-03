using System.Collections.Generic;

// The buffs a character has earned through its Connection: every Buff reward of a finished Talk chapter (whose Talk To is the
// character) or of a completed quest (whose Character is the character), unless the reward names another character. They hold for as
// long as what gave them stays finished, so nothing extra is saved: they are worked out again whenever the story or the quests change.
// OwnedCharacter.BuffMultiplier multiplies them in with self-awareness.
public static class ConnectionBuffs
{
    private static readonly Dictionary<CharacterType, List<StoryEffect>> byCharacter = new Dictionary<CharacterType, List<StoryEffect>>();
    private static string builtFor;

    // A short text that changes whenever the buffs could have changed (so callers can tell)
    public static string Stamp
    {
        get
        {
            StoryManager story = StoryManager.Instance;
            QuestManager quests = QuestManager.Instance;
            return (story != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(story) + ":" + story.State.Version + ":" + story.Chapters.Count : "-") + "|" +
                   (quests != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(quests) + ":" + quests.Log.Version + ":" + quests.Quests.Count : "-");
        }
    }

    public static float Multiplier(CharacterType type, BuffStat stat)
    {
        if (type == null) return 1f;
        Ensure();
        if (!byCharacter.TryGetValue(type, out List<StoryEffect> buffs)) return 1f;
        float multiplier = 1f;
        foreach (StoryEffect buff in buffs) if (buff.stat == stat) multiplier *= buff.multiplier;
        return multiplier;
    }

    // Every buff the character has earned (for showing them)
    public static IReadOnlyList<StoryEffect> For(CharacterType type)
    {
        Ensure();
        return type != null && byCharacter.TryGetValue(type, out List<StoryEffect> buffs) ? buffs : (IReadOnlyList<StoryEffect>)new List<StoryEffect>();
    }

    private static void Ensure()
    {
        string stamp = Stamp;
        if (stamp == builtFor) return;
        builtFor = stamp;
        byCharacter.Clear();

        StoryManager story = StoryManager.Instance;
        if (story != null)
        {
            foreach (StoryChapter chapter in story.Chapters)
            {
                if (!story.IsDone(chapter)) continue;
                foreach (StoryEffect reward in chapter.rewards) Collect(reward, chapter.talkTo);
            }
        }

        QuestManager quests = QuestManager.Instance;
        if (quests != null)
        {
            foreach (Quest quest in quests.Quests)
            {
                if (quests.StatusOf(quest) != QuestStatus.Complete) continue;
                foreach (StoryEffect reward in quest.rewards) Collect(reward, quest.character);
            }
        }
    }

    private static void Collect(StoryEffect reward, CharacterType owner)
    {
        if (reward == null || reward.kind != StoryEffect.Kind.Buff) return;
        CharacterType who = reward.character != null ? reward.character : owner;
        if (who == null) return;
        if (!byCharacter.TryGetValue(who, out List<StoryEffect> list)) byCharacter[who] = list = new List<StoryEffect>();
        list.Add(reward);
    }
}
