using System.Collections.Generic;
using UnityEngine;

// One decoration: which cell, where inside it (0..1 each way, from the cell's lower left corner), which of the terrain's pictures, how big
public struct DecorSpot
{
    public Vector2Int cell;
    public Vector2 offset;
    public int variant;
    public float scale;
}

// Where the trees, rocks and rubble stand on land that is not cleared. Pure rules: the same region always gets the same spots (they come
// from the cell's own numbers, not from chance at run time), so the land does not change between visits or after loading a save.
public static class RegionDecor
{
    // Chance of a decoration per cell when no Region Decor Set says otherwise
    public static float DefaultDensity(RegionTerrain terrain)
    {
        switch (terrain)
        {
            case RegionTerrain.Forest: return 0.6f;
            case RegionTerrain.Mountain: return 0.5f;
            case RegionTerrain.Rocks: return 0.4f;
            case RegionTerrain.Debris: return 0.35f;
            case RegionTerrain.Quarry: return 0.3f;
            default: return 0f; // a river is left bare
        }
    }

    // A stable pseudo random number in 0..1 for a cell and a purpose
    public static float Unit(Vector2Int cell, int salt)
    {
        unchecked
        {
            uint h = (uint)(cell.x * 73856093) ^ (uint)(cell.y * 19349663) ^ (uint)(salt * 83492791);
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15; h *= 0x27d4eb2du; h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    // The decorations of a region: each cell has one with the chance `density`, anywhere inside the cell (kept off its very edge)
    public static List<DecorSpot> Plan(RegionMap map, Region region, int variants, float density, Vector2 scaleRange)
    {
        List<DecorSpot> spots = new List<DecorSpot>();
        if (map == null || region == null || variants <= 0 || density <= 0f) return spots;
        float low = Mathf.Min(scaleRange.x, scaleRange.y), high = Mathf.Max(scaleRange.x, scaleRange.y);
        foreach (Vector2Int cell in map.CellsOf(region))
        {
            if (Unit(cell, 1) >= density) continue;
            spots.Add(new DecorSpot
            {
                cell = cell,
                offset = new Vector2(0.15f + 0.7f * Unit(cell, 2), 0.1f + 0.8f * Unit(cell, 3)),
                variant = Mathf.Min(variants - 1, (int)(Unit(cell, 4) * variants)),
                scale = Mathf.Lerp(low, high, Unit(cell, 5))
            });
        }
        return spots;
    }

    // Whether a region shows decorations (or its picture) right now: it is visible, not cleared, and not a river
    public static bool ShowsDecor(RegionMap map, Region region)
    {
        return map != null && region != null && region.terrain != RegionTerrain.River && map.IsRevealed(region) && !map.IsCleared(region);
    }
}

// Simple stand-in pictures drawn by code, until real ones are drawn: a tree, a boulder, a pile of planks. Three of each, one cell wide at most.
public static class PlaceholderDecor
{
    public const int Variants = 3;
    private const int Size = 32; // pixels; one cell at 32 pixels per unit

    private static readonly Dictionary<RegionTerrain, Sprite[]> cache = new Dictionary<RegionTerrain, Sprite[]>();

    public static Sprite[] For(RegionTerrain terrain)
    {
        if (cache.TryGetValue(terrain, out Sprite[] sprites) && sprites[0] != null) return sprites;
        sprites = new Sprite[Variants];
        for (int i = 0; i < Variants; i++) sprites[i] = Make(terrain, i);
        cache[terrain] = sprites;
        return sprites;
    }

    private static Sprite Make(RegionTerrain terrain, int variant)
    {
        Color32[] pixels = new Color32[Size * Size];
        switch (terrain)
        {
            case RegionTerrain.Forest:
            {
                int radius = 8 + variant * 2;
                Fill(pixels, 14, 0, 18, 12, new Color32(96, 62, 36, 255));                                   // trunk
                Ellipse(pixels, 16, 10 + radius, radius, radius, new Color32((byte)(40 + variant * 12), (byte)(110 + variant * 10), 50, 255)); // canopy
                break;
            }
            case RegionTerrain.Debris:
                for (int i = 0; i < 4; i++) Fill(pixels, 3 + i * 3 + variant * 2, i * 3, 14 + i * 3 + variant * 2, 4 + i * 3, new Color32((byte)(120 + i * 12), (byte)(88 + i * 8), 54, 255));
                break;
            default: // rocks, mountain, quarry
            {
                int width = 9 + variant * 3, height = 6 + variant * 3;
                Ellipse(pixels, 16, height, width, height, new Color32((byte)(120 + variant * 14), (byte)(120 + variant * 14), (byte)(128 + variant * 10), 255));
                break;
            }
        }
        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0f), Size);
    }

    private static void Fill(Color32[] pixels, int x0, int y0, int x1, int y1, Color32 colour)
    {
        for (int y = Mathf.Max(0, y0); y < Mathf.Min(Size, y1); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(Size, x1); x++) pixels[y * Size + x] = colour;
    }

    private static void Ellipse(Color32[] pixels, int cx, int cy, int rx, int ry, Color32 colour)
    {
        for (int y = Mathf.Max(0, cy - ry); y < Mathf.Min(Size, cy + ry); y++)
            for (int x = Mathf.Max(0, cx - rx); x < Mathf.Min(Size, cx + rx); x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy <= 1f) pixels[y * Size + x] = colour;
            }
    }
}
