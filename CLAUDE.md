# Notes for Claude

- Unity 6 (6000.0.44f1), URP 2D, Input System. Code in `Assets/_Project/Scripts`; overview in `docs/ARCHITECTURE.md`.
- Work on the branch `first-cleanup`; commit and push each step. No pull request unless asked.
- There is no Unity editor in the cloud: check changes with a compile and the headless simulation, and say plainly that nothing was seen in Unity.
- **Keep `docs/TO_TEST.md` up to date**: add what needs a look in Unity with every change, remove what the user confirms.
- **Keep `docs/DEPLOYMENT.md` up to date**: its status table, open decisions, and the "must stay in sync" list. When a character that can be pulled or a quest that pays letters is added or changed, update `firebase/functions/src/config.ts` too (and its tests: `npm test` in `firebase/functions`).
- Discuss and plan bigger features with the user before building them.
