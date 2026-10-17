# Eryville architecture (current)

A 2D city-building / gacha game (Unity 6, URP, new Input System). Everything below is as of branch `first-cleanup`.

## 1. The big picture

```
        ScriptableObject templates              Prefab shells
 BuildingType / CharacterType / RoadType  ->  Building / Character prefabs
 GachaPool                                           |
            \                                        v
             \                     +---------- one scene: GameScene ----------+
              \                    |  managers (single sources of truth)      |
   (data, edited in the inspector) |  BuildingManager  CharacterManager ...   |
                                   +------------------------------------------+
                                        |  events                 ^  commands
                                        v                         |
                              UIManager + menus  <--- InputManager / touch ---- player
                                        |
                                        v
                               Backend (IGameBackend): saves, server time, pulls, letters
```

Rules the code follows:
- **One owner per fact.** Which buildings exist: `BuildingManager`. Who is owned: `CharacterManager`. What is on each map cell: `MapGrid`. Everything else asks them.
- **Data vs. body.** A character is an `OwnedCharacter` (plain C#: stats, job, home). Its visible body is a `Character` MonoBehaviour (the "avatar"), which may not exist.
- **Templates by id.** Saves refer to types by `typeId` and to things by `Id`, never by object.
- **Events, not polling,** where something changes rarely (`TimeOfDayChanged`, `BuildingMoved`, `RosterChanged`...).

## 2. What is in GameScene

Root objects (all in `Assets/_Project/Scenes/GameScene.unity`):

| Object | Components | What it is |
|---|---|---|
| **GameManager** | `GameManager`, `BuildingPlacer`, `BuildingManager`, `ResourceManager`, `CharacterManager`, `GameClock`, `MapGrid`, `PathService`, `RoadManager`, `RoadPlacer`, `MoodSystem`, `WageSystem`, `GachaSystem` | All the game logic. One object, one component per system. |
| **UIManager** | `UIManager` | Owns every menu and panel (see 4). |
| **UICanvas** | Canvas, scaler, raycaster | The scene's UI: resource text, Build / Hire / Menu buttons, the build and hire panels, the details panel (`CharacterPanel` with its `AssignPanel`), the game menu (`MenuPanel`: Save / Load / Quit). |
| **InputManager** | `InputManager` | Turns clicks/taps into events. |
| **Main Camera** | Camera, `CameraController` | Pan / zoom (mouse) and, added at runtime, `TouchCameraControls`. |
| **Grid** (+ tilemaps) | Grid | Cell size and the background / gridlines tilemaps. Roads add their own tilemap here at runtime. |
| **EventSystem** | EventSystem, Input System UI module | UI clicks. |
| **Global Light 2D** | Light | Lighting. |

Created at runtime (nothing to add to the scene):
- Town, Recruit and Cheat panels and the Town / Recruit buttons and the Cheats button (cloned from existing buttons); the Move button (cloned from Assign); the Place / Cancel strip (phone layout).
- **PerfOverlay** (+ `StressTest`): the frame-time counter, in the editor and Development Builds only.
- **GameSession**: loads and autosaves the game (real-time clock only), survives scene changes.
- Buildings, characters (avatars), roads, the placement ghost and the road cursor.

## 3. The scripts, by folder (`Assets/_Project/Scripts`)

**Core/** - time, resources, saving
- `GameManager`: `currentGameState` (Normal / Menu), `worldState` (Peace / War), `SimulationPaused`.
- `GameClock`: the time of day. Two modes: *InGame* (30 s per phase, pauses in menus) or *RealTime* (follows the real clock, never pauses). Raises `TimeOfDayChanged`.
- `ResourceManager`: Gold, Wood, Letters; income tick once a second (sums every building's producer).
- `WageSystem`: pays every character's daily wage; unpaid characters refuse to work.
- `SaveSystem` + `SaveData`: builds a `GameData` (version 5) from the managers and rebuilds the world from one. It does no file work itself: it hands text to the backend.

**Gameplay/Characters/ (art)** - see `docs/NEW_CHARACTER.md`
- `CharacterAnimationSet`: a ScriptableObject of flip-book animations (idle, walk, walk happy, walk sad, enter building, exit building), each drawn in four directions (`DirectionalAnimation`, no mirroring), with fallbacks.
- `Facing` (Down/Up/Left/Right): characters only walk along axes (the pathfinder uses 4 directions and `CharacterMovement` goes x first, then y), so these are all the directions there are.
- `AnimationNaming`: fills a set from sprite names like `walk_down_0`. `CharacterChecks`: finds the usual mistakes in character types.
- `Scripts/Editor/`: *Eryville > New Character...* (`NewCharacterWizard`), *Check Characters*, and the "Fill from sprite sheet" box on animation sets.
- `AnimationPlayer` (pure frame logic) and `CharacterAnimator` (drives the SpriteRenderer: mood-based walk, mirroring, speed-scaled playback, enter/exit one-shots that `Character` waits for).
- `CharacterType` also holds `portrait` (full image) and `face` (small icon); both fall back to `sprite`.
- `UI/RevealPopup`: full-screen reveal after a pull.

**Gameplay/City/ (art)** - see `docs/NEW_BUILDING.md`
- `BuildingArt.cs`: `BuildingLayout` (where a picture goes: flat over the cells, or 3/4 raised above them), `DepthSorting` (draw order by height on the map: characters behind a building are hidden), `BuildingOpenState` (when a building counts as open).
- `BuildingAnimationSet` / `BuildingAnimator`: closed / open loops and opening / closing transitions. `BuildingChecks`: finds the usual mistakes. `Building.Body` is the renderer that shows the picture (a child for 3/4 art).
- Clicks go to whatever is drawn in front (`InputManager`).

**Gameplay/City/** - buildings and the map
- `BuildingType` (SO), `Building` (id, cell, workers, residents, which avatars are on site), `BuildingManager` (build / move / remove, registry), `MapGrid` (cells: bounds, buildings, roads, speed), `BuildingPlacer` (placing and moving; ghost; place-and-confirm on phones).
- Components on building prefabs: `ResourceProducer` (output; only runs with a worker on site), `Housing`, `CityHall`, `Tavern` (marks the recruitment building; gives the spawn point).

**Gameplay/Characters/**
- `CharacterType` (SO: stats, rarity, schedule, rest options, wage, self-awareness levels), `OwnedCharacter` (data), `CharacterManager` (roster, hire / dismiss, spawn avatars).
- `Character` (avatar: follows the schedule, asks for routes, walks, goes inside buildings), `CharacterMovement` (walks waypoints, speed from road), `CharacterSchedule`.
- `MoodSystem` (mood rules), `OwnedCharacter` speed = base x loyalty x mood x self-awareness buffs.

**Gameplay/Navigation/** - `GridPathfinder` (A*), `PathService` (cached, spread over frames, re-routes when buildings change).
**Gameplay/Roads/** - `RoadType` (SO), `RoadManager` (tilemap), `RoadPlacer` (paint / erase by mouse or finger stroke).
**Gameplay/Gacha/** - `GachaPool` (SO: characters, rarity odds, pity), `GachaRoller` (pure dice), `GachaSystem` (letters, pulls, duplicates -> self-awareness), `Tavern`, `Rarity`, `SelfAwareness`.

**Backend/** - everything "server-like"
- `IGameBackend` (the interface), `LocalBackend` (files + device clock + local rolls), `Backend.Current` (which one is in use), `BackendMessages` (request / response data), `BackendSelector` (picks Firebase if `Resources/FirebaseConfig.json` exists, else local).
- `Firebase/`: `FirebaseBackend` (calls the server's functions over HTTP; keeps a copy of saves on the device for offline), `FirebaseAuth` (anonymous sign-in, token renewal), `FirebaseConfig`, `IHttpClient` (+ `UnityHttpClient`), `JsonExtract`.
- The server itself is in `firebase/` (Cloud Functions in TypeScript, Firestore rules); see `docs/FIREBASE_SETUP.md`.
- `TimeSources`, `RealTimeCalendar` (real hours -> Morning/Afternoon/Evening/Night, game days), `OfflineProgress` (what buildings made while away), `GameSession` (auto load / save).

**Input/** - `InputManager` (click -> clickable or placement mode), `IClickable` (`Building`, `Character`), `IPlacementMode`, `CameraController`, `TouchGestures` (pure), `TouchCameraControls`, `UiHit`.

**UI/** - `UIManager` (details panel, build / hire strips, menu buttons, placement flow), `TownMenu` + `TownList`, `RecruitMenu`, `CheatMenu`, `MenuPanels` (builds rows / cards / headers), `UILayout` (portrait vs landscape), `PlacementBar`.

**Diagnostics/** - `Cheats` (testing tools), `PerfOverlay`, `FrameStatsWindow`, `StressTest`, `StressBenchmark`.

Data assets (`Assets/_Project/ScriptableObjects`): Buildings (Gold Mine, Lumberyard, Home, City Hall, Tavern), Characters (Darkie, John), Roads (Dirt Road), Gacha (TavernPool). Prefabs: building shells, `RandomVillager`, UI button prefab.

## 4. How it runs

**Start.** Each manager sets its `Instance` in `Awake`. In `Start`: `ResourceManager` sets starting resources; `UIManager` builds the runtime menus, creates `UILayout` and lays the UI out for the screen shape. `GameSession` (real-time clock) waits a frame, then loads `savegame.json` if there is one, and applies the time-away reward.

**Every frame.**
- `GameClock` (real time: reads the backend clock; in-game: counts seconds) -> on a change raises `TimeOfDayChanged` -> `CharacterManager` tells every avatar to re-read its schedule.
- `Character.Update`-ish work only happens while walking (`CharacterMovement` is enabled only then).
- `PathService` answers route requests, a limited number per frame.
- `ResourceManager` once a second adds each producer's output; `MoodSystem` once a second changes moods; `WageSystem` pays when a new day starts.
- `UIManager` refreshes open menus every half second; `PerfOverlay` measures.

**A click / tap.** `InputManager` collects it, ignores it if it is over UI (`UiHit`), asks which `IClickable` is under it (highest priority wins: characters over buildings). While a placement mode is active (building / moving / roads) the click goes to that mode instead. Selecting raises `Building.Selected` / `Character.Selected` -> `UIManager` shows the details panel.

**Building something.** Build card -> `BuildingPlacer.SelectBuildingToPlace` -> ghost follows the pointer -> click (or Place on a phone) -> `BuildingManager.Build` -> instantiates the prefab, registers it, marks `MapGrid` cells -> gold is charged by the placer. Moving: same, `BuildingManager.Move`, then everyone inside walks over.

**A character's day.** Time of day changes -> `Character.ApplySchedule` -> picks a place (work / home / rest option) -> `PathService` route -> `CharacterMovement` walks it (faster on roads) -> on arrival `EnterActivity`: hidden inside; workers register on site, which is what makes the building produce.

**Recruiting.** Recruit menu -> `GachaSystem.Pull` -> `Backend.Pull` -> (local: `GachaRoller` rolls) -> response: letters left, pity, rolls -> the game hires the character (spawns in front of the Tavern) or, at the ownership limit, raises its self-awareness.

**Saving / loading.** `SaveSystem.Save` -> `Backend.SaveBlob` (local: a file). Load validates the file first, then clears and rebuilds buildings, roads and characters through the same managers. The save records the time, so loading in real time pays the time away.

**Layout.** `UILayout` picks landscape (the scene as built) or portrait (bottom bar, sheets, bigger rows) from the screen shape, every frame, and restores exactly when it changes back.

## 5. Where to change what

| Want to... | Edit |
|---|---|
| Tune a building / character / road | its ScriptableObject in `ScriptableObjects/` |
| Gacha odds, pity, who can be pulled | `ScriptableObjects/Gacha/TavernPool` and each character's rarity / pull weight |
| Mood, speed, wage numbers | `CharacterType` (mood profile, speed effects, daily wage) |
| A new character | Eryville > New Character... (`docs/NEW_CHARACTER.md`) |
| A character's pictures / animations | `CharacterType` Art section + a Character Animations asset (`docs/NEW_CHARACTER.md`) |
| Time-of-day hours | `RealTimeCalendar` |
| Offline reward | `OfflineProgress` (efficiency, cap) |
| A new building | Eryville > New Building... (`docs/NEW_BUILDING.md`) |
| A new menu | follow `TownMenu` / `CheatMenu`; use `MenuPanels` |
| Something the server decides | `IGameBackend` + `LocalBackend` (and later the Firebase one) |
| Testing | Game menu -> Cheats; the performance panel (tap the fps box) |

## 6. Known gaps
- Roster, city and gold are still client-side (the server only bounds the gold it is told). Letters, pity, pulls and time are the server's. Not built yet: Google Play Games / Apple sign-in, purchases, App Check, rate limits.
- Phone layout and touch controls are untested on a real device; the performance budget is unmeasured.
- The tavern uses the Home sprite as a placeholder; art, sound and a main-menu flow are minimal.
- Three `FormerlySerializedAs` attributes remain until the project has been re-saved in Unity.
