using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Story menu: what to do next (the goals of the next chapters, and who has something to say) and the chapters already seen,
// which can be played again. Built at runtime like the Town menu; the story itself lives in StoryManager.
public class StoryMenu
{
    private readonly UIManager ui;
    private readonly GameObject buttonPrefab;
    private readonly GameObject panel;
    private readonly Transform content;
    private string shownSignature;

    public GameObject Panel => panel;

    public StoryMenu(UIManager ui, GameObject buttonPrefab, GameObject detailsPanel, GameObject listPanel)
    {
        this.ui = ui;
        this.buttonPrefab = buttonPrefab;

        panel = MenuPanels.CreatePanel("StoryPanel", detailsPanel);
        Transform header = MenuPanels.CreateHeader(panel, 1);
        MenuPanels.AddHeaderButton(header, buttonPrefab, null, out Button unused, 1).text = "Story";
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
        StoryManager story = StoryManager.Instance;
        List<string> notes = new List<string>();
        List<StoryChapter> seen = new List<StoryChapter>();
        if (story != null)
        {
            foreach (StoryChapter step in story.NextSteps())
            {
                if (step.trigger == StoryTrigger.Talk && step.talkTo != null && story.IsAvailable(step)) notes.Add($"> {step.talkTo.displayName} has something to say: select them and tap Talk.");
                else if (!string.IsNullOrWhiteSpace(step.goal)) notes.Add("> " + step.goal.Trim());
            }
            seen = story.FinishedChapters();
        }

        StringBuilder signature = new StringBuilder();
        foreach (string note in notes) signature.Append(note).Append('|');
        foreach (StoryChapter chapter in seen) signature.Append(chapter.Id).Append('|');
        if (!force && signature.ToString() == shownSignature) return;
        shownSignature = signature.ToString();

        MenuPanels.ClearChildren(content);
        if (notes.Count > 0)
        {
            MenuPanels.MakeRow(buttonPrefab, content, "GoalsHeader", "What next", null, false);
            foreach (string note in notes) MenuPanels.MakeRow(buttonPrefab, content, "Goal", note, null, false);
        }

        MenuPanels.MakeRow(buttonPrefab, content, "SeenHeader", seen.Count > 0 ? "Seen (tap to read again)" : "Nothing seen yet.", null, false);
        foreach (StoryChapter chapter in seen)
        {
            string label = string.IsNullOrWhiteSpace(chapter.summary) ? chapter.Title : $"{chapter.Title}: {chapter.summary.Trim()}";
            GameObject row = MenuPanels.MakeRow(buttonPrefab, content, chapter.Id + "Row", label, null);
            StoryChapter captured = chapter;
            row.GetComponent<Button>().onClick.AddListener(() => ui.ReplayChapter(captured));
        }
    }
}
