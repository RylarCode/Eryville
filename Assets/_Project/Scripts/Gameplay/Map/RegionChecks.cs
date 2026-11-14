using System.Collections.Generic;
using UnityEngine;

// Whether the Map regions the story and quests refer to exist (used by Check Story and Check Quests). Does nothing when the project has no
// Region assets (a game without a Map).
public static class RegionChecks
{
    // known: the ids of the regions that exist. Null: read them from the Region assets in Resources/Regions.
    public static string UnknownRegion(StoryCondition condition, ICollection<string> known = null)
    {
        if (condition == null || condition.kind != StoryCondition.Kind.RegionCleared) return null;
        return Unknown(condition.RegionId, known);
    }

    public static string UnknownRegion(StoryEffect effect, ICollection<string> known = null)
    {
        if (effect == null || effect.kind != StoryEffect.Kind.ClearRegion) return null;
        return Unknown(effect.RegionId, known);
    }

    // --- Check Regions: mistakes in the Map's image and Region assets ---

    // regions: every Region asset. pixels / width / height: the region image (null: not found). mapMin / mapSize: the Map's own corner and size in cells.
    public static List<string> Problems(IReadOnlyList<Region> regions, Color32[] pixels, int width, int height, Vector2Int mapMin, Vector2Int mapSize)
    {
        List<string> problems = new List<string>();
        if (regions == null || regions.Count == 0) { problems.Add("there are no Region assets in Resources/Regions, so the Map has no regions (all land is usable)."); return problems; }
        if (pixels == null || width <= 0 || height <= 0) { problems.Add("there is no region image (Resources/Regions/RegionMap.png) or it cannot be read: turn on Read/Write and set Compression to None in its import settings."); return problems; }
        if (width != mapSize.x || height != mapSize.y) problems.Add($"the region image is {width}x{height} but the Map is {mapSize.x}x{mapSize.y} cells: it needs one pixel per cell, so it should be {mapSize.x}x{mapSize.y}.");

        List<string> reading = new List<string>();
        RegionMap map = RegionMap.FromPixels(mapMin, width, height, pixels, regions, reading);
        foreach (string problem in reading) problems.Add("the image and regions disagree: " + problem + ".");

        List<Region> accepted = new List<Region>(map.Regions);
        foreach (Region region in regions)
        {
            if (region == null || !accepted.Contains(region)) continue;
            string who = $"\"{region.name}\" ({region.Id})";
            if (map.CellsOf(region).Count == 0) problems.Add($"{who} has no cells in the image: nothing is painted in its colour.");
            if (region.Clearable && !region.startsCleared)
            {
                if (region.charactersNeeded < 1) problems.Add($"{who} needs no characters, so nobody can be put on it.");
                if (region.clearSeconds <= 0f) problems.Add($"{who} takes no time to clear.");
                if (region.goldCost < 0) problems.Add($"{who} has a negative gold cost.");
            }
            if (region.startsCleared && !region.Clearable) problems.Add($"{who} starts cleared but is a river.");
            foreach (RegionYield y in region.yields)
            {
                if (y == null || string.IsNullOrWhiteSpace(y.item)) problems.Add($"{who} has a yield with no item.");
                else if (y.amount <= 0) problems.Add($"{who} has a yield of {y.item} with no amount.");
            }
        }

        int starts = 0;
        foreach (Region region in map.Regions) if (region.startsCleared) starts++;
        if (starts == 0) problems.Add("no region starts cleared, so there is nowhere to build at the start.");
        else if (starts > 1) problems.Add($"{starts} regions start cleared; usually just one (the land around the Town Hall).");

        if (starts > 0)
        {
            map.ResetToStart();
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (Region region in map.Regions) if (map.CanClear(region)) { map.Clear(region); progress = true; }
            }
            foreach (Region region in map.Regions)
                if (region.Clearable && !map.IsCleared(region)) problems.Add($"\"{region.name}\" ({region.Id}) can never be cleared: no chain of clearable regions leads to it from the start (a river in the way?).");
        }
        return problems;
    }

    // A colour no region uses yet, clearly different from the others and from transparent, for a new Region asset (and to paint it in the image)
    public static Color32 FreeColour(IEnumerable<Region> regions)
    {
        HashSet<int> used = new HashSet<int>();
        if (regions != null) foreach (Region r in regions) if (r != null) { Color32 c = r.Color32; used.Add((c.r << 16) | (c.g << 8) | c.b); }
        for (int step = 0; step < 360; step++)
        {
            Color c = Color.HSVToRGB(((step * 47) % 360) / 360f, 0.55f + 0.1f * (step % 3), 0.85f - 0.1f * (step % 2));
            Color32 candidate = c;
            if (used.Add((candidate.r << 16) | (candidate.g << 8) | candidate.b)) return new Color32(candidate.r, candidate.g, candidate.b, 255);
        }
        return new Color32(1, 2, 3, 255);
    }

    private static string Unknown(string id, ICollection<string> known)
    {
        if (string.IsNullOrWhiteSpace(id)) return "a region condition or effect without a region id.";
        if (known == null)
        {
            known = new List<string>();
            foreach (Region region in Resources.LoadAll<Region>(MapRegions.AssetsFolder)) known.Add(region.Id);
        }
        if (known.Count == 0 || known.Contains(id.Trim())) return null;
        return $"there is no Map region with the id \"{id}\".";
    }
}
