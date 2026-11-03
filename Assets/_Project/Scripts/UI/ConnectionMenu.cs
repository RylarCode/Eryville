using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A character's Connection: its loyalty, the chapters and quests that belong to it (play, accept, claim), the next one still locked
// with what it needs (only that one, so nothing further is given away), what is done (chapters can be read again), and the bonuses it
// has earned. Opened from the Connection button on the character's details. Built at runtime like the other menus.
public class ConnectionMenu
{
    private class Line
    {
        public string text;
        public Action click; // null: a plain note
    }

    private readonly UIManager ui;
    private readonly GameObject buttonPrefab;
    private readonly GameObject panel;
    private readonly Transform content;
    private readonly TMP_Text titleLabel;
    private string shownSignature;

    public GameObject Panel => panel;
    public OwnedCharacter Character { get; private set; }

    public ConnectionMenu(UIManager ui, GameObject buttonPrefab, GameObject detailsPanel, GameObject listPanel)
    {
        this.ui = ui;
        this.buttonPrefab = buttonPrefab;

        panel = MenuPanels.CreatePanel("ConnectionPanel", detailsPanel);
        Transform header = MenuPanels.CreateHeader(panel, 1);
        titleLabel = MenuPanels.AddHeaderButton(header, buttonPrefab, null, out Button unused, 2);
        MenuPanels.AddHeaderButton(header, buttonPrefab, () => { if (Character != null) ui.ShowCharacterDetails(Character); }, out Button back, 2).text = "Back";
        content = MenuPanels.CreateScrollList(panel, listPanel, MenuPanels.HeaderHeight(1));
        panel.SetActive(false);
    }

    public void Show(OwnedCharacter character)
    {
        Character = character;
        Refresh(true);
    }

    // Only rebuilds the list when what it shows changed
    public void Refresh(bool force = false)
    {
        OwnedCharacter character = Character;
        if (character == null) return;
        CharacterType type = character.Type;
        List<Line> lines = new List<Line>();
        List<ConnectionItem> done = new List<ConnectionItem>();
        bool shownLocked = false;
        QuestManager quests = QuestManager.Instance;

        foreach (ConnectionItem item in Connection.ItemsFor(type))
        {
            ConnectionState state = Connection.StateOf(item);
            switch (state)
            {
                case ConnectionState.Available:
                    if (item.Chapter != null)
                    {
                        StoryChapter chapter = item.Chapter;
                        lines.Add(new Line { text = $"Talk: {item.Title}", click = () => ui.PlayConnectionChapter(chapter, false) });
                    }
                    else
                    {
                        Quest quest = item.Quest;
                        lines.Add(new Line { text = $"Accept: {item.Title}", click = () => quests.Accept(quest) });
                        AddReward(lines, quest);
                    }
                    break;
                case ConnectionState.Ready:
                {
                    Quest quest = item.Quest;
                    string reward = quest.RewardText();
                    if (quests.IsClaiming(quest)) lines.Add(new Line { text = $"Claiming: {item.Title}..." });
                    else lines.Add(new Line { text = $"Claim: {item.Title}" + (reward.Length > 0 ? $" ({reward})" : ""), click = () => quests.Claim(quest) });
                    break;
                }
                case ConnectionState.Active:
                    lines.Add(new Line { text = item.Title });
                    foreach (KeyValuePair<string, bool> objective in quests.ObjectiveLines(item.Quest)) lines.Add(new Line { text = (objective.Value ? "   [x] " : "   [ ] ") + objective.Key });
                    AddReward(lines, item.Quest);
                    break;
                case ConnectionState.Done:
                    done.Add(item);
                    break;
                default:
                    if (shownLocked) break; // only the next one
                    shownLocked = true;
                    lines.Add(new Line { text = $"Next: {item.Title}" });
                    foreach (string need in Connection.Missing(item)) lines.Add(new Line { text = "   Needs: " + need });
                    break;
            }
        }

        if (lines.Count == 0) lines.Add(new Line { text = done.Count > 0 ? "Nothing new to share for now." : "Nothing to share yet." });

        if (done.Count > 0)
        {
            lines.Add(new Line { text = $"Done ({done.Count})" });
            foreach (ConnectionItem item in done)
            {
                if (item.Chapter != null)
                {
                    StoryChapter chapter = item.Chapter;
                    lines.Add(new Line { text = $"Read again: {item.Title}", click = () => ui.PlayConnectionChapter(chapter, true) });
                }
                else lines.Add(new Line { text = item.Title });
            }
        }

        IReadOnlyList<StoryEffect> bonuses = ConnectionBuffs.For(type);
        if (bonuses.Count > 0)
        {
            lines.Add(new Line { text = "Bonuses" });
            foreach (StoryEffect bonus in bonuses) lines.Add(new Line { text = $"   {bonus.stat} x{bonus.multiplier:0.##}" });
        }

        string title = $"{character.DisplayName}: loyalty {Mathf.FloorToInt(character.Loyalty)}";
        StringBuilder signature = new StringBuilder(title).Append('#');
        foreach (Line line in lines) signature.Append(line.text).Append(line.click != null ? "*" : "").Append('|');
        if (!force && signature.ToString() == shownSignature) return;
        shownSignature = signature.ToString();

        titleLabel.text = title;
        MenuPanels.ClearChildren(content);
        foreach (Line line in lines)
        {
            bool clickable = line.click != null;
            GameObject row = MenuPanels.MakeRow(buttonPrefab, content, "ConnectionRow", line.text, null, clickable);
            if (clickable) row.GetComponent<Button>().onClick.AddListener(() => { line.click(); if (panel.activeInHierarchy) Refresh(true); });
        }
    }

    private static void AddReward(List<Line> lines, Quest quest)
    {
        string reward = quest.RewardText();
        if (reward.Length > 0) lines.Add(new Line { text = "   Reward: " + reward });
    }
}
