using System;
using UnityEngine;

public enum NotifyKind
{
    Info,    // something happened (a new day, a cheat was used)
    Success, // something good (wages paid, a chapter finished)
    Warning, // needs attention soon (someone is hungry)
    Alert    // something went wrong (wages could not be paid)
}

// One message: what it says, how it looks, and what tapping it does. Merged repeats keep the same Notification and count up.
public class Notification
{
    public int Id;
    public string Text;
    public NotifyKind Kind;
    public Sprite Icon;          // optional: an item's icon, a character's face...
    public Action OnTap;         // optional: what tapping does (focus on what it is about, open a menu...)
    public string Key;           // messages with the same key merge while one is showing
    public int Count = 1;        // how many times it happened (merged)
    public float ShownAt;        // when it started to be shown, in the center's clock (its time on screen counts from here)
    public float Duration;       // seconds on screen
    public bool Held;            // being touched: it stays
    public bool Visible;         // on screen now
    public DateTime At;          // wall-clock time, for the history

    public string Display => Count > 1 ? $"{Text} (x{Count})" : Text;
    public bool HasTarget => OnTap != null;
}
