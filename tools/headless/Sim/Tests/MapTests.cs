using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

// The Map's regions: the rules (pure), and the game with the project's own sample map
public static class MapTests
{
    private static Region R(string id, byte r, byte g, byte b, RegionTerrain terrain = RegionTerrain.Forest, bool start = false)
    {
        Region region = ScriptableObject.CreateInstance<Region>();
        region.name = id; region.id = id; region.color = new Color32(r, g, b, 255); region.terrain = terrain; region.startsCleared = start;
        return region;
    }

    // A small map from text, top row first ('.' = no region), cell (0,0) at the bottom left
    private static RegionMap Build(string[] rows, Dictionary<char, Region> key, List<string> problems = null)
    {
        int height = rows.Length, width = rows[0].Length;
        Color32[] pixels = new Color32[width * height];
        for (int row = 0; row < height; row++)
            for (int x = 0; x < width; x++)
            {
                char c = rows[row][x];
                pixels[(height - 1 - row) * width + x] = c == '.' ? new Color32(0, 0, 0, 0) : key.ContainsKey(c) ? key[c].Color32 : new Color32(1, 2, 3, 255);
            }
        return RegionMap.FromPixels(Vector2Int.zero, width, height, pixels, key.Values, problems);
    }

    public static void Rules()
    {
        Region a = R("a", 10, 0, 0, start: true), b = R("b", 20, 0, 0), c = R("c", 30, 0, 0), d = R("d", 40, 0, 0);
        var key = new Dictionary<char, Region> { ['A'] = a, ['B'] = b, ['C'] = c, ['D'] = d };
        RegionMap map = Build(new[] { "AABB", "A.BC", ".DDC" }, key);

        T.Section("reading the image");
        T.Check(map.RegionAt(new Vector2Int(0, 2)) == a && map.RegionAt(new Vector2Int(3, 1)) == c && map.RegionAt(new Vector2Int(1, 1)) == null, "each cell has its region; a transparent pixel has none (the first row is the top)");
        T.Check(map.CellsOf(a).Count == 3 && map.CellsOf(b).Count == 3 && map.CellsOf(d).Count == 2, "regions know their cells");
        T.Check(map.NeighboursOf(a).SequenceEqual(new[] { b }) || (map.NeighboursOf(a).Count == 1 && map.NeighboursOf(a).Contains(b)), "A touches only B along an edge (D is only at its corner)");
        T.Check(map.NeighboursOf(d).Contains(b) && map.NeighboursOf(d).Contains(c) && !map.NeighboursOf(d).Contains(a), "D touches B and C, not A");

        T.Section("revealed and clearable");
        map.ResetToStart();
        T.Check(map.IsCleared(a) && !map.IsCleared(b) && map.ClearedCount == 1 && map.ClearedCells == 3, "only the starting region is cleared");
        T.Check(map.IsRevealed(a) && map.IsRevealed(b) && !map.IsRevealed(c) && !map.IsRevealed(d), "you see the cleared region and those that touch it along an edge");
        T.Check(map.CanClear(b) && !map.CanClear(d) && !map.CanClear(a), "only a revealed, uncleared region can be cleared (D touches A only at a corner)");
        T.Check(map.IsUsable(new Vector2Int(0, 2)) && !map.IsUsable(new Vector2Int(2, 2)) && !map.IsUsable(new Vector2Int(1, 1)) && !map.IsUsable(new Vector2Int(10, 10)), "usable: cleared land only (not locked, not the border, not off the image)");
        int changes = 0; map.Changed += () => changes++; int version = map.Version;
        T.Check(map.Clear(b) && !map.Clear(b) && changes == 1 && map.Version == version + 1, "clearing is announced once");
        T.Check(map.IsRevealed(d) && map.IsRevealed(c) && map.CanClear(d) && map.IsUsable(new Vector2Int(2, 2)), "clearing B reveals what touches B and opens its land");

        T.Section("rivers");
        Region river = R("river", 50, 0, 0, RegionTerrain.River);
        RegionMap withRiver = Build(new[] { "AR" }, new Dictionary<char, Region> { ['A'] = a, ['R'] = river });
        withRiver.ResetToStart();
        T.Check(withRiver.IsRevealed(river) && !withRiver.CanClear(river) && !river.Clearable, "a river is seen but can never be cleared");

        T.Section("saving");
        map.ResetToStart(); map.Clear(c);
        List<string> ids = map.ExportCleared();
        T.Check(ids.SequenceEqual(new[] { "a", "c" }), "the cleared ids, in region order");
        map.ImportCleared(new[] { "b", "gone", "A" });
        T.Check(map.IsCleared(b) && map.IsCleared(a) && !map.IsCleared(c) && map.ClearedCount == 2, "an import takes known ids (not case sensitive) and ignores unknown ones");
        map.ResetToStart();
        T.Check(map.ClearUnder(new[] { new Vector2Int(3, 0), new Vector2Int(0, 2), new Vector2Int(1, 1) }) == 1 && map.IsCleared(c), "land that is already used can be cleared in place");

        T.Section("mistakes in the image");
        var problems = new List<string>();
        Region twin = R("twin", 10, 0, 0), dup = R("a", 99, 0, 0);
        Build(new[] { "AX" }, new Dictionary<char, Region> { ['A'] = a, ['T'] = twin, ['D'] = dup }, problems);
        T.Check(problems.Any(p => p.Contains("same colour")) && problems.Any(p => p.Contains("same id") || p.Contains("id \"a\"")) && problems.Any(p => p.Contains("no Region has it")), "same colour, same id, and a colour nobody has are reported: " + problems.Count);
    }

    public static void SampleContent()
    {
        T.Section("the shipped regions");
        List<Region> regions = SampleMap.ReadRegions();
        SampleMap.Image image = SampleMap.ReadPng(System.IO.Path.Combine(SampleMap.Folder, "RegionMap.png"));
        T.Check(image.Width == 57 && image.Height == 29, "the image is one pixel per cell of the 57x29 Map: " + image.Width + "x" + image.Height);
        var problems = new List<string>();
        RegionMap map = RegionMap.FromPixels(new Vector2Int(-28, -17), image.Width, image.Height, image.Pixels, regions, problems);
        T.Check(problems.Count == 0, "every colour in the image belongs to a region, and no two regions share one or an id: " + string.Join("; ", problems));
        T.Check(regions.All(r => map.CellsOf(r).Count > 0), "every region has cells in the image");
        T.Check(regions.Count(r => r.startsCleared) == 1 && regions.First(r => r.startsCleared).id == "town-square", "one region starts cleared: the Town Square");
        map.ResetToStart();
        T.Check(map.IsUsable(new Vector2Int(0, 0)) && !map.IsUsable(new Vector2Int(-28, 0)) && !map.IsUsable(new Vector2Int(-15, 0)), "the middle of the Map is cleared; its border and the forest are not");
        // Everything that can be cleared can be reached by clearing one region after another
        bool progress = true;
        while (progress) { progress = false; foreach (Region r in regions) if (map.CanClear(r)) { map.Clear(r); progress = true; } }
        T.Check(regions.Where(r => r.Clearable).All(map.IsCleared), "every region that can be cleared can be reached from the start: left locked = " + string.Join(", ", regions.Where(r => r.Clearable && !map.IsCleared(r)).Select(r => r.id)));
        T.Check(regions.Where(r => r.Clearable && !r.startsCleared).All(r => r.goldCost > 0 && r.charactersNeeded > 0 && r.clearSeconds > 0), "clearable regions have a cost, characters and a time");
        T.Check(regions.First(r => r.terrain == RegionTerrain.Mountain).goldCost > 5 * regions.Where(r => r.terrain == RegionTerrain.Forest).Max(r => r.goldCost), "the mountain is very costly");
    }

    public static void InTheGame()
    {
        Town t = Town.Build();
        T.Section("without regions");
        T.Check(MapRegions.Existing != null && !MapRegions.Existing.HasRegions && t.map.IsUsable(new Vector2Int(-20, 5)), "a Map with no regions: everything inside it is usable, as before");

        T.Section("with the sample map");
        List<Region> regions = t.UseSampleMap();
        MapRegions mr = MapRegions.Existing;
        Region forest = regions.First(r => r.id == "west-forest"), river = regions.First(r => r.id == "river"), meadow = regions.First(r => r.id == "east-meadow");
        T.Check(mr.Active && mr.Map.ClearedCount == 1, "a new game starts with only the Town Square cleared");
        T.Check(t.Place(t.Home, 0, 0) != null, "buildings go up on cleared land");
        T.Check(t.bm.CanPlace(t.Home, new Vector2Int(-15, 0)) == false && t.Place(t.Home, -15, 0) == null, "not on locked land");
        T.Check(t.bm.CanPlace(t.Home, new Vector2Int(-7, 0)) == false, "not half on locked land (the footprint must be all cleared)");
        T.Check(t.roads.Build(t.DirtRoad, new Vector2Int(2, 2)) && !t.roads.Build(t.DirtRoad, new Vector2Int(-10, 0)), "roads: on cleared land only");
        T.Check(t.map.IsBlocked(new Vector2Int(-10, 0)) && !t.map.IsBlocked(new Vector2Int(3, 1)), "walking is blocked on locked land");
        var pf = new GridPathfinder(t.map.Min, t.map.Max, t.map.IsBlocked, t.map.SpeedMultiplierAt, 1f);
        T.Check(pf.FindPath(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(-15, 0) }, out float time) == null, "no path through locked land");
        int version = t.map.Version; int obstacles = 0; t.map.ObstaclesChanged += () => obstacles++;
        mr.Map.Clear(forest);
        T.Check(t.map.Version > version && obstacles == 1, "clearing tells paths to start over");
        pf = new GridPathfinder(t.map.Min, t.map.Max, t.map.IsBlocked, t.map.SpeedMultiplierAt, 1f);
        T.Check(pf.FindPath(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(-15, 0) }, out time) != null, "after clearing, there is a path");
        T.Check(t.Place(t.Home, -15, 0) != null, "and buildings can go up");

        T.Section("the fog");
        Tilemap overlay = t.grid.transform.Find("RegionOverlay").GetComponent<Tilemap>();
        T.Check(overlay != null && overlay.GetTile(new Vector3Int(0, 0, 0)) == null && overlay.GetTile(new Vector3Int(-15, 0, 0)) == null, "no fog or tint on cleared land");
        TileBase fog = overlay.GetTile(new Vector3Int(20, -10, 0));
        TileBase tint = overlay.GetTile(new Vector3Int(0, 6, 0));
        T.Check(fog != null && fog == overlay.GetTile(new Vector3Int(-28, 0, 0)), "far land and the Map's border are under fog (the same, very dense fog)");
        T.Check(tint != null && tint != fog && ((Tile)fog).color.a > 0.95f && ((Tile)tint).color.a < 0.5f, "the North Rocks (touching cleared land) are only tinted, so you can see what can be cleared; the fog is nearly opaque");
        T.Check(overlay.GetTile(new Vector3Int(15, -10, 0)) == fog, "the river is under fog until you reach it");
        mr.Map.Clear(meadow);
        TileBase riverTint = overlay.GetTile(new Vector3Int(15, -10, 0));
        T.Check(riverTint != null && riverTint != fog && riverTint != tint, "a revealed river is shown differently from land that can be cleared");
        T.Check(mr.OverlayTileAt(new Vector2Int(0, 0)) == null, "(and the overlay agrees with the rules)");

        T.Section("the switch");
        mr.Enabled = false;
        T.Check(!mr.Active && t.map.IsUsable(new Vector2Int(20, -10)) && overlay.GetTile(new Vector3Int(20, -10, 0)) == null, "switched off: all land usable, no fog");
        mr.Enabled = true;
        T.Check(mr.Active && !t.map.IsUsable(new Vector2Int(20, -10)) && overlay.GetTile(new Vector3Int(20, -10, 0)) != null, "switched on again");
    }

    public static void SavingTheMap()
    {
        Town t = Town.Build();
        List<Region> regions = t.UseSampleMap();
        MapRegions mr = MapRegions.Existing;
        Region forest = regions.First(r => r.id == "west-forest"), rocks = regions.First(r => r.id == "north-rocks");

        T.Section("round trip");
        mr.Map.Clear(forest);
        t.Place(t.Home, -15, 0);
        T.Check(SaveSystem.Save("map.json"), "saved");
        GameData data = JsonUtility.FromJson<GameData>(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.persistentDataPath, "map.json")));
        T.Check(data.version == SaveSystem.CurrentVersion && SaveSystem.CurrentVersion >= 11 && data.clearedRegions.SequenceEqual(new[] { "town-square", "west-forest" }), "the save lists the cleared regions: " + string.Join(",", data.clearedRegions));
        mr.Map.ResetToStart(); t.bm.ClearAll(); World.EndFrame();
        T.Check(SaveSystem.Load("map.json") && mr.Map.IsCleared(forest) && !mr.Map.IsCleared(rocks) && t.bm.Buildings.Count == 1, "loading brings back what was cleared, and the building on it");

        T.Section("older saves");
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "v10.json"),
            "{\"version\":10,\"resources\":[],\"buildings\":[{\"id\":\"b1\",\"typeId\":3,\"cellX\":-15,\"cellY\":0,\"position\":{\"x\":-14.5,\"y\":0.5,\"z\":0}}],\"roads\":[{\"cellX\":-20,\"cellY\":-8,\"typeId\":1}],\"characters\":[]}");
        mr.Map.ResetToStart(); t.bm.ClearAll(); World.EndFrame();
        T.Check(SaveSystem.Load("v10.json") && t.bm.Buildings.Count == 1 && mr.Map.IsCleared(forest), "a version 10 save with a building on what is now locked land loads it, and that land is cleared");
        T.Check(mr.Map.IsCleared(regions.First(r => r.id == "town-square")) && !mr.Map.IsCleared(rocks) && t.map.RoadAt(new Vector2Int(-20, -8)) == null || mr.Map.IsCleared(forest), "(the start stays cleared, other land stays locked)");
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "empty.json"), "{\"version\":11,\"resources\":[],\"buildings\":[],\"roads\":[],\"characters\":[],\"clearedRegions\":[\"no-such-region\"]}");
        T.Check(SaveSystem.Load("empty.json") && mr.Map.IsCleared(regions.First(r => r.startsCleared)), "a save that clears nothing known never leaves nowhere to build");

        T.Section("new game and cheats");
        mr.Map.ClearEverything();
        T.Check(Cheats.DescribeRegions().Contains("regions cleared") && mr.Map.ClearedCount == regions.Count, "Clear every region: " + Cheats.DescribeRegions());
        Cheats.ResetRegions();
        T.Check(mr.Map.ClearedCount == 1 && Cheats.DescribeRegions().StartsWith("Map: 1 of "), "Lock every region except the start");
        Cheats.ClearAllRegions(); Cheats.ResetGame();
        T.Check(mr.Map.ClearedCount == 1, "a reset game starts from the Town Square again");
        Cheats.ToggleRegions();
        T.Check(!mr.Enabled && Cheats.DescribeRegions().Contains("switched off"), "the switch is in the cheats");
    }
}
