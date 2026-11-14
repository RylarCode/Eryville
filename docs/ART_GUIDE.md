# Art guide: what to draw, how big, and how it is used

Everything an artist needs in one place: every picture the game uses, what it should look like, its size and shape, how it is used on screen, and how to get it into Unity. Facts here come from how the game really works (sizes, anchors, rules); the **Decisions** at the end are things only you can choose. Keep this file up to date when a new kind of art is needed (see `CLAUDE.md`).

Names used for things in the game are in `docs/NAMING.md`. The step-by-step of getting art into Unity is in `docs/NEW_CHARACTER.md`, `docs/NEW_BUILDING.md`, `docs/MAP.md` and `docs/PRODUCTION.md`; this file says what to draw.

## 1. The ground rules (read this first)

### The scale of the world
- The Map is a grid of **cells**; **1 cell = 1 world unit**. Buildings are 2x2 to 5x5 cells; a character stands about **1 cell** tall.
- The camera shows **about 10 cells from top to bottom** at the default zoom (the player can zoom). So a cell is about **108 px** on a 1080p desktop screen and about **230 px** on a tall phone (2340 px high, portrait). Art is drawn bigger than it is shown and scaled down, so draw big enough to stay sharp on a phone.

### The master resolution
**Draw at 256 px per cell** (a 2x2 building is 512 x 512, a 3x3 is 768 x 768). That is sharp on every screen. **128 px per cell** is acceptable if you are happy with slight softness on tall phones. Pick one and keep **all** art at the same pixels-per-cell, so nothing looks sharper or blurrier than its neighbours.

Characters are the exception: they are sized in game by a **Body Scale** number, so draw a character at whatever size is comfortable (the existing ones are 360 and 500 px) and keep every frame the same size. A character should look about **1 cell tall**, about the size of the existing villager.

### Import settings in Unity
Dropping a PNG into `Assets/_Project/Art/...` is enough, then set:
- **Pixels Per Unit: 100** (what every current sprite uses; the game scales pictures to the cells, so this mostly does not matter, but keep it consistent).
- **Texture Type: Sprite (2D and UI)**. For a sheet of frames: **Sprite Mode: Multiple**, then slice it (see below). Otherwise Single.
- **Filter Mode:** current art uses *Bilinear* (smooth). For crisp pixel art choose *Point*. Decide once for the whole game (see Decisions).
- **Compression:** None, or Normal Quality. **Alpha Is Transparency: on.** **Generate Mip Maps: off.**
- Max texture size: keep a single sheet to **2048 x 2048** or smaller (older phones); 4096 is fine on modern ones.

### In Krita (or any painting tool)
- **Transparent background**, export as **PNG (RGBA)**, colour profile **sRGB**.
- **Draw in daylight colours.** The game tints the whole world for dawn, dusk and night (`docs/DAY_NIGHT.md`), so do **not** paint night into art. Lit windows are the exception: they go in a building's "open" frames (below).
- **Light and shadow** are a style decision (see Decisions). Whatever you choose, use the same light direction everywhere.
- No picture should touch the edge of its canvas unless it is meant to tile: leave a few transparent pixels of margin.

## 2. Characters

Each character needs four pictures (the first is the only one that is strictly required):

| Picture | Where it shows | Size and shape |
|---|---|---|
| **Small sprite** | The character in the world, standing; also the fallback for everything else | One frame, same size as the animation frames. Facing the camera. |
| **Animation sheet** | Walking, standing, going into and out of buildings, eating | Frames in a grid, **all frames the same size** (see below) |
| **Portrait** | The recruit screen, the details panel, **story dialogue** | **Tall, about 2:3** (e.g. 800 x 1200), transparent, character from head to waist or full body. In the story dialogue it stands on the **left** of the screen next to the text, so draw the character **facing right** (toward the text). |
| **Face icon** | Lists, building occupants, notifications, hire cards | **Square, 128 x 128**, just the face, centred, readable at 40 px |

### The animation sheet
- **Directions:** only **down, up, left, right**. Nothing is mirrored by the game, so **left and right must both be drawn**. A direction you do not draw falls back to the first one you did (a character with only "down" frames always looks forward).
- **Frames:** 4 frames per direction is a good walk (left foot, passing, right foot, passing); the game plays **8 frames per second**, so a 4-frame walk loops twice a second. 2 is a simple bob, 6 to 8 is smoother. Frame counts may differ between animations.
- **Every frame in a sheet has the same canvas size** (e.g. 256 x 256) and the character's **feet are in the same place in every frame**; otherwise the character jitters while walking. Leave margin so a raised arm is not cut off.
- **Slice with "Grid By Cell Size"** in Unity's Sprite Editor (not Automatic, which trims each frame to a different size and makes the feet jump).
- **Naming:** `<animation>_<direction>_<frame>`: `walk_down_0`, `walk_down_1`, `idle_left_0`...

| Animation | Needed? | What it is |
|---|---|---|
| `idle` | Nice | Standing still (1 frame is fine, 2 to 4 for breathing) |
| `walk` | **Yes** (4 directions) | The ordinary walk |
| `walk_happy` / `walk_sad` | Optional | Walk when mood is high (70+) or low (30 or below). Fall back to `walk`. |
| `enter` (facing up) / `exit` (facing down) | Optional | Going into / coming out of a building door. Skipped if missing. |
| `eat` | Optional | Eating at a dining place |

Order to draw in: small sprite, portrait, face, then `walk_down`, the other three walk directions, idle, and the rest.

### The people to draw
Firsy, Condo, Baker, Fisherman, Farmer (all exist as placeholders), and the Gold Digger you planned. Every character that is **pulled** (gacha) needs all four pictures; story characters need a good portrait most of all, since that is what the dialogue shows.

## 3. Buildings

A building has a **footprint in cells** (the ground it occupies) and a picture. Footprints today:

| Size | Buildings |
|---|---|
| 2 x 2 | Home, Lumberyard, Mill, Bakery, Fishery, Kitchen, Granary |
| 3 x 3 | Gold Mine, Farm, Orchard, Tavern |
| 5 x 5 | Town Hall |

### The two ways to draw a building
**Top-down** (`FitFootprint`, every building today): a picture stretched over exactly its cells. Draw it **square for a square footprint**: 2x2 = 512 x 512, 3x3 = 768 x 768, 5x5 = 1280 x 1280 at 256 px per cell. Seen from straight above, like a map.

**3/4 view** (`ThreeQuarter`): drawn from the front and slightly above. The picture keeps its own shape and **stands on the footprint**:
- The **bottom edge of the image is the front edge of the footprint**, and the **lower part of the picture must be the base of the building, as wide as the footprint** (the walls, the door). The roof, chimney and anything taller go above that and cover the cells behind.
- The picture is as wide as the footprint (e.g. 3 cells = 768 px); if the picture sticks out at the sides (a wide roof), tell Claude the width in cells (the `Art Width` setting).
- Draw the picture **tall**: e.g. a 3x3 building might be 768 x 1100 px. There is no height limit; it just covers more of what is behind it.
- **Optional top-down version** of the same building (same footprint size), used when the player flattens the Map with the **View** button or while placing. Without it the 3/4 picture is squashed flat.
- Characters walking behind a tall building are hidden; they show as a faint outline.

### Animations (all optional)
Every building can show different pictures while **closed** and **open**. Open means, depending on the building: someone is working there, someone is resting inside, or it is night (so lit windows, smoke, an open door). Name frames of a sliced sheet:

| Name | Meaning |
|---|---|
| `closed_0`, `closed_1`... | Shut: loops (dark windows) |
| `open_0`, `open_1`... | Open for business: loops (lit windows, smoke) |
| `opening_0`... | The change from closed to open, plays once |
| `closing_0`... | The change from open to closed, plays once |

All frames of a building are the **same size** as its still picture. Missing animations are skipped (open falls back to closed and the other way round). Painted lit windows are fine in `open`; extra glow at night is added by the game with a light, not painted.

### The build menu card
Each building also appears as a small **card in the Build menu** (the picture above its name, at roughly 90 px wide). So the building must **read clearly small**: a simple, strong silhouette beats fine detail.

### The buildings to draw
All 12 above (Home, Gold Mine, Lumberyard, Farm, Mill, Bakery, Fishery, Orchard, Kitchen, Granary, Tavern, Town Hall) exist as placeholders. Priority: the ones every player sees first (Town Hall, Home, Gold Mine, Lumberyard, Tavern).

## 4. The Map

### Ground
- **Grass tile**: one **seamless, tileable** picture for **one cell** (256 x 256). It is repeated across the whole Map, so edges must match on all four sides and it must not have an obvious pattern when repeated. Variants (a few that tile together) would make the ground look natural; today the Map uses one tile.
- The ground is drawn in daylight colours like everything else.

### Roads
- **Dirt Road**: a picture for **one cell** (256 x 256), repeated along the road. Today there is **no automatic joining**: every cell shows the same picture whatever its neighbours, so design it as a **seamless tile that works next to itself in every direction** (a plain worn-earth texture works; corners and junctions are not supported yet). A road kind with a different look (stone, later) is another one-cell picture.

### Grid lines
Shown while placing buildings: a **32 x 32 px** tile with a thin line, **Point** filter, pixels per unit 32 (the existing one is fine; only redraw it to restyle).

### Regions (the Map's hand-drawn parts, `docs/MAP.md`)
- **The region image** is a **57 x 29 px** picture (one pixel per cell) where each region is a flat, exact colour; transparent = the Map's border. It is a *layout diagram*, not art: the details are in `docs/MAP.md`. Draw it with antialiasing **off**.
- **Fog** covers locked land. It is plain dark colour today. If you want fog art later: a **dark, nearly opaque, seamless one-cell tile** (clouds or mist) so the land underneath is not visible at all.
- **Decorations on locked land** (built: until you draw some, simple shapes stand in; see `docs/MAP.md` for hooking yours up with a Region Decor Set): a pool of small pictures for each wild terrain, scattered at random and removed when the region is cleared:

| Terrain | Pictures to draw | Size |
|---|---|---|
| Forest | 6 to 8 different trees and bushes | 0.8 to 1.5 cells tall, e.g. 200 to 380 px |
| Rocks | 6 to 8 boulders and rock piles | 0.5 to 1.2 cells |
| Debris | 6 to 8 pieces of rubble, planks, broken carts | 0.4 to 1 cell |

  Draw each decoration **standing, in the same 3/4 view as the buildings**, **transparent background**, with the **base (the bottom of the trunk or rock) at the bottom centre of the image** (they are sorted front to back by that point). Several variants of each, so the random scatter does not look repeated.
- **Special regions** (a mountain, a ruin): one big hand-drawn picture per region, placed by you. Draw it as wide as the region and in the same view, with a transparent background; tell Claude which region and where, and whether you want a different "cleared" picture. Rivers and quarries (later) are drawn the same way, or as ground tiles for water.

## 5. Items (icons)

One **square icon per item**, **128 x 128**, transparent background, one centred object, a strong silhouette that reads at **32 px** (the smallest use). They appear in the resource bar, the Stock tab, notifications, the build and recipe lists, and as the **food bubble above a character** while eating (about half a cell wide). Today items show plain coloured squares.

Items: **Gold, Wood, Letters** (recruitment letters; maybe an envelope), **Wheat, Flour, Bread, Fish, Fruit, Pie, Grilled Fish**, and **Stone** (new). Keep them in one style, and make the food readable as food.

## 6. Interface

Most of the interface is drawn by the game in plain colours (menus, toasts, the dialogue box, lists), so it needs **no art to work**. If you want to restyle it, these are the pieces; none are required:

| Piece | Spec |
|---|---|
| **Panel background** (menus and details) | A **9-sliced** rounded frame, e.g. 256 x 256 with a 48 px border, flat or lightly textured so text on it stays readable |
| **Button** | 9-sliced, normal and pressed looks, about 192 x 96 |
| **Toast** | Uses the item icon or character face; a coloured bar behind (the game picks colours: blue info, green success, amber warning, red alert) |
| **Dialogue box** | A 9-sliced bar for the bottom of the screen; the portrait stands beside it |
| **Fonts** | Not pictures, but decide one readable font (TextMeshPro font asset); text must stay legible at about 16 px on a phone |

### The title screen
- **TitleBackground** and **TitleLogo** are found by name in any `Resources` folder.
- The background is **stretched to fill the whole screen** (no cropping), so it **distorts on a different aspect ratio**. Until that is changed, draw it for the screen you care about most (phone portrait 1080 x 1920, or desktop 1920 x 1080), or ask Claude to change it to crop instead of stretch so one square-ish picture (e.g. 2160 x 2160 with the important part in the middle) works everywhere.
- The logo is a **transparent PNG about 1200 x 400**, drawn wider than tall. It is shown in the upper middle (the middle 80% of the width, the upper third) and keeps its shape.

## 7. App and store art (for Google Play)

| Asset | Size | Notes |
|---|---|---|
| **App icon** | **1024 x 1024** master | Unity makes the sizes. For Android's adaptive icon, keep the important part inside the **middle 66%** (it is cropped to a circle or squircle). A separate background and foreground layer (each 1024 x 1024) is best. |
| **Play Store icon** | **512 x 512** PNG | Required by the store |
| **Feature graphic** | **1024 x 500** | Required by the store |
| **Screenshots** | At least 2, phone, e.g. **1080 x 1920** (portrait) | Take them from the game; the store shows them in a row |

## 8. Everything, in the order to draw it

| # | Asset | How many | Status |
|---|---|---|---|
| 1 | Characters (small sprite, portrait, face, walk sheet) | 6 | Placeholders (Firsy and Condo have sprites; no animations yet) |
| 2 | Buildings (top-down, or 3/4 with the base on the footprint) | 12 | 3 placeholders (Home, Gold Mine, Lumberyard), the rest plain |
| 3 | Grass tile, dirt road tile | 2 (plus variants) | Placeholders |
| 4 | Item icons | 11 | Plain coloured squares |
| 5 | Region decorations (trees, rocks, debris) | about 20 | Placeholder shapes drawn by code; ready for your pictures |
| 6 | Special region pictures (mountain...) | as needed | Not started |
| 7 | Building animations (closed / open) | optional, per building | None yet |
| 8 | Character animations beyond walking | optional | None yet |
| 9 | Title screen background and logo | 2 | Generated gradient and lettering |
| 10 | App icon, store icon, feature graphic, screenshots | 4 kinds | None |
| 11 | Interface styling (panels, buttons, dialogue box, font) | optional | Plain colours |

## 9. Decisions only you can make

1. **Style:** smooth painted look (Bilinear) or crisp pixel art (Point filtering)? It changes the import settings and the master resolution (pixel art is usually drawn much smaller, e.g. 32 or 64 px per cell, and scaled up).
2. **Pixels per cell:** 256 (recommended), 128, or a pixel-art size.
3. **Light and shadow:** which direction the light comes from (top-left is common), and whether art has a baked contact shadow (a soft blob under characters and buildings) or the game draws one.
4. **Outlines:** dark outline around everything, coloured outlines, or none.
5. **Building view:** top-down for everything, or 3/4 for everything? (Both work together; mixed is fine but looks best if decided.)
6. **Palette:** a limited set of colours for the whole game, which keeps the many pictures looking like one world.
7. **Title screen:** stretch (as now) or crop-to-fill.
