# Quests

Goals the player works towards, in a **Quests** menu. **Main quests** are the story's thread: one at a time, in order. **Side quests** are optional and there can be as many at once as are available. Nothing fails, is declined or abandoned.

A quest is an asset (like a story chapter): a title, objectives, rewards and rules for when it shows up. Conditions and effects are the story's, so quests and chapters can start and wait for each other.

## How a quest goes

`Locked -> (Available) -> Active -> Ready -> Complete`

| State | Meaning |
|---|---|
| Locked | Its requirements are not met yet. |
| Available | **Offered** quests only: waiting for the player to press **Accept** (or for a story chapter to start it). |
| Active | Being worked on. |
| Ready | Every objective was done. It **stays** ready (tearing a building down later does not undo it). The player taps **Claim** in the menu. |
| Complete | The reward was **claimed**. Only now does a quest count as done: it is what lets the next main quest start, and unlocks buildings and chapters that wait for it. |

- **Main quests**: only one is available / active / ready at a time. The next starts (by itself, or offered) only after the previous one is claimed, even if its requirements are met sooner. When two could start, the lower **Order** goes first.
- **Side quests**: start as soon as their requirements are met (Automatic) or are offered (Offered) and wait for Accept. No limit on how many are active.
- **Rewards are never automatic**: the player claims each quest, main or side.

## Adding a quest

1. Eryville > **New Quest** (or Assets > Create > Scriptable Objects > Quest). It must be in a `Resources/Quests` folder: the game finds everything there by itself. The menu item puts it in the right place.
2. Fill in the fields (below).
3. Eryville > **Check Quests** lists mistakes: duplicate ids, a quest or chapter that does not exist, quests with no objectives, a main quest that waits to be accepted, an asset outside `Resources/Quests`, and letters the server does not know about.
4. To try quests: Cheats > Quests: "Start every quest that has not started", "Make every active quest ready to claim", "Forget all quest progress".

| Field | What it does |
|---|---|
| **Id** | A short unique name (`first-building`). Saves, stories and the server refer to it, so do not change it once players have saves. Empty = the asset's name. |
| **Title / Description** | What the player reads. |
| **Category** | *Main* or *Side*. |
| **Order** | Main quests are tried in this order; side quests are listed in it. |
| **Start** | *Automatic*: starts by itself once its requirements are met. *Offered*: shows as available and waits for Accept. (A main quest should be Automatic: an offered one holds the whole main line.) |
| **Requires** | Everything here must be true before it starts. Same conditions as chapters: *Quest Done* (put quests in order), *Chapter Done* (follow the story), a story value, has a building / character / item. |
| **Objectives** | All must be done. Each is a condition with an optional label. If it counts ("have 3 Gold Mines") the menu shows "(2/3)"; with no label the text is made up ("Build a Gold Mine"). |
| **Rewards** | Given when claimed: *Give* an item (Gold, Wood, Letters, Wheat...), *Add* / *Set* a story value, or *Start Quest*. |

## Recruitment letters

Online, the **server owns the letters**, so the game cannot add them itself. When a quest that pays letters is claimed, the game asks the server (`claimQuest`); the server pays once per player and shows the new total. If it cannot be reached the quest simply stays ready and can be claimed again later. With the local backend the game adds the letters itself.

The server must know each quest that pays letters, with the same amount, in `firebase/functions/src/config.ts`:

```ts
quests: {
  "hire-someone": { letters: 1 },
},
```

then redeploy (`firebase deploy --only functions`). **Check Quests** warns if a quest pays letters that `config.ts` does not list (or lists with another amount). The server can only check that a quest id is listed and unpaid, not that the town really finished it (the game simulates the town), so each listed quest can pay at most once per player.

## Quests and the story

- A chapter can **start a quest**: `@quest build-mine` in a script, `> Sure | quest build-mine` in an option, or a *Start Quest* reward on the chapter.
- A chapter can **wait for a quest**: `[if quest done build-mine] Mayor: Well done.` in a script (also `quest active id`, `not quest done id`), or a *Quest Done / Quest Active* requirement on the chapter. So "when this quest starts, play this scene" is an Automatic chapter requiring *Quest Active*.
- A building can be **locked behind a quest**: **Unlocked By Quest** on the BuildingType (needs the quest's reward claimed). It can be combined with *Unlocked By* (a chapter): both must be done.

## What the player sees

- **Quests** button (next to Story): shows **Quests (2)** when something is ready to claim or waiting to be accepted.
- The menu: the main quest first (with Claim / Accept), then side quests (ready first, then offered, then in progress), then completed ones. Each quest lists its objectives with `[x]` / `[ ]` and its reward.
- Messages (toasts): "New quest: X", "New quest available: X", "Quest ready: X (tap to claim)", "Quest complete: X (50 Gold, 1 Letters)". Tapping the first three opens the menu.

## Saving

Where each quest is (by id) is saved with the game (save version 10). A save from before quests starts them from the beginning. Quests are checked while you play; they are not advanced while the game is closed (state objectives are simply true when you return).

## How it works (for changing the code)

- `Quest` / `QuestObjective` (assets), `QuestLog` (state by id: what a save keeps), `QuestManager` (finds the quests, starts the due ones, marks the finished ones ready, claims, saves), `QuestChecks` (mistakes), `QuestMenu` (the screen). Conditions are `StoryCondition`, effects `StoryEffect`; `IStoryWorld` lets the story ask for a quest's state and start one.
- **Event goals** ("make 50 bread", "recruit 5 characters"): objectives are state goals today (`QuestObjective.Kind.State`). An event goal is another `Kind` that keeps a count in the quest log from when the quest started and is added to by the thing that happens (`QuestManager.Count(name, n)`): add the kind, the saved counter, and the calls where things happen.
