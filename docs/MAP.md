# The Map and its regions

The **Map** is the ground the town is built on: a grid of cells (today 57 x 29). Its land is divided into **regions**. At the start only the Town Square is **cleared** (usable); the rest is **locked** under **fog**. A region can be cleared once it touches a cleared region along an edge (corners do not count). Only cleared land can have buildings and roads, and only cleared land can be walked on.

## What is built so far

**Step 1 (this): regions and the rules.** The image and assets, which land is usable, the fog, saving, and cheats to try it. **Not built yet:** clicking a region, paying to clear it, characters clearing it over time, Stone, story and quest hooks, decorations (the fog and tint are plain colours for now). See "Planned" at the end.

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

The ids of the cleared regions are saved (save version 11). An older save starts with the Town Square cleared **plus whatever land its buildings and roads stand on**, so nothing already built is lost. A save that clears nothing known still starts with the Town Square.

## Trying it

Game menu > Cheats > **Map**: how many regions are cleared; **Clear every region**; **Lock every region except the start**; **Regions: ON / OFF** (off = all land usable and no fog, handy while clearing itself is not built).

## The sample map

The shipped image and nine sample regions (all placeholders: names, costs and layout are yours to change): Town Square (start, middle), West Forest, North Rocks, South Debris, East Meadow, The Pass, River (cannot be cleared), East Fields and Mountain (very costly). Every region that can be cleared can be reached from the Town Square. `tools/headless` checks that the image and assets agree (sizes, colours, reachability).

## Planned (the next steps)

2. **Seeing and choosing**: tap a region for its details (name, terrain, cost, time, what it needs) and a **Clear** button; random decorations (trees, rocks, debris from sprites you draw) on locked land, and your own picture for special regions.
3. **Clearing**: pay the gold, assign characters through the jobs list, progress over time (also while the game is closed), specialties and a preferred job make characters better at it, regions give their yields when done. Taking characters off a half-cleared region asks for a second confirmation; progress is kept, gold is not refunded.
4. **Stone**: the new material, building costs that use it.
5. **Story and quests**: conditions such as "region X cleared" and "N regions cleared", and effects that clear a region.
6. **Tools**: Eryville > New Region and Check Regions.

## How it works (for changing the code)

- `Region` (asset), `RegionMap` (pure rules: cells, neighbours, cleared, revealed, usable, saving; tested exactly), `MapRegions` (finds the assets and image, keeps the state, draws the fog on a `RegionOverlay` tilemap, tells the Map).
- `MapGrid.IsUsable(cell)` is the one question everything asks: placing (`BuildingManager.CanPlace`), roads (`RoadManager.CanBuild`) and walking (`MapGrid.IsBlocked`, which paths use). Clearing a region bumps the Map's version, so saved paths start over.
