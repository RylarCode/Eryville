# Adding a character

Short version: **Eryville > New Character...**, fill in a name and drop in your pictures, press *Create*.
The rest of this page says what that does, what to prepare, and how to do it by hand.

## What a character is made of

| Piece | What it is | Where it lives |
|---|---|---|
| **Character type** | the "template": name, rarity, wage, speed, mood, schedule, pictures | `ScriptableObjects/Characters/<Name>/<Name>.asset` |
| **Animation set** | the walking / idle / enter / exit frames, in four directions | `.../<Name>/<Name> Animations.asset` |
| **Prefab** | the body that walks around (sprite, collider, Character, CharacterMovement scripts) | `Prefabs/Characters/<Name>.prefab` |
| **typeId** | a unique number; saved games and the server refer to the character by it | field on the type |
| **Lists** | the CharacterManager in GameScene (who can be hired) and the gacha pool (who can be pulled) | scene / `ScriptableObjects/Gacha` |

The wizard makes all of these, picks a free typeId, copies the schedule, mood, speed and awareness levels from a template character, and puts the new one on both lists.

## 1. Prepare the pictures (all optional, all can come later)

A character shows up in four ways:

| Picture | Where it shows | Field |
|---|---|---|
| **Small sprite** | the body in the world when no animation is playing (fallback for everything) | `sprite` |
| **Full portrait** | the reveal screen after recruiting, and beside the details panel when you click the character | `portrait` |
| **Face icon** | the Town list, Recruit results, hire cards, building occupant rows | `face` |
| **Animations** | walking, standing, going into / coming out of a building | `animations` |

Missing pictures fall back: face -> portrait -> sprite.

### The animation sprite sheet

Characters walk **only up, down, left or right** (never diagonally), and **nothing is mirrored**: each direction has its own pictures.

1. Import the sheet into `Art/Sprites/Characters/`.
2. Select it: Sprite Mode = **Multiple**, Filtering as the other character art, Apply.
3. Open the Sprite Editor and **Slice** it (Automatic, or Grid By Cell Size), then **name every frame** like this:

```
<animation>_<direction>_<frame number>

walk_down_0   walk_down_1   walk_down_2   walk_down_3
walk_up_0     walk_up_1     ...
walk_left_0   walk_left_1   ...
walk_right_0  walk_right_1  ...
idle_down_0   (and idle_up_..., idle_left_..., idle_right_...)
walk_happy_down_0 ...       walk_sad_down_0 ...
enter_up_0 enter_up_1 ...   exit_down_0 exit_down_1 ...
```

- Animations: `idle`, `walk`, `walk_happy`, `walk_sad`, `enter`, `exit`, `eat`.
- Directions: `down`, `up`, `left`, `right` (`front`, `back`, `west`, `east`, `north`, `south` also work).
- Frames count up from 0 in the order they play. Capitals, and a prefix like `john_walk_down_0`, are fine.
- You do not need all of them. See the fallbacks below.

Tip: renaming 40 frames by hand is tedious. Most sprite tools (Aseprite, Piskel, LibreSprite...) can export frame names from tags/layers, or you can rename in a batch with any file renamer before importing.

## 2. Create the character

**Eryville > New Character...**

1. *Name*, *Rarity*. *Copy settings from*: pick a character that behaves roughly like the new one (default: the first).
2. Drag in the sprite sheet, and optionally the small sprite, portrait and face. Every field can be left empty and filled later.
3. Leave both "added to" boxes ticked (CharacterManager in the open scene, gacha pools). Have **GameScene open** so it can be added; **save the scene** afterwards.
4. *Create character*. The new character type is selected; the message says what was made and what to do next.
5. Tune what makes it different in the inspector: wage, speed, mood profile, schedule, pull weight, awareness levels, `maxOwned` (1 for a named story character), and **food**: `favouriteFood` (an edible item) and `appetite` (1 = ordinary, 2 = gets hungry twice as fast).
6. Run **Eryville > Check Characters**.
7. Using the Firebase backend? Add the character to `firebase/functions/src/config.ts` too (see the end of this page). It lists the usual mistakes: duplicate typeId, no prefab or sprite, a walk missing a direction, not on the CharacterManager's list.

If you add or change frames later, open the animation set, drop the sheet in the **Sprite sheet** box and press **Fill from sprite sheet**.

## What the game does with the animations

| Situation | Animation used |
|---|---|
| standing still | `idle`, in the direction it last walked |
| walking, mood >= `happyMood` (70) | `walk_happy` |
| walking, mood <= `sadMood` (30) | `walk_sad` |
| walking otherwise | `walk` |
| arriving at a building to go inside | `enter` (once, at the door; it goes in when it ends) |
| leaving a building | `exit` (once, at the door; then it walks off) |

Walking is played faster or slower with the character's real speed (roads, mood, loyalty).

**Fallbacks** (so a character is never invisible or broken):
- `walk_happy` / `walk_sad` missing -> `walk`; `walk` missing -> `idle`; no frames at all -> the small `sprite`.
- A direction that was not drawn -> the first direction that was (down, up, left, right). So a sheet with only `down` frames still works, and always looks forward.
- `enter` / `exit` missing -> skipped (the character just appears / disappears at the door).
- Loop and frames-per-second live on each direction of each animation in the asset (default 8 fps; enter/exit play once). The sheet only sets the frames.

## By hand (what the wizard does, if you ever need to)

1. Duplicate an existing character type asset, give it a **new typeId** (higher than all others).
2. Create > Scriptable Objects > Character Animations, fill it as above, assign it to `animations`.
3. Duplicate `Prefabs/Characters/RandomVillager.prefab`, put the sprite on its SpriteRenderer, adjust scale and collider; assign it to `characterPrefab`.
4. Add the type to the CharacterManager's `All Characters` in GameScene and to the gacha pool's `characters`.
5. Run **Eryville > Check Characters**.

## Good to know

- Everything is optional, so you can create a character with no art at all and add pictures later; it uses the template's small sprite meanwhile.
- The wizard cannot be undone with Ctrl+Z; delete the character's folder and prefab, and remove it from the two lists.
- **Firebase backend:** the server has its own copy of who can be pulled, in `firebase/functions/src/config.ts` (`typeId`, `rarity`, `weight`). Add a line for the new character there (same numbers as the character type) and redeploy (`firebase deploy --only functions`); until then the server will never roll it. The local backend needs nothing.
