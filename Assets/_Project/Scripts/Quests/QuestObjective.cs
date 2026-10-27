using System;

// One thing a quest asks for, with a progress display ("Build a Gold Mine", "Have 3 Gold Mine buildings (2/3)").
// For now an objective is a *state goal*: a condition about the town as it is (own N of a building, hold N of an item, a story value, a
// finished chapter...), checked again and again until it holds. Event goals ("make 50 bread", "recruit 5 characters"), which count things as
// they happen while the quest is active, slot in as another Kind that keeps its count in the quest log.
[Serializable]
public class QuestObjective
{
    public enum Kind { State }

    public Kind kind = Kind.State;
    [UnityEngine.Tooltip("What the player reads. Empty: made up from the condition ('Build a Gold Mine').")]
    public string label;
    public StoryCondition condition = new StoryCondition();

    // The text shown for it, with progress when it counts ("Have 3 Gold Mine buildings (2/3)")
    public string Text(StoryState state, IStoryWorld world)
    {
        string text = !string.IsNullOrWhiteSpace(label) ? label.Trim() : condition.Friendly();
        condition.Measure(state, world, out int current, out int target);
        return target > 1 ? $"{text} ({current}/{target})" : text;
    }

    public bool IsMet(StoryState state, IStoryWorld world)
    {
        return condition.Test(state, world);
    }
}
