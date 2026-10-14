using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Town menu: a list of everything the player owns, in two tabs (characters, buildings), with a
// button to change the sort order and one to filter. Clicking a row opens that character's or
// building's details and moves the camera to it.
//
// The panel is built here at runtime so it matches the rest of the UI: it sits in the same place and
// has the same look as the details panel, and its scrolling list is a copy of the one that already
// exists (the assign-job list). What is listed and in what order comes from TownList.
public class TownMenu
{
    private readonly UIManager ui;
    private readonly GameObject buttonPrefab;
    private readonly GameObject panel;
    private readonly Transform content;
    private readonly TMP_Text charactersTabLabel;
    private readonly TMP_Text buildingsTabLabel;
    private readonly TMP_Text sortLabel;
    private readonly TMP_Text filterLabel;

    private TownTab tab = TownTab.Characters;
    private CharacterSort characterSort = CharacterSort.Name;
    private CharacterFilter characterFilter = CharacterFilter.All;
    private BuildingSort buildingSort = BuildingSort.Name;
    private BuildingFilter buildingFilter = BuildingFilter.All;

    private string shownSignature; // what the list currently shows, so it is only rebuilt when it changes

    public GameObject Panel => panel;

    public TownMenu(UIManager ui, GameObject buttonPrefab, GameObject detailsPanel, GameObject listPanel)
    {
        this.ui = ui;
        this.buttonPrefab = buttonPrefab;

        // The panel: same slot and same look as the details panel
        panel = new GameObject("TownPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(detailsPanel.transform.parent, false);
        panel.transform.SetSiblingIndex(detailsPanel.transform.GetSiblingIndex());
        RectTransform rect = panel.GetComponent<RectTransform>();
        RectTransform detailsRect = detailsPanel.GetComponent<RectTransform>();
        if (detailsRect != null)
        {
            rect.anchorMin = detailsRect.anchorMin;
            rect.anchorMax = detailsRect.anchorMax;
            rect.pivot = detailsRect.pivot;
            rect.anchoredPosition = detailsRect.anchoredPosition;
            rect.sizeDelta = detailsRect.sizeDelta;
        }
        Image detailsImage = detailsPanel.GetComponent<Image>();
        if (detailsImage != null)
        {
            Image image = panel.GetComponent<Image>();
            image.sprite = detailsImage.sprite;
            image.color = detailsImage.color;
            image.type = detailsImage.type;
        }

        // Across the top: the two tabs, then the sort and filter buttons
        GameObject header = new GameObject("TownHeader", typeof(RectTransform), typeof(GridLayoutGroup));
        header.transform.SetParent(panel.transform, false);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -10f);
        headerRect.sizeDelta = new Vector2(-20f, 70f);
        GridLayoutGroup grid = header.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(160f, 30f);
        grid.spacing = new Vector2(10f, 10f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperLeft;

        charactersTabLabel = AddHeaderButton(header.transform, () => SelectTab(TownTab.Characters));
        buildingsTabLabel = AddHeaderButton(header.transform, () => SelectTab(TownTab.Buildings));
        sortLabel = AddHeaderButton(header.transform, CycleSort);
        filterLabel = AddHeaderButton(header.transform, CycleFilter);

        // Below that: a scrolling list, copied from the existing one so it looks and works the same
        Transform sourceScroll = listPanel.transform.Find("Scroll View");
        if (sourceScroll != null)
        {
            GameObject scroll = UnityEngine.Object.Instantiate(sourceScroll.gameObject, panel.transform);
            scroll.name = "TownScroll";
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(10f, 10f);
            scrollRect.offsetMax = new Vector2(-10f, -90f);
            content = scroll.transform.Find("Viewport/Content");
            if (content != null) ClearChildren(content);
        }
        if (content == null)
        {
            Debug.LogError("TownMenu: could not find the scrolling list to copy (AssignPanel/Scroll View/Viewport/Content).");
            content = panel.transform;
        }

        panel.SetActive(false);
    }

    // Opened: show the current state straight away
    public void Show()
    {
        Refresh(true);
    }

    // Brings the list up to date. Does nothing (cheaply) when nothing it shows has changed.
    public void Refresh(bool force = false)
    {
        List<TownRow> rows = tab == TownTab.Characters
            ? TownList.CharacterRows(CharacterManager.Instance.Roster, characterSort, characterFilter)
            : TownList.BuildingRows(BuildingManager.Instance.Buildings, buildingSort, buildingFilter);

        UpdateHeader();

        string signature = tab + "|" + SortName() + "|" + FilterName() + "|" + string.Join("\n", rows.Select(r => r.Key + "|" + r.Label));
        if (!force && signature == shownSignature) return;
        shownSignature = signature;
        Rebuild(rows);
    }

    // --- Header ---

    private TMP_Text AddHeaderButton(Transform header, Action onClick)
    {
        GameObject go = UnityEngine.Object.Instantiate(buttonPrefab, header);
        go.name = "TownHeaderButton";
        go.GetComponent<Button>().onClick.AddListener(() => onClick());
        return go.GetComponentInChildren<TMP_Text>();
    }

    private void UpdateHeader()
    {
        int characters = CharacterManager.Instance.Roster.Count;
        int buildings = BuildingManager.Instance.Buildings.Count;
        charactersTabLabel.text = tab == TownTab.Characters ? $"[Characters ({characters})]" : $"Characters ({characters})";
        buildingsTabLabel.text = tab == TownTab.Buildings ? $"[Buildings ({buildings})]" : $"Buildings ({buildings})";
        sortLabel.text = "Sort: " + SortName();
        filterLabel.text = "Filter: " + FilterName();
    }

    private string SortName() { return tab == TownTab.Characters ? TownList.Describe(characterSort) : TownList.Describe(buildingSort); }
    private string FilterName() { return tab == TownTab.Characters ? TownList.Describe(characterFilter) : TownList.Describe(buildingFilter); }

    private void SelectTab(TownTab newTab)
    {
        tab = newTab;
        Refresh(true);
    }

    private void CycleSort()
    {
        if (tab == TownTab.Characters) characterSort = Next(characterSort);
        else buildingSort = Next(buildingSort);
        Refresh(true);
    }

    private void CycleFilter()
    {
        if (tab == TownTab.Characters) characterFilter = Next(characterFilter);
        else buildingFilter = Next(buildingFilter);
        Refresh(true);
    }

    private static T Next<T>(T value) where T : struct
    {
        T[] values = (T[])Enum.GetValues(typeof(T));
        return values[(Array.IndexOf(values, value) + 1) % values.Length];
    }

    // --- Rows ---

    private static void ClearChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private void Rebuild(List<TownRow> rows)
    {
        ClearChildren(content);

        if (rows.Count == 0)
        {
            GameObject empty = UnityEngine.Object.Instantiate(buttonPrefab, content);
            empty.name = "TownEmptyLabel";
            empty.GetComponentInChildren<TMP_Text>().text = tab == TownTab.Characters ? "No characters to show." : "No buildings to show.";
            return;
        }

        foreach (TownRow row in rows)
        {
            GameObject go = UnityEngine.Object.Instantiate(buttonPrefab, content);
            go.name = row.Key;
            go.GetComponent<Image>().sprite = row.Sprite;
            go.GetComponentInChildren<TMP_Text>().text = row.Label;

            TownRow captured = row;
            go.GetComponent<Button>().onClick.AddListener(() => OnRowClicked(captured));
        }
    }

    // Opens the details (which closes this menu, like any other menu) and looks at the thing
    private void OnRowClicked(TownRow row)
    {
        if (row.Character != null)
        {
            ui.ShowCharacterDetails(row.Character);
            if (row.Character.HasAvatar) Focus(row.Character.Avatar.transform.position);
        }
        else if (row.Building != null)
        {
            ui.ShowBuildingDetails(row.Building);
            Focus(row.Building.transform.position);
        }
    }

    private static void Focus(Vector3 world)
    {
        if (CameraController.Instance != null) CameraController.Instance.FocusOn(world);
    }
}
