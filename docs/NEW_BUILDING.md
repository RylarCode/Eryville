# Adding a building, 3/4 art, open / closed animations

Short version: **Eryville > New Building...**, name it, pick a similar building to copy settings from, drop in your pictures, *Create*.

## What a building is made of

| Piece | What it is | Where it lives |
|---|---|---|
| **Building type** | name, size on the ground, costs, workers, housing, output, art settings | `ScriptableObjects/Buildings/<Name>/<Name>.asset` |
| **Animation set** | closed / open / opening / closing frames | `.../<Name>/<Name> Animations.asset` |
| **Recipes** | what it makes and uses (see `PRODUCTION.md`); empty = the old gold / wood per second fields | the type's *Recipes* |
| **Prefab** | the object placed in the world (Building, SpriteRenderer, BoxCollider2D + behaviour scripts such as ResourceProducer, Housing, Tavern) | `Prefabs/Buildings/<Name>.prefab` |
| **typeId** | unique number; saved games refer to the building by it | field on the type |
| **List** | the BuildingPlacer in GameScene (build menu and loading saves) | scene |

The wizard makes all of these. It **copies the prefab of the building you pick as the template**, so pick one that works like the new one (a Home for a house, a Gold Mine for a producer, the Tavern for a tavern): the behaviour scripts come from the prefab.

## 3/4 view: buildings that rise above the ground they stand on

The game is still top-down underneath: every building has a **size in cells** (the ground it occupies; nothing else can be built there, nobody can walk there). What changed is the **picture**, which can now be tall and drawn from the front (3/4):

- *Art style* **FitFootprint** (default; every existing building): the picture is stretched over exactly its cells.
- *Art style* **ThreeQuarter**: the picture keeps its own proportions, is as wide as the cells (or `artWidth` cells), and **stands on the cells with its bottom edge on the front edge of the footprint**. Whatever is taller than the cells rises over the cells behind (north of) it.

So when drawing the picture: the **bottom of the image is the front of the ground footprint**, and the lower part of the image should be the base of the building as wide as the footprint. The rest (roof, chimney, tower) goes above. If the picture sticks out to the sides, set *Art Width* (in cells), and use *Art Offset* to nudge it.

### Drawing order (what is in front of what)

Everything that stands on the map is sorted by height on the map:
- a **character** behind a building (further up the screen than its front edge) is **hidden by the picture**; in front, it is drawn over it;
- **roads** are always on the ground, so a road behind a tall building disappears behind its picture and comes out on the other side;
- the placement ghost, the road cursor and so on are always on top, so you can still see them behind a building.

Roads and characters can still use the ground behind the building: only the building's own cells are taken.

### Clicking

- Clicking anywhere on the **picture** (including the tall part) selects the building. Its click area covers the whole picture.
- If a character is hidden behind the building, clicking there selects the building. A character in front of the building gets the click.
- While **placing** a building or roads, a click always means the **ground cell** under the pointer. With a tall picture, a cell hidden behind a building is reached by pointing at the picture that covers it. When placing a 3/4 building, the ghost shows a **coloured square on the ground** (green = free, red = blocked) so you can see which cells it takes.
- On a phone the ghost is dragged and confirmed with the Place button, as before.

### Nuances worth knowing

- **The pivot does not matter.** Whatever pivot the sprite has (centre, bottom...), the picture is placed by its *bounds*, so its bottom edge lands on the front edge of the cells.
- **All animation frames must be the same size as the still picture** (cut them from one sheet with the same cell size): the size on screen is worked out from the still picture, so a bigger frame would be drawn bigger.
- **Doors can be at the back.** Characters may use any free cell next to the building as its entrance, including behind it, where the picture hides them (so you may not see them go in or come out).
- **The click area is a rectangle** around the whole picture, so a click on a transparent corner of a tall picture still selects the building.
- Tall pictures are cut off by nothing, but near the top edge of the map they simply hang over it.

## Open and closed animations

Name the frames of a sliced sprite sheet:

```
closed_0  closed_1 ...     looping while the building is shut
open_0    open_1 ...       looping while it is open
opening_0 opening_1 ...    plays once when it changes from closed to open (optional)
closing_0 closing_1 ...    plays once when it changes from open to closed (optional)
```

Then use the wizard, or open the animation set asset and use **Fill from sprite sheet**. Frames are read in order of their number; capitals and a prefix like `bakery_open_0` are fine. Speed and looping are per animation in the asset (default 8 fps).

**When is a building open?** (`Open Rule` on the building type)

| Rule | Open when |
|---|---|
| **Auto** (default) | a workplace: someone is working there. A home: someone is inside. Anything else: it is not night |
| Always | always |
| WorkersOnSite | at least one worker is there |
| PeopleInside | a worker or a resident is inside |
| Daytime | morning, afternoon, evening |

Fallbacks: only `open` drawn -> it is shown closed too (and the other way round); no opening / closing -> it just switches; no frames at all -> the still `sprite`.

## Dining places and storage

- **Dining Slots** (and **Meal Quality**) on the building type make it a place characters walk to for meals (see `FOOD.md`).
- **Storage Bonus** adds room for items while the building stands (a Granary).
- **Recipes**: with more than one, the building's details have a *Making: ... - tap to change* row.

## Checking

**Eryville > Check Buildings** lists the usual mistakes: duplicate typeId, no prefab / sprite / collider, a producer without `ResourceProducer`, 3/4 art whose picture is not taller than its cells, an animation set without frames, a building missing from the BuildingPlacer list.

## By hand

1. Duplicate a building type asset; give it a **new typeId**.
2. Duplicate the prefab of a similar building; assign it to `buildingPrefab`.
3. Create > Scriptable Objects > Building Animations; fill it; assign to `animations`.
4. Set `artStyle` etc. Add the type to the BuildingPlacer's `All Buildings` in GameScene.
5. Run **Eryville > Check Buildings**.
