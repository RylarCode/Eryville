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

    [MenuItem("Eryville/New Building...")]
    private static void OpenBuildingWizard()
    {
        NewBuildingWizard.Open();
    }

    // Lists the mistakes that are easy to make when adding a building (same typeId, no prefab, forgotten in the scene...)
    [MenuItem("Eryville/Check Buildings")]
    private static void CheckBuildings()
    {
        List<BuildingType> all = CharacterAssetTools.FindAll<BuildingType>();
        BuildingPlacer placer = Object.FindFirstObjectByType<BuildingPlacer>();
        List<BuildingType> inGame = placer != null && placer.AllBuildings != null ? placer.AllBuildings : null;
        if (placer == null) Debug.Log("Open GameScene to also check that every building is on the BuildingPlacer's list.");

        List<string> problems = BuildingChecks.Problems(all, inGame);
        if (problems.Count == 0)
        {
            Debug.Log($"Buildings: {all.Count} checked, nothing to report.");
            return;
        }
        Debug.LogWarning($"Buildings: {all.Count} checked, {problems.Count} things to look at:\n- " + string.Join("\n- ", problems));
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
