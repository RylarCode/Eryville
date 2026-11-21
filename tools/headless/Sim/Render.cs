using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine.Tilemaps;
using UnityEngine;
using UnityEngine.UI;

// Draws the game's UI without Unity: runs the game's real UI code in the headless town (with the scene's real canvas objects), works out where
// every rectangle ends up (anchors, layout groups, scroll masks) and writes the result as JSON for render/render_ui.py to turn into a PNG.
//
// It shows the real positions, sizes, colours and texts. It does NOT show the pictures (sprites are grey boxes), the real font (a similar one
// is used, so text can wrap differently) or the game world behind the UI (a flat green).
public static class Render
{
    private class Scenario
    {
        public string Name; public int Width, Height;
        public Action<Town> Setup;
    }

    public static void Run(string outDir, string sceneJson)
    {
        Directory.CreateDirectory(outDir);
        SceneUi.JsonPath = sceneJson;
        Action<UIManager.BookTab> tab = null;
        List<Scenario> scenarios = new List<Scenario>();
        void Add(string name, int w, int h, Action<Town> setup) => scenarios.Add(new Scenario { Name = name, Width = w, Height = h, Setup = setup });
        foreach (bool portrait in new[] { false, true })
        {
            string p = portrait ? "portrait" : "landscape";
            int w = portrait ? 1080 : 1280, h = portrait ? 2340 : 720;
            Add(p + "-start", w, h, t => { });
            Add(p + "-world", w, h, t => BuildWorld(t));
            Add(p + "-book-summary", w, h, t => { BuildWorld(t); UIManager.Instance.OpenBookTab(UIManager.BookTab.Summary); });
            Add(p + "-book-buildings", w, h, t => { BuildWorld(t); UIManager.Instance.OpenBookTab(UIManager.BookTab.Buildings); });
            Add(p + "-book-characters", w, h, t => { BuildWorld(t); UIManager.Instance.OpenBookTab(UIManager.BookTab.Characters); });
            Add(p + "-book-character-details", w, h, t => { BuildWorld(t); UIManager.Instance.ShowCharacterDetails(t.cm.Roster[0]); });
            Add(p + "-book-hire", w, h, t => { BuildWorld(t); UIManager.Instance.OpenBookTab(UIManager.BookTab.Hire); });
            Add(p + "-book-region", w, h, t => { BuildWorld(t); OpenRegion(t); });
            Add(p + "-settings", w, h, t => { BuildWorld(t); UIManager.Instance.Book.ToggleSettings(); });
            Add(p + "-resources", w, h, t => { BuildWorld(t); UIManager.Instance.Book.ToggleDropdown(); });
            Add(p + "-placing", w, h, t => { BuildWorld(t); UIManager.Instance.OpenBookTab(UIManager.BookTab.Buildings); Town.Click(t.ui.buildingButtonContainer.Find("Gold MineButton")); World.Tick(0.05f); });
        }

        foreach (Scenario s in scenarios)
        {
            T.Suite(s.Name, () =>
            {
                Screen.width = s.Width; Screen.height = s.Height;
                Town t = Town.Build();
                AddRealArt(t);
                AddRealBuildings(t);
                typeof(UIManager).GetField("characterText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.SetValue(t.ui, t.canvas.Find("CharacterPanel/CharacterInfo")?.GetComponent<TextMeshProUGUI>());
                typeof(UIManager).GetField("resourceText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.SetValue(t.ui, t.canvas.Find("ResourceText").GetComponent<TMP_Text>());
                t.rm.SetResourceAmount(ResourceManager.Gold, 1460); t.rm.SetResourceAmount(ResourceManager.Wood, 50); t.rm.SetResourceAmount(ResourceManager.Letters, 2);
                World.Tick(0.1f);
                s.Setup(t);
                World.Run(1f);
                World.EndFrame();
                Dump(t, s, Path.Combine(outDir, s.Name + ".json"));
            });
        }
    }

    // (the scene wires the Build button to its panel by hand in the editor; the stand-in canvas does not, so the panel is opened as the button would)
    private static void OpenBuild(Town t)
    {
        UIManager.Instance.TogglePanel(t.canvas.Find("BuildingMenuPanel").gameObject);
    }

    private static string Assets => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../Assets/_Project"));

    // "guid:fileID" of a sprite reference in an asset file ("  sprite: {fileID: 123, guid: abc, type: 3}"): what render_ui.py looks the picture up by
    private static string SpriteRef(string assetText, string field = "sprite")
    {
        Match m = Regex.Match(assetText, @"(?m)^  " + field + @": \{fileID: (-?\d+), guid: ([0-9a-f]{32})");
        return m.Success ? m.Groups[2].Value + ":" + m.Groups[1].Value : null;
    }

    private static Sprite Named(string reference)
    {
        Sprite sprite = new Sprite(100, 100);
        if (reference != null) sprite.name = reference;
        return sprite;
    }

    // The characters' and the road's real pictures, so lists and the world show them
    private static void AddRealArt(Town t)
    {
        foreach (string file in Directory.GetFiles(Path.Combine(Assets, "ScriptableObjects/Characters"), "*.asset"))
        {
            string text = File.ReadAllText(file);
            string name = Regex.Match(text, @"(?m)^  displayName: (.+)$").Groups[1].Value.Trim();
            CharacterType type = new[] { t.Firsy, t.Condo, t.Baker }.FirstOrDefault(c => c.displayName == name);
            if (type == null) continue;
            type.sprite = Named(SpriteRef(text));
            Match scale = Regex.Match(text, @"(?m)^  bodyScale: ([\d.]+)");
            if (scale.Success) type.bodyScale = float.Parse(scale.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        string road = Path.Combine(Assets, "ScriptableObjects/Roads/DirtRoad.asset");
        if (File.Exists(road)) t.DirtRoad.sprite = Named(SpriteRef(File.ReadAllText(road)));
    }

    // A town to look at: the sample Map, a few buildings, roads and characters on the Town Square (the picture of the world behind the UI)
    private static void BuildWorld(Town t)
    {
        t.UseSampleMap();
        World.Tick(0.1f);
        t.rm.SetResourceAmount(ResourceManager.Gold, 5000); t.rm.SetResourceAmount(ResourceManager.Wood, 500);
        BuildingType Pick(string name) => t.placer.AllBuildings.First(b => b.buildingName == name);
        t.Place(Pick("Home"), -4, 1); t.Place(Pick("Home"), -2, 1); t.Place(Pick("Gold Mine"), 2, 1); t.Place(Pick("Lumberyard"), -3, -2); t.Place(Pick("Farm"), 2, -2);
        for (int x = -5; x <= 4; x++) t.roads.Build(t.DirtRoad, new Vector2Int(x, -1));
        for (int y = -4; y <= 2; y++) t.roads.Build(t.DirtRoad, new Vector2Int(0, y));
        t.Hire(t.Firsy, 1f, -0.5f); t.Hire(t.Condo, -1.5f, -1.5f); t.Hire(t.Baker, 3.5f, -3f);
        t.camera.orthographicSize = 6.5f;
        t.camera.transform.position = new Vector3(0f, 0.5f, -10f);
    }

    private static List<Dictionary<string, object>> WorldItems(Town t, Scenario s)
    {
        List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();
        float half = t.camera.orthographicSize, aspect = s.Width / (float)s.Height;
        Vector3 c = t.camera.transform.position;
        MapRegions mr = MapRegions.Existing;
        for (int cy = Mathf.FloorToInt(c.y - half) - 1; cy <= Mathf.CeilToInt(c.y + half) + 1; cy++)
            for (int cx = Mathf.FloorToInt(c.x - half * aspect) - 1; cx <= Mathf.CeilToInt(c.x + half * aspect) + 1; cx++)
            {
                Vector2Int cell = new Vector2Int(cx, cy);
                if (!t.map.InBounds(cell)) continue;
                Dictionary<string, object> cellItem = new Dictionary<string, object> { ["kind"] = "ground", ["x"] = cx + 0.5f, ["y"] = cy + 0.5f, ["w"] = 1f, ["h"] = 1f };
                RoadType road = t.map.RoadAt(cell);
                if (road != null) cellItem["road"] = road.sprite?.name;
                items.Add(cellItem);
                if (mr != null && mr.Active && mr.OverlayTileAt(cell) is Tile tile)
                {
                    Color32 col = B(tile.color);
                    items.Add(new Dictionary<string, object> { ["kind"] = "overlay", ["x"] = cx + 0.5f, ["y"] = cy + 0.5f, ["w"] = 1f, ["h"] = 1f, ["rgba"] = new[] { (int)col.r, col.g, col.b, col.a } });
                }
            }
        // The trees, rocks and rubble on locked land: the game's own little pictures, sent as raw pixels
        Transform decor = t.grid.transform.Find("RegionDecor");
        if (decor != null)
        {
            Dictionary<Sprite, int> ids = new Dictionary<Sprite, int>();
            foreach (Transform child in decor.children)
            {
                SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                if (child.destroyed || sr == null || sr.sprite == null) continue;
                Vector3 p = child.position;
                if (Math.Abs(p.x - c.x) > half * aspect + 2 || Math.Abs(p.y - c.y) > half + 2) continue;
                if (!ids.TryGetValue(sr.sprite, out int id))
                {
                    id = ids.Count;
                    ids[sr.sprite] = id;
                    Color32[] px = sr.sprite.texture.GetPixels32();
                    byte[] raw = new byte[px.Length * 4];
                    for (int i = 0; i < px.Length; i++) { raw[i * 4] = px[i].r; raw[i * 4 + 1] = px[i].g; raw[i * 4 + 2] = px[i].b; raw[i * 4 + 3] = px[i].a; }
                    items.Add(new Dictionary<string, object> { ["kind"] = "decorSprite", ["id"] = id, ["size"] = sr.sprite.texture.width, ["rgba"] = Convert.ToBase64String(raw) });
                }
                items.Add(new Dictionary<string, object> { ["kind"] = "decor", ["id"] = id, ["x"] = p.x, ["y"] = p.y, ["scale"] = child.localScale.x, ["sortY"] = p.y });
            }
        }
        foreach (Building b in t.bm.Buildings)
            items.Add(new Dictionary<string, object> { ["kind"] = "building", ["x"] = b.transform.position.x, ["y"] = b.transform.position.y, ["w"] = (float)b.buildingType.size.x, ["h"] = (float)b.buildingType.size.y, ["ref"] = b.buildingType.sprite?.name, ["sortY"] = b.transform.position.y - b.buildingType.size.y * 0.5f });
        foreach (OwnedCharacter o in t.cm.Roster)
        {
            if (!o.HasAvatar) continue;
            float scale = o.Type.bodyScale > 0f ? o.Type.bodyScale : 1f;
            items.Add(new Dictionary<string, object> { ["kind"] = "character", ["x"] = o.Avatar.transform.position.x, ["y"] = o.Avatar.transform.position.y, ["h"] = 1f * scale, ["ref"] = o.Type.sprite?.name, ["sortY"] = o.Avatar.transform.position.y - 0.5f });
        }
        return items;
    }

    private static void OpenRegion(Town t)
    {
        t.UseSampleMap();
        World.Tick(0.1f);
        Region rocks = MapRegions.Existing.Map.Regions.First(r => r.id == "north-rocks");
        UIManager.Instance.OpenRegion(rocks);
    }

    // The building types of the project (names, sizes, costs) instead of the town's five stand-ins, so the Build menu has what the real one has
    private static void AddRealBuildings(Town t)
    {
        string dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../Assets/_Project/ScriptableObjects/Buildings"));
        if (!Directory.Exists(dir)) return;
        List<BuildingType> types = new List<BuildingType>();
        int id = 100;
        foreach (string file in Directory.GetFiles(dir, "*.asset"))
        {
            string text = File.ReadAllText(file);
            string name = Regex.Match(text, @"(?m)^  buildingName: (.+)$").Groups[1].Value.Trim();
            if (name.Length == 0) continue;
            int Num(string field) { Match m = Regex.Match(text, @"(?m)^  " + field + @": (\d+)"); return m.Success ? int.Parse(m.Groups[1].Value) : 0; }
            BuildingType b = Town.NewBuilding(name, id++, 2, goldCost: Num("goldCost"), woodCost: Num("woodCost"));
            b.sprite = Named(SpriteRef(text));
            Match size = Regex.Match(text, @"(?m)^  size: \{x: (\d+), y: (\d+)\}");
            if (size.Success) b.size = new Vector2Int(int.Parse(size.Groups[1].Value), int.Parse(size.Groups[2].Value));
            Match stone = Regex.Match(text, @"extraCosts:\r?\n  - item: \{[^}]*\}\r?\n    amount: (\d+)");
            if (stone.Success) b.extraCosts = new List<ItemAmount> { new ItemAmount(ItemCatalog.Get("Stone") ?? Stone(), float.Parse(stone.Groups[1].Value)) };
            types.Add(b);
        }
        // The cards are made once when the UI starts, so make them again for the new list
        t.placer.AllBuildings = types;
        foreach (Transform card in t.ui.buildingButtonContainer.children.ToList()) UnityEngine.Object.Destroy(card.gameObject);
        World.EndFrame();
        typeof(UIManager).GetMethod("GenerateBuildingPanelButtons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(t.ui, null);
    }

    private static ItemType Stone()
    {
        ItemType stone = ScriptableObject.CreateInstance<ItemType>();
        stone.itemName = "Stone"; stone.category = ItemCategory.Material;
        ItemCatalog.Register(stone);
        return stone;
    }

    // --- Working out the rectangles ---

    private struct R { public float x, y, w, h; public float Right => x + w; public float Top => y + h; }

    private static Vector2 ParentSize(R p) => new Vector2(p.w, p.h);

    private static R FromAnchors(RectTransform rt, R parent, float? heightOverride = null)
    {
        Vector2 size = rt.sizeDelta + Vector2.Scale(rt.anchorMax - rt.anchorMin, ParentSize(parent));
        if (heightOverride.HasValue) size.y = heightOverride.Value;
        Vector2 aMin = new Vector2(parent.x + rt.anchorMin.x * parent.w, parent.y + rt.anchorMin.y * parent.h);
        Vector2 aMax = new Vector2(parent.x + rt.anchorMax.x * parent.w, parent.y + rt.anchorMax.y * parent.h);
        Vector2 pivotPos = new Vector2(aMin.x + (aMax.x - aMin.x) * rt.pivot.x, aMin.y + (aMax.y - aMin.y) * rt.pivot.y) + rt.anchoredPosition;
        return new R { x = pivotPos.x - rt.pivot.x * size.x, y = pivotPos.y - rt.pivot.y * size.y, w = size.x, h = size.y };
    }

    private static List<RectTransform> ActiveChildren(RectTransform rt)
    {
        List<RectTransform> list = new List<RectTransform>();
        foreach (Transform c in rt.children) if (!c.destroyed && c.gameObject.activeSelf && c is RectTransform r && c.GetComponent<LayoutElement>()?.ignoreLayout != true) list.Add(r);
        return list;
    }

    private static float PreferredH(RectTransform c)
    {
        LayoutElement le = c.GetComponent<LayoutElement>();
        if (le != null && le.preferredHeight >= 0f) return le.preferredHeight;
        if (le != null && le.minHeight >= 0f) return le.minHeight;
        return c.sizeDelta.y > 0f ? c.sizeDelta.y : 0f;
    }

    private static float PreferredW(RectTransform c)
    {
        LayoutElement le = c.GetComponent<LayoutElement>();
        if (le != null && le.preferredWidth >= 0f) return le.preferredWidth;
        if (le != null && le.minWidth >= 0f) return le.minWidth;
        return 0f;
    }

    // Where a layout group puts each of its children (null when the object has no group)
    private static Dictionary<RectTransform, R> GroupLayout(RectTransform rt, R self, out float contentHeight)
    {
        contentHeight = -1f;
        List<RectTransform> kids = ActiveChildren(rt);
        Dictionary<RectTransform, R> map = new Dictionary<RectTransform, R>();

        VerticalLayoutGroup v = rt.GetComponent<VerticalLayoutGroup>();
        if (v != null && v.enabled)
        {
            RectOffset p = v.padding;
            float innerW = self.w - p.left - p.right, y = self.Top - p.top;
            float used = 0f; foreach (RectTransform k in kids) used += PreferredH(k);
            used += v.spacing * Math.Max(0, kids.Count - 1);
            float extra = v.childForceExpandHeight && kids.Count > 0 ? Math.Max(0f, self.h - p.top - p.bottom - used) / kids.Count : 0f;
            foreach (RectTransform k in kids)
            {
                float h = PreferredH(k) + extra;
                float w = v.childControlWidth ? innerW : k.sizeDelta.x;
                map[k] = new R { x = self.x + p.left, y = y - h, w = w, h = h };
                y -= h + v.spacing;
            }
            contentHeight = p.top + p.bottom + used;
            return map;
        }
        HorizontalLayoutGroup hz = rt.GetComponent<HorizontalLayoutGroup>();
        if (hz != null && hz.enabled)
        {
            RectOffset p = hz.padding;
            float innerH = self.h - p.top - p.bottom;
            float used = 0f; foreach (RectTransform k in kids) used += PreferredW(k);
            used += hz.spacing * Math.Max(0, kids.Count - 1);
            float extra = hz.childForceExpandWidth && kids.Count > 0 ? Math.Max(0f, self.w - p.left - p.right - used) / kids.Count : 0f;
            float x = self.x + p.left;
            foreach (RectTransform k in kids)
            {
                float w = PreferredW(k) + extra;
                map[k] = new R { x = x, y = self.y + p.bottom, w = w, h = hz.childControlHeight ? innerH : k.sizeDelta.y };
                x += w + hz.spacing;
            }
            contentHeight = p.top + p.bottom + (kids.Count > 0 ? kids.Max(k => PreferredH(k)) : 0f);
            return map;
        }
        GridLayoutGroup g = rt.GetComponent<GridLayoutGroup>();
        if (g != null && g.enabled)
        {
            RectOffset p = g.padding;
            float innerW = self.w - p.left - p.right;
            int cols = g.constraint == GridLayoutGroup.Constraint.FixedColumnCount ? Math.Max(1, g.constraintCount)
                : g.constraint == GridLayoutGroup.Constraint.FixedRowCount ? Math.Max(1, (int)Math.Ceiling(kids.Count / (float)Math.Max(1, g.constraintCount)))
                : Math.Max(1, (int)Math.Floor((innerW + g.spacing.x) / (g.cellSize.x + g.spacing.x)));
            for (int i = 0; i < kids.Count; i++)
            {
                int col = i % cols, row = i / cols;
                map[kids[i]] = new R { x = self.x + p.left + col * (g.cellSize.x + g.spacing.x), y = self.Top - p.top - (row + 1) * g.cellSize.y - row * g.spacing.y, w = g.cellSize.x, h = g.cellSize.y };
            }
            int rows = kids.Count == 0 ? 0 : (kids.Count + cols - 1) / cols;
            contentHeight = p.top + p.bottom + rows * g.cellSize.y + Math.Max(0, rows - 1) * g.spacing.y;
            return map;
        }
        return null;
    }

    private static Color32 B(Color c) => new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255f));

    private static void Walk(RectTransform rt, R rect, float scale, R? clip, List<Dictionary<string, object>> output, string path)
    {
        string here = path + "/" + rt.name;
        R? childClip = clip;
        if (rt.GetComponent<RectMask2D>() != null || rt.GetComponent<Mask>() != null)
            childClip = clip.HasValue ? Intersect(clip.Value, rect) : rect;

        Dictionary<string, object> item = new Dictionary<string, object>
        {
            ["name"] = rt.name, ["path"] = here, ["x"] = rect.x * scale, ["y"] = rect.y * scale, ["w"] = rect.w * scale, ["h"] = rect.h * scale,
        };
        if (clip.HasValue) item["clip"] = new[] { clip.Value.x * scale, clip.Value.y * scale, clip.Value.w * scale, clip.Value.h * scale };
        Image image = rt.GetComponent<Image>();
        if (image != null && image.enabled) { Color32 c = B(image.color); item["image"] = new Dictionary<string, object> { ["rgba"] = new[] { (int)c.r, c.g, c.b, c.a }, ["sprite"] = image.sprite != null, ["ref"] = image.sprite != null && image.sprite.name != null && image.sprite.name.Contains(":") ? image.sprite.name : null, ["fit"] = image.preserveAspect, ["sliced"] = image.type == Image.Type.Sliced, ["button"] = rt.GetComponent<Button>() != null }; }
        TMP_Text label = rt.GetComponent<TMP_Text>();
        if (label != null && label.enabled && !string.IsNullOrEmpty(label.text))
        {
            Color32 c = B(label.color);
            item["text"] = new Dictionary<string, object> { ["text"] = label.text, ["size"] = label.fontSize * scale, ["min"] = label.fontSizeMin * scale, ["max"] = label.fontSizeMax * scale, ["auto"] = label.enableAutoSizing, ["rgba"] = new[] { (int)c.r, c.g, c.b, c.a }, ["align"] = (int)label.alignment, ["bold"] = (label.fontStyle & FontStyles.Bold) != 0 };
        }
        output.Add(item);

        float contentHeight;
        Dictionary<RectTransform, R> group = GroupLayout(rt, rect, out contentHeight);
        foreach (RectTransform child in ActiveChildren(rt))
        {
            R childRect;
            if (group != null && group.TryGetValue(child, out R placed)) childRect = placed;
            else
            {
                float? fitted = null;
                ContentSizeFitter fit = child.GetComponent<ContentSizeFitter>();
                if (fit != null && fit.verticalFit != ContentSizeFitter.FitMode.Unconstrained)
                {
                    R pre = FromAnchors(child, rect);
                    GroupLayout(child, pre, out float wanted);
                    if (wanted >= 0f) fitted = wanted;
                }
                childRect = FromAnchors(child, rect, fitted);
            }
            Walk(child, childRect, scale, childClip, output, here);
        }
    }

    private static R Intersect(R a, R b)
    {
        float x = Math.Max(a.x, b.x), y = Math.Max(a.y, b.y), r = Math.Min(a.Right, b.Right), t = Math.Min(a.Top, b.Top);
        return new R { x = x, y = y, w = Math.Max(0f, r - x), h = Math.Max(0f, t - y) };
    }

    private static void Dump(Town t, Scenario s, string file)
    {
        RectTransform canvas = (RectTransform)t.canvas;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        float scale = 1f;
        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            float lw = Mathf.Log(s.Width / scaler.referenceResolution.x, 2f), lh = Mathf.Log(s.Height / scaler.referenceResolution.y, 2f);
            scale = Mathf.Pow(2f, Mathf.Lerp(lw, lh, scaler.matchWidthOrHeight));
        }
        else if (scaler != null) scale = scaler.scaleFactor <= 0f ? 1f : scaler.scaleFactor;

        R root = new R { x = 0, y = 0, w = s.Width / scale, h = s.Height / scale };
        List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();
        foreach (RectTransform child in ActiveChildren(canvas)) Walk(child, FromAnchors(child, root), scale, null, items, "");
        // The placement strip and other objects the game adds directly under the canvas are included above; the toasts live in ToastArea
        Vector3 cam = t.camera.transform.position;
        string json = JsonSerializer.Serialize(new Dictionary<string, object> { ["name"] = s.Name, ["width"] = s.Width, ["height"] = s.Height, ["scale"] = scale, ["camera"] = new[] { cam.x, cam.y, t.camera.orthographicSize }, ["world"] = WorldItems(t, s), ["items"] = items }, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(file, json);
    }
}
