using System;
using System.Collections.Generic;
using UnityEngine;

// The rules for notifications, with no screen in them: what is showing, in what order, for how long, what merges and what is kept.
//
//  - A new message goes on top and pushes the others down. Only MaxVisible are shown at once: the oldest leaves the screen (it stays in the history).
//  - The same message (same key) while it is still showing is not shown twice: it moves to the top and counts up ("Baker is hungry (x2)").
//  - A message can carry a cooldown: the same key is ignored for that long after it was last posted (so ten hungry characters do not flood the screen).
//  - Messages go away by themselves (longer for warnings), unless they are being touched.
//  - Everything posted is kept in a history (the last HistoryLimit).
public class NotificationCenter
{
    public const int MaxVisible = 4;
    public const int HistoryLimit = 100;

    private readonly Func<float> clock;
    private readonly List<Notification> visible = new List<Notification>(); // index 0 = the newest, on top
    private readonly List<Notification> history = new List<Notification>(); // oldest first
    private readonly Dictionary<string, float> lastPosted = new Dictionary<string, float>();
    private int nextId = 1;

    public IReadOnlyList<Notification> Visible => visible;
    public IReadOnlyList<Notification> History => history;

    // Raised when what is showing (or the history) changed
    public event Action Changed;
    // Raised for each brand-new message (not for a merged repeat): the place to play a sound
    public event Action<Notification> Posted;

    public NotificationCenter(Func<float> clock)
    {
        this.clock = clock;
    }

    public static float DefaultDuration(NotifyKind kind)
    {
        switch (kind)
        {
            case NotifyKind.Warning: return 6f;
            case NotifyKind.Alert: return 8f;
            default: return 4f;
        }
    }

    // Shows a message. Returns it, or null if it was left out because of its cooldown.
    public Notification Post(string text, NotifyKind kind = NotifyKind.Info, Sprite icon = null, Action onTap = null, string key = null, float cooldown = 0f, float duration = -1f)
    {
        if (string.IsNullOrEmpty(text)) return null;
        float now = clock();
        if (string.IsNullOrEmpty(key)) key = text;

        if (cooldown > 0f && lastPosted.TryGetValue(key, out float last) && now - last < cooldown) return null;
        lastPosted[key] = now;

        // The same thing again while it is still showing: count up, bring it to the top and give it its full time again
        foreach (Notification existing in visible)
        {
            if (existing.Key != key) continue;
            existing.Count++;
            existing.Text = text;
            if (kind > existing.Kind) existing.Kind = kind;
            if (icon != null) existing.Icon = icon;
            if (onTap != null) existing.OnTap = onTap;
            existing.ShownAt = now;
            existing.At = DateTime.Now;
            visible.Remove(existing);
            visible.Insert(0, existing);
            history.Remove(existing);
            history.Add(existing);
            Changed?.Invoke();
            return existing;
        }

        Notification note = new Notification
        {
            Id = nextId++, Text = text, Kind = kind, Icon = icon, OnTap = onTap, Key = key,
            ShownAt = now, Duration = duration > 0f ? duration : DefaultDuration(kind), Visible = true, At = DateTime.Now
        };
        visible.Insert(0, note);
        while (visible.Count > MaxVisible)
        {
            visible[visible.Count - 1].Visible = false; // pushed off the bottom of the screen; still in the history
            visible.RemoveAt(visible.Count - 1);
        }
        history.Add(note);
        if (history.Count > HistoryLimit) history.RemoveRange(0, history.Count - HistoryLimit);

        Posted?.Invoke(note);
        Changed?.Invoke();
        return note;
    }

    // Takes down the messages whose time is up (not those being touched). Call it every frame.
    public bool Tick()
    {
        float now = clock();
        bool removed = false;
        for (int i = visible.Count - 1; i >= 0; i--)
        {
            Notification note = visible[i];
            if (note.Held || now - note.ShownAt < note.Duration) continue;
            note.Visible = false;
            visible.RemoveAt(i);
            removed = true;
        }
        if (removed) Changed?.Invoke();
        return removed;
    }

    public void Dismiss(Notification note)
    {
        if (note == null || !visible.Remove(note)) return;
        note.Visible = false;
        note.Held = false;
        Changed?.Invoke();
    }

    // Tapping a message: it does what it is for, and goes away
    public void Tap(Notification note)
    {
        if (note == null) return;
        Action action = note.OnTap;
        Dismiss(note);
        if (action == null) return;
        try { action(); }
        catch (Exception e) { Debug.LogWarning("A notification's action failed: " + e.Message); }
    }

    // While a message is touched (dragged) it must not time out
    public void Hold(Notification note, bool held)
    {
        if (note != null) note.Held = held;
    }

    public void ClearHistory()
    {
        history.Clear();
        Changed?.Invoke();
    }

    // Forget everything (a new game session)
    public void Reset()
    {
        foreach (Notification note in visible) note.Visible = false;
        visible.Clear();
        history.Clear();
        lastPosted.Clear();
        Changed?.Invoke();
    }
}
