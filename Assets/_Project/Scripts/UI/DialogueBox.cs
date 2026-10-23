using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The story's dialogue screen: the speaker's picture on the left, a text box along the bottom (the words appear one after another;
// tapping finishes them, tapping again moves on) and, for a choice, the options as buttons above the box. A chapter that was seen before
// (a replay) also has a Skip button. The game stands still while it is open. What is said comes from a StoryPlayer.
public class DialogueBox : MonoBehaviour
{
    public const float CharactersPerSecond = 50f;

    private GameObject panel;
    private Image portrait;
    private TMP_Text nameLabel;
    private TMP_Text textLabel;
    private TMP_Text hintLabel;
    private Transform choiceList;
    private GameObject skipButton;
    private GameObject fontSource;

    private StoryPlayer player;
    private Func<string, Sprite> portraitOf;
    private Action<bool> finished;
    private float revealed;                 // characters of the line shown so far
    private GameManager.gameState before;   // the game's state to put back

    public bool IsOpen => root != null && root.activeSelf;
    public string ShownSpeaker => nameLabel != null ? nameLabel.text : "";
    public string ShownText => textLabel != null ? textLabel.text : "";
    public bool ShowingChoices => choiceList != null && choiceList.gameObject.activeSelf;
    public int ChoiceCount => choiceList != null ? choiceList.childCount : 0;
    public bool PortraitShown => portrait != null && portrait.gameObject.activeSelf;
    public bool SkipShown => skipButton != null && skipButton.activeSelf;
    public bool LineComplete => textLabel != null && revealed >= textLabel.text.Length;

    private GameObject root;

    public static DialogueBox Create(Transform canvas, GameObject fontSource)
    {
        GameObject go = new GameObject("DialogueBox", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvas, false);
        DialogueBox box = go.AddComponent<DialogueBox>();
        box.Build(fontSource);
        return box;
    }

    private void Build(GameObject fontSource)
    {
        this.fontSource = fontSource;
        root = gameObject;
        float scale = MenuPanels.Scale;

        // A clear screen over everything: it keeps clicks from reaching the town, and tapping it moves the dialogue on
        RectTransform rootRect = GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        Image dim = GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.25f);
        Button tap = GetComponent<Button>();
        tap.targetGraphic = dim;
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(OnTap);

        GameObject pictureGO = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        pictureGO.transform.SetParent(transform, false);
        RectTransform pictureRect = pictureGO.GetComponent<RectTransform>();
        pictureRect.anchorMin = new Vector2(0.02f, 0.27f);
        pictureRect.anchorMax = new Vector2(0.42f, 0.9f);
        pictureRect.offsetMin = Vector2.zero;
        pictureRect.offsetMax = Vector2.zero;
        portrait = pictureGO.GetComponent<Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        panel = new GameObject("TextBox", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.02f, 0.02f);
        panelRect.anchorMax = new Vector2(0.98f, 0.27f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.06f, 0.07f, 0.1f, 0.92f);
        panelImage.raycastTarget = false;

        nameLabel = Place(MenuPanels.MakeLabel(fontSource, panel.transform, "Name", 12f * scale, 26f * scale, TextAlignmentOptions.Left), 0.03f, 0.74f, 0.7f, 0.97f);
        nameLabel.color = new Color(1f, 0.82f, 0.3f, 1f);
        textLabel = Place(MenuPanels.MakeLabel(fontSource, panel.transform, "Text", 12f * scale, 26f * scale, TextAlignmentOptions.TopLeft), 0.03f, 0.14f, 0.97f, 0.74f);
        textLabel.color = Color.white;
        hintLabel = Place(MenuPanels.MakeLabel(fontSource, panel.transform, "Hint", 8f * scale, 16f * scale, TextAlignmentOptions.Right), 0.6f, 0.02f, 0.97f, 0.16f);
        hintLabel.color = new Color(0.7f, 0.7f, 0.7f, 1f);

        GameObject choices = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
        choices.transform.SetParent(transform, false);
        RectTransform choiceRect = choices.GetComponent<RectTransform>();
        choiceRect.anchorMin = new Vector2(0.1f, 0.3f);
        choiceRect.anchorMax = new Vector2(0.98f, 0.8f);
        choiceRect.offsetMin = Vector2.zero;
        choiceRect.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = choices.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f * scale;
        layout.childAlignment = TextAnchor.LowerRight;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        choiceList = choices.transform;

        skipButton = MenuPanels.MakeBarButton(fontSource, transform, "SkipButton", "Skip", () => Close(false));
        RectTransform skipRect = skipButton.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(0.78f, 0.92f);
        skipRect.anchorMax = new Vector2(0.98f, 0.99f);
        skipRect.offsetMin = Vector2.zero;
        skipRect.offsetMax = Vector2.zero;

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

    // Starts showing a chapter; done(true) when it plays to the end, done(false) when it is skipped or cancelled
    public void Open(StoryPlayer story, Func<string, Sprite> portraitFor, bool replay, Action<bool> done)
    {
        if (story == null) return;
        player = story;
        portraitOf = portraitFor;
        finished = done;
        before = GameManager.Instance != null ? GameManager.Instance.currentGameState : GameManager.gameState.Normal;
        if (GameManager.Instance != null) GameManager.Instance.currentGameState = GameManager.gameState.Menu;
        skipButton.SetActive(replay);
        transform.SetAsLastSibling();
        root.SetActive(true);
        Show();
    }

    // Closes at once. completed: whether the chapter reached its end.
    public void Close(bool completed)
    {
        if (!IsOpen && player == null) return;
        Action<bool> done = finished;
        player = null;
        finished = null;
        root.SetActive(false);
        if (GameManager.Instance != null && GameManager.Instance.currentGameState == GameManager.gameState.Menu) GameManager.Instance.currentGameState = before;
        done?.Invoke(completed);
    }

    // Tap: show the whole line, or move on
    public void OnTap()
    {
        if (player == null || player.IsChoice) return;
        if (!LineComplete) { revealed = textLabel.text.Length; ApplyReveal(); return; }
        player.Advance();
        Show();
    }

    public void Choose(int index)
    {
        if (player == null || !player.IsChoice) return;
        player.Choose(index);
        Show();
    }

    private void Show()
    {
        if (player == null) return;
        if (player.Finished) { Close(true); return; }

        for (int i = choiceList.childCount - 1; i >= 0; i--)
        {
            Transform old = choiceList.GetChild(i);
            old.gameObject.SetActive(false);
            Destroy(old.gameObject);
        }

        bool choice = player.IsChoice;
        choiceList.gameObject.SetActive(choice);
        hintLabel.text = choice ? "" : "tap to continue";
        if (choice)
        {
            nameLabel.text = "";
            textLabel.text = "";
            portrait.gameObject.SetActive(false);
            for (int i = 0; i < player.Choices.Count; i++)
            {
                int captured = i;
                GameObject button = MenuPanels.MakeBarButton(fontSource, choiceList, "Choice" + i, player.Choices[i].Text, () => Choose(captured));
                LayoutElement element = button.GetComponent<LayoutElement>();
                element.minHeight = 58f * MenuPanels.Scale;
                element.preferredHeight = 58f * MenuPanels.Scale;
            }
            return;
        }

        nameLabel.text = player.Speaker;
        textLabel.text = player.Text;
        Sprite picture = portraitOf != null ? portraitOf(player.Speaker) : null;
        portrait.sprite = picture;
        portrait.gameObject.SetActive(picture != null);
        revealed = 0f;
        ApplyReveal();
    }

    private void Update()
    {
        if (!IsOpen || player == null || player.IsChoice || LineComplete) return;
        revealed += CharactersPerSecond * Time.unscaledDeltaTime;
        ApplyReveal();
    }

    private void ApplyReveal()
    {
        if (textLabel != null) textLabel.maxVisibleCharacters = Mathf.Max(0, Mathf.FloorToInt(revealed));
    }
}
