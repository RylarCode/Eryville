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

        panel = MenuPanels.CreatePanel("TownPanel", detailsPanel);

        // Across the top: the two tabs, then the sort and filter buttons
        Transform header = MenuPanels.CreateHeader(panel, 2);
        charactersTabLabel = AddHeaderButton(header, () => SelectTab(TownTab.Characters));
        buildingsTabLabel = AddHeaderButton(header, () => SelectTab(TownTab.Buildings));
        sortLabel = AddHeaderButton(header, CycleSort);
        filterLabel = AddHeaderButton(header, CycleFilter);

        // Below that: a scrolling list, copied from the existing one so it looks and works the same
        content = MenuPanels.CreateScrollList(panel, listPanel, MenuPanels.HeaderHeight(2));

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
        return MenuPanels.AddHeaderButton(header, buttonPrefab, onClick, out Button unused);
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

    private void Rebuild(List<TownRow> rows)
    {
        MenuPanels.ClearChildren(content);

        if (rows.Count == 0)
        {
            MenuPanels.MakeRow(buttonPrefab, content, "TownEmptyLabel", tab == TownTab.Characters ? "No characters to show." : "No buildings to show.", null, false);
            return;
        }

        foreach (TownRow row in rows)
        {
            GameObject go = MenuPanels.MakeRow(buttonPrefab, content, row.Key, row.Label, row.Sprite);

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
