# Seeing the UI without Unity

`tools/headless/render.sh [output folder]` draws the game's UI as PNG pictures (default folder `tools/headless/render/out`, not committed).

## How it works

1. `render/scene_ui.py` reads the canvas out of `GameScene.unity` (names, anchors, sizes, picture colours, texts) into JSON.
2. The headless town (`Sim/Town.cs`) is built with those real scene objects (`Sim/SceneUi.cs`), and the **game's own UI code runs** on it: the layout for landscape and portrait, the menus it builds in code (Build cards, Region panel, Cancel strip...).
3. `Sim/Render.cs` works out where every rectangle ends up (anchors, layout groups, content fitters, scroll masks) for several screens (`landscape-*` at 1280x720, `portrait-*` at 1080x2340) and writes JSON.
4. `render/render_ui.py` draws the JSON with Pillow.

## What it is, and what it is not

- **Real:** positions, sizes, which things overlap, which menu covers what, colours and texts as the game sets them, the scene's canvas, and **the project's own pictures** (building, character and road sprites are cut out of `Assets/_Project/Art` by their asset ids; the grass tile, roads, the Map's tint and fog, and the placeholder trees / rocks / rubble the game draws itself).
- **Not real:** the font (Liberation Sans is used, close to Unity's default, so text can wrap a little differently and auto-sizing is approximate), anything animated (frames, walking, day / night light), lighting and shadows, characters in the world are drawn as their still picture about one cell tall (no animation, no body scale tuning), 3/4 building art (all buildings are drawn flat over their cells), the panels' own background pictures (rounded boxes), and the scene's hand-wired button clicks (the render opens menus the way the buttons would).
- Stand-in numbers: gold 1460 / 5000, wood 50 / 500, letters 2, the project's real building types (names, sizes, costs, pictures) placed on the Town Square for the `*-world` screens.
- It cannot tell you how something *feels*. Comparing with Unity is how to find where it is wrong; tell Claude what differs and it gets fixed in the renderer.

Add a screen: add a `Scenario` in `Sim/Render.cs` (screen size and a few lines that open it).
