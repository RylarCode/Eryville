using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Region panel: opened by tapping a region on the Map. Shows what it is, what clearing it costs and gives, and why it can or cannot be
// cleared now. (The Clear button itself arrives with the clearing step; until then the last row says so.) Built at runtime like the other menus.
public class RegionMenu
{
    private readonly GameObject buttonPrefab;
    private readonly GameObject panel;
    private readonly Transform content;
    private readonly TMP_Text titleLabel;
    private Region region;
    private string shownSignature;

    public GameObject Panel => panel;
    public Region Region => region;

    public RegionMenu(UIManager ui, GameObject buttonPrefab, GameObject detailsPanel, GameObject listPanel)
    {
        this.buttonPrefab = buttonPrefab;
        panel = MenuPanels.CreatePanel("RegionPanel", detailsPanel);
        Transform header = MenuPanels.CreateHeader(panel, 1);
        titleLabel = MenuPanels.AddHeaderButton(header, buttonPrefab, null, out Button unused, 1);
        content = MenuPanels.CreateScrollList(panel, listPanel, MenuPanels.HeaderHeight(1));
        panel.SetActive(false);
    }

    public void Show(Region region)
    {
        this.region = region;
        Refresh(true);
    }

    // The rows for a region, as plain text (also what the tests read)
    public static List<string> Describe(Region region, MapRegions regions, float gold)
    {
        List<string> rows = new List<string>();
        if (region == null || regions == null || regions.Map == null) return rows;
        RegionMap map = regions.Map;
        int cells = map.CellsOf(region).Count;

        if (map.IsCleared(region)) rows.Add($"{TerrainName(region.terrain)}: cleared");
        else if (!region.Clearable) rows.Add($"{TerrainName(region.terrain)}: cannot be cleared");
        else if (map.CanClear(region)) rows.Add($"{TerrainName(region.terrain)}: next to cleared land, can be cleared");
        else rows.Add($"{TerrainName(region.terrain)}: not reachable yet");

        if (!string.IsNullOrWhiteSpace(region.description)) rows.Add(region.description.Trim().Replace("\n", " "));
        rows.Add($"Size: {cells} cells");

        if (map.CanClear(region)) // (what clearing costs is only shown for land you can clear now)
        {
            rows.Add($"Cost: {region.goldCost} gold (you have {Mathf.FloorToInt(gold)})");
            rows.Add($"Characters needed: {region.charactersNeeded}");
            rows.Add($"Time: {FormatTime(region.clearSeconds)} with every place filled");
            string gives = region.YieldText();
            if (gives.Length > 0) rows.Add($"Gives: {gives}");
            rows.Add("Clear: coming in the next update");
        }
        return rows;
    }

    public static string TerrainName(RegionTerrain terrain)
    {
        return terrain.ToString();
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
        if (total < 60) return $"{total} s";
        int hours = total / 3600, minutes = (total % 3600) / 60;
        if (hours == 0) return $"{minutes} min";
        return minutes == 0 ? $"{hours} h" : $"{hours} h {minutes} min";
    }

    // Only rebuilds the list when what it shows changed (the gold changes while you watch)
    public void Refresh(bool force = false)
    {
        if (region == null) return;
        float gold = ResourceManager.Instance != null ? ResourceManager.Instance.GetResourceAmount(ResourceManager.Gold) : 0f;
        List<string> rows = Describe(region, MapRegions.Existing, gold);
        StringBuilder signature = new StringBuilder(region.Id).Append('#');
        foreach (string row in rows) signature.Append(row).Append('|');
        if (!force && signature.ToString() == shownSignature) return;
        shownSignature = signature.ToString();

        titleLabel.text = region.DisplayName;
        MenuPanels.ClearChildren(content);
        foreach (string row in rows) MenuPanels.MakeRow(buttonPrefab, content, "RegionRow", row, null, false);
    }
}
