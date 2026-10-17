import assert from "node:assert/strict";
import { test } from "node:test";
import { Candidate, pick, PityRule, RarityOdds } from "../src/gachaRoller";

// A small seeded random generator, so the tests give the same numbers every time
function seeded(seed: number): () => number {
  let s = seed >>> 0;
  return () => {
    s = (s + 0x6d2b79f5) >>> 0;
    let t = s;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const odds: RarityOdds[] = [{ rarity: 0, weight: 70 }, { rarity: 1, weight: 22 }, { rarity: 2, weight: 7 }, { rarity: 3, weight: 1 }];
const candidates: Candidate[] = [{ rarity: 0, weight: 1 }, { rarity: 0, weight: 3 }, { rarity: 1, weight: 1 }, { rarity: 2, weight: 1 }, { rarity: 3, weight: 1 }];

test("rarity odds and weights", () => {
  const random = seeded(1234);
  const counts = [0, 0, 0, 0, 0];
  const n = 200_000;
  for (let i = 0; i < n; i++) counts[pick(candidates, odds, [], [], random).candidate]++;
  const f = (i: number) => counts[i] / n;
  assert.ok(Math.abs(f(0) + f(1) - 0.7) < 0.01);
  assert.ok(Math.abs(f(2) - 0.22) < 0.01);
  assert.ok(Math.abs(f(3) - 0.07) < 0.005);
  assert.ok(Math.abs(f(4) - 0.01) < 0.003);
  assert.ok(Math.abs(f(1) / f(0) - 3) < 0.15);
});

test("rarities missing from the pool are skipped and the rest renormalised", () => {
  const random = seeded(5);
  const pool: Candidate[] = [{ rarity: 0, weight: 1 }, { rarity: 2, weight: 1 }];
  let epics = 0;
  const n = 100_000;
  for (let i = 0; i < n; i++) if (pick(pool, odds, [], [], random).candidate === 1) epics++;
  assert.ok(Math.abs(epics / n - 7 / 77) < 0.005);
});

test("zero weights are never picked; nothing to pick gives -1", () => {
  const random = seeded(9);
  const pool: Candidate[] = [{ rarity: 0, weight: 0 }, { rarity: 0, weight: 2 }];
  for (let i = 0; i < 1000; i++) assert.equal(pick(pool, odds, [], [], random).candidate, 1);
  assert.equal(pick([], odds, [], [], () => 0.5).candidate, -1);
});

test("pity: every 10th pull is at least Rare, every 50th at least Epic", () => {
  const rules: PityRule[] = [{ minRarity: 1, every: 10 }, { minRarity: 2, every: 50 }];
  const counters = [0, 0];
  const seq: number[] = [];
  const flags: boolean[] = [];
  for (let i = 0; i < 60; i++) {
    const r = pick(candidates, odds, rules, counters, () => 0);
    seq.push(r.rarity);
    flags.push(r.pityApplied);
  }
  assert.ok(seq.slice(0, 9).every((r) => r === 0));
  assert.equal(seq[9], 1);
  assert.ok(flags[9] && !flags[8]);
  assert.equal(seq[19], 1);
  assert.equal(seq[49], 2);
});

test("pity counters reset on a good result and fall back when nothing is that rare", () => {
  const rules: PityRule[] = [{ minRarity: 1, every: 10 }];
  const c = [8];
  pick(candidates, odds, rules, c, () => 0.99);
  assert.equal(c[0], 0);
  const c2 = [3];
  pick(candidates, odds, rules, c2, () => 0);
  assert.equal(c2[0], 4);
  const commons: Candidate[] = [{ rarity: 0, weight: 1 }];
  const fallback = pick(commons, odds, [{ minRarity: 1, every: 10 }, { minRarity: 2, every: 50 }], [99, 99], () => 0.5);
  assert.equal(fallback.candidate, 0);
  assert.equal(fallback.pityApplied, false);
});
