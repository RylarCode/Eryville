# Notes for Claude

- Unity 6 (6000.0.44f1), URP 2D, Input System. Code in `Assets/_Project/Scripts`; overview in `docs/ARCHITECTURE.md`.
- Work on the branch `first-cleanup`; commit and push each step. No pull request unless asked.
- There is no Unity editor in the cloud: check changes with a compile and the headless simulation, and say plainly that nothing was seen in Unity.
- **Keep `docs/TO_TEST.md` up to date**: add what needs a look in Unity with every change, remove what the user confirms.
- **Keep `docs/DEPLOYMENT.md` up to date**: its status table, open decisions, and the "must stay in sync" list. When a character that can be pulled or a quest that pays letters is added or changed, update `firebase/functions/src/config.ts` too (and its tests: `npm test` in `firebase/functions`).
- Discuss and plan bigger features with the user before building them.
- **Names: `docs/NAMING.md` is the source of truth** for every player-facing name. Use those names in game text, docs and conversation. When a name is added or changed, update `docs/NAMING.md` in the same change. If the user uses a name that differs from it (an old name, a code name, a typo), point it out and give the right one.
