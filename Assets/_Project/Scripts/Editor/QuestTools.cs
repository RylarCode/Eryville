using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Menu items under "Eryville" for writing quests
public static class QuestTools
{
    private const string Folder = "Assets/_Project/Resources/Quests";

    // A new quest in Resources/Quests, selected so it can be filled in
    [MenuItem("Eryville/New Quest")]
    private static void NewQuest()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
        }

        Quest quest = ScriptableObject.CreateInstance<Quest>();
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/NewQuest.asset");
        quest.id = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        quest.title = "New quest";
        quest.objectives.Add(new QuestObjective());
        AssetDatabase.CreateAsset(quest, path);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = quest;
        Debug.Log($"Made {path}. Give it an id, a title, objectives and rewards; it starts when its requirements are met.");
    }

    // Lists the mistakes that are easy to make when writing quests (same ids, missing quests or chapters, letters the server does not know...)
    [MenuItem("Eryville/Check Quests")]
    private static void CheckQuests()
    {
        List<Quest> quests = CharacterAssetTools.FindAll<Quest>();
        List<StoryChapter> chapters = CharacterAssetTools.FindAll<StoryChapter>();
        List<string> problems = QuestChecks.Problems(quests, chapters);

        foreach (Quest quest in quests)
        {
            string path = AssetDatabase.GetAssetPath(quest);
            if (!path.Contains("/Resources/Quests/")) problems.Add($"\"{quest.name}\" is not in a Resources/Quests folder, so the game will not find it ({path}).");
        }
        foreach (BuildingType building in CharacterAssetTools.FindAll<BuildingType>())
        {
            if (building.unlockedByQuest != null && !AssetDatabase.GetAssetPath(building.unlockedByQuest).Contains("/Resources/Quests/"))
                problems.Add($"{building.buildingName} is unlocked by the quest \"{building.unlockedByQuest.name}\", which the game cannot find (not in Resources/Quests): the building would stay locked.");
        }

        // Letters are paid by the server when playing online, and the server must list each quest that pays them (with the same amount)
        string configPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../firebase/functions/src/config.ts"));
        if (File.Exists(configPath))
        {
            string config = File.ReadAllText(configPath);
            foreach (Quest quest in quests)
            {
                if (quest.Letters <= 0) continue;
                Match listed = Regex.Match(config, "[\"']?" + Regex.Escape(quest.Id) + "[\"']?\\s*:\\s*\\{\\s*letters\\s*:\\s*(\\d+)");
                if (!listed.Success) problems.Add($"\"{quest.name}\" pays {quest.Letters} letters, but firebase/functions/src/config.ts does not list \"{quest.Id}\" under quests: online it would not pay. Add: \"{quest.Id}\": {{ letters: {quest.Letters} }} and redeploy.");
                else if (int.Parse(listed.Groups[1].Value) != quest.Letters) problems.Add($"\"{quest.name}\" pays {quest.Letters} letters, but config.ts says {listed.Groups[1].Value}: online it would pay what config.ts says.");
            }
        }

        if (problems.Count == 0)
        {
            Debug.Log($"Quests: {quests.Count} checked, nothing to report.");
            return;
        }
        Debug.LogWarning($"Quests: {quests.Count} checked, {problems.Count} things to look at:\n- " + string.Join("\n- ", problems));
    }
}
