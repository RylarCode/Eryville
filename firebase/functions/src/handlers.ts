import { GameConfig } from "./config";
import { buyLetter, claimQuest, newPlayer, PlayerDoc, pull, reportGold } from "./player";
import { Store } from "./store";

// What each server function does, separated from Firebase itself so it can be tested. Every handler gets the
// authenticated player's id from the caller; nothing the game sends can change whose data is used.

export interface Context {
  store: Store;
  uid: string;
  nowMs: number;
  random: () => number;
  config: GameConfig;
  devAllowed: boolean;       // testing actions (grant letters, reset) are only allowed on development projects
}

const DEFAULT_SLOT = "savegame.json";

export function cleanSlot(slot: unknown): string {
  const text = typeof slot === "string" && slot.length > 0 ? slot : DEFAULT_SLOT;
  return text.replace(/[^A-Za-z0-9_.-]/g, "_").slice(0, 64);
}

function stateOf(p: PlayerDoc) {
  return { letters: p.letters, totalPulls: p.totalPulls, pity: p.pity };
}

// Reads or creates the player
async function touch<T>(ctx: Context, fn: (p: PlayerDoc) => { player: PlayerDoc; result: T }): Promise<T> {
  return ctx.store.transact<T>(ctx.uid, (existing) => {
    const base = existing ?? newPlayer(ctx.config, ctx.nowMs);
    const out = fn({ ...base, lastSeenMs: ctx.nowMs });
    return out;
  });
}

export async function getTime(ctx: Context) {
  return { ok: true, error: "", serverMs: ctx.nowMs };
}

export async function getPlayerState(ctx: Context) {
  return touch(ctx, (p) => ({ player: p, result: { ok: true, error: "", ...stateOf(p), serverMs: ctx.nowMs } }));
}

export async function loadGame(ctx: Context, data: { slot?: string; includeJson?: boolean }) {
  const slot = cleanSlot(data?.slot);
  const state = await touch(ctx, (p) => ({ player: p, result: stateOf(p) }));
  const save = await ctx.store.getSave(ctx.uid, slot);
  const wantJson = data?.includeJson !== false;
  return {
    ok: true,
    error: "",
    exists: save !== null,
    json: save !== null && wantJson ? save.json : "",
    savedAtMs: save?.savedAtMs ?? 0,
    ...state,
    serverMs: ctx.nowMs,
  };
}

function goldIn(json: string): number | null {
  try {
    const parsed = JSON.parse(json);
    const gold = (parsed.resources ?? []).find((r: { name?: string }) => r.name === "Gold");
    return gold && typeof gold.amount === "number" ? gold.amount : null;
  } catch {
    return null;
  }
}

export async function saveGame(ctx: Context, data: { slot?: string; json?: unknown }) {
  const fail = (error: string) => ({ ok: false, error, savedAtMs: 0 });
  if (typeof data?.json !== "string" || data.json.length === 0) return fail("There is nothing to save.");

  const bytes = Buffer.byteLength(data.json, "utf8");
  if (bytes > ctx.config.maxSaveBytes) return fail(`The save is too big (${bytes} bytes).`);
  const gold = goldIn(data.json);
  if (gold === null) return fail("The save is not a valid game.");

  const slot = cleanSlot(data.slot);
  await touch(ctx, (p) => ({ player: reportGold(p, gold, ctx.config, ctx.nowMs), result: null })); // letters in the save are ignored: the server has the real ones
  await ctx.store.putSave(ctx.uid, slot, { json: data.json, savedAtMs: ctx.nowMs, bytes });
  return { ok: true, error: "", savedAtMs: ctx.nowMs };
}

export async function deleteSave(ctx: Context, data: { slot?: string }) {
  await ctx.store.deleteSave(ctx.uid, cleanSlot(data?.slot));
  return { ok: true, error: "" };
}

export async function pullHandler(ctx: Context, data: { poolId?: string; count?: number }) {
  return touch(ctx, (p) => {
    const { player, response } = pull(p, String(data?.poolId ?? "Tavern"), Number(data?.count), ctx.config, ctx.random);
    return { player, result: response };
  });
}

export async function buyLetterHandler(ctx: Context, data: { clientGold?: number }) {
  return touch(ctx, (p) => {
    const { player, response } = buyLetter(p, Number(data?.clientGold), ctx.config, ctx.nowMs);
    return { player, result: response };
  });
}

export async function claimQuestHandler(ctx: Context, data: { questId?: string }) {
  return touch(ctx, (p) => {
    const { player, response } = claimQuest(p, String(data?.questId ?? ""), ctx.config);
    return { player, result: response };
  });
}

// Testing tools. Refused on any server that has not switched them on.
export async function devAction(ctx: Context, data: { action?: string; amount?: number }) {
  if (!ctx.devAllowed) return { ok: false, error: "Testing actions are not enabled on this server.", lettersAfter: 0 };

  const amount = Number.isFinite(Number(data?.amount)) ? Math.trunc(Number(data?.amount)) : 0;
  switch (data?.action) {
    case "grantLetters":
      return touch(ctx, (p) => {
        const player = { ...p, letters: Math.max(0, p.letters + amount) };
        return { player, result: { ok: true, error: "", lettersAfter: player.letters } };
      });
    case "resetPity":
      return touch(ctx, (p) => ({ player: { ...p, pity: [], totalPulls: 0 }, result: { ok: true, error: "", lettersAfter: p.letters } }));
    case "reset":
      await ctx.store.deleteAll(ctx.uid);
      return touch(ctx, (p) => ({ player: p, result: { ok: true, error: "", lettersAfter: p.letters } }));
    default:
      return { ok: false, error: `Unknown testing action "${data?.action}".`, lettersAfter: 0 };
  }
}
