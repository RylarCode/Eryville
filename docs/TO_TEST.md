# To test at the computer

Things built in the cloud (headless tests only, never seen in Unity) that need a look in the editor. Tick them off, note what is wrong under the item, and tell Claude; Claude adds new items here with every change and removes the ones confirmed working.

Before starting: pull the branch `first-cleanup`, let Unity import (no restart needed), stop Play mode before scripts recompile. If Unity re-saves any of the hand-written assets, commit those changes.

## 1. After pulling: does it start cleanly?

- [ ] No compile errors in the Console after the import.
- [ ] No errors on Play from the title screen into GameScene.
- [ ] "There are 2 event systems in the scene" no longer appears (fixed in 589b21a, never confirmed).
- [ ] Re-save assets once (open and save GameScene; right-click the `ScriptableObjects` folder > Reimport, or change and revert a field) so Claude can remove the three `FormerlySerializedAs` attributes (BuildingType.buildingPrefab, BuildingType.role, Building.cell). Tell Claude when done.

## 2. Menus and layout (c3325ff)

- [ ] The **View** button sits just left of the Menu button (top right), with its own label, and does not cover Build.
- [ ] The **Build** menu scrolls when there are more buildings than fit (mouse wheel and drag). Same for **Hire**.
- [ ] Assigning a character to a job: the list's numbers ("Gold Mine (1/3)") change immediately, without closing the menu.

## 3. Flat / 3/4 view (5660f31)

The button does nothing until a building uses 3/4 art.
- [ ] Set one building type's Art Style to ThreeQuarter (ideally with a tall sprite), build it, press View: it squashes onto its cells and back. Characters behind it show a faint outline.
- [ ] Placing / moving a building switches to flat by itself, and back afterwards.

## 4. Story (f752d1b)

- [ ] On a fresh game (Cheats > Story > "Forget all story progress"), the placeholder **welcome** chapter opens by itself after a moment: portrait area, name, words appearing, tap to continue.
- [ ] The choice shows two buttons; the reply after "Firmly" appears only for that option.
- [ ] After placing a building, chapter 2 plays and its line depends on the choice made in chapter 1.
- [ ] With Darkie owned: Darkie's details show a **Talk** button (to the right of Assign: check it fits the panel); Talk plays the chat; the button disappears afterwards.
- [ ] **Story** button (bottom bar): "What next" goals, chapters seen; tapping one replays it with a Skip button and changes nothing.
- [ ] Save, quit, reopen: finished chapters stay finished.
- [ ] Eryville > New Story Chapter makes an asset in `Resources/Story`; Eryville > Check Story reports nothing for the samples.

## 5. Characters share a prefab (af22b1c)

- [ ] Existing characters look the same size as before (Body Scale 1).
- [ ] Eryville > New Character... no longer makes a prefab; the new character uses the template's prefab and a Body Scale that makes it as tall as the template. Changing Body Scale in the inspector changes its size in game.

## 6. Notifications (35f801d)

- [ ] Toasts appear at the **top centre**; several stack with the newest on top, pushing the others down; at most 4.
- [ ] Each fades after a few seconds (warnings longer). Holding one keeps it.
- [ ] **Tap** a "X is hungry" toast: opens that character and moves the camera to them.
- [ ] **Swipe** a toast sideways to dismiss it; a short drag springs back. The **X** dismisses it (mouse).
- [ ] A repeated message counts up ("(x2)") instead of stacking.
- [ ] Messages seen: time of day changing, wages paid / not paid, chapter finished, building unlocked, hungry, "While you were away..." after tabbing out for over a minute (real-time clock), cheats ("Cheat: +1000 gold").
- [ ] Game menu > **Notifications**: history, newest first, with the time; tapping a row goes there; Clear works. The button text fits.
- [ ] Phone-shaped Game view (e.g. 1080x2340): toasts sit below the top row and do not cover the resources or the Menu / View buttons. Tell Claude if the four time-of-day toasts a day are too chatty.

## 7. Quests (ba4894d)

- [ ] After the welcome chapter, the main quest "Your first building" starts (toast "New quest").
- [ ] **Quests** button (bottom bar, after Story): the menu shows the main quest with `[ ]` / `[x]` objectives and its reward.
- [ ] Place a building: toast "Quest ready (tap to claim)"; the button says "Quests (1)"; tapping the toast opens the menu.
- [ ] **Claim**: gold is added, toast "Quest complete", the next main quest ("Someone to help") starts, and the side quest "Strike gold" is offered with **Accept**.
- [ ] Claiming "Someone to help" with the local backend adds 1 letter.
- [ ] Save, quit, reopen: quest states are kept.
- [ ] Phone layout: six buttons fit in the bottom bar, including "Quests (2)".
- [ ] Eryville > New Quest / Check Quests work; Check Quests reports nothing for the samples.

## 8. Older, never confirmed

Built earlier in this project; tick them if you have already seen them working.
- [ ] Title screen: click anywhere fades into GameScene; Play in the editor starts from the title (PlayFromTitle).
- [ ] Day / night lighting changes through the day; Cheats can preview an hour.
- [ ] Production chain: Farm > Mill > Bakery makes bread; the Stock tab shows rates and limits; a Granary raises the wheat limit.
- [ ] Hunger: characters eat in meal windows, favourite food first; a dining place (Tavern) fills its seats; eating bubble shows.
- [ ] Four-direction walking animations, no diagonal walking.
- [ ] Building open / closed animations, depth sorting (characters in front of / behind buildings correctly).

## Done

(Move confirmed items here with the date, or just delete them.)
