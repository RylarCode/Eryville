import assert from "node:assert/strict";
import { test } from "node:test";
import { defaultConfig, mergeConfig } from "../src/config";
import * as h from "../src/handlers";
import { MemoryStore } from "./memoryStore";

function ctxFor(store: MemoryStore, uid = "player1", over: Partial<h.Context> = {}): h.Context {
  return { store, uid, nowMs: 1_000_000, random: () => 0, config: defaultConfig, devAllowed: false, ...over };
}

test("a new player starts with the starting letters", async () => {
  const store = new MemoryStore();
  const state = await h.getPlayerState(ctxFor(store));
  assert.equal(state.letters, 2);
  assert.equal(state.totalPulls, 0);
  assert.ok(store.players.has("player1"));
});

test("pull: spends letters, counts, rolls from the pool and keeps pity", async () => {
  const store = new MemoryStore();
  const r = await h.pullHandler(ctxFor(store), { poolId: "Tavern", count: 1 });
  assert.equal(r.ok, true);
  assert.equal(r.lettersAfter, 1);
  assert.equal(r.totalPulls, 1);
  assert.equal(r.rolls.length, 1);
  assert.equal(r.rolls[0].typeId, 1);          // random() = 0: the first common
  assert.equal(r.pity.length, 2);
  assert.deepEqual(store.players.get("player1")?.pity, r.pity);
});

test("a ten-pull costs 9 and rolls 10 times", async () => {
  const store = new MemoryStore();
  await h.devAction(ctxFor(store, "player1", { devAllowed: true }), { action: "grantLetters", amount: 20 });
  const r = await h.pullHandler(ctxFor(store), { poolId: "Tavern", count: 10 });
  assert.equal(r.ok, true);
  assert.equal(r.rolls.length, 10);
  assert.equal(r.lettersAfter, 22 - 9);
  assert.equal(r.totalPulls, 10);
  assert.ok(r.rolls.some((x) => x.rarity >= 1)); // the 10th pull is guaranteed at least Rare
});

test("pull refuses when there are not enough letters, or for nonsense", async () => {
  const store = new MemoryStore();
  const poor = await h.pullHandler(ctxFor(store), { poolId: "Tavern", count: 10 });
  assert.equal(poor.ok, false);
  assert.match(poor.error, /need 9/);
  assert.equal(store.players.get("player1")?.letters, 2);
  assert.equal((await h.pullHandler(ctxFor(store), { poolId: "Tavern", count: 3 })).ok, false);
  assert.equal((await h.pullHandler(ctxFor(store), { poolId: "Nope", count: 1 })).ok, false);
  assert.equal((await h.pullHandler(ctxFor(store), { poolId: "Tavern", count: -1 })).ok, false);
  assert.equal(store.players.get("player1")?.letters, 2);
});

test("two pulls at once cannot spend the same letters", async () => {
  const store = new MemoryStore();
  const results = await Promise.all([1, 2, 3, 4, 5].map(() => h.pullHandler(ctxFor(store), { poolId: "Tavern", count: 1 })));
  assert.equal(results.filter((r) => r.ok).length, 2); // the player had 2 letters
  assert.equal(store.players.get("player1")?.letters, 0);
});

test("players are separate", async () => {
  const store = new MemoryStore();
  await h.pullHandler(ctxFor(store, "a"), { poolId: "Tavern", count: 1 });
  assert.equal(store.players.get("b"), undefined);
  assert.equal((await h.getPlayerState(ctxFor(store, "b"))).letters, 2);
});

test("buying a letter: costs gold, adds a letter, and the claimed gold is bounded", async () => {
  const store = new MemoryStore();
  await h.getPlayerState(ctxFor(store));
  // a new player has 100 gold; claims of 150 a moment later are believed (the ceiling is high enough)
  const ok = await h.buyLetterHandler(ctxFor(store, "player1", { nowMs: 1_000_000 + 1000 }), { clientGold: 150 });
  assert.equal(ok.ok, true);
  assert.equal(ok.lettersAfter, 3);
  assert.equal(ok.goldAfter, 50);
  const poor = await h.buyLetterHandler(ctxFor(store, "player1", { nowMs: 1_000_000 + 2000 }), { clientGold: 20 });
  assert.equal(poor.ok, false);
  assert.match(poor.error, /costs 100/);
  assert.equal(store.players.get("player1")?.letters, 3);
});

test("claiming impossible gold is capped and noted", async () => {
  const store = new MemoryStore();
  await h.getPlayerState(ctxFor(store));
  const r = await h.buyLetterHandler(ctxFor(store, "player1", { nowMs: 1_000_000 + 1000 }), { clientGold: 9_999_999 });
  assert.equal(r.ok, true);
  assert.ok(r.goldAfter < 5000);                // not millions
  assert.equal(store.players.get("player1")?.suspicious, 1);
});

test("save and load: the save comes back; letters and pity are the server's", async () => {
  const store = new MemoryStore();
  const json = JSON.stringify({ version: 5, resources: [{ name: "Gold", amount: 120 }, { name: "Letters", amount: 9999 }] });
  const saved = await h.saveGame(ctxFor(store), { json });
  assert.equal(saved.ok, true);
  const loaded = await h.loadGame(ctxFor(store), {});
  assert.equal(loaded.exists, true);
  assert.equal(loaded.json, json);
  assert.equal(loaded.letters, 2);               // not 9999
  const meta = await h.loadGame(ctxFor(store), { includeJson: false });
  assert.equal(meta.exists, true);
  assert.equal(meta.json, "");
  assert.equal((await h.loadGame(ctxFor(store, "stranger"), {})).exists, false);
});

test("bad saves are refused", async () => {
  const store = new MemoryStore();
  assert.equal((await h.saveGame(ctxFor(store), { json: "" })).ok, false);
  assert.equal((await h.saveGame(ctxFor(store), {})).ok, false);
  assert.equal((await h.saveGame(ctxFor(store), { json: "not json" })).ok, false);
  assert.equal((await h.saveGame(ctxFor(store), { json: JSON.stringify({ nothing: 1 }) })).ok, false);
  const big = JSON.stringify({ resources: [{ name: "Gold", amount: 1 }], pad: "x".repeat(1_000_000) });
  assert.equal((await h.saveGame(ctxFor(store), { json: big })).ok, false);
});

test("save slots are separate, names are cleaned, saves can be deleted", async () => {
  const store = new MemoryStore();
  const json = JSON.stringify({ resources: [{ name: "Gold", amount: 1 }] });
  await h.saveGame(ctxFor(store), { slot: "a.json", json });
  await h.saveGame(ctxFor(store), { slot: "../../evil/x", json });
  assert.equal(h.cleanSlot("../../evil/x"), ".._.._evil_x");
  assert.equal((await h.loadGame(ctxFor(store), { slot: "b.json" })).exists, false);
  await h.deleteSave(ctxFor(store), { slot: "a.json" });
  assert.equal((await h.loadGame(ctxFor(store), { slot: "a.json" })).exists, false);
});

test("testing actions are refused unless switched on", async () => {
  const store = new MemoryStore();
  const refused = await h.devAction(ctxFor(store), { action: "grantLetters", amount: 10 });
  assert.equal(refused.ok, false);
  assert.equal(store.players.get("player1"), undefined);
  const ctx = ctxFor(store, "player1", { devAllowed: true });
  const granted = await h.devAction(ctx, { action: "grantLetters", amount: 10 });
  assert.equal(granted.ok, true);
  assert.equal(granted.lettersAfter, 12);
  await h.saveGame(ctx, { json: JSON.stringify({ resources: [{ name: "Gold", amount: 1 }] }) });
  const reset = await h.devAction(ctx, { action: "reset" });
  assert.equal(reset.lettersAfter, 2);
  assert.equal((await h.loadGame(ctx, {})).exists, false);
  assert.equal((await h.devAction(ctx, { action: "dance" })).ok, false);
  await h.pullHandler(ctx, { poolId: "Tavern", count: 1 });
  assert.equal(store.players.get("player1")?.totalPulls, 1);
  await h.devAction(ctx, { action: "resetPity" });
  assert.equal(store.players.get("player1")?.totalPulls, 0);
  assert.deepEqual(store.players.get("player1")?.pity, []);
});

test("the rules can be changed from Firestore without a new release", () => {
  const merged = mergeConfig(defaultConfig, { letterGoldCost: 250, pools: { Event: { characters: [{ typeId: 9, rarity: 3, weight: 1 }], odds: [], pity: [] } } });
  assert.equal(merged.letterGoldCost, 250);
  assert.equal(merged.tenPullCost, 9);
  assert.ok(merged.pools.Tavern && merged.pools.Event);
});
