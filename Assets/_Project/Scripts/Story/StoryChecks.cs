using System;
using System.Collections.Generic;

// Mistakes that are easy to make when writing the story (Eryville > Check Story lists them)
public static class StoryChecks
{
    public static List<string> Problems(IEnumerable<StoryChapter> chapters)
    {
        List<string> problems = new List<string>();
        List<StoryChapter> all = new List<StoryChapter>();
        foreach (StoryChapter chapter in chapters) if (chapter != null) all.Add(chapter);

        Dictionary<string, StoryChapter> byId = new Dictionary<string, StoryChapter>(StringComparer.OrdinalIgnoreCase);
        foreach (StoryChapter chapter in all)
        {
            if (byId.TryGetValue(chapter.Id, out StoryChapter other)) problems.Add($"\"{chapter.name}\" and \"{other.name}\" have the same id \"{chapter.Id}\": saves would mix them up.");
            else byId[chapter.Id] = chapter;
        }

        foreach (StoryChapter chapter in all)
        {
            string who = $"\"{chapter.name}\"";
            if (string.IsNullOrWhiteSpace(chapter.ScriptText)) problems.Add($"{who} has no script.");
            else if (chapter.Beats.Count == 0) problems.Add($"{who}: the script has no lines.");
            foreach (string problem in chapter.ScriptProblems) problems.Add($"{who}: {problem}");
            if (chapter.trigger == StoryTrigger.Talk && chapter.talkTo == null) problems.Add($"{who} is a Talk chapter but has nobody to talk to.");
            if (chapter.trigger != StoryTrigger.Talk && chapter.talkTo != null) problems.Add($"{who} has a character to talk to but its trigger is {chapter.trigger}.");

            foreach (StoryCondition condition in chapter.requires) CheckCondition(condition, who + " (requires)", byId, problems);
            foreach (StoryBeat beat in chapter.Beats) CheckBeat(beat, who, byId, problems);
            foreach (StoryEffect reward in chapter.rewards) CheckEffect(reward, who + " (rewards)", problems);
        }
        return problems;
    }

    private static void CheckBeat(StoryBeat beat, string who, Dictionary<string, StoryChapter> byId, List<string> problems)
    {
        foreach (StoryCondition condition in beat.onlyIf) CheckCondition(condition, $"{who} line {beat.sourceLine}", byId, problems);
        if (beat.effect != null) CheckEffect(beat.effect, $"{who} line {beat.sourceLine}", problems);
        foreach (StoryOption option in beat.options)
        {
            foreach (StoryCondition condition in option.onlyIf) CheckCondition(condition, $"{who} line {beat.sourceLine}", byId, problems);
            foreach (StoryEffect effect in option.effects) CheckEffect(effect, $"{who} line {beat.sourceLine}", problems);
            foreach (StoryBeat reply in option.replies) CheckBeat(reply, who, byId, problems);
        }
    }

    private static void CheckCondition(StoryCondition condition, string where, Dictionary<string, StoryChapter> byId, List<string> problems)
    {
        if (condition == null) return;
        bool chapterKind = condition.kind == StoryCondition.Kind.ChapterDone || condition.kind == StoryCondition.Kind.ChapterNotDone;
        if (chapterKind && !byId.ContainsKey(condition.ChapterId ?? "")) problems.Add($"{where}: there is no chapter with the id \"{condition.ChapterId}\".");
        if (condition.kind == StoryCondition.Kind.Value && string.IsNullOrWhiteSpace(condition.name)) problems.Add($"{where}: a value condition without a name.");
    }

    private static void CheckEffect(StoryEffect effect, string where, List<string> problems)
    {
        if (effect != null && string.IsNullOrWhiteSpace(effect.name)) problems.Add($"{where}: a change without a name.");
    }
}
