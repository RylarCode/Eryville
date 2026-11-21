using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The game's main chrome: resources along the top left, a settings button and a book button at the top right, and the BOOK: a panel that slides
// in from the right (landscape) or up from the bottom (portrait) and takes half the screen. Its pages are the game's menus, reached by
// bookmark-style tabs along its top edge. The settings button slides the game menu (Save, Load...) in from the right the same way.
//
// The menus themselves are unchanged (they are still made and shown by UIManager); this only decides where they sit, shows the tabs, and moves
// everything in and out. The old bottom bar, Menu and View buttons are hidden: their jobs are tabs and settings now.
public class BookUi
{
    private const float SlideSeconds = 0.22f;

    private readonly UIManager ui;
    private readonly RectTransform canvas;
    private readonly GameObject font;
    private readonly UILayout layout;

    // The menus that are pages of the book (found by name each time: the game makes some of them again when the screen changes shape)
    private static readonly string[] SheetNames = { "BuildingMenuPanel", "HireMenuPanel", "CharacterPanel", "TownPanel", "RecruitPanel", "StoryPanel", "QuestPanel", "NotificationPanel", "ConnectionPanel", "RegionPanel", "SummaryPanel", "CheatPanel" };
    private static readonly string[] OldChrome = { "OpenBuildButton", "OpenHireButton", "OpenTownButton", "OpenRecruitButton", "OpenStoryButton", "OpenQuestButton", "OpenMenuButton", "ViewButton", "ResourceText", "TestButtonforFunctions" };

    // The top right
    private RectTransform icons;
    private GameObject settingsButton, bookButton;
    // The top left
    private RectTransform hud;
    private Chip goldChip, foodChip;
    private GameObject allButton;
    private RectTransform dropdown;
    private Transform dropdownContent;
    private bool dropdownOpen;
    private float nextDropdownRefresh;
    // The book
    private RectTransform book;
    private RectTransform tabStrip;
    private readonly List<TabButton> tabs = new List<TabButton>();
    private float bookT, settingsT;       // 0 hidden .. 1 shown
    private bool settingsWanted;
    private float lastAppliedBook = -1f, lastAppliedSettings = -1f;
    private bool portrait;
    private float bookSize, tabHeight, pad;
    private float settingsWidth;

    private class Chip { public GameObject root; public Image icon; public TMP_Text number; }
    private class TabButton { public UIManager.BookTab tab; public GameObject root; public Image image; public TMP_Text label; }

    public bool BookVisible => bookT > 0.02f && PageOpen;
    public bool SettingsOpen => settingsWanted;

    // Whether any page of the book is open right now
    public bool PageOpen
    {
        get
        {
            foreach (string name in SheetNames)
            {
                if (name == "CheatPanel") continue;
                Transform sheet = canvas.Find(name);
                if (sheet != null && sheet.gameObject.activeSelf) return true;
            }
            return false;
        }
    }

    private readonly Transform keepBelow; // the dialogue box: everything here sits under it

    public BookUi(UIManager ui, Transform canvas, GameObject font, UILayout layout, Transform keepBelow)
    {
        this.ui = ui; this.canvas = (RectTransform)canvas; this.font = font; this.layout = layout; this.keepBelow = keepBelow;
        foreach (string name in OldChrome) { Transform old = canvas.Find(name); if (old != null) old.gameObject.SetActive(false); }
        BuildTopRight();
        BuildHud();
        BuildBook();
        BuildDropdown();
    }

    // --- Building the pieces ---

    private GameObject MakeButton(Transform parent, string name, string text, Action onClick, Color colour)
    {
        GameObject go = MenuPanels.MakeBarButton(font, parent, name, text, onClick);
        go.GetComponent<Image>().color = colour;
        UnityEngine.Object.Destroy(go.GetComponent<LayoutElement>());
        return go;
    }

    private void BuildTopRight()
    {
        icons = NewRect("TopRightButtons", canvas);
        settingsButton = MakeButton(icons, "SettingsButton", "Settings", ToggleSettings, new Color(0.93f, 0.9f, 0.8f, 1f));
        bookButton = MakeButton(icons, "BookButton", "Book", ToggleBook, new Color(0.85f, 0.7f, 0.5f, 1f));
    }

    private void BuildHud()
    {
        hud = NewRect("ResourceBar", canvas);
        goldChip = MakeChip("GoldChip");
        foodChip = MakeChip("FoodChip");
        allButton = MakeButton(hud, "AllResourcesButton", "All", ToggleDropdown, new Color(0.93f, 0.9f, 0.8f, 0.95f));
        goldChip.icon.sprite = ItemCatalog.Get(ResourceManager.Gold)?.Icon;
        foodChip.icon.sprite = (ItemCatalog.Get("Bread") ?? ItemCatalog.All.FirstOrDefault(i => i.edible))?.Icon;
    }

    private Chip MakeChip(string name)
    {
        Chip chip = new Chip();
        chip.root = new GameObject(name, typeof(RectTransform), typeof(Image));
        chip.root.transform.SetParent(hud, false);
        chip.root.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.6f);
        chip.root.GetComponent<Image>().raycastTarget = false;
        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(chip.root.transform, false);
        chip.icon = icon.GetComponent<Image>();
        chip.icon.preserveAspect = true;
        chip.icon.raycastTarget = false;
        chip.number = MenuPanels.MakeLabel(font, chip.root.transform, "Number", 10f, 40f, TextAlignmentOptions.MidlineLeft);
        chip.number.color = Color.white;
        chip.number.raycastTarget = false;
        return chip;
    }

    private void BuildBook()
    {
        book = NewRect("Book", canvas);
        Image cover = book.gameObject.AddComponent<Image>();
        cover.color = new Color(0.36f, 0.22f, 0.12f, 1f); // the cover: the pages sit inside it
        tabStrip = NewRect("Tabs", book);
        UIManager.BookTab[] order = (UIManager.BookTab[])Enum.GetValues(typeof(UIManager.BookTab));
        foreach (UIManager.BookTab tab in order)
        {
            UIManager.BookTab captured = tab;
            TabButton button = new TabButton { tab = tab };
            button.root = MakeButton(tabStrip, "Tab" + tab, tab.ToString(), () => ui.OpenBookTab(captured), new Color(0.78f, 0.62f, 0.4f, 1f));
            button.image = button.root.GetComponent<Image>();
            button.label = button.root.GetComponentInChildren<TMP_Text>();
            button.label.enableAutoSizing = true;
            tabs.Add(button);
        }
        book.gameObject.SetActive(false);
    }

    private void BuildDropdown()
    {
        dropdown = NewRect("ResourceDropdown", canvas);
        Image background = dropdown.gameObject.AddComponent<Image>();
        background.color = new Color(0.96f, 0.93f, 0.85f, 0.98f);
        dropdownContent = dropdown;
        dropdown.gameObject.SetActive(false);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    // --- Opening and closing ---

    public void ToggleBook()
    {
        if (BookVisible || PageOpen) { ui.CloseBook(); return; }
        CloseSettings();
        CloseDropdown();
        ui.OpenBookTab(UIManager.BookTab.Summary);
    }

    public void ToggleSettings()
    {
        if (settingsWanted) { CloseSettings(); return; }
        ui.CloseBook();
        CloseDropdown();
        settingsWanted = true;
        Transform menu = canvas.Find("MenuPanel");
        if (menu != null) menu.gameObject.SetActive(true);
    }

    public void CloseSettings() { settingsWanted = false; }

    public void ToggleDropdown()
    {
        dropdownOpen = !dropdownOpen;
        if (dropdownOpen) { CloseSettings(); RefreshDropdown(true); }
        dropdown.gameObject.SetActive(dropdownOpen);
    }

    public void CloseDropdown()
    {
        dropdownOpen = false;
        dropdown.gameObject.SetActive(false);
    }

    // Everything that sits over the map closes (a tap on the ground)
    public void CloseTransient()
    {
        CloseSettings();
        CloseDropdown();
    }

    // --- Where everything is ---

    // Works out sizes for the current screen shape and puts everything in place. Called when the layout changes.
    public void Relayout()
    {
        portrait = layout != null && layout.IsPortrait;
        float s = portrait ? MenuPanels.Scale : 1f;
        Vector2 size = canvas.rect.size;
        pad = 8f * s;
        tabHeight = 38f * s;
        bookSize = portrait ? size.y * 0.5f : size.x * 0.5f;
        Transform menuPanel = canvas.Find("MenuPanel");
        settingsWidth = (menuPanel != null ? ((RectTransform)menuPanel).sizeDelta.x : 180f) * (portrait ? 2f : 1f) + 8f; // the game menu as wide as it is

        float iconSize = 44f * s, margin = 14f * s;
        // The two buttons at the top right (settings above the book)
        icons.anchorMin = icons.anchorMax = icons.pivot = new Vector2(1f, 1f);
        icons.sizeDelta = new Vector2(iconSize * 2.2f, iconSize * 2f + margin * 0.5f);
        Place(settingsButton, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(iconSize * 2.2f, iconSize));
        Place(bookButton, new Vector2(1f, 1f), new Vector2(0f, -(iconSize + margin * 0.5f)), new Vector2(iconSize * 2.2f, iconSize));
        FitLabel(settingsButton, 10f * s, 20f * s); FitLabel(bookButton, 10f * s, 20f * s);

        // The resource bar at the top left
        hud.anchorMin = hud.anchorMax = hud.pivot = new Vector2(0f, 1f);
        hud.anchoredPosition = new Vector2(margin, -margin);
        float chipW = (portrait ? 100f : 150f) * s, chipH = 44f * s, gap = 8f * s;
        hud.sizeDelta = new Vector2(chipW * 2f + iconSize * 1.4f + gap * 2f, chipH);
        PlaceChip(goldChip, 0f, chipW, chipH);
        PlaceChip(foodChip, chipW + gap, chipW, chipH);
        Place(allButton, new Vector2(0f, 1f), new Vector2((chipW + gap) * 2f, 0f), new Vector2(iconSize * 1.4f, chipH));
        FitLabel(allButton, 10f * s, 20f * s);

        // The dropdown hangs under the resource bar
        dropdown.anchorMin = dropdown.anchorMax = dropdown.pivot = new Vector2(0f, 1f);
        dropdown.anchoredPosition = new Vector2(margin, -(margin + chipH + gap));
        dropdown.sizeDelta = new Vector2(chipW * 2f + iconSize * 1.4f + gap * 2f, 100f);

        // The tabs along the top of the book: as wide as the book, shared equally
        tabStrip.anchorMin = new Vector2(0f, 1f); tabStrip.anchorMax = new Vector2(1f, 1f); tabStrip.pivot = new Vector2(0.5f, 1f);
        tabStrip.anchoredPosition = new Vector2(0f, -pad * 0.5f); tabStrip.sizeDelta = new Vector2(-pad, tabHeight);
        for (int i = 0; i < tabs.Count; i++)
        {
            RectTransform r = (RectTransform)tabs[i].root.transform;
            r.anchorMin = new Vector2(i / (float)tabs.Count, 0f); r.anchorMax = new Vector2((i + 1) / (float)tabs.Count, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(1f, 0f); r.offsetMax = new Vector2(-1f, 0f);
            float tabFont = portrait ? 17f : 11f;   // the same size on every tab (only a longer word shrinks)
            tabs[i].label.enableAutoSizing = true; tabs[i].label.fontSizeMax = tabFont; tabs[i].label.fontSizeMin = tabFont * 0.7f; tabs[i].label.fontSize = tabFont;
        }
        lastAppliedBook = lastAppliedSettings = -1f;
        Apply();
        ApplyTop();
        // Drawing order: the book under every page; the resource bar, its dropdown and the two buttons over the pages, under the dialogue
        book.SetAsFirstSibling();
        if (keepBelow != null)
            foreach (Transform t in new Transform[] { hud, dropdown, icons }) t.SetSiblingIndex(Math.Max(0, keepBelow.GetSiblingIndex()));
    }

    private static void Place(GameObject go, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        r.anchoredPosition = position;
        r.sizeDelta = size;
    }

    private void PlaceChip(Chip chip, float x, float w, float h)
    {
        RectTransform r = (RectTransform)chip.root.transform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, 0f);
        r.sizeDelta = new Vector2(w, h);
        RectTransform icon = (RectTransform)chip.icon.transform;
        icon.anchorMin = new Vector2(0f, 0f); icon.anchorMax = new Vector2(0f, 1f); icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(4f, 0f); icon.sizeDelta = new Vector2(h - 8f, -8f);
        RectTransform number = (RectTransform)chip.number.transform;
        number.anchorMin = Vector2.zero; number.anchorMax = Vector2.one;
        number.offsetMin = new Vector2(h, 0f); number.offsetMax = new Vector2(-6f, 0f);
        chip.number.fontSizeMin = 10f; chip.number.fontSizeMax = Mathf.Max(14f, h * 0.55f); chip.number.enableAutoSizing = true;
    }

    private static void FitLabel(GameObject button, float min, float max)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label == null) return;
        label.enableAutoSizing = true; label.fontSizeMin = min; label.fontSizeMax = max;
    }

    // --- Moving ---

    public void Tick(float dt)
    {
        bool wantBook = PageOpen;
        bookT = Mathf.MoveTowards(bookT, wantBook ? 1f : 0f, dt / SlideSeconds);
        float settingsTarget = settingsWanted ? 1f : 0f;
        settingsT = Mathf.MoveTowards(settingsT, settingsTarget, dt / SlideSeconds);
        Transform menu = canvas.Find("MenuPanel");
        if (menu != null && !settingsWanted && settingsT <= 0f && menu.gameObject.activeSelf) menu.gameObject.SetActive(false);

        if (bookT != lastAppliedBook || settingsT != lastAppliedSettings) { Apply(); ApplyTop(); }
        UpdateTexts();
        if (dropdownOpen && Time.unscaledTime >= nextDropdownRefresh) { nextDropdownRefresh = Time.unscaledTime + 0.5f; RefreshDropdown(false); }
    }

    // The book and its pages, and the settings menu, at their current place of the slide
    private void Apply()
    {
        lastAppliedBook = bookT; lastAppliedSettings = settingsT;
        float s = portrait ? MenuPanels.Scale : 1f;
        float eased = 1f - (1f - bookT) * (1f - bookT);
        float hide = (1f - eased) * (bookSize + 20f); // how far it is still off the screen

        // The book itself
        if (portrait)
        {
            book.anchorMin = new Vector2(0f, 0f); book.anchorMax = new Vector2(1f, 0f); book.pivot = new Vector2(0.5f, 0f);
            book.sizeDelta = new Vector2(0f, bookSize); book.anchoredPosition = new Vector2(0f, -hide);
        }
        else
        {
            book.anchorMin = new Vector2(1f, 0f); book.anchorMax = new Vector2(1f, 1f); book.pivot = new Vector2(1f, 0.5f);
            book.sizeDelta = new Vector2(bookSize, 0f); book.anchoredPosition = new Vector2(hide, 0f);
        }
        bool shown = bookT > 0.001f;
        if (book.gameObject.activeSelf != shown) book.gameObject.SetActive(shown);

        // The pages: the same box, inside the cover and under the tabs
        foreach (string name in SheetNames)
        {
            Transform sheet = canvas.Find(name);
            if (sheet == null) continue;
            RectTransform r = (RectTransform)sheet;
            if (portrait)
            {
                r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(0.5f, 0f);
                r.sizeDelta = new Vector2(-2f * pad, bookSize - tabHeight - 2f * pad); r.anchoredPosition = new Vector2(0f, pad - hide);
            }
            else
            {
                r.anchorMin = new Vector2(1f, 0f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 0.5f);
                r.sizeDelta = new Vector2(bookSize - 2f * pad, -(tabHeight + 2f * pad)); r.anchoredPosition = new Vector2(-pad + hide, -(tabHeight * 0.5f));
            }
            if (name == "CheatPanel") continue; // (the testing tools keep the whole screen's worth; they just sit in the same place)
            r.localScale = Vector3.one;
        }

        // The settings menu (the game menu as it is, sliding in from the right)
        Transform menu = canvas.Find("MenuPanel");
        if (menu != null)
        {
            RectTransform r = (RectTransform)menu;
            float easedS = 1f - (1f - settingsT) * (1f - settingsT);
            float width = settingsWidth;
            float iconRow = (44f * s) * 2f + 24f * s;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(-8f + (1f - easedS) * (width + 20f), -iconRow);
            r.localScale = new Vector3(portrait ? 2f : 1f, portrait ? 2f : 1f, 1f);
        }
    }

    // The two buttons ride along with whatever slides in from the right (on a phone nothing covers them, so they stay)
    private void ApplyTop()
    {
        float s = portrait ? MenuPanels.Scale : 1f;
        float shift = 0f;
        if (!portrait)
        {
            float easedBook = 1f - (1f - bookT) * (1f - bookT);
            float easedSet = 1f - (1f - settingsT) * (1f - settingsT);
            shift = Mathf.Max(easedBook * bookSize, easedSet * (settingsWidth + 8f));
        }
        icons.anchoredPosition = new Vector2(-14f * s - shift, -14f * s);
    }

    // --- What the pieces say ---

    private void UpdateTexts()
    {
        ResourceManager rm = ResourceManager.Instance;
        if (rm != null)
        {
            goldChip.number.text = Mathf.FloorToInt(rm.Get(ResourceManager.Gold)).ToString();
            float food = 0f;
            foreach (ItemType item in ItemCatalog.All) if (item.edible) food += rm.Get(item.Name);
            foodChip.number.text = Mathf.FloorToInt(food).ToString();
        }
        UIManager.BookTab active = ui.ActiveTab;
        QuestManager quests = QuestManager.Instance;
        int waiting = quests != null ? quests.AttentionCount : 0;
        foreach (TabButton button in tabs)
        {
            bool isActive = PageOpen && button.tab == active;
            button.image.color = isActive ? new Color(0.97f, 0.92f, 0.8f, 1f) : new Color(0.72f, 0.55f, 0.36f, 1f);
            string text = button.tab == UIManager.BookTab.Quests && waiting > 0 ? $"Quests ({waiting})" : button.tab.ToString();
            if (button.label.text != text) button.label.text = text;
        }
    }

    // The list of every resource under the "All" button
    private void RefreshDropdown(bool force)
    {
        ResourceManager rm = ResourceManager.Instance;
        if (rm == null) return;
        MenuPanels.ClearChildren(dropdownContent);
        float rowHeight = MenuPanels.RowHeight * 0.8f;
        int count = 0;
        foreach (Resource resource in rm.resources.Values)
        {
            bool core = resource.resourceName == ResourceManager.Gold || resource.resourceName == ResourceManager.Wood || resource.resourceName == ResourceManager.Letters;
            if (!core && resource.currentAmount < 1f) continue;
            float capacity = rm.Capacity(resource.resourceName);
            string text = capacity > 0f ? $"{resource.resourceName}: {Mathf.FloorToInt(resource.currentAmount)}/{Mathf.FloorToInt(capacity)}" : $"{resource.resourceName}: {Mathf.FloorToInt(resource.currentAmount)}";
            GameObject row = MenuPanels.MakeRow(font, dropdownContent, "ResourceRow", text, ItemCatalog.Get(resource.resourceName)?.Icon, false);
            RectTransform r = (RectTransform)row.transform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -(4f + count * (rowHeight + 2f))); r.sizeDelta = new Vector2(-8f, rowHeight);
            UnityEngine.Object.Destroy(row.GetComponent<LayoutElement>());
            count++;
        }
        dropdown.sizeDelta = new Vector2(dropdown.sizeDelta.x, 8f + count * (rowHeight + 2f));
    }
}
