using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Quests menu: the main quest first, then the side quests, then the completed ones. Each quest shows its objectives with a tick when
// done and its reward. A ready quest has a Claim row; an offered one an Accept row. Built at runtime like the Story menu; the quests
// themselves live in QuestManager.
public class QuestMenu
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

    public QuestMenu(UIManager ui, GameObject buttonPrefab, GameObject detailsPanel, GameObject listPanel)
    {
        this.ui = ui;
        this.buttonPrefab = buttonPrefab;

        panel = MenuPanels.CreatePanel("QuestPanel", detailsPanel);
        Transform header = MenuPanels.CreateHeader(panel, 1);
        titleLabel = MenuPanels.AddHeaderButton(header, buttonPrefab, null, out Button unused, 1);
        content = MenuPanels.CreateScrollList(panel, listPanel, MenuPanels.HeaderHeight(1));
        panel.SetActive(false);
    }

    public void Show()
    {
        Refresh(true);
    }

    // Only rebuilds the list when what it shows changed
    public void Refresh(bool force = false)
    {
        QuestManager quests = QuestManager.Instance;
        List<Line> lines = new List<Line>();
        int attention = 0;
        if (quests != null)
        {
            attention = quests.AttentionCount;
            lines.Add(new Line { text = "Main quest" });
            Quest main = quests.CurrentMain();
            if (main != null) AddQuest(lines, quests, main);
            else lines.Add(new Line { text = "No main quest right now." });

            List<Quest> sides = quests.WithStatus(QuestCategory.Side, QuestStatus.Ready, QuestStatus.Available, QuestStatus.Active);
            if (sides.Count > 0)
            {
                lines.Add(new Line { text = "Side quests" });
                // ready ones first, then offered, then in progress
                foreach (QuestStatus status in new[] { QuestStatus.Ready, QuestStatus.Available, QuestStatus.Active })
                {
                    foreach (Quest side in sides) if (quests.StatusOf(side) == status) AddQuest(lines, quests, side);
                }
            }

            List<Quest> done = new List<Quest>();
            done.AddRange(quests.WithStatus(QuestCategory.Main, QuestStatus.Complete));
            done.AddRange(quests.WithStatus(QuestCategory.Side, QuestStatus.Complete));
            if (done.Count > 0)
            {
                lines.Add(new Line { text = $"Completed ({done.Count})" });
                foreach (Quest quest in done) lines.Add(new Line { text = quest.Title });
            }
        }
        else
        {
            lines.Add(new Line { text = "Quests are not running." });
        }

        StringBuilder signature = new StringBuilder(attention.ToString()).Append('#');
        foreach (Line line in lines) signature.Append(line.text).Append(line.click != null ? "*" : "").Append('|');
        if (!force && signature.ToString() == shownSignature) return;
        shownSignature = signature.ToString();

        titleLabel.text = attention > 0 ? $"Quests ({attention} waiting)" : "Quests";
        MenuPanels.ClearChildren(content);
        foreach (Line line in lines)
        {
            bool clickable = line.click != null;
            GameObject row = MenuPanels.MakeRow(buttonPrefab, content, "QuestRow", line.text, null, clickable);
            if (clickable) row.GetComponent<Button>().onClick.AddListener(() => { line.click(); Refresh(true); }); // (the list changes at once: a claimed quest moves away)
        }
    }

    // One quest: its title (a button when there is something to do), the description, the objectives and the reward
    private void AddQuest(List<Line> lines, QuestManager quests, Quest quest)
    {
        QuestStatus status = quests.StatusOf(quest);
        string reward = quest.RewardText();
        string rewardPart = reward.Length > 0 ? $" ({reward})" : "";
        if (status == QuestStatus.Ready)
        {
            if (quests.IsClaiming(quest)) lines.Add(new Line { text = $"Claiming: {quest.Title}..." });
            else lines.Add(new Line { text = $"Claim: {quest.Title}{rewardPart}", click = () => quests.Claim(quest) });
        }
        else if (status == QuestStatus.Available)
        {
            lines.Add(new Line { text = $"Accept: {quest.Title}", click = () => quests.Accept(quest) });
        }
        else
        {
            lines.Add(new Line { text = quest.Title });
        }

        if (!string.IsNullOrWhiteSpace(quest.description)) lines.Add(new Line { text = "   " + quest.description.Trim().Replace("\n", " ") });
        if (status == QuestStatus.Active || status == QuestStatus.Ready)
        {
            foreach (KeyValuePair<string, bool> objective in quests.ObjectiveLines(quest)) lines.Add(new Line { text = (objective.Value ? "   [x] " : "   [ ] ") + objective.Key });
        }
        if (reward.Length > 0 && status != QuestStatus.Ready) lines.Add(new Line { text = "   Reward: " + reward });
    }
}
