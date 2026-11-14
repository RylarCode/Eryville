using System;
using System.Collections.Generic;
using UnityEngine;

// The Map divided into regions, and which of them are cleared: pure logic (no scene), so the rules are tested exactly.
//
//  - Every cell belongs to at most one region (or none: the Map's border, always unusable).
//  - A cell is usable (buildings, roads, walking) only when its region is cleared.
//  - A region is *revealed* when it is cleared or touches a cleared one along an edge (corners do not count): that is all the player
//    sees; everything else stays under fog. A revealed region that is not cleared and not a river can be cleared.
public class RegionMap
{
    private readonly Dictionary<Vector2Int, Region> byCell = new Dictionary<Vector2Int, Region>();
    private readonly Dictionary<Region, List<Vector2Int>> cells = new Dictionary<Region, List<Vector2Int>>();
    private readonly Dictionary<Region, HashSet<Region>> neighbours = new Dictionary<Region, HashSet<Region>>();
    private readonly HashSet<Region> cleared = new HashSet<Region>();
    private readonly List<Region> regions = new List<Region>();

    public Vector2Int Min { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<Region> Regions => regions;

    // Goes up with every change of what is cleared
    public int Version { get; private set; }
    public event Action Changed;

    // Builds the map from the region image's pixels (as Texture2D.GetPixels32 gives them: row by row from the bottom left). The pixel at
    // (x, y) is the cell Min + (x, y). A transparent pixel is no region; a colour no Region asset has is reported in problems and is no region.
    public static RegionMap FromPixels(Vector2Int min, int width, int height, Color32[] pixels, IEnumerable<Region> all, List<string> problems = null)
    {
        RegionMap map = new RegionMap(min, width, height);
        Dictionary<Color32Key, Region> byColour = new Dictionary<Color32Key, Region>();
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Region region in all)
        {
            if (region == null) continue;
            if (!ids.Add(region.Id)) { problems?.Add($"two regions have the id \"{region.Id}\""); continue; }
            Color32Key key = new Color32Key(region.Color32);
            if (byColour.TryGetValue(key, out Region other)) { problems?.Add($"\"{region.name}\" and \"{other.name}\" have the same colour {key}"); continue; }
            byColour[key] = region;
            map.regions.Add(region);
            map.cells[region] = new List<Vector2Int>();
            map.neighbours[region] = new HashSet<Region>();
        }

        HashSet<Color32Key> unknown = new HashSet<Color32Key>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (index >= pixels.Length) break;
                Color32 pixel = pixels[index];
                if (pixel.a == 0) continue;
                Color32Key key = new Color32Key(pixel);
                if (!byColour.TryGetValue(key, out Region region))
                {
                    if (unknown.Add(key)) problems?.Add($"the image has the colour {key} (first at {min.x + x},{min.y + y}) but no Region has it");
                    continue;
                }
                Vector2Int cell = new Vector2Int(min.x + x, min.y + y);
                map.byCell[cell] = region;
                map.cells[region].Add(cell);
            }
        }

        foreach (KeyValuePair<Vector2Int, Region> pair in map.byCell)
        {
            foreach (Vector2Int step in Steps)
            {
                if (map.byCell.TryGetValue(pair.Key + step, out Region other) && other != pair.Value) map.neighbours[pair.Value].Add(other);
            }
        }
        return map;
    }

    private static readonly Vector2Int[] Steps = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

    private RegionMap(Vector2Int min, int width, int height)
    {
        Min = min; Width = width; Height = height;
    }

    // An exact colour as a dictionary key (Color32 compares by reference-like equality in some engines)
    private struct Color32Key : IEquatable<Color32Key>
    {
        private readonly int packed;
        public Color32Key(Color32 c) { packed = (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a; }
        public bool Equals(Color32Key o) => packed == o.packed;
        public override bool Equals(object o) => o is Color32Key k && Equals(k);
        public override int GetHashCode() => packed;
        public override string ToString() => $"#{(packed >> 24) & 255:X2}{(packed >> 16) & 255:X2}{(packed >> 8) & 255:X2}";
    }

    // --- Looking ---

    public Region RegionAt(Vector2Int cell)
    {
        byCell.TryGetValue(cell, out Region region);
        return region;
    }

    public IReadOnlyList<Vector2Int> CellsOf(Region region)
    {
        return region != null && cells.TryGetValue(region, out List<Vector2Int> list) ? list : new List<Vector2Int>();
    }

    public IReadOnlyCollection<Region> NeighboursOf(Region region)
    {
        return region != null && neighbours.TryGetValue(region, out HashSet<Region> set) ? (IReadOnlyCollection<Region>)set : new Region[0];
    }

    public Region Find(string id)
    {
        foreach (Region region in regions) if (string.Equals(region.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase)) return region;
        return null;
    }

    // --- What is cleared ---

    public bool IsCleared(Region region) => region != null && cleared.Contains(region);

    // Buildings, roads and walking are allowed here: the cell is in a region and the region is cleared
    public bool IsUsable(Vector2Int cell) => byCell.TryGetValue(cell, out Region region) && cleared.Contains(region);

    // Visible to the player: cleared, or touching a cleared region along an edge
    public bool IsRevealed(Region region)
    {
        if (region == null) return false;
        if (cleared.Contains(region)) return true;
        foreach (Region other in NeighboursOf(region)) if (cleared.Contains(other)) return true;
        return false;
    }

    // Revealed, not yet cleared, and something that can be cleared (not a river)
    public bool CanClear(Region region) => region != null && region.Clearable && !cleared.Contains(region) && IsRevealed(region);

    public int ClearedCount => cleared.Count;

    public int ClearedCells
    {
        get { int total = 0; foreach (Region region in cleared) total += CellsOf(region).Count; return total; }
    }

    public bool Clear(Region region)
    {
        if (region == null || !cells.ContainsKey(region) || !cleared.Add(region)) return false;
        Touch();
        return true;
    }

    public bool Lock(Region region)
    {
        if (region == null || !cleared.Remove(region)) return false;
        Touch();
        return true;
    }

    // Back to a new game: only the regions that start cleared
    public void ResetToStart()
    {
        cleared.Clear();
        foreach (Region region in regions) if (region.startsCleared) cleared.Add(region);
        Touch();
    }

    public void ClearEverything()
    {
        foreach (Region region in regions) cleared.Add(region);
        Touch();
    }

    // Clears the regions these cells are in (an old save with buildings in land that is now locked keeps them)
    public int ClearUnder(IEnumerable<Vector2Int> usedCells)
    {
        int count = 0;
        foreach (Vector2Int cell in usedCells)
        {
            Region region = RegionAt(cell);
            if (region != null && cleared.Add(region)) count++;
        }
        if (count > 0) Touch();
        return count;
    }

    // --- Saving ---

    public List<string> ExportCleared()
    {
        List<string> ids = new List<string>();
        foreach (Region region in regions) if (cleared.Contains(region)) ids.Add(region.Id);
        return ids;
    }

    // Takes the saved ids (ones that no longer exist are ignored)
    public void ImportCleared(IEnumerable<string> ids)
    {
        cleared.Clear();
        if (ids != null) foreach (string id in ids) { Region region = Find(id); if (region != null) cleared.Add(region); }
        Touch();
    }

    private void Touch()
    {
        Version++;
        Changed?.Invoke();
    }
}
