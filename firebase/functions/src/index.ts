import { randomInt } from "node:crypto";
import { initializeApp } from "firebase-admin/app";
import { getFirestore } from "firebase-admin/firestore";
import { setGlobalOptions } from "firebase-functions/v2";
import { CallableRequest, HttpsError, onCall } from "firebase-functions/v2/https";
import { defaultConfig, GameConfig, mergeConfig } from "./config";
import * as handlers from "./handlers";
import { FirestoreStore } from "./store";

// The game's server. Every function is "callable": the game calls it with a signed-in player's token, and the
// player's id comes from that token, never from the request. The database itself is closed to the game (see
// firestore.rules): only these functions touch it.

initializeApp();
setGlobalOptions({ region: "us-central1", maxInstances: 10 });

const store = new FirestoreStore(getFirestore());

// The game rules (config.ts plus the overrides in Firestore config/game), cached for a minute
let cached: { config: GameConfig; atMs: number } | null = null;
async function currentConfig(): Promise<GameConfig> {
  const now = Date.now();
  if (cached && now - cached.atMs < 60_000) return cached.config;
  const config = mergeConfig(defaultConfig, await store.getConfigOverride());
  cached = { config, atMs: now };
  return config;
}

// Secure random numbers in [0, 1)
function secureRandom(): number {
  return randomInt(0, 2 ** 48) / 2 ** 48;
}

const devAllowed = process.env.FUNCTIONS_EMULATOR === "true" || process.env.ALLOW_DEV_ACTIONS === "true";

function callable<D, R>(handler: (ctx: handlers.Context, data: D) => Promise<R>) {
  return onCall(async (request: CallableRequest<D>) => {
    if (!request.auth) throw new HttpsError("unauthenticated", "Sign in first.");
    const ctx: handlers.Context = {
      store,
      uid: request.auth.uid,
      nowMs: Date.now(),
      random: secureRandom,
      config: await currentConfig(),
      devAllowed,
    };
    return handler(ctx, request.data);
  });
}

export const getTime = callable(handlers.getTime);
export const getPlayerState = callable(handlers.getPlayerState);
export const loadGame = callable(handlers.loadGame);
export const saveGame = callable(handlers.saveGame);
export const deleteSave = callable(handlers.deleteSave);
export const pull = callable(handlers.pullHandler);
export const buyLetter = callable(handlers.buyLetterHandler);
export const devAction = callable(handlers.devAction);
