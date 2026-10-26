using System;
using UnityEngine;

// The one place anything posts a message to the player:
//
//     Notify.Show("Wages paid: 120 gold");
//     Notify.Show("Baker is hungry", NotifyKind.Warning, face, NotifyActions.ForCharacter(baker), key: "hungry:" + id, cooldown: 300f);
//
// Callers never touch the screen: the toast view (UI/ToastView.cs) and the history menu read the NotificationCenter.
// Posting before there is a screen (or in tests) is fine: the message just waits in the history.
public static class Notify
{
    public static NotificationCenter Center { get; } = new NotificationCenter(() => Time.unscaledTime);

    // The sound hook: set this to play a sound for a new message (by kind). Nothing is set yet; the game is silent.
    //     Notify.Sound = kind => audioSource.PlayOneShot(clips[(int)kind]);
    public static Action<NotifyKind> Sound;

    static Notify()
    {
        Center.Posted += note => Sound?.Invoke(note.Kind);
    }

    public static Notification Show(string text, NotifyKind kind = NotifyKind.Info, Sprite icon = null, Action onTap = null, string key = null, float cooldown = 0f, float duration = -1f)
    {
        return Center.Post(text, kind, icon, onTap, key, cooldown, duration);
    }
}
