using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The UI objects the real scene has (read from GameScene.unity by render/scene_ui.py into JSON): sizes, positions, picture colours and text.
// Only the renderer uses it, to put the town's stand-in canvas in the scene's real state before the game's own UI code starts; tests are untouched.
public static class SceneUi
{
    public static string JsonPath;   // set by the renderer; null = the plain stand-in canvas

    public static void Apply(Transform canvas)
    {
        if (JsonPath == null || !File.Exists(JsonPath)) return;
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(JsonPath)))
        {
            JsonElement root = doc.RootElement.GetProperty("canvas");
            foreach (JsonElement child in root.GetProperty("children").EnumerateArray()) Sync(canvas, child);
        }
    }

    private static Vector2 V(JsonElement e) => new Vector2((float)e[0].GetDouble(), (float)e[1].GetDouble());
    private static Color C(JsonElement e) => new Color((float)e[0].GetDouble(), (float)e[1].GetDouble(), (float)e[2].GetDouble(), (float)e[3].GetDouble());

    private static void Sync(Transform parent, JsonElement node)
    {
        string name = node.GetProperty("name").GetString();
        if (name == "DialoguePanel") return; // the game builds its own dialogue box
        Transform t = null;
        foreach (Transform c in parent.children) if (c.name == name && !c.destroyed) { t = c; break; }
        if (t == null)
        {
            GameObject g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            t = g.transform;
            if (node.TryGetProperty("button", out _)) g.AddComponent<Button>();
            if (node.TryGetProperty("scroll", out _)) g.AddComponent<ScrollRect>();
        }
        RectTransform r = (RectTransform)t;
        r.anchorMin = V(node.GetProperty("anchorMin")); r.anchorMax = V(node.GetProperty("anchorMax"));
        r.pivot = V(node.GetProperty("pivot")); r.anchoredPosition = V(node.GetProperty("position")); r.sizeDelta = V(node.GetProperty("size"));

        if (node.TryGetProperty("image", out JsonElement image))
        {
            Image img = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
            img.color = C(image.GetProperty("color"));
            img.enabled = image.GetProperty("enabled").GetBoolean();
            if (image.GetProperty("sprite").GetBoolean() && img.sprite == null) img.sprite = new Sprite(64, 64);
            img.type = image.GetProperty("type").GetInt32() == 1 ? Image.Type.Sliced : Image.Type.Simple;
        }
        if (node.TryGetProperty("mask", out _) && t.GetComponent<Mask>() == null) t.gameObject.AddComponent<Mask>();
        if (node.TryGetProperty("fitter", out JsonElement fit) && t.GetComponent<ContentSizeFitter>() == null)
        {
            ContentSizeFitter f = t.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = (ContentSizeFitter.FitMode)fit.GetProperty("v").GetInt32();
            f.horizontalFit = (ContentSizeFitter.FitMode)fit.GetProperty("h").GetInt32();
        }
        if (node.TryGetProperty("grid", out JsonElement grid) && t.GetComponent<GridLayoutGroup>() == null)
        {
            GridLayoutGroup g = t.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = V(grid.GetProperty("cell")); g.spacing = V(grid.GetProperty("spacing"));
            g.constraint = (GridLayoutGroup.Constraint)grid.GetProperty("constraint").GetInt32(); g.constraintCount = grid.GetProperty("count").GetInt32();
        }
        if (node.TryGetProperty("text", out JsonElement text))
        {
            TMP_Text label = t.GetComponent<TMP_Text>() ?? t.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text.GetProperty("text").GetString();
            label.fontSize = (float)text.GetProperty("size").GetDouble();
            label.color = C(text.GetProperty("color"));
            label.enableAutoSizing = text.GetProperty("auto").GetBoolean();
            label.fontSizeMin = (float)text.GetProperty("min").GetDouble(); label.fontSizeMax = (float)text.GetProperty("max").GetDouble();
            label.alignment = (TextAlignmentOptions)(text.GetProperty("h").GetInt32() | text.GetProperty("v").GetInt32());
        }

        // The stand-in canvas gives some objects a child label the scene does not have (the scene's text sits on the object itself)
        if (node.TryGetProperty("text", out _))
        {
            HashSet<string> sceneChildren = new HashSet<string>();
            foreach (JsonElement child in node.GetProperty("children").EnumerateArray()) sceneChildren.Add(child.GetProperty("name").GetString());
            foreach (Transform extra in t.children) if (!extra.destroyed && extra.name == "Text (TMP)" && !sceneChildren.Contains("Text (TMP)")) extra.gameObject.SetActive(false);
        }

        t.gameObject.SetActive(node.GetProperty("active").GetBoolean());
        foreach (JsonElement child in node.GetProperty("children").EnumerateArray())
        {
            // The scene's text objects sit under their button as "Text (TMP)": the stand-in already has one per button
            Sync(t, child);
        }
    }
}
