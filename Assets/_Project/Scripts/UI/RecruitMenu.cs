using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Recruit menu (the tavern's gacha): shows your recruitment letters and daily wages, lets you buy a
// letter with gold and pull once or ten times, and lists what the latest pulls gave. Clicking a result
// opens that character's details. Built at runtime from the existing UI, like the Town menu; the rules
// live in GachaSystem.
public class RecruitMenu
{
    private readonly UIManager ui;
    private readonly GameObject buttonPrefab;
    private readonly GameObject panel;
    private readonly Transform content;

    private readonly TMP_Text lettersLabel;
    private readonly TMP_Text wagesLabel;
    private readonly TMP_Text buyLabel;
    private readonly Button buyButton;
    private readonly TMP_Text pullOneLabel;
    private readonly Button pullOneButton;
    private readonly TMP_Text pullTenLabel;
    private readonly Button pullTenButton;
    private readonly TMP_Text pityLabel;

    private List<PullResult> lastResults = new List<PullResult>();
    private string shownSignature;

    public GameObject Panel => panel;

    public RecruitMenu(UIManager ui, GameObject buttonPrefab, GameObject detailsPanel, GameObject listPanel)
    {
        this.ui = ui;
        this.buttonPrefab = buttonPrefab;

        panel = MenuPanels.CreatePanel("RecruitPanel", detailsPanel);

        Transform header = MenuPanels.CreateHeader(panel, 3);
        lettersLabel = Add(header, null, out Button unusedA);
        wagesLabel = Add(header, null, out Button unusedB);
        buyLabel = Add(header, BuyLetter, out buyButton);
        pullOneLabel = Add(header, () => Pull(1), out pullOneButton);
        pullTenLabel = Add(header, () => Pull(GachaSystem.TenPull), out pullTenButton);
        pityLabel = Add(header, null, out Button unusedC);

        content = MenuPanels.CreateScrollList(panel, listPanel, MenuPanels.HeaderHeight(3));
        panel.SetActive(false);
    }

    private TMP_Text Add(Transform header, System.Action onClick, out Button button)
    {
        return MenuPanels.AddHeaderButton(header, buttonPrefab, onClick, out button);
    }

    public void Show()
    {
        Refresh(true);
    }

    // Brings the labels, button states and list up to date (the list only when it changed)
    public void Refresh(bool force = false)
    {
        GachaSystem gacha = GachaSystem.Instance;
        if (gacha == null) return;

        lettersLabel.text = $"Letters: {gacha.Letters}";
        wagesLabel.text = WageSystem.Instance != null ? $"Wages: {WageSystem.Instance.TotalDailyWages()}/day" : "Wages: -";
        buyLabel.text = $"Buy letter ({gacha.LetterGoldCost} gold)";
        MenuPanels.SetButtonEnabled(buyButton, buyLabel, ResourceManager.Instance.GetResourceAmount(ResourceManager.Gold) >= gacha.LetterGoldCost);
        pullOneLabel.text = $"Pull x1 ({gacha.CostOf(1)} letter)";
        MenuPanels.SetButtonEnabled(pullOneButton, pullOneLabel, gacha.CanPull(1, out string reasonOne));
        pullTenLabel.text = $"Pull x10 ({gacha.CostOf(GachaSystem.TenPull)} letters)";
        MenuPanels.SetButtonEnabled(pullTenButton, pullTenLabel, gacha.CanPull(GachaSystem.TenPull, out string reasonTen));
        pityLabel.text = DescribePity(gacha);

        string message = !string.IsNullOrEmpty(gacha.LastMessage) ? gacha.LastMessage : (reasonOne.Length > 0 ? reasonOne : "Pull to recruit.");
        StringBuilder signature = new StringBuilder(message);
        foreach (PullResult result in lastResults) signature.Append('|').Append(Describe(result));
        if (!force && signature.ToString() == shownSignature) return;
        shownSignature = signature.ToString();

        MenuPanels.ClearChildren(content);
        AddRow("Message", message, null, null);
        foreach (PullResult result in lastResults)
        {
            PullResult captured = result;
            AddRow("Result", Describe(result), result.Type != null ? result.Type.sprite : null,
                result.Character != null ? (System.Action)(() => ui.ShowCharacterDetails(captured.Character)) : null);
        }
    }

    private void AddRow(string name, string text, Sprite sprite, System.Action onClick)
    {
        GameObject go = MenuPanels.MakeRow(buttonPrefab, content, name, text, sprite, onClick != null);
        if (onClick != null) go.GetComponent<Button>().onClick.AddListener(() => onClick());
    }

    // --- Actions ---

    private void BuyLetter()
    {
        GachaSystem.Instance.BuyLetter();
        Refresh(true);
    }

    private void Pull(int count)
    {
        GachaSystem gacha = GachaSystem.Instance;
        List<PullResult> results = gacha.Pull(count);
        if (results.Count > 0) lastResults = results;
        Refresh(true);
    }

    // --- Text ---

    public static string Describe(PullResult result)
    {
        string name = result.Character != null ? result.Character.DisplayName : (result.Type != null ? result.Type.displayName : "?");
        string text = $"{name} [{result.Rarity}]";
        if (result.IsNew) return text + " - NEW";
        if (result.ConvertedToLoyalty) return text + $" - duplicate: loyalty +{result.LoyaltyGained:0.#}";
        int max = result.Character != null ? result.Character.MaxAwareness : 0;
        return text + $" - duplicate: self-awareness {result.AwarenessLevel}/{max}";
    }

    private static string DescribePity(GachaSystem gacha)
    {
        List<PityRule> rules = gacha.Pool.pity;
        if (rules == null || rules.Count == 0) return "No pity";

        StringBuilder text = new StringBuilder();
        for (int i = 0; i < rules.Count && i < gacha.PityCounters.Count; i++)
        {
            if (rules[i].every <= 0) continue;
            int left = rules[i].every - gacha.PityCounters[i];
            if (text.Length > 0) text.Append(", ");
            text.Append($"{rules[i].minRarity}+ in {Mathf.Max(1, left)}");
        }
        return text.Length > 0 ? "Sure: " + text : "No pity";
    }
}
