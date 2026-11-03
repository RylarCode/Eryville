using System;
using System.Collections.Generic;

// Mistakes that are easy to make when writing quests (Eryville > Check Quests lists them)
public static class QuestChecks
{
    public static List<string> Problems(IEnumerable<Quest> quests, IEnumerable<StoryChapter> chapters)
    {
        List<string> problems = new List<string>();
        List<Quest> all = new List<Quest>();
        foreach (Quest quest in quests) if (quest != null) all.Add(quest);
        HashSet<string> chapterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (chapters != null) foreach (StoryChapter chapter in chapters) if (chapter != null) chapterIds.Add(chapter.Id);

        Dictionary<string, Quest> byId = new Dictionary<string, Quest>(StringComparer.OrdinalIgnoreCase);
        foreach (Quest quest in all)
        {
            if (byId.TryGetValue(quest.Id, out Quest other)) problems.Add($"\"{quest.name}\" and \"{other.name}\" have the same id \"{quest.Id}\": saves would mix them up.");
            else byId[quest.Id] = quest;
        }

        foreach (Quest quest in all)
        {
            string who = $"\"{quest.name}\"";
            if (quest.objectives.Count == 0) problems.Add($"{who} has no objectives: it is ready the moment it starts.");
            if (quest.category == QuestCategory.Main && quest.start == QuestStart.Offered) problems.Add($"{who} is a main quest that waits to be accepted: the whole main line waits with it.");

            foreach (StoryCondition condition in quest.requires) CheckCondition(condition, who + " (requires)", byId, chapterIds, chapters != null, problems);
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                QuestObjective objective = quest.objectives[i];
                if (objective == null || objective.condition == null) { problems.Add($"{who}: objective {i + 1} is empty."); continue; }
                CheckCondition(objective.condition, $"{who} (objective {i + 1})", byId, chapterIds, chapters != null, problems);
            }
            foreach (StoryEffect reward in quest.rewards)
            {
                if (reward == null) continue;
                if (reward.kind == StoryEffect.Kind.StartQuest && !byId.ContainsKey(reward.QuestId ?? "")) problems.Add($"{who} (rewards): there is no quest with the id \"{reward.QuestId}\" to start.");
                else if ((reward.kind == StoryEffect.Kind.Add || reward.kind == StoryEffect.Kind.Set || reward.kind == StoryEffect.Kind.Give) && string.IsNullOrWhiteSpace(reward.name)) problems.Add($"{who} (rewards): a reward without a name.");
            }
        }

        // The story may start quests and wait for them: the ids must exist
        if (chapters != null)
        {
            foreach (StoryChapter chapter in chapters)
            {
                if (chapter == null) continue;
                foreach (StoryCondition condition in chapter.requires) CheckCondition(condition, $"\"{chapter.name}\" (requires)", byId, chapterIds, true, problems);
                foreach (StoryEffect reward in chapter.rewards) if (reward != null && reward.kind == StoryEffect.Kind.StartQuest && !byId.ContainsKey(reward.QuestId ?? "")) problems.Add($"\"{chapter.name}\" (rewards): there is no quest with the id \"{reward.QuestId}\" to start.");
                CheckBeats(chapter.Beats, $"\"{chapter.name}\"", byId, chapterIds, problems);
            }
        }

        CheckConnections(all, chapters, problems);
        return problems;
    }

    // Story characters are unique, and loyalty and buffs need to know whose
    private static void CheckConnections(List<Quest> quests, IEnumerable<StoryChapter> chapters, List<string> problems)
    {
        HashSet<CharacterType> warned = new HashSet<CharacterType>();
        void Unique(CharacterType type, string who)
        {
            if (type == null || type.maxOwned == 1 || !warned.Add(type)) return;
            problems.Add($"{type.displayName} has a Connection ({who}) but Max Owned is {type.maxOwned}: story characters must be unique (Max Owned 1).");
        }
        bool NeedsSubject(StoryCondition c) => c != null && c.kind == StoryCondition.Kind.Loyalty && c.character == null;
        bool EffectNeedsSubject(StoryEffect e) => e != null && (e.kind == StoryEffect.Kind.Loyalty || e.kind == StoryEffect.Kind.Buff) && e.character == null;

        foreach (Quest quest in quests)
        {
            Unique(quest.character, $"quest \"{quest.name}\"");
            if (quest.character != null) continue;
            string who = $"\"{quest.name}\"";
            foreach (StoryCondition c in quest.requires) if (NeedsSubject(c)) problems.Add($"{who}: a Loyalty requirement, but whose? Set the quest's Character, or the condition's character.");
            foreach (QuestObjective o in quest.objectives) if (o != null && NeedsSubject(o.condition)) problems.Add($"{who}: a Loyalty objective, but whose? Set the quest's Character, or the condition's character.");
            foreach (StoryEffect e in quest.rewards) if (EffectNeedsSubject(e)) problems.Add($"{who}: a {e.kind} reward, but for whom? Set the quest's Character, or the reward's character.");
        }

        if (chapters == null) return;
        foreach (StoryChapter chapter in chapters)
        {
            if (chapter == null) continue;
            if (chapter.trigger == StoryTrigger.Talk) Unique(chapter.talkTo, $"chapter \"{chapter.name}\"");
            if (chapter.talkTo != null) continue;
            string who = $"\"{chapter.name}\"";
            foreach (StoryCondition c in chapter.requires) if (NeedsSubject(c)) problems.Add($"{who}: a Loyalty requirement, but whose? Make it a Talk chapter, or pick the condition's character.");
            foreach (StoryEffect e in chapter.rewards) if (EffectNeedsSubject(e)) problems.Add($"{who}: a {e.kind} reward, but for whom? Make it a Talk chapter, or pick the reward's character.");
            foreach (StoryBeat beat in Flatten(chapter.Beats))
            {
                foreach (StoryCondition c in beat.onlyIf) if (NeedsSubject(c)) problems.Add($"{who} line {beat.sourceLine}: \"loyalty\" in a chapter with nobody to talk to: whose loyalty?");
                if (EffectNeedsSubject(beat.effect)) problems.Add($"{who} line {beat.sourceLine}: a loyalty change in a chapter with nobody to talk to: whose?");
                foreach (StoryOption option in beat.options) foreach (StoryEffect e in option.effects) if (EffectNeedsSubject(e)) problems.Add($"{who} line {beat.sourceLine}: a loyalty change in a chapter with nobody to talk to: whose?");
            }
        }
    }

    private static IEnumerable<StoryBeat> Flatten(IEnumerable<StoryBeat> beats)
    {
        foreach (StoryBeat beat in beats)
        {
            yield return beat;
            foreach (StoryOption option in beat.options) foreach (StoryBeat reply in Flatten(option.replies)) yield return reply;
        }
    }

    private static void CheckBeats(IEnumerable<StoryBeat> beats, string who, Dictionary<string, Quest> byId, HashSet<string> chapterIds, List<string> problems)
    {
        foreach (StoryBeat beat in beats)
        {
            foreach (StoryCondition condition in beat.onlyIf) CheckCondition(condition, $"{who} line {beat.sourceLine}", byId, chapterIds, true, problems);
            if (beat.effect != null && beat.effect.kind == StoryEffect.Kind.StartQuest && !byId.ContainsKey(beat.effect.QuestId ?? "")) problems.Add($"{who} line {beat.sourceLine}: there is no quest with the id \"{beat.effect.QuestId}\" to start.");
            foreach (StoryOption option in beat.options)
            {
                foreach (StoryCondition condition in option.onlyIf) CheckCondition(condition, $"{who} line {beat.sourceLine}", byId, chapterIds, true, problems);
                foreach (StoryEffect effect in option.effects) if (effect.kind == StoryEffect.Kind.StartQuest && !byId.ContainsKey(effect.QuestId ?? "")) problems.Add($"{who} line {beat.sourceLine}: there is no quest with the id \"{effect.QuestId}\" to start.");
                CheckBeats(option.replies, who, byId, chapterIds, problems);
            }
        }
    }

    private static void CheckCondition(StoryCondition condition, string where, Dictionary<string, Quest> byId, HashSet<string> chapterIds, bool chaptersKnown, List<string> problems)
    {
        if (condition == null) return;
        if ((condition.kind == StoryCondition.Kind.QuestDone || condition.kind == StoryCondition.Kind.QuestActive) && !byId.ContainsKey(condition.QuestId ?? ""))
            problems.Add($"{where}: there is no quest with the id \"{condition.QuestId}\".");
        if (chaptersKnown && (condition.kind == StoryCondition.Kind.ChapterDone || condition.kind == StoryCondition.Kind.ChapterNotDone) && !chapterIds.Contains(condition.ChapterId ?? ""))
            problems.Add($"{where}: there is no story chapter with the id \"{condition.ChapterId}\".");
        if (condition.kind == StoryCondition.Kind.Value && string.IsNullOrWhiteSpace(condition.name)) problems.Add($"{where}: a value condition without a name.");
    }
}
