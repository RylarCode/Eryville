using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The notification history: everything that was posted lately, newest first, with the time. Rows that are about something
// (a character, a building) can be tapped to go there. Built at runtime like the Town menu; the messages live in Notify.Center.
public class NotificationMenu
{
    private readonly UIManager ui;
    private readonly GameObject buttonPrefab;
    private readonly GameObject panel;
    private readonly Transform content;
    private readonly TMP_Text titleLabel;
    private string shownSignature;

    public GameObject Panel => panel;

    public NotificationMenu(UIManager ui, GameObject buttonPrefab, GameObject detailsPanel, GameObject listPanel)
    {
        this.ui = ui;
        this.buttonPrefab = buttonPrefab;

        panel = MenuPanels.CreatePanel("NotificationPanel", detailsPanel);
        Transform header = MenuPanels.CreateHeader(panel, 1);
        titleLabel = MenuPanels.AddHeaderButton(header, buttonPrefab, null, out Button unused, 2);
        MenuPanels.AddHeaderButton(header, buttonPrefab, () => { Notify.Center.ClearHistory(); Refresh(true); }, out Button clear, 2).text = "Clear";
        content = MenuPanels.CreateScrollList(panel, listPanel, MenuPanels.HeaderHeight(1));
        panel.SetActive(false);
    }

    public void Show()
    {
        Refresh(true);
    }

    // Only rebuilds the list when something changed
    public void Refresh(bool force = false)
    {
        IReadOnlyList<Notification> history = Notify.Center.History;
        StringBuilder signature = new StringBuilder();
        foreach (Notification note in history) signature.Append(note.Id).Append(':').Append(note.Count).Append(note.HasTarget ? "t" : "").Append('|');
        if (!force && signature.ToString() == shownSignature) return;
        shownSignature = signature.ToString();

        titleLabel.text = $"Notifications ({history.Count})";
        MenuPanels.ClearChildren(content);
        if (history.Count == 0)
        {
            MenuPanels.MakeRow(buttonPrefab, content, "NoNotifications", "Nothing yet.", null, false);
            return;
        }

        for (int i = history.Count - 1; i >= 0; i--)
        {
            Notification note = history[i];
            string text = $"{note.At:HH:mm}  {note.Display}";
            bool clickable = note.HasTarget;
            GameObject row = MenuPanels.MakeRow(buttonPrefab, content, "Notification" + note.Id, text, note.Icon, clickable);
            if (!clickable) continue;
            Notification captured = note;
            row.GetComponent<Button>().onClick.AddListener(() =>
            {
                ui.CloseEverything(); // the menu closes, then it goes to what the message is about
                if (captured.OnTap != null) captured.OnTap();
            });
        }
    }
}
