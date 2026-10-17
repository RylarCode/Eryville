# Setting up the Firebase backend

The game talks to Firebase over plain HTTP (no Firebase package in Unity). The server code is in `firebase/`:
Cloud Functions (`firebase/functions/src`) do everything that matters (pulls, letters, saves, server time), and the
database is closed to the game (`firebase/firestore.rules` denies all access; only the functions can use it).

**Where is data stored?** In **Firestore** (one database per Firebase project):

```
players/{uid}                   letters, pull count, pity counters, last reported gold, ...
players/{uid}/saves/{slot}      one saved game, as text
config/game                     (optional) game rules you can change without a new release
```

Use a **new Firebase project for the game**, not the one from your other project. Better still, make two:
a *dev* project (cheats allowed) and later a *live* one (cheats forbidden).

## A. In the Firebase console (https://console.firebase.google.com)

1. **Add project** -> name it (for example `eryville-dev`) -> Google Analytics: off is fine -> Create.
2. **Upgrade to the Blaze plan** (bottom left of the console: "Spark - Upgrade"). Cloud Functions need it. It has a free
   allowance that a game this size will not leave while testing, but it needs a billing account:
   Google Cloud console -> Billing -> **Budgets & alerts** -> create a budget (for example 5 USD) so you get an email if anything
   ever starts costing money.
3. **Build -> Authentication -> Get started -> Sign-in method -> Anonymous -> Enable -> Save.**
   (Players are signed in without any screen; later you can add Google Play Games / Apple.)
4. **Build -> Firestore Database -> Create database** -> pick a location near your players (it cannot be changed later) ->
   **Start in production mode** (this means "deny all", which is what we want; the rules file we deploy says the same) -> Enable.
5. **Project settings** (gear icon, top left) -> **General** -> *Your apps* -> **Add app -> Web (`</>`)** -> nickname `unity` ->
   Register (skip Hosting). Copy the **`apiKey`** from the snippet shown. (This key is not a secret: it only says which project a
   request is for. You can restrict it later in Google Cloud console -> APIs & Services -> Credentials.)
6. Write down the **Project ID** (Project settings -> General).

## B. On your computer (once)

1. Install **Node.js 22** (https://nodejs.org). Then in a terminal: `npm install -g firebase-tools` and `firebase login`.
2. In the repository: `cd firebase`, then copy `.firebaserc.example` to `.firebaserc` and put your project id in it.
3. `cd functions`, `npm install`, `npm test` (the server rules are tested: you should see all tests pass).
4. For a **development project only**: copy `functions/.env.example` to `functions/.env.dev`
   (it contains `ALLOW_DEV_ACTIONS=true`, which lets the Cheats menu grant letters and reset your server data). Never do this on the live project.
5. Deploy: `cd ..` (back to `firebase/`), then `firebase use dev` and `firebase deploy --only functions,firestore`.
   The first deploy takes a few minutes and may ask to enable a few Google APIs: say yes.
   (The functions run in `us-central1`; if you want another region change it in `functions/src/index.ts` AND in the game's config.)

## C. In Unity

1. Create the file **`Assets/_Project/Resources/FirebaseConfig.json`** (copy `docs/FirebaseConfig.example.json`) and fill in `projectId` and `apiKey`.
2. Press Play. The Console says `Backend: Firebase (your-project)`. Open the game menu -> **Cheats**: the Saving section shows
   `Backend: Firebase as <player id>...`.
3. Check in the Firebase console: **Authentication -> Users** shows one anonymous user; **Firestore -> Data** shows `players/<that id>`.

To go back to the local backend: Cheats -> "Next start: use the LOCAL backend" (or delete the config file).
Your existing local save is uploaded as the first server save the first time you play online.

## D. Trying it on your computer without deploying (optional)

`cd firebase`, `cd functions && npm run build`, then `firebase emulators:start --only functions,firestore,auth` (needs Java 11+ for the
Firestore emulator). In `FirebaseConfig.json` set `"useEmulator": true` (use the computer's network address as `emulatorHost` if you test on a phone).
The emulator allows the testing actions automatically and starts empty every time.

## What the server decides, and what it cannot

- **Decided by the server:** letters, the pity counters, the pull count, the rolls themselves, the price of letters, the clock.
- **Still decided by the game (for now):** gold, wood, the city and the roster. The server keeps the last gold the game reported and
  refuses claims beyond what a player could plausibly have earned (see `maxGoldPerSecond` in `config.ts`); other things are trusted.
- Saves are limited to 900 KB. Autosave to the server is every 3 minutes (and when the app is paused or closed).
- Not built yet: sign-in with Google Play Games / Apple (an anonymous account is lost on uninstall), purchases, App Check, rate limits.

## Changing the rules without a release

Firestore -> `config` collection -> document `game`: any field of `GameConfig` (`functions/src/config.ts`) you put there overrides the default
(for example `letterGoldCost`: 150). It applies within a minute. The pool (who can be pulled, odds, pity) is in `config.ts`; keep it the same
as the characters in the game (`typeId`, rarity, weight).
