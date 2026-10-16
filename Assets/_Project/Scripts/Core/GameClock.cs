using System;
using UnityEngine;

public enum ClockMode
{
    InGame,   // the game's own clock: a time of day lasts secondsPerPhase seconds of play (and pauses while a menu is open). For testing.
    RealTime, // the real time of day: morning, afternoon, evening and night follow the player's clock, and keep going when the game is closed
}

// The time of day. Two ways of keeping it, chosen with SetMode (and remembered between runs):
//
//   InGame:   advances on its own every secondsPerPhase seconds, paused while a menu is open, like resource production.
//   RealTime: follows the time (the backend's, so the phone's clock cannot be cheated) in the player's time zone.
//             It never pauses. In the editor and Development Builds the time can still be moved for testing: Advance()
//             skips to the next time of day, and running the game faster (Time.timeScale) makes the clock run faster too.
//
// TimeOfDayChanged is raised whenever the time of day changes, in either mode.
public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }

    public const string ModePreference = "ClockMode";

    public event Action<TimeOfDay> TimeOfDayChanged;

    [SerializeField] private float secondsPerPhase = 30f;
    [SerializeField] private bool autoAdvance = true;
    [SerializeField] private ClockMode defaultMode = ClockMode.RealTime; // used until the player (or tester) picks one

    public ClockMode Mode { get; private set; }
    public bool IsRealTime => Mode == ClockMode.RealTime;

    public TimeOfDay Current { get; private set; } = TimeOfDay.Morning;

    // 0..1 progress through the current phase
    public float PhaseProgress => IsRealTime ? (phaseLength > 0f ? Mathf.Clamp(elapsed / phaseLength, 0f, 1f) : 0f)
                                             : (secondsPerPhase > 0f ? elapsed / secondsPerPhase : 0f);

    // Seconds spent in the current phase (saved so a loaded game continues mid-phase)
    public float Elapsed => elapsed;

    private float elapsed;
    private float phaseLength;

    // --- Real time ---

    public RealTimeCalendar Calendar { get; } = new RealTimeCalendar();

    // The offset from UTC to the player's local time (replaced in tests)
    public Func<DateTime, TimeSpan> LocalOffset = utc => TimeZoneInfo.Local.GetUtcOffset(utc);

    private readonly DebugOffsetTimeSource timeSource = new DebugOffsetTimeSource(new BackendTimeSource());

    public static bool DebugAllowed => Application.isEditor || Debug.isDebugBuild;

    // The time the real-time clock goes by (the backend's time, moved by the testing offset when there is one)
    public DateTime UtcNow => DebugAllowed ? timeSource.UtcNow : Backend.Current.ServerUtcNow;
    public DateTime LocalNow { get { DateTime utc = UtcNow; return utc + LocalOffset(utc); } }

    // Counts game days (one per real day, starting each morning)
    public int DayNumber => Calendar.DayNumber(LocalNow);

    // How far testing has moved the clock from the real time
    public TimeSpan DebugOffset => timeSource.Offset;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        int saved = PlayerPrefs.GetInt(ModePreference, (int)defaultMode);
        Mode = Enum.IsDefined(typeof(ClockMode), saved) ? (ClockMode)saved : defaultMode;
        if (IsRealTime) SyncFromRealTime(false);
    }

    private void Update()
    {
        if (IsRealTime)
        {
            if (DebugAllowed) timeSource.Tick(Time.unscaledDeltaTime, Time.deltaTime); // a faster game means a faster clock
            SyncFromRealTime(true);
            return;
        }

        if (!autoAdvance || secondsPerPhase <= 0f) return;
        if (GameManager.Instance != null && GameManager.Instance.SimulationPaused) return;

        elapsed += Time.deltaTime;
        if (elapsed >= secondsPerPhase)
        {
            Advance();
        }
    }

    // Reads the real time and sets the time of day and how far into it we are
    private void SyncFromRealTime(bool raise)
    {
        DateTime local = LocalNow;
        TimeOfDay phase = Calendar.PhaseAt(local);
        DateTime start = Calendar.PhaseStart(local);
        elapsed = (float)(local - start).TotalSeconds;
        phaseLength = (float)(Calendar.NextPhaseStart(local) - start).TotalSeconds;

        if (phase == Current) return;
        Current = phase;
        if (raise) TimeOfDayChanged?.Invoke(phase);
    }

    // --- Changing it ---

    public void SetMode(ClockMode mode)
    {
        if (mode == Mode) return;
        Mode = mode;
        PlayerPrefs.SetInt(ModePreference, (int)mode);
        PlayerPrefs.Save();
        if (IsRealTime) SyncFromRealTime(true);
    }

    // Jumps to a time of day without raising TimeOfDayChanged. Used when loading a game: only meaningful in the
    // game's own clock (the real time cannot be set).
    public void SetTime(TimeOfDay time, float secondsIntoPhase)
    {
        if (IsRealTime) return;
        Current = time;
        elapsed = Mathf.Max(0f, secondsIntoPhase);
    }

    // Goes to the next time of day. In the game's own clock that is simply the next phase. In real time it is a
    // testing tool (editor and Development Builds): the clock is moved forward to the moment the next phase begins.
    public void Advance()
    {
        if (IsRealTime)
        {
            if (!DebugAllowed)
            {
                Debug.LogWarning("The real-time clock cannot be moved.");
                return;
            }
            DateTime local = LocalNow;
            timeSource.SkipBy(Calendar.NextPhaseStart(local) - local);
            SyncFromRealTime(true);
            return;
        }

        int count = Enum.GetValues(typeof(TimeOfDay)).Length;
        Current = (TimeOfDay)(((int)Current + 1) % count);
        elapsed = 0f;
        TimeOfDayChanged?.Invoke(Current);
    }

    // Puts the real-time clock back to the real time (testing)
    public void ResetDebugOffset()
    {
        timeSource.Reset();
        if (IsRealTime) SyncFromRealTime(true);
    }
}
