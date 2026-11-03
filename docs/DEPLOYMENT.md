# Deployment: Firebase and Google Play

The one place to look before deploying the server or putting a build on Google Play. The click-by-click Firebase steps are in `docs/FIREBASE_SETUP.md`; this page says **where things stand, what must stay in sync, and the order to do things in**.

## Where things stand

| | Status |
|---|---|
| Firebase project | **Not created yet** |
| Cloud Functions deployed | **No** (code is ready and tested: `npm test` in `firebase/functions`, 21 tests) |
| Unity talking to Firebase | Not tried yet (needs `Resources/FirebaseConfig.json`) |
| Google Play developer account | **Not created yet** |
| Android package name | **Not decided** (currently Unity's default `com.DefaultCompany.2D-URP`) |
| Keystore | **Not created yet** |
| First internal test build | Not yet |

Update this table as each step is done.

## Decisions still open

- **Package name** (for example `com.rylar.eryville`). It is permanent once uploaded to Play.
- **Company name / product name** shown on the phone (currently `DefaultCompany` / `City Building Gacha`).
- **One Firebase project or two.** Recommended: `eryville-dev` (cheats allowed, used from the editor) and `eryville-live` (no cheats, used by Play builds). Internal testing can start on dev if you accept that the dev server's testing actions are on.
- **Commit `FirebaseConfig.json` or not.** It holds the project id and web API key, which are not secrets, but dev and live builds need different files. Not committing it (and keeping a copy per project) avoids shipping the dev one by mistake.
- **What testers start with.** Release builds have no Cheats menu, so testers get `startingLetters` (2) and whatever gold they earn. Raise it in `config.ts` (or the Firestore `config/game` document) if testers should pull more.

## Things that must stay in sync

The server has its own copy of a few game rules. If they drift, things silently stop working online:

| In the game | Must match on the server (`firebase/functions/src/config.ts`) | If it drifts |
|---|---|---|
| Characters that can be pulled (`CharacterType.typeId`, rarity, pull weight) and the `TavernPool` asset | `pools.Tavern.characters` | The server never rolls a missing character |
| Quests that pay recruitment letters (`Quest.id` and its Letters reward) | `quests` (`"id": { letters: N }`) | Online the quest pays no letters. **Eryville > Check Quests** warns about this |
| Letter price, pull costs | `letterGoldCost`, `lettersPerPull`, `tenPullCost` | The server's price wins |
| Function region (`FirebaseConfig.json` `region`) | `setGlobalOptions({ region })` in `functions/src/index.ts` | Calls go nowhere |

**After changing `config.ts`, redeploy** (`firebase deploy --only functions`). Small changes can also go in Firestore `config/game` without a deploy (see `FIREBASE_SETUP.md`), but `pools` and `quests` are easier to keep right in the file.

Currently in `config.ts`: characters typeId 1-5 (Darkie, John, Baker, Fisherman, Farmer) and the quest `hire-someone` (1 letter). **Add the Gold Digger once it exists.**

## What the server does

Functions (all callable, region `us-central1`): `getTime`, `getPlayerState`, `loadGame`, `saveGame`, `deleteSave`, `pull`, `buyLetter`, `claimQuest`, `devAction` (testing only: refused unless `ALLOW_DEV_ACTIONS=true` or the emulator).

- Decides: letters, pity, pull counts and rolls, letter price, the clock, quest letter payouts (once per quest per player).
- Trusts the game for: gold (bounded by `maxGoldPerSecond`), wood, items, the town, the roster, story and quest progress.
- Database: Firestore, closed to the game (`firestore.rules` denies everything; only the functions read and write).
- Players sign in anonymously. **An anonymous account is lost when the app is uninstalled** (fine for testing).
- Not built yet: Google Play Games sign-in, App Check, rate limits, purchases.

## Part 1: Firebase (do this first)

Full steps: `docs/FIREBASE_SETUP.md`. Summary:

1. Create the project(s). Blaze plan + a budget alert. Enable **Anonymous** sign-in. Create **Firestore** (production mode). Add a **Web app** and copy the `apiKey`; note the project id.
2. On your computer: Node 22, `npm install -g firebase-tools`, `firebase login`.
3. `firebase/.firebaserc` from `.firebaserc.example` with your project id(s). For the **dev** project only: `functions/.env.dev` from `.env.example` (`ALLOW_DEV_ACTIONS=true`). Never for live.
4. `cd firebase/functions && npm install && npm test`, then `cd .. && firebase use dev && firebase deploy --only functions,firestore`.
5. Unity: `Assets/_Project/Resources/FirebaseConfig.json` from `docs/FirebaseConfig.example.json`. Press Play: the Console says `Backend: Firebase (...)`; Cheats > Saving shows the player id; the Firebase console shows the anonymous user and `players/<id>`.

## Part 2: Google Play (internal testing)

**Accounts and tools (yours):**
1. Play Console developer account (one-off fee, identity verification).
2. Unity Hub: add **Android Build Support** with OpenJDK, Android SDK and NDK to Unity 6000.0.44f1.

**Project settings (Claude can do these once the names are decided):**
3. Player Settings > Android: package name, company and product name, version (`bundleVersion`, e.g. 0.1.0) and **version code** (`AndroidBundleVersionCode`, must go up by 1 every upload).
4. Internet Access: **Require**. Scripting backend IL2CPP and ARM64 are already set. Target API level: whatever Play currently requires (it rises every year; check the Play Console's warning).
5. Orientation: portrait (the phone layout is portrait). An app icon.

**Signing (yours, and back it up):**
6. Player Settings > Publishing Settings > Keystore Manager: create a keystore and key. **Keep the keystore file and both passwords somewhere safe outside the project.** Do not commit it.
7. In Play Console, use **Play App Signing** (Google holds the app key; your keystore becomes the upload key, which can be reset if lost).

**Build and upload:**
8. Put the `FirebaseConfig.json` of the project the testers should use in `Resources`.
9. Build Settings > Android > tick **Build App Bundle (Google Play)**. Not a Development Build (Play rejects debuggable builds), so there is no Cheats menu.
10. Play Console > create the app > **Testing > Internal testing** > create a release > upload the `.aab` > add testers (email list) > share the opt-in link. No review; up to 100 testers.

**Store forms Play asks for even for testing:** privacy policy URL (the game stores an anonymous user id and saves on the server), Data safety form, content rating questionnaire, target audience, app category, and for the listing: a 512x512 icon, a 1024x500 feature graphic, at least 2 phone screenshots.

## Every release

1. `npm test` in `firebase/functions`; Eryville > Check Characters, Check Buildings, Check Story, Check Quests.
2. `config.ts` matches the game (table above); redeploy functions if it changed.
3. Version code +1 (and version name if you like).
4. Right `FirebaseConfig.json` in `Resources` (live, not dev).
5. Build App Bundle, upload to Internal testing, write release notes.
6. Install from the Play link on a phone and do a quick smoke test (start, build, recruit, save, close, reopen).

## Later (before a public launch)

- Separate live project with dev actions off; restrict the API key in Google Cloud > Credentials.
- Google Play Games sign-in (keep progress across reinstalls and phones).
- App Check and rate limits on the functions.
- Personal developer accounts must run a closed test (a minimum number of testers for a minimum number of days; check Play's current rule) before production.
