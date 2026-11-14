using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
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

    public static void SeeingTheMap()
    {
        T.Section("scatter rules (pure)");
        Region forest0 = R("f", 10, 0, 0, RegionTerrain.Forest, start: true);
        var key = new Dictionary<char, Region> { ['F'] = forest0 };
        string[] rows = new string[10]; for (int i = 0; i < rows.Length; i++) rows[i] = "FFFFFFFFFF";
        RegionMap map = Build(rows, key);
        var a = RegionDecor.Plan(map, forest0, 3, 0.5f, new Vector2(0.8f, 1.2f));
        var b = RegionDecor.Plan(map, forest0, 3, 0.5f, new Vector2(0.8f, 1.2f));
        T.Check(a.Count > 25 && a.Count < 75 && a.Count == b.Count && a.Select(x => x.cell).SequenceEqual(b.Select(x => x.cell)) && a.Select(x => x.offset).SequenceEqual(b.Select(x => x.offset)), "about half the cells get one (" + a.Count + " of 100), and the same ones every time");
        T.Check(a.All(x => x.variant >= 0 && x.variant < 3 && x.offset.x >= 0.15f && x.offset.x <= 0.85f && x.offset.y >= 0.1f && x.offset.y <= 0.9f && x.scale >= 0.8f && x.scale <= 1.2f), "variants, positions inside the cell and sizes are in range");
        T.Check(a.Select(x => x.variant).Distinct().Count() == 3 && a.Select(x => x.offset.x).Distinct().Count() > 20, "the picks vary");
        T.Check(RegionDecor.Plan(map, forest0, 3, 0f, Vector2.one).Count == 0 && RegionDecor.Plan(map, forest0, 3, 1f, Vector2.one).Count == 100 && RegionDecor.Plan(map, forest0, 0, 1f, Vector2.one).Count == 0, "density 0 none, 1 every cell, no pictures none");
        T.Check(RegionDecor.DefaultDensity(RegionTerrain.River) == 0f && RegionDecor.DefaultDensity(RegionTerrain.Forest) > RegionDecor.DefaultDensity(RegionTerrain.Debris), "rivers stay bare; forests are denser than debris");

        T.Section("decorations in the game");
        Town t = Town.Build();
        List<Region> regions = t.UseSampleMap();
        MapRegions mr = MapRegions.Existing;
        Region west = regions.First(r => r.id == "west-forest"), rocks = regions.First(r => r.id == "north-rocks"), river = regions.First(r => r.id == "river"), meadow = regions.First(r => r.id == "east-meadow"), mountain = regions.First(r => r.id == "mountain");
        Transform decor = t.grid.transform.Find("RegionDecor");
        int Count() { World.EndFrame(); return mr.DecorCount; }
        int before = Count();
        T.Check(before > 0 && decor != null, "the regions next to the Town Square have decorations (" + before + ")");
        int expected = new[] { west, rocks, regions.First(r => r.id == "south-debris"), meadow }.Sum(r => RegionDecor.Plan(mr.Map, r, PlaceholderDecor.Variants, RegionDecor.DefaultDensity(r.terrain), new Vector2(0.85f, 1.15f)).Count);
        T.Check(before == expected, "exactly the planned ones: " + before + " = " + expected);
        T.Check(decor.GetComponentsInChildren<SpriteRenderer>().All(r => r.sprite != null && r.sortingOrder == DepthSorting.Order(r.transform.position.y)), "each is a sprite sorted by its height on the Map");
        var inForest = decor.GetComponentsInChildren<SpriteRenderer>().Where(r => mr.Map.RegionAt(t.map.WorldToCell(r.transform.position)) == west).ToList();
        T.Check(inForest.Count == RegionDecor.Plan(mr.Map, west, 3, 0.6f, new Vector2(0.85f, 1.15f)).Count && inForest.Count > 30, "the West Forest has its own trees (" + inForest.Count + ")");
        T.Check(!decor.GetComponentsInChildren<SpriteRenderer>().Any(r => { Region at = mr.Map.RegionAt(t.map.WorldToCell(r.transform.position)); return at == null || at.id == "town-square" || at == mountain; }), "none on cleared land, hidden land or the border");

        mr.Map.Clear(west);
        T.Check(Count() < before && !t.grid.transform.Find("RegionDecor").GetComponentsInChildren<SpriteRenderer>().Any(r => mr.Map.RegionAt(t.map.WorldToCell(r.transform.position)) == west), "clearing a region removes its decorations (" + mr.DecorCount + ")");
        int afterWest = mr.DecorCount;
        mr.Map.Clear(meadow);
        T.Check(mr.Map.IsRevealed(river) && mr.DecorCount <= afterWest + RegionDecor.Plan(mr.Map, mountain, 3, 0.5f, Vector2.one).Count, "a revealed river is left bare");
        mr.Enabled = false;
        T.Check(Count() == 0, "switched off: no decorations");
        mr.Enabled = true;
        T.Check(Count() > 0, "...and back on");

        T.Section("a region's own picture");
        Sprite art = new Sprite(256, 256);
        mountain.picture = art; mountain.pictureOffset = new Vector2(0f, 1f);
        Region pass = regions.First(r => r.id == "pass");
        mr.Map.Clear(pass);
        World.EndFrame(); Transform decor2 = t.grid.transform.Find("RegionDecor");
        var pictures = decor2.GetComponentsInChildren<SpriteRenderer>().Where(r => r.sprite == art).ToList();
        T.Check(pictures.Count == 1, "a revealed region with a picture shows it once (instead of scattering)");
        var cells = mr.Map.CellsOf(mountain);
        float centreX = (cells.Min(c => c.x) + cells.Max(c => c.x) + 1) / 2f;
        T.Check(pictures.Count == 1 && System.Math.Abs(pictures[0].transform.position.x - centreX) < 0.01f && pictures[0].transform.position.y > cells.Min(c => c.y), "standing at the bottom centre of the region, moved by its offset: " + pictures[0].transform.position);
        T.Check(!decor2.GetComponentsInChildren<SpriteRenderer>().Any(r => r.sprite != art && mr.Map.RegionAt(t.map.WorldToCell(r.transform.position)) == mountain), "no scatter on it");
        Sprite done = new Sprite(64, 64);
        mountain.clearedPicture = done;
        mr.Map.Clear(mountain);
        World.EndFrame(); decor2 = t.grid.transform.Find("RegionDecor");
        var after = decor2.GetComponentsInChildren<SpriteRenderer>().Where(r => r.sprite == done || r.sprite == art).Select(r => r.sprite).ToList();
        T.Check(after.Count == 1 && after[0] == done, "once cleared it shows its cleared picture");
        mountain.picture = null; mountain.clearedPicture = null;
    }

    public static void TappingARegion()
    {
        Town t = Town.Build();
        List<Region> regions = t.UseSampleMap();
        MapRegions mr = MapRegions.Existing;
        Region rocks = regions.First(r => r.id == "north-rocks"), river = regions.First(r => r.id == "river"), meadow = regions.First(r => r.id == "east-meadow");
        Vector3 inRocks = new Vector3(0.5f, 6.5f, 0), inTown = new Vector3(0.5f, 0.5f, 0), inFog = new Vector3(22.5f, -10.5f, 0), inRiver = new Vector3(15.5f, -10.5f, 0);
        World.Tick(0.1f);
        bool Open() { World.EndFrame(); return t.Panel("RegionPanel") != null && t.Panel("RegionPanel").gameObject.activeInHierarchy; }

        T.Section("what a tap opens");
        t.ClickWorld(inTown);
        T.Check(!Open(), "tapping cleared land opens nothing");
        t.ClickWorld(inFog);
        T.Check(!Open(), "tapping fog opens nothing");
        t.ClickWorld(inRocks);
        T.Check(Open(), "tapping land you can clear opens the Region panel");
        List<string> rows = t.PanelRows("RegionPanel");
        T.Check(rows.Any(r => r.StartsWith("Rocks: next to cleared land, can be cleared")) && rows.Any(r => r.StartsWith("Cost: " + rocks.goldCost + " gold")) && rows.Any(r => r.StartsWith("Characters needed: " + rocks.charactersNeeded)) && rows.Any(r => r.StartsWith("Time: ")) && rows.Any(r => r.StartsWith("Size: ")), "it shows the terrain, cost, characters, time and size: " + string.Join(" | ", rows));
        T.Check(t.Panel("RegionPanel").GetComponentInChildren<TMP_Text>().text == rocks.DisplayName, "titled with the region's name: " + t.Panel("RegionPanel").GetComponentInChildren<TMP_Text>().text);

        T.Section("other cases");
        mr.Map.Clear(meadow);
        t.ClickWorld(inTown);
        T.Check(!Open(), "tapping the ground closes the panel");
        t.ClickWorld(inRiver);
        T.Check(Open(), "tapping a revealed river opens the panel too");
        rows = t.PanelRows("RegionPanel");
        T.Check(rows[0].Contains("cannot be cleared") && !rows.Any(r => r.StartsWith("Cost")), "...saying it cannot be cleared, with no cost: " + rows[0]);

        t.ClickWorld(inRocks);
        T.Check(Open() && t.PanelRows("RegionPanel").Any(r => r.StartsWith("Cost")), "tapping another region opens that one");
        t.ClickWorld(inTown);
        mr.Enabled = false;
        t.ClickWorld(inRocks);
        T.Check(!Open(), "switched off: tapping land opens nothing");
        mr.Enabled = true;

        T.Section("a drag is not a tap");
        UIManager.Instance.CloseEverything();
        Vector3 screen = t.camera.WorldToScreenPoint(inRocks);
        t.actions.Gameplay.PointerPosition.Perform(new Vector2(screen.x, screen.y));
        t.actions.Gameplay.PointerClick.Perform();
        World.Tick(0.016f);
        t.actions.Gameplay.PointerPosition.Perform(new Vector2(screen.x + 120f, screen.y + 40f));
        t.actions.Gameplay.PointerClick.Cancel();
        T.Check(!Open(), "dragging the camera across a region opens nothing");

        T.Section("the rows (pure)");
        T.Check(RegionMenu.FormatTime(45) == "45 s" && RegionMenu.FormatTime(120) == "2 min" && RegionMenu.FormatTime(3600) == "1 h" && RegionMenu.FormatTime(7200 + 1800) == "2 h 30 min", "times read as seconds, minutes, hours");
        var lines = RegionMenu.Describe(regions.First(r => r.id == "mountain"), mr, 12.9f);
        T.Check(lines.Any(l => l.Contains("not reachable yet")) && !lines.Any(l => l.StartsWith("Cost")), "a region you cannot reach yet says so: " + string.Join(" | ", lines));
        lines = RegionMenu.Describe(rocks, mr, 12.9f);
        T.Check(lines.Any(l => l.Contains("you have 12")), "the gold you have is shown");
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
