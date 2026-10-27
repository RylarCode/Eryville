using System;
using System.Collections.Generic;
using UnityEngine;

// One quest: what is asked, when it shows up, and what the reward is. Make one with Assets > Create > Scriptable Objects > Quest (or
// Eryville > New Quest) in a Resources/Quests folder, and the game finds it. See docs/QUESTS.md.
//
// Main quests come one at a time, in order (the next one starts only once the previous one's reward has been claimed). Side quests are
// available whenever their requirements are met, as many at once as there are. Nothing fails, is declined or abandoned.
[CreateAssetMenu(fileName = "NewQuest", menuName = "Scriptable Objects/Quest")]
public class Quest : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Short unique name used by saves, stories and the server (e.g. 'first-building'). Empty: the asset's name. Do not change it once players have saves.")]
    public string id;
    public string title;
    [TextArea(2, 5)] public string description;
    public QuestCategory category = QuestCategory.Main;
    [Tooltip("Main quests are tried in this order; side quests are listed in it.")]
    public int order;

    [Header("When it starts")]
    [Tooltip("Automatic: starts by itself. Offered: shows as available and waits for the player to accept it (a story chapter can also start it with '@quest id').")]
    public QuestStart start = QuestStart.Automatic;
    [Tooltip("Everything here must be true before it starts. Put 'Quest done' here to put quests in order, 'Chapter done' to follow the story.")]
    public List<StoryCondition> requires = new List<StoryCondition>();

    [Header("What it asks")]
    [Tooltip("All of these must be done. When they are, the quest is ready to claim.")]
    public List<QuestObjective> objectives = new List<QuestObjective>();

    [Header("What it gives")]
    [Tooltip("Given when the player claims it: items (Gold, Wood, Letters, Wheat...), story values, or starting another quest. Letters are paid by the server when playing online: they must be listed in firebase/functions/src/config.ts.")]
    public List<StoryEffect> rewards = new List<StoryEffect>();

    public string Id => !string.IsNullOrWhiteSpace(id) ? id.Trim() : name;
    public string Title => !string.IsNullOrWhiteSpace(title) ? title.Trim() : Id;

    // The items the reward gives, as a short text ("100 Gold, 1 Letters"); empty if it gives none
    public string RewardText(bool includeLetters = true)
    {
        List<string> parts = new List<string>();
        foreach (StoryEffect reward in rewards)
        {
            if (reward == null || reward.kind != StoryEffect.Kind.Give || string.IsNullOrWhiteSpace(reward.name)) continue;
            if (!includeLetters && reward.name.Trim() == ResourceManager.Letters) continue;
            parts.Add($"{reward.amount} {reward.name.Trim()}");
        }
        return string.Join(", ", parts);
    }

    // How many recruitment letters it pays
    public int Letters
    {
        get
        {
            int total = 0;
            foreach (StoryEffect reward in rewards) if (reward != null && reward.kind == StoryEffect.Kind.Give && reward.name != null && reward.name.Trim() == ResourceManager.Letters) total += reward.amount;
            return total;
        }
    }
}
