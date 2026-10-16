using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Profiling;
using UnityEngine.UI;

// A small frame-time counter in the corner of the screen, and (tap it, or press F3) a panel with the
// numbers that show whether the game runs smoothly, plus the stress-test buttons. It is created by itself
// when the game starts, in the editor and in Development Builds only (never in a release build), so it needs
// nothing in the scene.
//
// Real numbers come from a real phone: build with "Development Build" ticked, run there, and read it.
public class PerfOverlay : MonoBehaviour
{
    public static PerfOverlay Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!(Application.isEditor || Debug.isDebugBuild)) return;
        if (Instance != null) return;

        GameObject go = new GameObject("PerfOverlay");
        go.AddComponent<PerfOverlay>();
        DontDestroyOnLoad(go);
    }

    private const float RefreshSeconds = 0.5f;
    private static readonly int[] BenchmarkLevels = { 0, 100, 250, 500, 1000 };
    private static readonly int[] FrameRateCaps = { 30, 60, 120, -1 };

    public bool Expanded { get; private set; }
    public FrameStatsWindow Frames { get; } = new FrameStatsWindow(300); // about the last five seconds

    private StressTest stress;
    private string message = "";
    private string benchmarkReport = "";
    private float refreshTimer;
    private int gcAtLastRefresh;
    private float gcPerSecond;
    private int gcAtMeasureStart;
    private StressBenchmark.Phase lastPhase = StressBenchmark.Phase.Done;
    private FrameStatsWindow.Summary summary;
    private readonly StringBuilder builder = new StringBuilder(512);

    // UI
    private Canvas canvas;
    private CanvasScaler scaler;
    private RectTransform panel;
    private TMP_Text statsText;
    private TMP_Text resultsText;
    private GameObject buttonsRoot;
    private TMP_Text churnLabel;
    private TMP_Text capLabel;
    private Vector2 lastScreen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        stress = gameObject.AddComponent<StressTest>();
        gcAtLastRefresh = GC.CollectionCount(0);
        BuildUi();
        SetExpanded(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        Frames.Add(Time.unscaledDeltaTime);

        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) SetExpanded(!Expanded);
        if (Screen.width != lastScreen.x || Screen.height != lastScreen.y) FitCanvas();
        TrackBenchmark();

        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer >= RefreshSeconds)
        {
            int collections = GC.CollectionCount(0);
            gcPerSecond = (collections - gcAtLastRefresh) / refreshTimer;
            gcAtLastRefresh = collections;
            refreshTimer = 0f;
            Refresh();
        }
    }

    // --- Text ---

    public void Refresh()
    {
        summary = Frames.Summarize();
        builder.Length = 0;

        if (!Expanded)
        {
            builder.Append(summary.fps.ToString("0")).Append(" fps   ").Append(summary.averageMs.ToString("0.0")).Append(" ms");
            statsText.SetText(builder);
            return;
        }

        builder.Append(summary.fps.ToString("0")).Append(" fps   avg ").Append(summary.averageMs.ToString("0.0")).Append(" ms   (cap ")
            .Append(Application.targetFrameRate).Append(", vsync ").Append(QualitySettings.vSyncCount).Append(")\n");
        builder.Append("p95 ").Append(summary.p95Ms.ToString("0.0")).Append("   p99 ").Append(summary.p99Ms.ToString("0.0"))
            .Append("   worst ").Append(summary.worstMs.ToString("0.0")).Append(" ms\n");
        builder.Append("slow frames (>33 ms): ").Append(summary.slowFrames).Append(" of ").Append(summary.frames).Append('\n');
        builder.Append("GC collections/s: ").Append(gcPerSecond.ToString("0.0")).Append("   memory ")
            .Append((Profiler.GetTotalAllocatedMemoryLong() / 1048576f).ToString("0")).Append(" MB (managed ")
            .Append((GC.GetTotalMemory(false) / 1048576f).ToString("0")).Append(")\n");
        builder.Append("characters ").Append(CharacterManager.Instance != null ? CharacterManager.Instance.Roster.Count : 0)
            .Append("   buildings ").Append(BuildingManager.Instance != null ? BuildingManager.Instance.Buildings.Count : 0)
            .Append("   ").Append(Screen.width).Append('x').Append(Screen.height).Append('\n');
        builder.Append(message);
        statsText.SetText(builder);

        resultsText.text = benchmarkReport;
        if (churnLabel != null) churnLabel.text = stress.Churn ? "Churn: on" : "Churn: off";
        if (capLabel != null) capLabel.text = "Cap: " + (Application.targetFrameRate <= 0 ? "default" : Application.targetFrameRate.ToString());
    }

    public void Toggle()
    {
        SetExpanded(!Expanded);
    }

    private void SetExpanded(bool expanded)
    {
        Expanded = expanded;
        buttonsRoot.SetActive(expanded);
        resultsText.gameObject.SetActive(expanded);
        panel.sizeDelta = expanded ? new Vector2(760f, 760f) : new Vector2(330f, 76f);
        statsText.alignment = TextAlignmentOptions.TopLeft;
        RectTransform statsRect = statsText.GetComponent<RectTransform>();
        statsRect.offsetMax = new Vector2(-10f, -6f);
        statsRect.offsetMin = expanded ? new Vector2(10f, panel.sizeDelta.y - 236f) : new Vector2(10f, 4f);
        statsText.enableAutoSizing = true;
        statsText.fontSizeMin = 14f;
        statsText.fontSizeMax = expanded ? 28f : 38f;
        Refresh();
    }

    // --- Stress test and benchmark ---

    private void AddCharacters(int count)
    {
        if (!StressTest.CanRun) { message = "Open the game scene first."; return; }
        int added = stress.AddCharacters(count);
        message = added == 0 ? "Could not add characters (map full?)." : $"Added {added} characters (stress test: saving is off until Clear).";
    }

    private void AddBuildings(int count)
    {
        if (!StressTest.CanRun) { message = "Open the game scene first."; return; }
        message = $"Built {stress.AddBuildings(count)} buildings.";
    }

    private void Clear()
    {
        stress.Clear();
        benchmarkReport = "";
        message = "Stress test cleared.";
    }

    private void ToggleChurn()
    {
        stress.Churn = !stress.Churn;
    }

    private void CycleFrameRateCap()
    {
        int current = Array.IndexOf(FrameRateCaps, Application.targetFrameRate);
        Application.targetFrameRate = FrameRateCaps[(current + 1) % FrameRateCaps.Length];
    }

    private void StartBenchmark()
    {
        if (!StressTest.CanRun) { message = "Open the game scene first."; return; }
        stress.Clear();
        benchmarkReport = "";
        stress.BenchmarkResult = OnBenchmarkResult;
        stress.BenchmarkFinished = OnBenchmarkFinished;
        stress.StartBenchmark(BenchmarkLevels);
        lastPhase = StressBenchmark.Phase.Done;
        message = "Benchmark running... leave the phone alone (about a minute).";
    }

    private void TrackBenchmark()
    {
        StressBenchmark benchmark = stress.Benchmark;
        if (benchmark == null) return;

        if (benchmark.CurrentPhase == StressBenchmark.Phase.Measuring && lastPhase != StressBenchmark.Phase.Measuring) gcAtMeasureStart = GC.CollectionCount(0);
        lastPhase = benchmark.CurrentPhase;
        if (Expanded && benchmark.CurrentPhase != StressBenchmark.Phase.Done)
        {
            message = $"Benchmark {benchmark.LevelNumber}/{benchmark.LevelCount}: {benchmark.TargetCharacters} characters, {benchmark.CurrentPhase}...";
        }
    }

    private void OnBenchmarkResult(BenchResult result)
    {
        result.gcCollections = GC.CollectionCount(0) - gcAtMeasureStart;
        result.managedMemoryMb = GC.GetTotalMemory(false) / 1048576f;
        result.totalMemoryMb = Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
        benchmarkReport = stress.Benchmark.Report(); // already includes this level
    }

    private void OnBenchmarkFinished()
    {
        benchmarkReport = stress.Benchmark.Report();
        string path = Path.Combine(Application.persistentDataPath, "stress_results.txt");
        string text = DeviceSummary() + "\n" + benchmarkReport;
        try { File.WriteAllText(path, text); message = "Benchmark done. Saved to " + path; }
        catch (Exception e) { message = "Benchmark done (could not save the file: " + e.Message + ")"; }
        Debug.Log("Stress benchmark results:\n" + text);
    }

    public static string DeviceSummary()
    {
        return $"{SystemInfo.deviceModel} | {SystemInfo.processorType} x{SystemInfo.processorCount} | {SystemInfo.systemMemorySize} MB RAM | {SystemInfo.graphicsDeviceName} | " +
               $"{Screen.width}x{Screen.height} | cap {Application.targetFrameRate} vsync {QualitySettings.vSyncCount} | Unity {Application.unityVersion} | " +
               (Application.isEditor ? "EDITOR (numbers are not representative)" : "device build");
    }

    // --- Building the UI ---

    private void FitCanvas()
    {
        lastScreen = new Vector2(Screen.width, Screen.height);
        bool wide = Screen.width > Screen.height;
        scaler.matchWidthOrHeight = wide ? 1f : 0f; // 1 unit is about a pixel on a 1080p landscape or 1080-wide portrait screen
        float inset = Screen.safeArea.width > 0f ? (Screen.height - Screen.safeArea.yMax) / Mathf.Max(0.01f, canvas.scaleFactor) : 0f;
        panel.anchoredPosition = new Vector2(24f, -(inset + 230f));
    }

    private void BuildUi()
    {
        GameObject canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000; // above everything
        scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        if (EventSystem.current == null)
        {
            GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
        }

        GameObject panelGO = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Button));
        panelGO.transform.SetParent(canvasGO.transform, false);
        panel = panelGO.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        Image background = panelGO.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.7f);
        Button toggle = panelGO.GetComponent<Button>();
        toggle.targetGraphic = background;
        toggle.onClick.AddListener(() => SetExpanded(!Expanded));

        statsText = NewText(panel, "Stats", Color.white, TextAlignmentOptions.TopLeft);
        RectTransform statsRect = statsText.GetComponent<RectTransform>();
        statsRect.anchorMin = Vector2.zero;
        statsRect.anchorMax = Vector2.one;

        // The results of the benchmark, under the stats
        resultsText = NewText(panel, "Results", new Color(0.8f, 1f, 0.8f, 1f), TextAlignmentOptions.TopLeft);
        RectTransform resultsRect = resultsText.GetComponent<RectTransform>();
        resultsRect.anchorMin = Vector2.zero;
        resultsRect.anchorMax = Vector2.one;
        resultsRect.offsetMin = new Vector2(10f, 250f);
        resultsRect.offsetMax = new Vector2(-10f, -250f);
        resultsText.fontSize = 17f;

        // The buttons, along the bottom
        buttonsRoot = new GameObject("Buttons", typeof(RectTransform), typeof(GridLayoutGroup));
        buttonsRoot.transform.SetParent(panel, false);
        RectTransform buttonsRect = buttonsRoot.GetComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0f, 0f);
        buttonsRect.anchorMax = new Vector2(1f, 0f);
        buttonsRect.pivot = new Vector2(0.5f, 0f);
        buttonsRect.anchoredPosition = new Vector2(0f, 10f);
        buttonsRect.sizeDelta = new Vector2(-20f, 230f);
        GridLayoutGroup grid = buttonsRoot.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(232f, 68f);
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        AddButton("+100 chars", () => AddCharacters(100));
        AddButton("+500 chars", () => AddCharacters(500));
        AddButton("+10 bldgs", () => AddBuildings(10));
        churnLabel = AddButton("Churn: off", ToggleChurn);
        AddButton("Benchmark", StartBenchmark);
        AddButton("Clear", Clear);
        capLabel = AddButton("Cap", CycleFrameRateCap);

        FitCanvas();
    }

    private TMP_Text AddButton(string label, Action onClick)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(buttonsRoot.transform, false);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick());

        TMP_Text text = NewText(go.GetComponent<RectTransform>(), "Label", new Color(0.05f, 0.05f, 0.08f, 1f), TextAlignmentOptions.Center);
        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 2f);
        rect.offsetMax = new Vector2(-4f, -2f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 28f;
        text.text = label;
        return text;
    }

    private static TMP_Text NewText(Transform parent, string name, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }
}
