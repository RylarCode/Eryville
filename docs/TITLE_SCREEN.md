# Title screen (front page)

The game starts on the **title screen**: a dusk-coloured background, the name "Eryville", and a pulsing "Click / Tap anywhere to start". A click, tap or key press fades to black, loads GameScene (saved game included, as usual), and fades back in.

## How it works

- The scene is `Scenes/MainMenuScene` (first in Build Settings, so it is what a built game starts with). It only needs a camera: `TitleBootstrap` creates the `TitleScreen` whenever that scene loads, and `TitleScreen` builds its own UI in code.
- `SceneTransition.Go("SceneName")` is the fade-load-fade used for every scene change. It builds its own cover and survives the scene switch.
- Presses in the first 0.4 s are ignored (so the click that focused the window does not skip the page).
- **Back to the title:** Cheats > Tools > *Back to the title screen* (it saves first). `GameManager.LoadMainMenu()` does the same, so a future "Quit to title" button can call it.

## In the editor

*Eryville > Play From Title Screen* (on by default) makes Play always start at the title screen, whichever scene is open. Turn it off to test GameScene directly.

## Your own art

Put a Sprite named **TitleBackground** and/or **TitleLogo** in any `Resources` folder (e.g. `Assets/_Project/Resources/`). They replace the gradient and the plain lettering. Both are optional.

## Adding login / load / settings later

Add buttons in `TitleScreen.Build()`. `SetStatus("Signing in...")` shows a line of text under the prompt. Before the click-to-start moment is the natural place to connect to the backend (`Backend.Current.Connect`) and show its result; `GameSession` currently does the connect and the autoload when GameScene starts, so moving it earlier is a small change once the Firebase setup is settled.
