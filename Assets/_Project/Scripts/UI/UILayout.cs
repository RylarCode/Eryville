using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lays the on-screen UI out for the shape of the screen.
//
//   Landscape (a PC window, normally): the layout as built in the scene, untouched.
//   Portrait (a phone):  the canvas scales with the screen width, the four menu buttons become a bar along
//                        the bottom, every panel is a sheet that slides up above that bar, everything is
//                        sized for fingers, and the top row stays clear of notches (the safe area).
//
// It works on the scene's existing objects by name and remembers how they were, so changing the window
// shape back restores them exactly. Tick() should be called every frame; it re-lays-out when the screen
// size, shape or safe area changes.
public class UILayout
{
    public static UILayout Instance { get; private set; }

    public const float ReferenceWidth = 1080f;   // portrait canvas width, in canvas units
    public const float ReferenceHeight = 1920f;
    public const float TouchScale = 2.6f;        // how much bigger the generated menus are on a phone
    public const float BarHeight = 130f;         // the bottom button bar
    public const float Gap = 10f;

    // Where the screen size and safe area come from (replaced in tests)
    public Func<Vector2> ScreenSize = () => new Vector2(Screen.width, Screen.height);
    public Func<Rect> SafeArea = () => Screen.safeArea;

    public bool IsPortrait { get; private set; }

    // The Place / Cancel strip (exists once the layout has been portrait)
    public PlacementBar Bar { get; private set; }
    public Action OnBarConfirm;
    public Action OnBarCancel;

    private readonly Transform canvas;
    private readonly GameObject fontSource;
    private readonly CanvasScaler scaler;

    private class RectState { public Vector2 anchorMin, anchorMax, pivot, position, size; public Vector3 scale; }
    private class TextState { public bool autoSize; public float min, max, size; }
    private class GridState { public Vector2 cell, spacing; public GridLayoutGroup.Constraint constraint; public int count; }
    private readonly Dictionary<RectTransform, RectState> rects = new Dictionary<RectTransform, RectState>();
    private readonly Dictionary<TMP_Text, TextState> texts = new Dictionary<TMP_Text, TextState>();
    private readonly Dictionary<GridLayoutGroup, GridState> grids = new Dictionary<GridLayoutGroup, GridState>();
    private CanvasScaler.ScaleMode originalScaleMode;
    private Vector2 originalReference;
    private float originalMatch;
    private bool scalerSaved;

    private Vector2 lastScreen;
    private Rect lastSafe;
    private bool laidOut;

    public UILayout(Transform canvas, GameObject fontSource)
    {
        this.canvas = canvas;
        this.fontSource = fontSource;
        scaler = canvas.GetComponent<CanvasScaler>();
        Instance = this;
        IsPortrait = DetectPortrait();
    }

    private bool DetectPortrait()
    {
        Vector2 screen = ScreenSize();
        return screen.y > screen.x;
    }

    // Called every frame. Returns true when the layout changed between portrait and landscape,
    // so the menus built with the old sizes can be rebuilt.
    public bool Tick()
    {
        Vector2 screen = ScreenSize();
        Rect safe = SafeArea();
        if (laidOut && screen == lastScreen && SameRect(safe, lastSafe)) return false;

        bool wasPortrait = IsPortrait;
        IsPortrait = DetectPortrait();
        Apply();
        return laidOut && wasPortrait != IsPortrait;
    }

    private static bool SameRect(Rect a, Rect b)
    {
        return a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height;
    }

    // --- Laying out ---

    public void Apply()
    {
        Vector2 screen = ScreenSize();
        Rect safe = SafeArea();
        lastScreen = screen;
        lastSafe = safe;
        laidOut = true;
        Forget();

        if (!IsPortrait)
        {
            RestoreAll();
            if (Bar != null) Bar.Hide();
            return;
        }

        SaveScaler();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // keep the width: the canvas is as wide as the reference and as tall as the screen allows
        }

        float scale = screen.x / ReferenceWidth;                 // pixels per canvas unit
        float canvasHeight = screen.y / scale;
        float top = safe.width > 0f ? (screen.y - safe.yMax) / scale : 0f;
        float bottom = safe.width > 0f ? safe.yMin / scale : 0f;
        float sheetBottom = bottom + BarHeight + 2f * Gap;
        float sheetHeight = Mathf.Clamp(canvasHeight * 0.45f, 700f, 1000f);

        // The bottom bar: the five menu buttons, side by side
        string[] bar = { "OpenBuildButton", "OpenHireButton", "OpenTownButton", "OpenRecruitButton", "OpenStoryButton" };
        for (int i = 0; i < bar.Length; i++)
        {
            RectTransform button = Find(bar[i]);
            if (button == null) continue;
            Place(button, new Vector2(i / (float)bar.Length, 0f), new Vector2((i + 1) / (float)bar.Length, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bottom + Gap), new Vector2(-2f * Gap, BarHeight));
            Fit(button, 20f, 60f);
        }

        // The top row: resources on the left, the game menu on the right
        RectTransform resources = Find("ResourceText");
        if (resources != null)
        {
            Place(resources, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -(24f + top)), new Vector2(560f, 190f));
            Fit(resources, 28f, 60f);
        }
        RectTransform menuButton = Find("OpenMenuButton");
        if (menuButton != null)
        {
            Place(menuButton, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -(24f + top)), new Vector2(240f, 100f));
            Fit(menuButton, 20f, 52f);
        }
        RectTransform viewButton = Find("ViewButton");
        if (viewButton != null)
        {
            Place(viewButton, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-(24f + 240f + 16f), -(24f + top)), new Vector2(240f, 100f));
            Fit(viewButton, 20f, 52f);
        }
        RectTransform toast = Find("ToastArea"); // below the top row, which has the resources and the menu buttons
        if (toast != null) Place(toast, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(24f + top + 210f)), new Vector2(1000f, 10f));
        RectTransform test = Find("TestButtonforFunctions");
        if (test != null)
        {
            Place(test, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -(24f + top + 112f)), new Vector2(240f, 80f));
            Fit(test, 14f, 36f);
        }
        RectTransform menuPanel = Find("MenuPanel");
        if (menuPanel != null)
        {
            RectState state = Touch(menuPanel);
            Place(menuPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -(24f + top + 112f + 90f)), state.size);
            menuPanel.localScale = new Vector3(2f, 2f, 1f);
        }

        // Every panel becomes a sheet above the bottom bar
        string[] sheets = { "BuildingMenuPanel", "HireMenuPanel", "CharacterPanel", "TownPanel", "RecruitPanel", "StoryPanel", "NotificationPanel", "CheatPanel" };
        foreach (string sheetName in sheets)
        {
            RectTransform sheet = Find(sheetName);
            if (sheet != null) Place(sheet, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, sheetBottom), new Vector2(0f, sheetHeight));
        }

        // The build / hire panels hold a grid of cards: make it fit the width
        foreach (string gridName in new[] { "BuildingMenuPanel", "HireMenuPanel" })
        {
            RectTransform panel = Find(gridName);
            GridLayoutGroup grid = panel != null ? MenuPanels.GridOf(panel) : null;
            if (grid == null) continue;
            if (!grids.ContainsKey(grid)) grids[grid] = new GridState { cell = grid.cellSize, spacing = grid.spacing, constraint = grid.constraint, count = grid.constraintCount };
            grid.cellSize = new Vector2(236f, 270f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
        }

        PlaceDetails();

        // The Place / Cancel strip sits just above the bottom bar
        if (Bar == null && fontSource != null) Bar = new PlacementBar(canvas, fontSource, () => OnBarConfirm?.Invoke(), () => OnBarCancel?.Invoke());
        if (Bar != null) Place(Bar.Rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, sheetBottom), new Vector2(0f, 130f));
    }

    // The details sheet: the picture on the left (when the character has one), the text beside it, the buttons along the
    // bottom, and the occupant / job list over the text
    private void PlaceDetails()
    {
        RectTransform details = Find("CharacterPanel");
        if (details == null) return;

        float textLeft = detailsHavePortrait ? 0.4f : 0f;
        RectTransform info = Child(details, "CharacterInfo");
        if (info != null)
        {
            Place(info, new Vector2(textLeft, 0.26f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            info.offsetMin = new Vector2(24f, 0f);
            info.offsetMax = new Vector2(-24f, -24f);
            TMP_Text text = info.GetComponentInChildren<TMP_Text>();
            if (text != null) { Touch(text); text.enableAutoSizing = true; text.fontSizeMin = 26f; text.fontSizeMax = 56f; }
        }
        RectTransform picture = Child(details, "PortraitImage");
        if (picture != null)
        {
            Place(picture, new Vector2(0f, 0.26f), new Vector2(0.4f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            picture.offsetMin = new Vector2(16f, 0f);
            picture.offsetMax = new Vector2(-8f, -24f);
        }
        string[] buttons = { "AssignButton", "MoveButton", "TalkButton" };
        int[] places = { 0, 1, 1 }; // Move (buildings) and Talk (characters) are never shown together
        for (int i = 0; i < buttons.Length; i++)
        {
            RectTransform button = Child(details, buttons[i]);
            if (button == null) continue;
            Place(button, new Vector2(places[i] * 0.5f, 0f), new Vector2((places[i] + 1) * 0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Gap), new Vector2(-2f * Gap, 120f));
            Fit(button, 20f, 56f);
        }
        RectTransform list = Child(details, "AssignPanel");
        if (list != null)
        {
            Place(list, new Vector2(0f, 0.26f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            list.offsetMin = Vector2.zero;
            list.offsetMax = Vector2.zero;
            RectTransform scroll = Child(list, "Scroll View");
            if (scroll != null)
            {
                Place(scroll, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                scroll.offsetMin = new Vector2(16f, 16f);
                scroll.offsetMax = new Vector2(-16f, -16f);
            }
        }
    }

    private bool detailsHavePortrait;

    // Whether the character on the details sheet has a picture (the text then makes room for it). Only matters on a phone.
    public void SetDetailsHavePortrait(bool has)
    {
        if (detailsHavePortrait == has) return;
        detailsHavePortrait = has;
        if (laidOut && IsPortrait) PlaceDetails();
    }

    // Drops the part of the layout that belongs to the old shape (the strip is rebuilt for the new sizes)
    private void Forget()
    {
        foreach (RectTransform key in new List<RectTransform>(rects.Keys)) if (key == null) rects.Remove(key);
        foreach (TMP_Text key in new List<TMP_Text>(texts.Keys)) if (key == null) texts.Remove(key);
        foreach (GridLayoutGroup key in new List<GridLayoutGroup>(grids.Keys)) if (key == null) grids.Remove(key);
        if (Bar != null && !IsPortrait) { Bar.Destroy(); Bar = null; }
    }

    // --- Remembering how things were ---

    private RectState Touch(RectTransform rect)
    {
        if (!rects.TryGetValue(rect, out RectState state))
        {
            state = new RectState
            {
                anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot,
                position = rect.anchoredPosition, size = rect.sizeDelta, scale = rect.localScale,
            };
            rects[rect] = state;
        }
        return state;
    }

    private void Touch(TMP_Text text)
    {
        if (!texts.ContainsKey(text)) texts[text] = new TextState { autoSize = text.enableAutoSizing, min = text.fontSizeMin, max = text.fontSizeMax, size = text.fontSize };
    }

    private void SaveScaler()
    {
        if (scaler == null || scalerSaved) return;
        originalScaleMode = scaler.uiScaleMode;
        originalReference = scaler.referenceResolution;
        originalMatch = scaler.matchWidthOrHeight;
        scalerSaved = true;
    }

    private void RestoreAll()
    {
        foreach (KeyValuePair<RectTransform, RectState> entry in rects)
        {
            if (entry.Key == null) continue;
            RectTransform rect = entry.Key;
            RectState state = entry.Value;
            rect.anchorMin = state.anchorMin;
            rect.anchorMax = state.anchorMax;
            rect.pivot = state.pivot;
            rect.anchoredPosition = state.position;
            rect.sizeDelta = state.size;
            rect.localScale = state.scale;
        }
        foreach (KeyValuePair<TMP_Text, TextState> entry in texts)
        {
            if (entry.Key == null) continue;
            entry.Key.enableAutoSizing = entry.Value.autoSize;
            entry.Key.fontSizeMin = entry.Value.min;
            entry.Key.fontSizeMax = entry.Value.max;
            entry.Key.fontSize = entry.Value.size;
        }
        foreach (KeyValuePair<GridLayoutGroup, GridState> entry in grids)
        {
            if (entry.Key == null) continue;
            entry.Key.cellSize = entry.Value.cell;
            entry.Key.spacing = entry.Value.spacing;
            entry.Key.constraint = entry.Value.constraint;
            entry.Key.constraintCount = entry.Value.count;
        }
        if (scaler != null && scalerSaved)
        {
            scaler.uiScaleMode = originalScaleMode;
            scaler.referenceResolution = originalReference;
            scaler.matchWidthOrHeight = originalMatch;
        }
    }

    // Copies a rect as it was before any phone layout moved it. False if the layout has not touched it (then it is as it was).
    public bool CopyOriginalRect(RectTransform from, RectTransform to)
    {
        if (!rects.TryGetValue(from, out RectState state)) return false;
        to.anchorMin = state.anchorMin;
        to.anchorMax = state.anchorMax;
        to.pivot = state.pivot;
        to.anchoredPosition = state.position;
        to.sizeDelta = state.size;
        return true;
    }

    // --- Small helpers ---

    private RectTransform Find(string name)
    {
        return canvas.Find(name) as RectTransform;
    }

    private static RectTransform Child(RectTransform parent, string name)
    {
        return parent.Find(name) as RectTransform;
    }

    private void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        Touch(rect);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // Makes the text of a button or label shrink or grow to fill it
    private void Fit(RectTransform owner, float min, float max)
    {
        TMP_Text text = owner.GetComponentInChildren<TMP_Text>();
        if (text == null) return;
        Touch(text);
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
    }
}
