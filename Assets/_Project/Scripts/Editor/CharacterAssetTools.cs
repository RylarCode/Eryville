using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The editor-side helpers for characters: filling animations from a sprite sheet, and finding the assets a new
// character needs to be added to. Used by the New Character window and by the animation set's inspector.
public static class CharacterAssetTools
{
    // Reads the sprites of a sliced sprite sheet and puts the ones named like "walk_down_0" into the animation set.
    // Returns a message to show the person (what was found, and which names were not understood).
    public static string FillAnimations(CharacterAnimationSet set, Texture2D sheet)
    {
        if (set == null || sheet == null) return "Nothing to fill: pick a sprite sheet first.";

        string path = AssetDatabase.GetAssetPath(sheet);
        List<Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
        if (sprites.Count == 0)
        {
            return "That texture has no sprites. Select it, set Sprite Mode to Multiple, open the Sprite Editor and slice it (Slice > Automatic or Grid By Cell Size), then name the frames like walk_down_0.";
        }

        Undo.RecordObject(set, "Fill animations from sprites");
        List<string> ignored = new List<string>();
        int used = AnimationNaming.Fill(set, sprites, ignored);
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();

        string message = $"Used {used} of {sprites.Count} sprites.";
        if (ignored.Count > 0) message += $" Not understood (the name must look like walk_down_0): {string.Join(", ", ignored)}.";
        return message;
    }

    // Every asset of a type in the project
    public static List<T> FindAll<T>() where T : Object
    {
        List<T> found = new List<T>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) found.Add(asset);
        }
        return found;
    }

    // The next free typeId (one more than the highest in use)
    public static int NextTypeId()
    {
        int highest = 0;
        foreach (CharacterType type in FindAll<CharacterType>()) highest = Mathf.Max(highest, type.typeId);
        return highest + 1;
    }

    // Makes sure a folder exists (every level of it) and returns its path
    public static string EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
        return path;
    }

    // Adds the type to the list on the CharacterManager in the open scene. False if there is no manager or it was listed already.
    public static bool AddToCharacterManager(CharacterType type)
    {
        CharacterManager manager = Object.FindFirstObjectByType<CharacterManager>();
        if (manager == null) return false;

        SerializedObject serialized = new SerializedObject(manager);
        SerializedProperty list = serialized.FindProperty("AllCharacters");
        if (list == null) return false;
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == type) return false;
        }
        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = type;
        serialized.ApplyModifiedProperties();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        return true;
    }

    // Adds the type to every gacha pool that lists its characters by hand (a pool with an empty list already offers everybody).
    // Returns how many pools it was added to.
    public static int AddToGachaPools(CharacterType type)
    {
        int added = 0;
        foreach (GachaPool pool in FindAll<GachaPool>())
        {
            if (pool.characters == null || pool.characters.Count == 0 || pool.characters.Contains(type)) continue;
            Undo.RecordObject(pool, "Add character to gacha pool");
            pool.characters.Add(type);
            EditorUtility.SetDirty(pool);
            added++;
        }
        return added;
    }
}
