# Headless checks (no Unity needed)

Checks the game's code without opening Unity: a **compile check** of every script (game and editor), and a **simulation** that runs the game's real scripts on stand-ins for Unity and checks how they behave.

```
tools/headless/run.sh              # everything
tools/headless/run.sh Quests       # only the suites whose name contains "Quests"
tools/headless/run.sh -v           # also print each section
```

Needs the .NET 8 SDK (`dotnet`). Unity does not see this folder (it is outside `Assets`), and the build output (`bin/`, `obj/`) is ignored by git.

## What is here

| | |
|---|---|
| `Stubs/Runtime/` | Stand-ins for the Unity API the game uses: objects and components with Unity's lifecycle (Awake, OnEnable, Start, Update, OnDestroy, coroutines), transforms, math, sprites, colliders and 2D physics, the camera, grids and tilemaps, uGUI and TextMeshPro, the Input System, scenes, JsonUtility, PlayerPrefs, Resources. They behave like Unity where the game relies on it (destroyed objects equal null at the end of the frame, `Instantiate` copies serialized fields and gives each copy its own UnityEvents, inactive objects are not updated...). |
| `Stubs/Runtime/PlayerInputActions.cs` | Stands in for the generated input actions class: the same maps and actions, which tests fire with `Perform` / `Cancel`. |
| `Stubs/Editor/` | Empty stand-ins for the editor API, only so the editor scripts compile. Editor tools can only be tried in Unity. |
| `Check/` | The compile check: every script in `Assets/_Project/Scripts`, editor ones included. |
| `Sim/` | The simulation: `Harness.cs` (the runner: suites, checks, a clean slate before each suite), `Town.cs` (a small GameScene: managers, grid, camera, input, a canvas with the panels the UI looks for by name, a few building and character types), `Tests/` (the suites, listed in `Tests/Suites.cs`). |

## Driving the game in a test

- `Town t = Town.Build();` makes the scene and runs every `Start`.
- `World.Tick(seconds)` runs one frame; `World.Run(seconds)` runs many. `World.EndFrame()` finishes destroying what was destroyed.
- Time of day: the town uses the game's own clock (`t.clock.Advance()` moves to the next phase).
- Clicking: `Town.Click(transform)` presses a button; `t.ClickWorld(position)` clicks the map (what is under the pointer is found by its collider).
- Lists and menus: `t.PanelRows("QuestPanel")`, `Town.RowStartingWith(t.Content("QuestPanel"), "Claim:")`.
- Assets the game loads from `Resources` can be registered with `Resources.Add(path, asset)`.
- An exception in a game script stops the suite and is reported (`World.ThrowExceptions`).

## Adding tests

Add a static method to a file in `Sim/Tests/` (or a new file), list it in `Sim/Tests/Suites.cs`, and use `T.Section("...")` and `T.Check(condition, "what should be true")`. Each suite starts from an empty world, so it builds what it needs. When a change to the game needs a Unity API the stand-ins do not have yet, add it to `Stubs/` (the compile check says what is missing).

Not covered (only Unity can show these): how things look, real touch input, the editor tools, performance on a phone.
