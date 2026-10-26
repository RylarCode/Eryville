# Notifications (toasts)

Short messages that slide in at the top of the screen ("Wages paid: 120 gold", "Baker is hungry"), and a history of them in the game menu. Anything can post one in a line.

## Posting a message

```csharp
Notify.Show("Wages paid: 120 gold");                                    // plain
Notify.Show("Could not pay wages", NotifyKind.Alert);                    // coloured by kind
Notify.Show("Baker is hungry", NotifyKind.Warning, face,                 // an icon, and what a tap does,
            NotifyActions.ForCharacter(baker),                           //   and a key + cooldown so it cannot flood the screen
            key: "hungry:" + baker.Id, cooldown: 300f);
```

Parameters of `Notify.Show(text, kind, icon, onTap, key, cooldown, duration)`; all but the text are optional.

| Parameter | Meaning |
|---|---|
| `kind` | `Info` (a new day, a cheat), `Success` (wages paid, chapter finished), `Warning` (someone is hungry), `Alert` (wages could not be paid). Sets the colour and how long it stays: 4 s, 4 s, 6 s, 8 s. |
| `icon` | A small picture (an item's icon, a character's face). |
| `onTap` | What tapping the banner (or its row in the history) does. `NotifyActions.ForCharacter(c)`, `ForBuilding(b)` and `ForPosition(world)` open the details and move the camera there; or pass your own `() => ...`. |
| `key` | Messages with the same key merge while one is showing (default: the text). |
| `cooldown` | Seconds: the same key is ignored for that long after it was posted (even after the first was dismissed). |
| `duration` | Seconds on screen, instead of the kind's default. |

Posting does not need any screen: before the UI exists (or in tests) a message just waits in the history.

## What the player sees

- A new message appears on top and **pushes the others down**. At most 4 show at once; the oldest leaves the bottom (it stays in the history).
- The same message again while it is still showing is **counted** ("Baker is hungry (x2)") and goes back to the top with a fresh timer.
- **Tap** a banner: it does its action (focus on the thing it is about) and goes away. **Swipe** it sideways, or press its **X**, to dismiss it. A banner that is being touched does not time out.
- **Notifications** in the game menu (below Quit) lists the history (the last 100), newest first with the time. Tap a row to go to what it is about; **Clear** empties it. The history is not saved.
- On a phone the banners sit below the top row; on a wide screen at the top centre.

## What posts today

| Message | From |
|---|---|
| "A new day begins", "It is afternoon", "Evening falls", "Night falls" | `GameClock` (each time of day) |
| "Wages paid: N gold" / "Could not pay N wages..." | `WageSystem.RunPayroll` |
| "Chapter finished: X", "New building unlocked: Y" | `StoryManager` |
| "X is hungry" (tap: go to them; once per 5 minutes each) | `HungerSystem` |
| "While you were away (3 h): +340 gold, ..." and what held production back | `OfflineProgress.Apply` |
| "Cheat: +1000 gold" | the cheat menu |

## Sound

There is a hook and no sound yet. When you have audio, set it once at start-up:

```csharp
Notify.Sound = kind => audioSource.PlayOneShot(clips[(int)kind]);   // kind: Info, Success, Warning, Alert
```

It is called once for each new message (not for a merged repeat).

## How it works (for changing the code)

- `NotificationCenter` (pure rules, tested headlessly): the list that is showing, merging, cooldowns, expiry, the history. `Notify` is the one static instance (`Notify.Center`).
- `ToastView` / `ToastItem` (UI) draw `Notify.Center.Visible`: they animate the stack and handle tap, swipe and X. `NotificationMenu` is the history list.
- To add a message, call `Notify.Show` where it happens. For quests: post when progress changes and when a quest completes.
