// The dice of the gacha. A line-for-line port of GachaRoller.cs in the game, so the server and the game's own
// stand-in server (LocalBackend) give the same odds. Rarity numbers: 0 Common, 1 Rare, 2 Epic, 3 Legendary.

export interface Candidate { rarity: number; weight: number; }
export interface RarityOdds { rarity: number; weight: number; }
export interface PityRule { minRarity: number; every: number; }
export interface Roll { candidate: number; rarity: number; pityApplied: boolean; }

const RARITIES = [0, 1, 2, 3];

function clamp01(x: number): number {
  return x < 0 ? 0 : x >= 1 ? 0.999999999 : x;
}

function oddsOf(odds: RarityOdds[], rarity: number): number {
  let total = 0;
  for (const entry of odds) if (entry.rarity === rarity && entry.weight > 0) total += entry.weight;
  return total;
}

function raritiesPresent(candidates: Candidate[], minimum: number): number[] {
  return RARITIES.filter((r) => r >= minimum && candidates.some((c) => c.rarity === r && c.weight > 0));
}

// pityCounters has one entry per rule (pulls in a row below that rule's rarity); it is updated in place.
// Returns candidate -1 when there is nothing that can be picked.
export function pick(candidates: Candidate[], odds: RarityOdds[], rules: PityRule[], pityCounters: number[], random: () => number): Roll {
  const result: Roll = { candidate: -1, rarity: 0, pityApplied: false };

  // Pity: when a rule is due, the result must be at least that rare
  let minimum = 0;
  for (let i = 0; i < rules.length; i++) {
    if (rules[i].every > 0 && pityCounters[i] + 1 >= rules[i].every && rules[i].minRarity > minimum) minimum = rules[i].minRarity;
  }

  let allowed = raritiesPresent(candidates, minimum);
  if (allowed.length === 0) {
    minimum = 0; // nothing that rare to give: fall back to whatever exists
    allowed = raritiesPresent(candidates, minimum);
    if (allowed.length === 0) return result;
  }
  result.pityApplied = minimum > 0;

  // Pick a rarity by its odds among the ones that can be given
  let total = 0;
  for (const r of allowed) total += oddsOf(odds, r);
  let chosen = allowed[allowed.length - 1];
  if (total <= 0) {
    chosen = allowed[Math.min(Math.floor(clamp01(random()) * allowed.length), allowed.length - 1)]; // no odds given: all equal
  } else {
    let roll = clamp01(random()) * total;
    for (const r of allowed) {
      roll -= oddsOf(odds, r);
      if (roll < 0) { chosen = r; break; }
    }
  }

  // Pick a character of that rarity by its weight
  let weightTotal = 0;
  for (const c of candidates) if (c.rarity === chosen && c.weight > 0) weightTotal += c.weight;
  let pickRoll = clamp01(random()) * weightTotal;
  let picked = -1;
  for (let i = 0; i < candidates.length; i++) {
    if (candidates[i].rarity !== chosen || candidates[i].weight <= 0) continue;
    picked = i;
    pickRoll -= candidates[i].weight;
    if (pickRoll < 0) break;
  }

  result.candidate = picked;
  result.rarity = chosen;

  for (let i = 0; i < rules.length; i++) {
    if (rules[i].every <= 0) continue;
    pityCounters[i] = chosen >= rules[i].minRarity ? 0 : pityCounters[i] + 1;
  }
  return result;
}
