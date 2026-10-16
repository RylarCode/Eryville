using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps the game going between runs when it uses the real-time clock: loads the saved game when the game scene
// starts, saves now and then and when the app is paused or closed, and gives the resources earned while away when
// the app comes back from the background. Starts by itself with the game scene (nothing to add to the scene).
// With the game's own clock (testing) it does nothing: saving and loading stay manual.
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    public const string AutoPreference = "AutoSession";

    public float autosaveSeconds = 60f;

    private float sinceSave;
    private bool ready;                 // the first load is done: only then may it save over the file
    private System.DateTime pausedAt;
    private bool hasPausedAt;
    private Coroutine starting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("GameSession");
        go.AddComponent<GameSession>();
        DontDestroyOnLoad(go); // it outlives scenes: each time the game scene loads it loads the save again
    }

    // Whether the game loads and saves by itself (needs the real-time clock)
    public static bool Automatic
    {
        get { return PlayerPrefs.GetInt(AutoPreference, 1) == 1 && GameClock.Instance != null && GameClock.Instance.IsRealTime; }
        set { PlayerPrefs.SetInt(AutoPreference, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    private void Awake()
    {
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        BeginSession();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BeginSession();
    }

    // A scene has started: if it is the game scene, put the saved game into it (once everything has started)
    private void BeginSession()
    {
        ready = false;
        sinceSave = 0f;
        if (starting != null) StopCoroutine(starting);
        starting = StartCoroutine(LoadRoutine());
    }

    private IEnumerator LoadRoutine()
    {
        yield return null; // let every manager finish starting before the saved game is put into them
        if (GameManager.Instance != null)
        {
            if (Automatic && SaveSystem.HasSave()) SaveSystem.Load();
            ready = true; // from now on it may save
        }
        starting = null;
    }

    private void Update()
    {
        if (!ready || !Automatic) return;

        sinceSave += Time.unscaledDeltaTime;
        if (sinceSave >= autosaveSeconds) SaveNow();
    }

    public void SaveNow()
    {
        sinceSave = 0f;
        if (ready && Automatic) SaveSystem.Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!ready) return;

        if (paused)
        {
            pausedAt = Backend.Current.ServerUtcNow;
            hasPausedAt = true;
            SaveNow();
        }
        else if (hasPausedAt && Automatic)
        {
            hasPausedAt = false;
            OfflineProgress.Apply((float)(Backend.Current.ServerUtcNow - pausedAt).TotalSeconds);
        }
    }

    private void OnApplicationQuit()
    {
        SaveNow();
    }
}
