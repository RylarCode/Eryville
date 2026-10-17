using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The editor-side helpers for buildings: filling animations from a sprite sheet, a free typeId, and the lists a new
// building has to be on. Used by the New Building window and by the building animation set's inspector.
public static class BuildingAssetTools
{
    // Reads the sprites of a sliced sprite sheet and puts the ones named like "open_0" into the animation set.
    // Returns a message to show the person.
    public static string FillAnimations(BuildingAnimationSet set, Texture2D sheet)
    {
        if (set == null || sheet == null) return "Nothing to fill: pick a sprite sheet first.";

        string path = AssetDatabase.GetAssetPath(sheet);
        List<Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
        if (sprites.Count == 0)
        {
            return "That texture has no sprites. Select it, set Sprite Mode to Multiple, open the Sprite Editor and slice it, then name the frames like closed_0, open_0, opening_0, closing_0.";
        }

        Undo.RecordObject(set, "Fill animations from sprites");
        List<string> ignored = new List<string>();
        int used = set.Fill(sprites, ignored);
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();

        string message = $"Used {used} of {sprites.Count} sprites.";
        if (ignored.Count > 0) message += $" Not understood (the name must look like open_0): {string.Join(", ", ignored)}.";
        return message;
    }

    // The next free typeId (one more than the highest in use)
    public static int NextTypeId()
    {
        int highest = 0;
        foreach (BuildingType type in CharacterAssetTools.FindAll<BuildingType>()) highest = Mathf.Max(highest, type.typeId);
        return highest + 1;
    }

    // Adds the type to the list on the BuildingPlacer in the open scene (what appears in the build menu and what a save can load).
    // False if there is no placer or it was listed already.
    public static bool AddToBuildingPlacer(BuildingType type)
    {
        BuildingPlacer placer = Object.FindFirstObjectByType<BuildingPlacer>();
        if (placer == null) return false;

        SerializedObject serialized = new SerializedObject(placer);
        SerializedProperty list = serialized.FindProperty("AllBuildings");
        if (list == null) return false;
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == type) return false;
        }
        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = type;
        serialized.ApplyModifiedProperties();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(placer.gameObject.scene);
        return true;
    }
}
