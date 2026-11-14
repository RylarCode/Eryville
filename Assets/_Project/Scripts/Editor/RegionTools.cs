using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Menu items under "Eryville" for the Map's regions (see docs/MAP.md)
public static class RegionTools
{
    private const string Folder = "Assets/_Project/Resources/Regions";
    private const string ImagePath = Folder + "/RegionMap.png";

    // A new Region asset in Resources/Regions with a colour no other region has, selected so it can be filled in
    [MenuItem("Eryville/New Region")]
    private static void NewRegion()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
        }

        List<Region> existing = CharacterAssetTools.FindAll<Region>();
        Region region = ScriptableObject.CreateInstance<Region>();
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/NewRegion.asset");
        string name = Path.GetFileNameWithoutExtension(path);
        region.id = name.ToLowerInvariant();
        region.displayName = "New region";
        region.color = RegionChecks.FreeColour(existing);
        AssetDatabase.CreateAsset(region, path);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = region;
        Color32 c = region.Color32;
        Debug.Log($"Made {path} with the colour #{c.r:X2}{c.g:X2}{c.b:X2} (RGB {c.r}, {c.g}, {c.b}). Paint the region in exactly that colour in RegionMap.png (no anti-aliasing), then fill in its name, terrain, cost and yields. Run Eryville > Check Regions when done.");
    }

    // Lists the mistakes that are easy to make when drawing the Map (colours that do not match, regions with no cells, land that can never be reached...)
    [MenuItem("Eryville/Check Regions")]
    private static void CheckRegions()
    {
        List<Region> regions = CharacterAssetTools.FindAll<Region>();
        List<string> problems = new List<string>();

        foreach (Region region in regions)
        {
            string path = AssetDatabase.GetAssetPath(region);
            if (!path.Contains("/Resources/Regions/")) problems.Add($"\"{region.name}\" is not in a Resources/Regions folder, so the game will not find it ({path}).");
        }

        Color32[] pixels = null;
        int width = 0, height = 0;
        Texture2D image = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);
        if (image != null)
        {
            try { pixels = image.GetPixels32(); width = image.width; height = image.height; }
            catch (UnityException) { pixels = null; }
        }

        MapGrid map = Object.FindFirstObjectByType<MapGrid>();
        Vector2Int min = map != null ? map.Min : new Vector2Int(-28, -17);
        Vector2Int size = map != null ? map.Max - map.Min + Vector2Int.one : new Vector2Int(57, 29);
        problems.AddRange(RegionChecks.Problems(regions, pixels, width, height, min, size));

        if (problems.Count == 0)
        {
            Debug.Log($"Regions: {regions.Count} checked, nothing to report.");
            return;
        }
        Debug.LogWarning($"Regions: {regions.Count} checked, {problems.Count} things to look at:\n- " + string.Join("\n- ", problems));
    }
}
