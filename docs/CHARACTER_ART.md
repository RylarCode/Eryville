# Character art and animations

Every character type (`CharacterType` asset) has an **Art** section. Everything is optional: a character with only `sprite` behaves exactly as before.

| Field | Used for |
|---|---|
| `sprite` | the small 2D body in the world (also the fallback for everything below) |
| `portrait` | the full image: the reveal screen after a gacha pull, and beside the details panel when you click the character |
| `face` | small face crop: icons in the Town list, Recruit results, hire cards, building occupant rows (falls back to `portrait`, then `sprite`) |
| `animations` | a *Character Animations* asset (below) |

## Adding animations
1. Import the sprite sheet, set Sprite Mode = Multiple, slice it into frames.
2. Create the asset: Assets -> Create -> Scriptable Objects -> Character Animations.
3. For each slot drag the frames in order and set frames-per-second / loop:
   - `idle` - standing still
   - `walk` - normal walk
   - `walkHappy` - used when mood >= `happyMood` (default 70)
   - `walkSad` - used when mood <= `sadMood` (default 30)
   - `enterBuilding` - plays at the door on arrival (not looping); the character goes inside when it ends
   - `exitBuilding` - plays at the door before leaving (not looping)
4. Tick `facesRight` if the art faces right (the sprite is mirrored when walking left). Untick if it faces left.
5. Assign the asset to the character type's `animations`.

## Fallbacks
Empty slots fall back: walkHappy / walkSad -> walk -> idle. Missing enter/exit animations are simply skipped. Walk playback speeds up or slows down with the character's actual speed (roads, terrain, mood).

## Notes
- Enter/exit animations delay arrival/departure by their duration; a change of plan cancels them.
- Portrait placement beside the details panel (wide screen) / left of the sheet (phone) has not been checked visually; adjust in `UIManager.CreateDetailsPortrait` and `UILayout.PlaceDetails`.
