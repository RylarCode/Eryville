using System.Collections.Generic;
using System.Text;

// One row of a benchmark: how the game ran with this many characters
public class BenchResult
{
    public int characters;
    public int buildings;
    public FrameStatsWindow.Summary frames;
    public int gcCollections;     // garbage collections during the measurement (any here means per-frame allocations)
    public float managedMemoryMb;
    public float totalMemoryMb;

    public override string ToString()
    {
        return $"{characters,5} chars {buildings,4} bldgs | {frames.fps,5:0} fps avg {frames.averageMs,5:0.0} ms | p95 {frames.p95Ms,5:0.0} p99 {frames.p99Ms,5:0.0} worst {frames.worstMs,6:0.0} ms | " +
               $"slow(>33ms) {frames.slowFrames,3} | GC {gcCollections,2} | mem {totalMemoryMb,5:0} MB (managed {managedMemoryMb:0})";
    }
}

// Steps through "have N characters, settle, measure" for a list of N. Pure logic: the caller does the
// spawning and feeds in frame times, so it can be tested without a running game.
//
//   Spawning  : the caller makes the world have TargetCharacters, then calls SpawnDone()
//   Warmup    : frames are ignored for a moment (everything is starting to walk, paths are being found)
//   Measuring : frame times are collected; when time is up Tick returns the result
//   Done      : after the last level
public class StressBenchmark
{
    public enum Phase { Spawning, Warmup, Measuring, Done }

    private readonly int[] levels;
    private readonly float warmupSeconds;
    private readonly float measureSeconds;
    private readonly FrameStatsWindow window = new FrameStatsWindow(4000);
    private int levelIndex;
    private float phaseTime;

    public Phase CurrentPhase { get; private set; }
    public List<BenchResult> Results { get; } = new List<BenchResult>();

    public StressBenchmark(int[] levels, float warmupSeconds = 3f, float measureSeconds = 6f)
    {
        this.levels = levels != null && levels.Length > 0 ? levels : new[] { 0 };
        this.warmupSeconds = warmupSeconds;
        this.measureSeconds = measureSeconds;
        CurrentPhase = Phase.Spawning;
    }

    public bool Finished => CurrentPhase == Phase.Done;
    public int TargetCharacters => levels[System.Math.Min(levelIndex, levels.Length - 1)];
    public int LevelNumber => levelIndex + 1;
    public int LevelCount => levels.Length;

    // The world now has TargetCharacters
    public void SpawnDone()
    {
        if (CurrentPhase != Phase.Spawning) return;
        CurrentPhase = Phase.Warmup;
        phaseTime = 0f;
    }

    // Call once per frame with how long the frame took (seconds). Returns the finished level's result
    // (frame numbers filled in; the caller adds memory and GC figures) when a measurement ends, otherwise null.
    public BenchResult Tick(float frameSeconds)
    {
        switch (CurrentPhase)
        {
            case Phase.Warmup:
                phaseTime += frameSeconds;
                if (phaseTime >= warmupSeconds)
                {
                    CurrentPhase = Phase.Measuring;
                    phaseTime = 0f;
                    window.Clear();
                }
                return null;

            case Phase.Measuring:
                window.Add(frameSeconds);
                phaseTime += frameSeconds;
                if (phaseTime < measureSeconds) return null;

                BenchResult result = new BenchResult { characters = TargetCharacters, frames = window.Summarize() };
                Results.Add(result);
                levelIndex++;
                if (levelIndex >= levels.Length) CurrentPhase = Phase.Done;
                else { CurrentPhase = Phase.Spawning; phaseTime = 0f; }
                return result;

            default:
                return null;
        }
    }

    public string Report()
    {
        StringBuilder text = new StringBuilder();
        foreach (BenchResult result in Results) text.AppendLine(result.ToString());
        return text.ToString();
    }
}
