using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Menu items under "Eryville" for writing the story
public static class StoryTools
{
    private const string Folder = "Assets/_Project/Resources/Story";

    private const string Template =
        "# Lines are \"Speaker: words\". A line with no colon is narration. Lines starting with # are notes to yourself.\n" +
        "# Choices: \"> option | value +1\" (more > lines make more options), \"=> Speaker: reply\" shows only for the option above it.\n" +
        "# \"[if kindness >= 2] Speaker: words\" only plays when it is true. See docs/STORY.md.\n" +
        "\n" +
        "Narrator: Something happens.\n";

    // A new chapter in Resources/Story with a template script, selected so it can be filled in
    [MenuItem("Eryville/New Story Chapter")]
    private static void NewChapter()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
        }

        StoryChapter chapter = ScriptableObject.CreateInstance<StoryChapter>();
        chapter.script = Template;
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/NewChapter.asset");
        chapter.id = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        chapter.title = "New chapter";
        AssetDatabase.CreateAsset(chapter, path);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = chapter;
        Debug.Log($"Made {path}. Give it an id, a title and a script; it plays when its requirements are met.");
    }

    // Lists the mistakes that are easy to make when writing the story (same ids, unreadable lines, missing chapters...)
    [MenuItem("Eryville/Check Story")]
    private static void CheckStory()
    {
        List<StoryChapter> all = CharacterAssetTools.FindAll<StoryChapter>();
        List<string> problems = StoryChecks.Problems(all);

        foreach (StoryChapter chapter in all)
        {
            string path = AssetDatabase.GetAssetPath(chapter);
            if (!path.Contains("/Resources/Story/")) problems.Add($"\"{chapter.name}\" is not in a Resources/Story folder, so the game will not find it ({path}).");
        }
        foreach (BuildingType building in CharacterAssetTools.FindAll<BuildingType>())
        {
            if (building.unlockedBy != null && !AssetDatabase.GetAssetPath(building.unlockedBy).Contains("/Resources/Story/"))
            {
                problems.Add($"{building.buildingName} is unlocked by \"{building.unlockedBy.name}\", which the game cannot find (not in Resources/Story): the building would stay locked.");
            }
        }

        if (problems.Count == 0)
        {
            Debug.Log($"Story: {all.Count} chapters checked, nothing to report.");
            return;
        }
        Debug.LogWarning($"Story: {all.Count} chapters checked, {problems.Count} things to look at:\n- " + string.Join("\n- ", problems));
    }
}
