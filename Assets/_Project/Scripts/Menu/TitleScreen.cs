using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The front page: shown first when the game starts. Click or tap anywhere to go to the game.
// Built entirely in code (like the in-game menus) and created by TitleBootstrap whenever the title scene is loaded,
// so the scene itself only needs a camera. Later this is the place for login, load game, settings: add them to Build(),
// and use SetStatus for messages such as "Signing in...".
//
// Art: drop a Sprite named "TitleBackground" and/or "TitleLogo" into any Resources folder and they are used instead of
// the built-in dusk gradient and the plain "Eryville" lettering.
public class TitleScreen : MonoBehaviour
{
    public const string TitleSceneName = "MainMenuScene";
    public const string GameSceneName = "GameScene";
    public const float IgnoreTapsSeconds = 0.4f;   // so a click that opened the game window does not skip the page

    public static TitleScreen Instance { get; private set; }

    // How a "press" is recognised (replaced in tests)
    public static Func<bool> PressDetector = AnyPress;

    private TMP_Text prompt;
    private TMP_Text status;
    private float shownFor;
    private bool starting;

    public bool Starting => starting;
    public string PromptText => prompt != null ? prompt.text : "";

    private void Awake()
    {
        Instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        shownFor += Time.unscaledDeltaTime;

        // The prompt breathes
        if (prompt != null)
        {
            float pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(shownFor * 3f));
            prompt.color = new Color(1f, 1f, 1f, pulse);
        }

        if (!starting && shownFor >= IgnoreTapsSeconds && PressDetector()) Begin();
    }

    // Leaves the front page for the game
    public void Begin()
    {
        if (starting) return;
        starting = true;
        if (prompt != null) prompt.text = "Loading...";
        SceneTransition.Go(GameSceneName);
    }

    // A line of text under the prompt (sign-in progress, errors...). Empty hides it.
    public void SetStatus(string message)
    {
        if (status != null) status.text = message ?? "";
    }

    private static bool AnyPress()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
        return Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
    }

    // --- Looks ---

    private void Build()
    {
        GameObject canvasObject = new GameObject("TitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Background: a picture if there is one, otherwise a dusk gradient
        Image background = AddImage(canvasObject.transform, "Background", Vector2.zero, Vector2.one);
        Sprite backgroundSprite = Resources.Load<Sprite>("TitleBackground");
        background.sprite = backgroundSprite != null ? backgroundSprite : MakeGradient();
        background.preserveAspect = false;
        background.raycastTarget = false;

        // Name of the game: a logo picture if there is one, otherwise lettering
        Sprite logoSprite = Resources.Load<Sprite>("TitleLogo");
        if (logoSprite != null)
        {
            Image logo = AddImage(canvasObject.transform, "Logo", new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.85f));
            logo.sprite = logoSprite;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
        }
        else
        {
            TMP_Text title = AddText(canvasObject.transform, "Title", "Eryville", new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.82f), 90f, 260f);
            title.color = Color.white;
        }

        prompt = AddText(canvasObject.transform, "Prompt", (Application.isMobilePlatform ? "Tap" : "Click") + " anywhere to start", new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.26f), 36f, 70f);
        status = AddText(canvasObject.transform, "Status", "", new Vector2(0.05f, 0.11f), new Vector2(0.95f, 0.17f), 24f, 44f);
        status.color = new Color(1f, 1f, 1f, 0.8f);
        TMP_Text version = AddText(canvasObject.transform, "Version", "v" + Application.version, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.06f), 20f, 32f);
        version.color = new Color(1f, 1f, 1f, 0.5f);
    }

    private static Image AddImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go.GetComponent<Image>();
    }

    private static TMP_Text AddText(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float minSize, float maxSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TMP_Text label = go.GetComponent<TMP_Text>();
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = minSize;
        label.fontSizeMax = maxSize;
        label.fontSize = maxSize;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    // Night sky at the top fading to a warm horizon at the bottom
    private static Sprite MakeGradient()
    {
        const int height = 256;
        Texture2D texture = new Texture2D(1, height);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color top = new Color(0.07f, 0.09f, 0.24f, 1f);
        Color middle = new Color(0.50f, 0.28f, 0.46f, 1f);
        Color bottom = new Color(1f, 0.64f, 0.36f, 1f);
        for (int y = 0; y < height; y++)
        {
            float t = 1f - y / (float)(height - 1); // y = 0 is the bottom row of a texture
            Color color = t < 0.55f ? Blend(top, middle, t / 0.55f) : Blend(middle, bottom, (t - 0.55f) / 0.45f);
            texture.SetPixel(0, y, color);
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Color Blend(Color a, Color b, float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);
    }
}

// Puts the title screen into the title scene: when the game starts, and every time the scene is loaded again (back to the title)
public static class TitleBootstrap
{
    private static bool listening;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnGameStart()
    {
        if (!listening)
        {
            listening = true;
            SceneManager.sceneLoaded += (scene, mode) => EnsureTitle(scene);
        }
        EnsureTitle(SceneManager.GetActiveScene());
    }

    public static void EnsureTitle(Scene scene)
    {
        if (scene.name != TitleScreen.TitleSceneName) return;
        if (UnityEngine.Object.FindFirstObjectByType<TitleScreen>() != null) return;
        new GameObject("TitleScreen").AddComponent<TitleScreen>();
    }
}
