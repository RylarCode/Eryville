# Connection (character stories)

Every story character can have a **Connection**: its own chapters and quests, unlocked as its **loyalty** grows and as the story moves on. The player opens it with the **Connection** button on the character's details.

## Loyalty

- Runs from 0 to 100. A newly recruited character starts at **0**.
- Changes **once a day, at payday** (so it is slow, and the same whether the game was open or not), by adding up the loyalty rules:

| Rule | Default | Setting (`Resources/LoyaltySettings`) |
|---|---|---|
| Paid, in a good mood (mood >= 60) | +2 | `paidHappy`, `happyMood` |
| Paid, ordinary mood | +1 | `paid` |
| Not paid | -3 | `unpaid` |
| In a bad mood (mood < 30) | -1 | `unhappy`, `unhappyMood` |
| No home | -1 | `homeless` |

- Also: story choices and rewards (`@loyalty +5`, a *Loyalty* reward on a chapter or quest), and a duplicate pull once awareness is maxed (+10).
- The details panel shows it, with the latest change: "Loyalty: 52 (+2 today)".
- **Adding a reason** (a favourite workplace, gifts...): write a class implementing `ILoyaltyRule` and call `LoyaltyRules.Add(rule)`. Nothing else changes.

## What is in a Connection

A character's Connection lists, in **Order** (chapters and quests mixed):
- **Story chapters** with Trigger **Talk** and **Talk To** = the character.
- **Quests** whose **Character** field is the character.

Each entry is one of:

| | Shown as |
|---|---|
| Available chapter | **Talk: title** (tap: it plays) |
| Offered quest | **Accept: title** and its reward |
| Active quest | the title and its objectives `[x]` / `[ ]` |
| Ready quest | **Claim: title (reward)** |
| Locked | Only the **next** one: **Next: title** and what it still **Needs** ("Loyalty 40 (now 32)", "Finish the story chapter ...", "Have Condo"). Story-value requirements are not spelled out ("Something else must happen first"), so nothing is given away. Later locked entries are hidden. |
| Done | Under **Done**: chapters can be **read again** (a replay changes nothing) |

Below that, **Bonuses** lists the buffs the Connection has given.

The button says **Connection (1)** when something waits (a chapter to play, a quest to accept or claim). When a chapter becomes playable (loyalty reached, a chapter or quest it needed is done...), a message says **"Condo has something to share"**; tapping it opens the Connection. Quests announce themselves as usual, and a character's quest messages open that character's Connection. What is already playable when a game is loaded is not announced again.

A character's chapters and quests only become available once the player **owns** the character.

## Story characters are unique

A Connection belongs to a character **type**, and loyalty to the owned character, so story characters must be **unique**: **Max Owned 1** on the CharacterType. (Duplicates of a unique character still raise awareness, and give loyalty once it is maxed.) **Eryville > Check Quests** reports a character with a Connection whose Max Owned is not 1.

## Writing a Connection

**A chapter** (Eryville > New Story Chapter): Trigger **Talk**, Talk To the character, Order to place it.
- Requires: for example *Chapter Done* (the previous Connection chapter) and **Loyalty** 10 (with no character picked it means the Talk To character).
- In the script: `[if loyalty >= 20] Condo: ...` (the Talk To character's loyalty), `@loyalty 5` / `@loyalty -3`, or in an option `> Thanks | loyalty +2`. "loyalty" is a reserved word: never a story value. Loyalty changes are kept when the chapter ends (like everything a chapter does), so `[if loyalty ...]` later in the same chapter still sees the old value.

**A quest** (Eryville > New Quest): set **Character**. Usually a Side quest, Offered. Requires and objectives can use **Loyalty** (meaning the quest's character).

**Rewards** (chapter or quest):
- **Loyalty**: amount (+ or -), for the subject (or the character picked on the reward).
- **Buff**: a stat and multiplier (Productivity, MoveSpeed, DailyWage, MoodRecovery, MoodDrain; 1.1 = +10%; below 1 is better for DailyWage and MoodDrain). It holds as long as the chapter stays finished or the quest complete; it multiplies with awareness. Shown under Bonuses.
- And everything else: items, story values, starting quests.

**Check**: Eryville > **Check Quests** also checks Connections (unique characters; a Loyalty condition or reward with nobody to be about, e.g. in a non-Talk chapter or a quest without a Character). Eryville > **Check Story** checks the scripts.

**Try it**: Cheats > Characters > "Everyone: loyalty +10", and "Pay wages now" (a payday, so the daily loyalty change).

The placeholder samples: Condo (unique) has `03_CondoFirstWord` (after the welcome chapter), `04_CondoTrust` (loyalty 10; gives Productivity x1.1) and the side quest `C01_CondoFavour` (after `condo-trust`, loyalty 15; gives loyalty +5 and gold).

## How it works (for changing the code)

- `LoyaltyRules` / `ILoyaltyRule` / `LoyaltySettings`: the daily change, run by `WageSystem.RunPayroll` for everyone.
- `StoryCondition.Kind.Loyalty`, `StoryEffect.Kind.Loyalty` / `Buff`: conditions and effects take a **subject** (the Talk To character of a chapter, the Character of a quest) for "whose".
- `Connection` (what belongs to a character, its state, what a locked one needs), `ConnectionBuffs` (bonuses worked out from finished chapters and completed quests: nothing extra is saved), `ConnectionWatcher` (the "has something to share" messages; refreshes everyone's buffs when the story or quests change), `UI/ConnectionMenu` (the screen).
- Nothing new in the save: loyalty was already saved with each character; Connection progress is the story's and the quests'.
