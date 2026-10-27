# The story

The story is made of **chapters**. A chapter is a few scenes of dialogue (a picture of who is speaking, and their words), plus *when it plays* and *what it gives*. You write chapters in Unity (or in a text file Unity reads); no code is needed.

What exists:

- **Story values**: named whole numbers (say `warmth`, `pride`) that choices change. Later lines and chapters can react to them. There are no branches: everyone sees the same chapters, but a line can be different (or missing) depending on a value.
- **Automatic chapters** play by themselves when their requirements are met and the game is quiet (nothing being placed, no menu or dialogue open). A chapter with no requirements plays at the start of a new game: that is your intro.
- **Talk chapters** play when the player selects a character and taps **Talk**.
- **Locked buildings**: a building can stay locked until a chapter is finished (the tutorial, or progression).
- **The Story menu** (the Story button) shows what to do next and lets the player read finished chapters again (a replay changes nothing and can be skipped).
- Finished chapters and the values are **saved** with the game (save version 9). A save from before the story starts from the beginning.

## Adding a chapter

1. Eryville > **New Story Chapter** (or Assets > Create > Scriptable Objects > Story Chapter). It must be in a `Resources/Story` folder: the game finds everything there by itself, nothing to add to the scene. The menu item puts it in the right place.
2. Fill in the fields (below), write the script.
3. Eryville > **Check Story** lists mistakes (same ids, a line it cannot read, a chapter that does not exist, an asset outside `Resources/Story`).
4. To try it: in Play mode open Cheats > Story. "Play the next chapter that is not done" plays one whatever it needs, and "Forget all story progress" starts the story again. (The sample chapters play on their own in a fresh game, and in an old save, because they need nothing: that is the intro. Delete or rewrite them.)

## The fields

| Field | What it does |
|---|---|
| **Id** | A short unique name (`welcome`). Saves and other chapters refer to it, so do not change it once players have saves. Empty = the asset's name. |
| **Title / Summary** | Shown in the Story menu once the chapter is finished. |
| **Order** | When several chapters could start at once, the lowest goes first. |
| **Trigger** | *Automatic*, *Talk* (needs **Talk To**: whose Talk button), or *Manual* (only a replay, a cheat, or later an event starts it). |
| **Requires** | Everything here must be true. Kinds: *Value* (a story value, with at least / at most / exactly / not), *Chapter Done* / *Chapter Not Done* (this is how chapters are put in order), *Has Building* (empty building = any), *Has Character* (empty = any), *Has Item* (gold, wood, wheat...). |
| **Goal** | Optional hint ("Build a Gold Mine") shown in the Story menu while this is the next step (every chapter it needs is done, but it has not played). |
| **Rewards** | Given once when the chapter finishes: *Give* an item (Gold, Wood, Letters, Wheat...), *Add* to or *Set* a story value. Letters cannot be given while the server owns them (online). |
| **Cast** | A picture for each person who speaks but is not a character of the game (the Mayor). A character of the game is found by its name (a line from `Darkie:` shows Darkie's portrait). |
| **Script / Script File** | The dialogue. Write it in the box, or in a `.txt` file (assign it to Script File, which wins). A text file is easier for long writing. |

To lock a building: on its **BuildingType**, set **Unlocked By** to the chapter. It shows greyed out in the Build menu, saying what unlocks it (or is left out entirely with **Hide While Locked**). The chapter must be in `Resources/Story`.

## Writing the script

One thing per line:

```
# a note to yourself (also // ...)
Mayor: Welcome to Eryville!
A cold wind blows.                      <- no colon: narration, nobody speaking (Narrator: also works)
Mayor: One line.\nA second line.        <- \n starts a new line inside the box
Mayor: You have {kindness} points.      <- {name} shows a story value

> Help the farmer | kindness +1         <- a choice: consecutive ">" lines are its options
> Ignore them     | kindness -1 | give Gold 20
=> Farmer: Thank you!                   <- a reply, shown only if the option above it was picked

@add kindness 1                         <- change a value on the spot (@set name 3, @give Gold 50 too)
[if kindness >= 2] Mayor: Well done.    <- only plays when true
[if kindness >= 2 and not done intro] Mayor: ...
[if met] > A secret option | x +1       <- works in front of options and @ lines too
```

- **What an option does** goes after `|`: `name +1`, `name -2`, `name =3` (set), `give Item amount`. Several can follow.
- **Conditions** (in `[if ...]`, joined with `and`): `name >= 2` (also `<=`, `<`, `>`, `==`, `!=`), `name` (more than 0), `not name`, `done chapter-id`, `not done chapter-id`. Values nobody set are 0.
- **Quests** (see `docs/QUESTS.md`): `@quest id` (or `| quest id` in an option) starts a quest; `[if quest done id]`, `[if quest active id]` and `[if not quest done id]` ask about one.
- **A line it cannot understand** is skipped and reported, with its line number, by Check Story (and in the console when the chapter plays).
- **Nothing is kept until the chapter ends.** The values a choice changes, and the rewards, are committed at the end; quitting half way means it plays again from the start. Within a chapter, later lines already see earlier choices.
- A replay (from the Story menu) shows the chapter again with the current values, but changes nothing and has a Skip button.

## A small example: a tutorial step

`01_Welcome` (Automatic, no requirements) → `02_FirstBuilding` (Automatic; Requires: Chapter Done `welcome`, Has Building (any) at least 1; Goal "Open Build and place your first building") → a Talk chapter with Darkie. And a Tavern with **Unlocked By** = `02_FirstBuilding` stays locked until the second chapter is seen. The three sample chapters in `Resources/Story` are exactly this, with placeholder text.

## How it works (for changing the code)

- `Gameplay`-free pure pieces, tested headlessly: `StoryScript` (reads a script), `StoryState` (finished chapters + values, what the save keeps), `StoryCondition` / `StoryEffect` (what to check, what to change), `StoryPlayer` (walks a chapter on a *copy* of the state and commits at the end).
- `StoryManager` (starts by itself, survives scenes, resets on each scene load and is refilled by the save) finds the chapters, starts the due ones, answers "is this building unlocked / does this character have something to say", and talks to the `DialogueBox`.
- `DialogueBox` (the screen), `StoryMenu` (the list) and the Talk button live with the UI; `UnityStoryWorld` is how conditions look at the real town and how rewards are given.
- To make the story look at something new, add a `Kind` to `StoryCondition` and a case in `Test`. To react to something that happens (a pull, a building finished), start a chapter with `StoryManager.Instance.Play(chapter)` or add a requirement.
