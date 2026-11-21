using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Summary page of the book: what needs attention now (urgent messages), what the town has, and how it is doing (happiness, who is
// hungry or unpaid, how many are at work). Built at runtime like the other menus; rebuilt only when its text changes.
public class SummaryMenu
{
    private class Line
    {
        public string text;
        public Action click; // null: a plain note
    }

    private readonly GameObject buttonPrefab;
    private readonly GameObject panel;
    private readonly Transform content;
    private readonly TMP_Text titleLabel;
    private string shownSignature;

    public GameObject Panel => panel;

    public SummaryMenu(UIManager ui, GameObject buttonPrefab, GameObject detailsPanel, GameObject listPanel)
    {
        this.buttonPrefab = buttonPrefab;
        panel = MenuPanels.CreatePanel("SummaryPanel", detailsPanel);
        Transform header = MenuPanels.CreateHeader(panel, 1);
        titleLabel = MenuPanels.AddHeaderButton(header, buttonPrefab, null, out Button unused, 1);
        titleLabel.text = "Summary";
        content = MenuPanels.CreateScrollList(panel, listPanel, MenuPanels.HeaderHeight(1));
        panel.SetActive(false);
    }

    public void Show() { Refresh(true); }

    // The lines of the page (also what the tests read)
    public static List<string> Describe()
    {
        return Build().Select(l => l.text).ToList();
    }

    private static List<Line> Build()
    {
        List<Line> lines = new List<Line>();

        // Urgent: the latest warnings and alerts (newest first)
        lines.Add(new Line { text = "Needs attention" });
        List<Notification> urgent = Notify.Center.History.Where(n => n.Kind == NotifyKind.Warning || n.Kind == NotifyKind.Alert).Reverse().Take(4).ToList();
        QuestManager quests = QuestManager.Instance;
        int waiting = quests != null ? quests.AttentionCount : 0;
        if (waiting > 0) lines.Add(new Line { text = $"{waiting} quest{(waiting == 1 ? "" : "s")} waiting", click = () => UIManager.Instance?.OpenBookTab(UIManager.BookTab.Quests) });
        foreach (Notification note in urgent)
        {
            Notification captured = note;
            lines.Add(new Line { text = captured.Display, click = captured.HasTarget ? (Action)(() => captured.OnTap()) : null });
        }
        if (urgent.Count == 0 && waiting == 0) lines.Add(new Line { text = "Nothing urgent." });

        // Resources
        lines.Add(new Line { text = "Resources" });
        ResourceManager rm = ResourceManager.Instance;
        if (rm != null)
        {
            foreach (Resource resource in rm.resources.Values)
            {
                bool core = resource.resourceName == ResourceManager.Gold || resource.resourceName == ResourceManager.Wood;
                if (!core && resource.currentAmount < 1f) continue;
                float capacity = rm.Capacity(resource.resourceName);
                lines.Add(new Line { text = capacity > 0f ? $"{resource.resourceName}: {Mathf.FloorToInt(resource.currentAmount)}/{Mathf.FloorToInt(capacity)}" : $"{resource.resourceName}: {Mathf.FloorToInt(resource.currentAmount)}" });
            }
        }

        // The town
        lines.Add(new Line { text = "Town" });
        CharacterManager characters = CharacterManager.Instance;
        if (characters != null)
        {
            List<OwnedCharacter> roster = characters.Roster.ToList();
            if (roster.Count == 0) lines.Add(new Line { text = "Characters: none yet" });
            else
            {
                int working = roster.Count(c => c.WorkBuilding != null);
                int hungry = roster.Count(c => c.IsHungry);
                int unpaid = roster.Count(c => !c.WagePaid);
                float average = roster.Average(c => c.Mood);
                OwnedCharacter lowest = roster.OrderBy(c => c.Mood).First();
                lines.Add(new Line { text = $"Characters: {roster.Count} ({working} with a job)" });
                lines.Add(new Line { text = $"Happiness: {average:0}/100 on average" + (roster.Count > 1 ? $" (lowest: {lowest.DisplayName} {lowest.Mood:0})" : "") });
                if (hungry > 0) lines.Add(new Line { text = $"Hungry: {hungry}" });
                if (unpaid > 0) lines.Add(new Line { text = $"Unpaid: {unpaid}" });
            }
        }
        if (BuildingManager.Instance != null) lines.Add(new Line { text = $"Buildings: {BuildingManager.Instance.Buildings.Count}" });
        MapRegions regions = MapRegions.Existing;
        if (regions != null && regions.HasRegions) lines.Add(new Line { text = $"Land: {regions.Map.ClearedCount} of {regions.Map.Regions.Count} regions cleared" });
        return lines;
    }

    // Only rebuilds the list when what it shows changed
    public void Refresh(bool force = false)
    {
        List<Line> lines = Build();
        StringBuilder signature = new StringBuilder();
        foreach (Line line in lines) signature.Append(line.text).Append(line.click != null ? "*" : "").Append('|');
        if (!force && signature.ToString() == shownSignature) return;
        shownSignature = signature.ToString();

        MenuPanels.ClearChildren(content);
        string[] headings = { "Needs attention", "Resources", "Town" };
        foreach (Line line in lines)
        {
            bool clickable = line.click != null;
            bool heading = Array.IndexOf(headings, line.text) >= 0;
            GameObject row = MenuPanels.MakeRow(buttonPrefab, content, heading ? "SummaryHeading" : "SummaryRow", line.text, null, clickable);
            if (clickable) { Action click = line.click; row.GetComponent<Button>().onClick.AddListener(() => { click(); Refresh(true); }); }
        }
    }
}
