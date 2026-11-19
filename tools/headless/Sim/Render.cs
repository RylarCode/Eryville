using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using TMPro;
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
        List<Scenario> scenarios = new List<Scenario>
        {
            new Scenario { Name = "landscape-start", Width = 1280, Height = 720, Setup = t => { } },
            new Scenario { Name = "landscape-game-menu", Width = 1280, Height = 720, Setup = t => t.canvas.Find("MenuPanel").gameObject.SetActive(true) },
            new Scenario { Name = "landscape-build-menu", Width = 1280, Height = 720, Setup = t => OpenBuild(t) },
            new Scenario { Name = "landscape-placing", Width = 1280, Height = 720, Setup = t => { OpenBuild(t); Town.Click(t.ui.buildingButtonContainer.Find("Gold MineButton")); World.Tick(0.05f); } },
            new Scenario { Name = "landscape-region", Width = 1280, Height = 720, Setup = t => OpenRegion(t) },
            new Scenario { Name = "portrait-start", Width = 1080, Height = 2340, Setup = t => { } },
            new Scenario { Name = "portrait-build-menu", Width = 1080, Height = 2340, Setup = t => OpenBuild(t) },
            new Scenario { Name = "portrait-game-menu", Width = 1080, Height = 2340, Setup = t => t.canvas.Find("MenuPanel").gameObject.SetActive(true) },
            new Scenario { Name = "portrait-region", Width = 1080, Height = 2340, Setup = t => OpenRegion(t) },
        };

        foreach (Scenario s in scenarios)
        {
            T.Suite(s.Name, () =>
            {
                Screen.width = s.Width; Screen.height = s.Height;
                Town t = Town.Build();
                AddRealBuildings(t);
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
        if (image != null && image.enabled) { Color32 c = B(image.color); item["image"] = new Dictionary<string, object> { ["rgba"] = new[] { (int)c.r, c.g, c.b, c.a }, ["sprite"] = image.sprite != null, ["sliced"] = image.type == Image.Type.Sliced, ["button"] = rt.GetComponent<Button>() != null }; }
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
        string json = JsonSerializer.Serialize(new Dictionary<string, object> { ["name"] = s.Name, ["width"] = s.Width, ["height"] = s.Height, ["scale"] = scale, ["items"] = items }, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(file, json);
    }
}
