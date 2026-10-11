using System;
using UnityEngine;

// Drives the day cycle. The time of day advances on its own every secondsPerPhase seconds
// (paused while a menu is open, like resource production) and TimeOfDayChanged is raised
// each time. Advance() can also be called directly, e.g. from the test button.
public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }

    public event Action<TimeOfDay> TimeOfDayChanged;

    [SerializeField] private float secondsPerPhase = 30f;
    [SerializeField] private bool autoAdvance = true;

    public TimeOfDay Current { get; private set; } = TimeOfDay.Morning;

    // 0..1 progress through the current phase
    public float PhaseProgress => secondsPerPhase > 0f ? elapsed / secondsPerPhase : 0f;

    // Seconds spent in the current phase (saved so a loaded game continues mid-phase)
    public float Elapsed => elapsed;

    private float elapsed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (!autoAdvance || secondsPerPhase <= 0f) return;
        if (GameManager.Instance != null && GameManager.Instance.currentGameState != GameManager.gameState.Normal) return;

        elapsed += Time.deltaTime;
        if (elapsed >= secondsPerPhase)
        {
            Advance();
        }
    }

    // Jumps to a time of day without raising TimeOfDayChanged. Used when loading a game.
    public void SetTime(TimeOfDay time, float secondsIntoPhase)
    {
        Current = time;
        elapsed = Mathf.Max(0f, secondsIntoPhase);
    }

    public void Advance()
    {
        int count = Enum.GetValues(typeof(TimeOfDay)).Length;
        Current = (TimeOfDay)(((int)Current + 1) % count);
        elapsed = 0f;
        TimeOfDayChanged?.Invoke(Current);
    }
}
