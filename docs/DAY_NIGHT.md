# Day and night lighting

The world is tinted through the day: **dawn** (pink, then warm gold), **day** (plain), **dusk** (orange, then purple), **night** (dark blue). It follows the game clock, so in real-time mode it matches the player's real time of day, and in the in-game clock it sweeps through a day every four phases.

## How it works

- `GameClock.HourOfDay` (0..24): the real local time in real-time mode; in the in-game clock each phase covers its hours (morning 6-12, afternoon 12-18, evening 18-22, night 22-6).
- `DayNightCycle` (added to the GameClock's object by the clock itself, nothing to set up in the scene) turns that hour into a colour with a `LightingProfile` and puts it on one **global 2D light** (it uses a global light already in the scene if there is one, otherwise it creates "Day Night Light").
- Everything drawn with a **lit** material is multiplied by that colour: sprites and tilemaps with the 2D renderer's default material. The UI is not affected. A sprite using an *Unlit* material stays at full brightness.

## Changing the look

Create > Scriptable Objects > **Lighting Profile**, edit the list of *keys* (an hour 0-24 and a colour; white = full daylight), then drag the asset onto the **Profile** field of the `DayNightCycle` component (on the GameClock object in GameScene). Colours are blended in straight lines between keys and around midnight; keys can be in any order. `dawnStart / dayStart / duskStart / nightStart` only decide the names shown by the cheat menu.

Default keys: 00:00 night, 05:00 night, 06:00 pink, 07:00 gold, 09:00 day, 16:30 day, 18:00 orange, 19:30 purple, 21:00 evening blue, 22:00 night.

## Lamps and glowing windows

Add a **Light2D** (Point) as a child of a building prefab (over a window or door), add the **NightLight** component to the building, and the light fades in as it gets dark and out in the morning. `Full Intensity` is how bright it is at night; `Starts / Full At Darkness` set when it comes on.

## Testing

Game menu > **Cheats** > Time:
- *Lighting (day / night tint): on / off*
- *Look at an hour of the day*: each tap moves 3 hours on (00:00, 03:00 ... 21:00), then back to following the clock
- *Next time of day* / *Skip to next morning* as before
A line above them shows the hour and which of the four looks it is. Also saved as the player's choice: `DayNightLighting` (PlayerPrefs).

## Notes

- If the project ever gets other sorting layers, make sure the global light applies to them (Light2D > Target Sorting Layers).
- The placement ghost and road cursor are tinted by the light too.
