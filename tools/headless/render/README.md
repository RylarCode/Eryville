# Seeing the UI without Unity

`tools/headless/render.sh [output folder]` draws the game's UI as PNG pictures (default folder `tools/headless/render/out`, not committed).

## How it works

1. `render/scene_ui.py` reads the canvas out of `GameScene.unity` (names, anchors, sizes, picture colours, texts) into JSON.
2. The headless town (`Sim/Town.cs`) is built with those real scene objects (`Sim/SceneUi.cs`), and the **game's own UI code runs** on it: the layout for landscape and portrait, the menus it builds in code (Build cards, Region panel, Cancel strip...).
3. `Sim/Render.cs` works out where every rectangle ends up (anchors, layout groups, content fitters, scroll masks) for several screens (`landscape-*` at 1280x720, `portrait-*` at 1080x2340) and writes JSON.
4. `render/render_ui.py` draws the JSON with Pillow.

## What it is, and what it is not

- **Real:** positions, sizes, which things overlap, which menu covers what, colours and texts as the game sets them, the scene's canvas.
- **Not real:** pictures (sprites are plain boxes), the font (Liberation Sans is used, close to Unity's default, so text can wrap a little differently and auto-sizing is approximate), the game world behind the UI (flat green), anything animated, and the scene's hand-wired button clicks (the render opens menus the way the buttons would).
- Stand-in numbers: gold 1460, wood 50, letters 2, the project's real building types (names, sizes, costs) but no pictures.
- It cannot tell you how something *feels*. Comparing with Unity is how to find where it is wrong; tell Claude what differs and it gets fixed in the renderer.

Add a screen: add a `Scenario` in `Sim/Render.cs` (screen size and a few lines that open it).
