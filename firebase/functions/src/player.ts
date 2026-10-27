import { GameConfig } from "./config";
import { pick } from "./gachaRoller";

// What the server keeps for every player (the document players/{uid}). The letters, pity and pull count are the
// server's word: the game cannot change them. Gold is simulated by the game, so the server only keeps what the
// game last reported, to bound what it may claim.
export interface PlayerDoc {
  letters: number;
  totalPulls: number;
  pity: number[];
  goldReported: number;
  goldReportedAtMs: number;
  createdAtMs: number;
  lastSeenMs: number;
  suspicious: number;     // how many times the game claimed more gold than was possible
  claimedQuests?: string[]; // quests whose letters were paid out (each pays once); missing in documents from before quests
}

export interface PullRollData { typeId: number; rarity: number; pityApplied: boolean; }

export interface BackendResult { ok: boolean; error: string; }
export interface PullResponse extends BackendResult { rolls: PullRollData[]; lettersAfter: number; totalPulls: number; pity: number[]; }
export interface BuyLetterResponse extends BackendResult { lettersAfter: number; goldAfter: number; }
export interface ClaimQuestResponse extends BackendResult { lettersAfter: number; lettersGranted: number; alreadyClaimed: boolean; }

export function newPlayer(cfg: GameConfig, nowMs: number): PlayerDoc {
  return {
    letters: cfg.startingLetters,
    totalPulls: 0,
    pity: [],
    goldReported: cfg.startingGold,
    goldReportedAtMs: nowMs,
    createdAtMs: nowMs,
    lastSeenMs: nowMs,
    suspicious: 0,
    claimedQuests: [],
  };
}

export function costOf(cfg: GameConfig, count: number): number {
  return count === 10 ? cfg.tenPullCost : count * cfg.lettersPerPull;
}

// The most gold the game can honestly have by now: what it last reported plus the best a player could have earned since
export function goldCeiling(p: PlayerDoc, cfg: GameConfig, nowMs: number): number {
  const seconds = Math.max(0, (nowMs - p.goldReportedAtMs) / 1000);
  return p.goldReported + cfg.maxGoldPerSecond * seconds + cfg.goldSlack;
}

// Takes the game's claim about its gold, but never more than the ceiling
export function clampGold(p: PlayerDoc, claimed: number, cfg: GameConfig, nowMs: number): { gold: number; suspicious: boolean } {
  const claim = Number.isFinite(claimed) ? Math.max(0, claimed) : 0;
  const ceiling = goldCeiling(p, cfg, nowMs);
  return claim > ceiling ? { gold: ceiling, suspicious: true } : { gold: claim, suspicious: false };
}

export function reportGold(p: PlayerDoc, claimed: number, cfg: GameConfig, nowMs: number): PlayerDoc {
  const { gold, suspicious } = clampGold(p, claimed, cfg, nowMs);
  return { ...p, goldReported: gold, goldReportedAtMs: nowMs, suspicious: p.suspicious + (suspicious ? 1 : 0) };
}

export function pull(p: PlayerDoc, poolId: string, count: number, cfg: GameConfig, random: () => number): { player: PlayerDoc; response: PullResponse } {
  const fail = (error: string) => ({ player: p, response: { ok: false, error, rolls: [], lettersAfter: p.letters, totalPulls: p.totalPulls, pity: p.pity } });

  if (!Number.isInteger(count) || (count !== 1 && count !== 10)) return fail("A pull is for 1 or 10.");
  const pool = cfg.pools[poolId];
  if (!pool || pool.characters.length === 0) return fail("Nobody can be recruited right now.");

  const cost = costOf(cfg, count);
  if (p.letters < cost) return fail(`You need ${cost} recruitment letters (you have ${p.letters}).`);

  const counters = pool.pity.map((_, i) => p.pity[i] ?? 0);
  const candidates = pool.characters.map((c) => ({ rarity: c.rarity, weight: c.weight }));
  const rolls: PullRollData[] = [];
  let totalPulls = p.totalPulls;
  for (let i = 0; i < count; i++) {
    const roll = pick(candidates, pool.odds, pool.pity, counters, random);
    totalPulls++;
    if (roll.candidate < 0) continue;
    rolls.push({ typeId: pool.characters[roll.candidate].typeId, rarity: roll.rarity, pityApplied: roll.pityApplied });
  }

  const player = { ...p, letters: p.letters - cost, totalPulls, pity: counters };
  return { player, response: { ok: true, error: "", rolls, lettersAfter: player.letters, totalPulls, pity: counters } };
}

export function buyLetter(p: PlayerDoc, clientGold: number, cfg: GameConfig, nowMs: number): { player: PlayerDoc; response: BuyLetterResponse } {
  const { gold, suspicious } = clampGold(p, clientGold, cfg, nowMs);
  if (gold < cfg.letterGoldCost) {
    const noted = suspicious ? { ...p, suspicious: p.suspicious + 1 } : p;
    return { player: noted, response: { ok: false, error: `A letter costs ${cfg.letterGoldCost} gold.`, lettersAfter: p.letters, goldAfter: gold } };
  }

  const goldAfter = gold - cfg.letterGoldCost;
  const player = { ...p, letters: p.letters + 1, goldReported: goldAfter, goldReportedAtMs: nowMs, suspicious: p.suspicious + (suspicious ? 1 : 0) };
  return { player, response: { ok: true, error: "", lettersAfter: player.letters, goldAfter } };
}

// Pays a quest's letters, once. The game decides whether the quest was really finished (it simulates the town), but only quests the
// server lists can pay letters, only the amount the server lists, and only once per player. Claiming again is harmless: it pays nothing.
export function claimQuest(p: PlayerDoc, questId: string, cfg: GameConfig): { player: PlayerDoc; response: ClaimQuestResponse } {
  const quest = typeof questId === "string" ? cfg.quests[questId] : undefined;
  if (!quest) return { player: p, response: { ok: false, error: "That quest does not pay letters here.", lettersAfter: p.letters, lettersGranted: 0, alreadyClaimed: false } };

  const claimed = p.claimedQuests ?? [];
  if (claimed.includes(questId)) return { player: p, response: { ok: true, error: "", lettersAfter: p.letters, lettersGranted: 0, alreadyClaimed: true } };

  const player = { ...p, letters: p.letters + quest.letters, claimedQuests: [...claimed, questId] };
  return { player, response: { ok: true, error: "", lettersAfter: player.letters, lettersGranted: quest.letters, alreadyClaimed: false } };
}
