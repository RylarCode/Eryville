using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The screen that shows who was recruited: the character's full picture, name, rarity, and whether it is new or a
// duplicate (with what the duplicate gave). One pull shows one character; a ten-pull shows them one after another
// (tap anywhere or press Next), with Skip to close at once.
public class RevealPopup
{
    private readonly GameObject root;
    private readonly Image portrait;
    private readonly TMP_Text title;
    private readonly TMP_Text detail;
    private readonly TMP_Text nextLabel;
    private List<PullResult> results = new List<PullResult>();
    private int index;

    public bool IsOpen => root.activeSelf;
    public int Index => index;
    public int Count => results.Count;
    public PullResult Current => index >= 0 && index < results.Count ? results[index] : null;

    public RevealPopup(Transform canvas, GameObject fontSource)
    {
        float scale = MenuPanels.Scale;

        // A dark screen over everything; tapping it moves on
        root = new GameObject("RevealPopup", typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(canvas, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        Image dim = root.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.88f);
        Button background = root.GetComponent<Button>();
        background.targetGraphic = dim;
        background.onClick.AddListener(Next);

        // The picture takes the upper part of the screen
        GameObject pictureGO = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        pictureGO.transform.SetParent(root.transform, false);
        RectTransform pictureRect = pictureGO.GetComponent<RectTransform>();
        pictureRect.anchorMin = new Vector2(0.08f, 0.3f);
        pictureRect.anchorMax = new Vector2(0.92f, 0.94f);
        pictureRect.offsetMin = Vector2.zero;
        pictureRect.offsetMax = Vector2.zero;
        portrait = pictureGO.GetComponent<Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        title = Place(MenuPanels.MakeLabel(fontSource, root.transform, "Title", 14f * scale, 34f * scale, TextAlignmentOptions.Center), 0.05f, 0.21f, 0.95f, 0.29f);
        detail = Place(MenuPanels.MakeLabel(fontSource, root.transform, "Detail", 12f * scale, 24f * scale, TextAlignmentOptions.Center), 0.05f, 0.12f, 0.95f, 0.21f);
        detail.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        GameObject next = MenuPanels.MakeBarButton(fontSource, root.transform, "NextButton", "Next", Next);
        PlaceButton(next, 0.05f, 0.02f, 0.65f, 0.11f);
        nextLabel = next.GetComponentInChildren<TMP_Text>();
        GameObject skip = MenuPanels.MakeBarButton(fontSource, root.transform, "SkipButton", "Skip", Close);
        PlaceButton(skip, 0.68f, 0.02f, 0.95f, 0.11f);

        root.SetActive(false);
    }

    private static TMP_Text Place(TMP_Text text, float xMin, float yMin, float xMax, float yMax)
    {
        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }

    private static void PlaceButton(GameObject button, float xMin, float yMin, float xMax, float yMax)
    {
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Starts showing the results from the first
    public void Show(List<PullResult> pulled)
    {
        if (pulled == null || pulled.Count == 0) return;
        results = new List<PullResult>(pulled);
        index = 0;
        root.transform.SetAsLastSibling(); // above every other screen
        root.SetActive(true);
        ShowCurrent();
    }

    public void Next()
    {
        if (!IsOpen) return;
        index++;
        if (index >= results.Count) Close();
        else ShowCurrent();
    }

    public void Close()
    {
        root.SetActive(false);
        results.Clear();
        index = 0;
    }

    public void Destroy()
    {
        Object.Destroy(root);
    }

    private void ShowCurrent()
    {
        PullResult result = Current;
        if (result == null) return;

        portrait.sprite = result.Type != null ? result.Type.PortraitOrSprite : null;
        portrait.enabled = portrait.sprite != null;
        title.text = Title(result);
        title.color = ColorOf(result.Rarity);
        detail.text = Detail(result);
        nextLabel.text = index + 1 < results.Count ? $"Next ({index + 1}/{results.Count})" : "Done";
    }

    // --- The words ---

    public static string NameOf(PullResult result)
    {
        return result.Character != null ? result.Character.DisplayName : (result.Type != null ? result.Type.displayName : "?");
    }

    public static string Title(PullResult result)
    {
        return $"{NameOf(result)}   [{result.Rarity}]";
    }

    // New recruit, or what the duplicate gave
    public static string Detail(PullResult result)
    {
        string text;
        if (result.IsNew) text = "New recruit!";
        else if (result.ConvertedToLoyalty) text = $"Duplicate: nothing more to unlock, +{result.LoyaltyGained:0.#} loyalty instead";
        else
        {
            int max = result.Character != null ? result.Character.MaxAwareness : 0;
            text = $"Duplicate: self-awareness {result.AwarenessLevel}/{max}";
            if (result.Character != null && result.AwarenessLevel >= 1 && result.AwarenessLevel <= max)
            {
                text += "\n" + result.Type.awarenessLevels[result.AwarenessLevel - 1].Describe();
            }
        }
        if (result.PityApplied) text += "\n(guaranteed by the pity counter)";
        return text;
    }

    public static Color ColorOf(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Rare: return new Color(0.45f, 0.72f, 1f, 1f);
            case Rarity.Epic: return new Color(0.78f, 0.5f, 1f, 1f);
            case Rarity.Legendary: return new Color(1f, 0.82f, 0.3f, 1f);
            default: return new Color(0.92f, 0.92f, 0.92f, 1f);
        }
    }
}
