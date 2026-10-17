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
    public const float OnlineAutosaveSeconds = 180f;

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
            IGameBackend backend = Backend.Current;
            if (backend.ServerOwnsBalances)
            {
                IEnumerator connecting = ConnectToServer(backend);
                while (connecting.MoveNext()) yield return connecting.Current;
            }

            if (Automatic)
            {
                // Ask whether there is a saved game, and wait for the answer (an online backend answers later)
                bool asked = false, hasSave = false;
                backend.HasBlob(SaveSystem.DefaultFileName, has => { asked = true; hasSave = has; });
                foreach (object frame in WaitUntil(() => asked, 30f)) yield return null;

                bool loaded = !(asked && hasSave);
                if (!loaded)
                {
                    SaveSystem.Load(SaveSystem.DefaultFileName, ok => loaded = true);
                    foreach (object frame in WaitUntil(() => loaded, 30f)) yield return null;
                }
            }
            ready = true; // from now on it may save (and not before: saving an empty game over the real one would lose it)
        }
        starting = null;
    }

    // Signs in, syncs the clock and takes the player's letters / pity from the server
    private IEnumerator ConnectToServer(IGameBackend backend)
    {
        bool connected = false;
        BackendResult connection = null;
        backend.Connect(result => { connection = result; connected = true; });
        foreach (object frame in WaitUntil(() => connected, 30f)) yield return null;
        if (connection == null || !connection.ok)
        {
            Debug.LogWarning("Could not connect to the server: " + (connection != null ? connection.error : "no answer") + ". Playing with the copy on this device.");
            yield break;
        }

        bool got = false;
        backend.GetPlayerState(state =>
        {
            if (GachaSystem.Instance != null) GachaSystem.Instance.ApplyServerState(state);
            got = true;
        });
        foreach (object frame in WaitUntil(() => got, 30f)) yield return null;
    }

    // Frames to wait (without freezing the game) until something is true, for at most this many seconds
    private static System.Collections.IEnumerable WaitUntil(System.Func<bool> condition, float seconds)
    {
        float waited = 0f;
        while (!condition() && waited < seconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void Update()
    {
        if (!ready || !Automatic) return;

        sinceSave += Time.unscaledDeltaTime;
        // An online save is a write to the server's database, so it happens less often than saving to a file
        float interval = Backend.Current.ServerOwnsBalances ? Mathf.Max(autosaveSeconds, OnlineAutosaveSeconds) : autosaveSeconds;
        if (sinceSave >= interval) SaveNow();
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
