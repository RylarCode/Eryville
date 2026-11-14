# The Map and its regions

The **Map** is the ground the town is built on: a grid of cells (today 57 x 29). Its land is divided into **regions**. At the start only the Town Square is **cleared** (usable); the rest is **locked** under **fog**. A region can be cleared once it touches a cleared region along an edge (corners do not count). Only cleared land can have buildings and roads, and only cleared land can be walked on.

## What is built so far

**Step 1: regions and the rules.** The image and assets, which land is usable, the fog, saving, and cheats to try it.

**Step 2: seeing and choosing.** Tap a region you can see to open its **Region panel** (name, terrain, what it is, size, and for land you can clear: cost, characters, time, what it gives). Locked land is dotted with **decorations** (trees, rocks, rubble), and a special region can have **its own picture**.

**Step 3 (this): clearing.** The Region panel has a **Clear** button: it takes the gold at once and starts a job; you then put characters on it from the jobs list, and while they are at work the region is cleared over time (also while the game is closed). **Not built yet:** Stone as a proper item, story and quest hooks, the editor tools. See "Planned" at the end.

## Clearing a region

1. Tap a region you can clear (amber tint). If you have the gold, the panel has **Clear <region> for N gold**; press it. The gold is taken at once (it is **not refunded** if you stop). If you are short, the panel says how much is missing; if no free ground touches the region (everything beside it is built over), it says so.
2. A job now exists ("Clearing: 0% done, 0 of 2 places filled"). Open a character, press **Assign**, and pick **Clear <region> (0/2) 0%** from the jobs list (the list now ends with the regions being cleared, after the buildings).
3. In work hours the character's schedule sends them there like to any workplace: they walk to the edge of the region, go "inside" (out of sight) and work. At night they go home and the job stands still. The panel shows the progress and the time left.
4. When it reaches 100% the region is **cleared** (fog, tint and decorations go), its **yields** are added to the stock, the characters are free again, and a toast says so.

**How fast.** A region's **Clear Seconds** is how long it takes with **every place filled by an ordinary character** (productivity 1). Each character at work adds their productivity (hunger and Connection buffs count, like at a building) multiplied by their **Clearing specialty** (a Specialty on the character type with the tag **`Clearing`**, the same thing as being good at a recipe), and the total is shared over the characters the region needs. So half the places filled = half speed, and a character who is twice as good at clearing counts twice. (Nobody has the specialty yet; the planned Gold Digger could.)

**While the game is closed** the job goes on the same way as production: for each stretch of the away time, every paid character on the job whose schedule says "work" at that real time of day does their share (at most a day counts). A job can finish while you are away and gives its yields; the toast is there when you return.

**Taking a character off a half-cleared region.** Picking another job for them in the jobs list asks first: the list shows "Firsy is 12% through clearing North Rocks. Moving them keeps the progress but the gold is not refunded. Pick a job again to confirm." Picking again moves them. A region with no progress yet just moves them. The job keeps its progress and waits for new characters.

**Other ways a region gets cleared** (a cheat, later the story) drop its job and free the characters (no yields).

**The clearing site.** Each job is an invisible, one-cell workplace on the region's edge beside free cleared ground (the cell with the most free ground around it). It is not one of the town's buildings (not listed, saved or built), but characters treat it as any workplace, so the schedule, walking, "inside", wages and time away all work as they do for buildings. If you later build over all the ground beside it, the region can still be finished only once there is a way in again.

**Cheats** (Cheats > Map): **Finish every region being cleared now**; "Lock every region" also drops jobs.

**Saving** (save version 12): the jobs and their progress. The characters' jobs are saved like any other (the site's id is `clear:<region id>`). Older saves have no jobs.

## Tapping a region

- Tap (or click) land that is **visible but not cleared** (the amber tint, or a river): the Region panel opens in the details' place. Tapping cleared land or fog does nothing. Tapping the ground again closes it; tapping another region opens that one.
- A **drag** (moving the camera) is not a tap: it only counts when the finger comes up within about 20 pixels of where it went down.
- The panel is not offered while placing a building or road, or while another menu is open.
- For land you cannot reach yet there is nothing to tap (it is under fog).

## Decorations

- Every visible, uncleared region (except rivers) is dotted with small pictures of its terrain: **trees on a forest, boulders on rocks, rubble on debris** (a mountain and a quarry use boulders). Each cell has one with a chance (forest 60%, mountain 50%, rocks 40%, debris 35%) at a random spot in the cell, a random picture of the pool and a slightly random size. It is **the same every time**: the spots come from the cell's own position, so it does not change between visits or after loading. Clearing the region removes them.
- **Until you draw some, simple placeholder shapes** drawn by the game stand in (a green tree, a grey boulder, planks).
- **Your pictures**: make a **Region Decor Set** (Assets > Create > Scriptable Objects > Region Decor Set) in `Resources/Regions`. One pool per terrain: the sprites (draw them standing, the base at the bottom centre, see `docs/ART_GUIDE.md`), the density and the size range. A terrain without a pool keeps the placeholders. The game uses the first set it finds.
- **A special region's own picture**: on the Region asset, **Picture** (shown while it is locked, instead of the scatter) and **Cleared Picture** (shown once cleared); both stand with their bottom centre at the bottom centre of the region, moved by **Picture Offset** (in cells).
- Decorations are sorted by height like buildings, so a character walking behind a tree is hidden by it.

## How a region is drawn

1. **The image** `Assets/_Project/Resources/Regions/RegionMap.png`: one pixel per cell (57 x 29 for the current Map; its lower left pixel is the Map's lowest cell). Each region is a flat colour. **A transparent pixel is no region**: the Map's border, always unusable.
2. **A Region asset** per colour: Eryville > New Region (or Assets > Create > Scriptable Objects > Region), in `Resources/Regions`. It says which colour is which region and what the region is.

The game finds both by their place (`Resources/Regions`): nothing to set up in the scene.

### Drawing it in Krita

- New image, **57 x 29 px** (the same size as the Map), transparent background.
- Draw with the **Pencil / a hard, aliasing brush with antialiasing off**, or fill with the Fill tool with *Grow/Feather = 0* and *Anti-aliasing* off. The colours must be **exact**: no soft edges, no blending, no gradients. (A stray blended pixel is reported in the Console as a colour no region has, and that cell is unusable.)
- One flat colour per region; leave the border cells transparent.
- Export as **PNG** over `RegionMap.png` (keep RGBA). Unity sets the import settings by itself (readable, uncompressed, no smoothing; see `Editor/RegionImageImporter.cs`).
- If you change the Map's size, make the image the same size, and keep the lower left corner at the Map's lowest cell.

### The Region asset

| Field | What it does |
|---|---|
| **Id** | Short unique name used by saves and stories (`west-forest`). Empty = the asset's name. Do not change it once players have saves. |
| **Display Name / Description** | What the player reads. |
| **Terrain** | Forest, Rocks, Debris, Mountain, River, Quarry. A **River** can never be cleared (it blocks the way until there is a bridge). A **Quarry** is cleared land where a quarry can be built (later). |
| **Color** | The region's colour in the image. Must be unique and exactly what is painted. |
| **Starts Cleared** | Cleared in a new game (the Town Square). At least one region must. |
| **Gold Cost / Characters Needed / Clear Seconds / Yields** | What clearing costs and gives (used from step 3: the fields are there so the assets do not change). |

## The rules

- Land starts locked. Regions that **start cleared** are cleared in a new game.
- A region is **revealed** (visible) when it is cleared or touches a cleared region **along an edge**. Everything else, and the Map's border, is under **fog**: very dense, nearly opaque.
- A revealed region that is not cleared and not a river **can be cleared**: it is shown with an amber tint. A revealed river is tinted blue-grey.
- **Buildings** need every cell of their footprint on cleared land. **Roads** too. **Walking** is blocked on locked land.
- Costly regions (a Mountain) make the town grow around them: clear what is next to what you have, in any order.
- With no region image and no Region assets the Map has no regions and all land is usable (as before).

## Saving

The ids of the cleared regions are saved (save version 11), and the regions being cleared with their progress (version 12). An older save starts with the Town Square cleared **plus whatever land its buildings and roads stand on**, so nothing already built is lost. A save that clears nothing known still starts with the Town Square.

## Trying it

Game menu > Cheats > **Map**: how many regions are cleared; **Clear every region**; **Lock every region except the start**; **Regions: ON / OFF** (off = all land usable and no fog, handy while clearing itself is not built).

## The sample map

The shipped image and nine sample regions (all placeholders: names, costs and layout are yours to change): Town Square (start, middle), West Forest, North Rocks, South Debris, East Meadow, The Pass, River (cannot be cleared), East Fields and Mountain (very costly). Every region that can be cleared can be reached from the Town Square. `tools/headless` checks that the image and assets agree (sizes, colours, reachability).

## Planned (the next steps)

2. ~~Seeing and choosing~~ (built; the Clear button comes with step 3).
3. ~~Clearing~~ (built).
4. **Stone**: the new material, building costs that use it.
5. **Story and quests**: conditions such as "region X cleared" and "N regions cleared", and effects that clear a region.
6. **Tools**: Eryville > New Region and Check Regions.

## How it works (for changing the code)

- `Region` (asset), `RegionMap` (pure rules: cells, neighbours, cleared, revealed, usable, saving; tested exactly), `MapRegions` (finds the assets and image, keeps the state, draws the fog on a `RegionOverlay` tilemap, tells the Map).
- `ClearingManager` (jobs, progress, time away, saving; added to the Map's object by `MapRegions`): each job is a `ClearingJob` with a runtime `Building` as its site (not registered with `BuildingManager`; `Building.Id` is `clear:<region id>`), so `Character`, `PathService`, the jobs list and `OfflineProgress` need no special cases. The jobs list and the second-press question are in `UIManager.FillAssignList` / `ChooseJob`; the Clear button is in `RegionMenu`.
- `RegionDecor` (pure: where decorations go, `PlaceholderDecor` the stand-in pictures), `RegionDecorSet` (the asset with your pictures), `MapRegions` draws them on a `RegionDecor` object and answers "which region is under this point"; `RegionMenu` (UI) is the panel and `UIManager` decides what is a tap.
- `MapGrid.IsUsable(cell)` is the one question everything asks: placing (`BuildingManager.CanPlace`), roads (`RoadManager.CanBuild`) and walking (`MapGrid.IsBlocked`, which paths use). Clearing a region bumps the Map's version, so saved paths start over.
