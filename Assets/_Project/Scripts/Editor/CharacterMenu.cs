using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Menu items under "Eryville" for working with characters
public static class CharacterMenu
{
    [MenuItem("Eryville/New Character...")]
    private static void OpenWizard()
    {
        NewCharacterWizard.Open();
    }

    // Lists the mistakes that are easy to make when adding a character (same typeId, no prefab, forgotten in the scene...)
    [MenuItem("Eryville/Check Characters")]
    private static void CheckCharacters()
    {
        List<CharacterType> all = CharacterAssetTools.FindAll<CharacterType>();
        CharacterManager manager = Object.FindFirstObjectByType<CharacterManager>();
        List<CharacterType> inGame = manager != null && manager.AllCharacters != null ? manager.AllCharacters : null;
        if (manager == null) Debug.Log("Open GameScene to also check that every character is on the CharacterManager's list.");

        List<string> problems = CharacterChecks.Problems(all, inGame);
        if (problems.Count == 0)
        {
            Debug.Log($"Characters: {all.Count} checked, nothing to report.");
            return;
        }
        Debug.LogWarning($"Characters: {all.Count} checked, {problems.Count} things to look at:\n- " + string.Join("\n- ", problems));
    }
}
