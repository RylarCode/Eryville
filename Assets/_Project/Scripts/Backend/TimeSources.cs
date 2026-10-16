using System;
using UnityEngine;

// Where "now" comes from. The game's real-time clock asks one of these, never DateTime.UtcNow directly,
// so the time can come from the server (which players cannot cheat by changing their phone's clock) and
// so testing tools can move it.
public interface ITimeSource
{
    DateTime UtcNow { get; }
}

// The backend's idea of the time (the device's clock for the local backend, the server's for an online one)
public class BackendTimeSource : ITimeSource
{
    public DateTime UtcNow => Backend.Current.ServerUtcNow;
}

// A time source for testing: the real time plus an offset that can be moved forward. Skipping ahead adds to the
// offset, and running the game faster adds a little more every frame, so everything that follows the clock
// (the time of day, wages, the day counter) sees time pass faster or jump ahead, as if it really had.
public class DebugOffsetTimeSource : ITimeSource
{
    private readonly ITimeSource inner;

    public TimeSpan Offset { get; private set; }

    public DebugOffsetTimeSource(ITimeSource inner)
    {
        this.inner = inner;
    }

    public DateTime UtcNow => inner.UtcNow + Offset;

    public void SkipBy(TimeSpan amount)
    {
        Offset += amount;
    }

    // Jumps so that "now" is the given moment (only forwards: going back would repeat days)
    public void SkipTo(DateTime utc)
    {
        TimeSpan ahead = utc - UtcNow;
        if (ahead > TimeSpan.Zero) Offset += ahead;
    }

    // Called every frame: when the game runs at 5x, five seconds of game time pass for every real second
    public void Tick(float realSeconds, float gameSeconds)
    {
        Offset += TimeSpan.FromSeconds(gameSeconds - realSeconds);
        if (Offset < TimeSpan.Zero) Offset = TimeSpan.Zero; // running slower than real time is not supported
    }

    public void Reset()
    {
        Offset = TimeSpan.Zero;
    }
}
