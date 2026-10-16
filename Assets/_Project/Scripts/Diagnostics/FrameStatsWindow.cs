using System;

// Remembers the most recent frame times and works out the numbers that matter for smoothness.
// An average alone hides stutter: a game can average 60 fps and still hitch, so this also gives the
// slow tail (95th / 99th percentile, the worst frame) and how many frames were slower than 33 ms.
// Written not to allocate once created, so measuring does not disturb what is measured.
public class FrameStatsWindow
{
    public struct Summary
    {
        public int frames;
        public float averageMs;
        public float fps;
        public float p95Ms;     // 95% of frames were faster than this
        public float p99Ms;     // 99% of frames were faster than this ("1% low" is 1000 / this)
        public float worstMs;
        public int slowFrames;  // frames slower than 33.3 ms (below 30 fps): the ones a player notices
    }

    public const float SlowFrameMs = 1000f / 30f;

    private readonly float[] samples;   // frame times in seconds, as a ring
    private readonly float[] scratch;   // sorted copy for percentiles
    private int next;
    private int count;

    public FrameStatsWindow(int capacity = 600)
    {
        samples = new float[Math.Max(1, capacity)];
        scratch = new float[samples.Length];
    }

    public int Count => count;

    public void Clear()
    {
        next = 0;
        count = 0;
    }

    public void Add(float seconds)
    {
        if (seconds < 0f) return;
        samples[next] = seconds;
        next = (next + 1) % samples.Length;
        if (count < samples.Length) count++;
    }

    public Summary Summarize()
    {
        Summary summary = new Summary { frames = count };
        if (count == 0) return summary;

        double total = 0;
        float worst = 0f;
        int slow = 0;
        for (int i = 0; i < count; i++)
        {
            float value = samples[i];
            scratch[i] = value;
            total += value;
            if (value > worst) worst = value;
            if (value * 1000f > SlowFrameMs) slow++;
        }
        Array.Sort(scratch, 0, count);

        summary.averageMs = (float)(total / count) * 1000f;
        summary.fps = summary.averageMs > 0f ? 1000f / summary.averageMs : 0f;
        summary.p95Ms = Percentile(0.95f) * 1000f;
        summary.p99Ms = Percentile(0.99f) * 1000f;
        summary.worstMs = worst * 1000f;
        summary.slowFrames = slow;
        return summary;
    }

    // scratch must be sorted (Summarize does that)
    private float Percentile(float fraction)
    {
        int index = (int)Math.Ceiling(fraction * count) - 1;
        if (index < 0) index = 0;
        if (index >= count) index = count - 1;
        return scratch[index];
    }
}
