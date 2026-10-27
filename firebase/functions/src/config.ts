import { PityRule, RarityOdds } from "./gachaRoller";

export interface PoolCharacter { typeId: number; rarity: number; weight: number; }
export interface PoolConfig { characters: PoolCharacter[]; odds: RarityOdds[]; pity: PityRule[]; }
// What a quest pays in recruitment letters (the server owns the letters, so it must know). Only quests listed here can pay letters.
export interface QuestConfig { letters: number; }

// The rules of the game that the server enforces. These are the defaults; anything can be changed without
// a new release by putting the same fields in the Firestore document config/game (it is merged over these).
export interface GameConfig {
  letterGoldCost: number;      // price of one recruitment letter in gold
  lettersPerPull: number;
  tenPullCost: number;         // a ten-pull costs a little less than ten single pulls
  startingLetters: number;     // what a new player has (the game's own default is 2)
  startingGold: number;        // gold of a new player (the game's own default is 100)
  maxGoldPerSecond: number;    // the most gold a player can plausibly earn per second: bounds what the game may claim
  goldSlack: number;           // extra gold allowed on top, for rounding and bursts
  maxSaveBytes: number;        // Firestore documents are limited to 1 MiB
  pools: Record<string, PoolConfig>;
  quests: Record<string, QuestConfig>;   // by Quest id in the game
}

// Must match the characters in the game (CharacterType.typeId, rarity, pullWeight) and the TavernPool asset.
export const defaultConfig: GameConfig = {
  letterGoldCost: 100,
  lettersPerPull: 1,
  tenPullCost: 9,
  startingLetters: 2,
  startingGold: 100,
  maxGoldPerSecond: 500,
  goldSlack: 2000,
  maxSaveBytes: 900_000,
  quests: {
    "hire-someone": { letters: 1 }, // must match the quest asset's Letters reward in Resources/Quests
  },
  pools: {
    Tavern: {
      characters: [
        { typeId: 1, rarity: 0, weight: 1 }, // Darkie
        { typeId: 2, rarity: 2, weight: 1 }, // John
        { typeId: 3, rarity: 1, weight: 1 }, // Baker
        { typeId: 4, rarity: 1, weight: 1 }, // Fisherman
        { typeId: 5, rarity: 0, weight: 1 }, // Farmer
      ],
      odds: [
        { rarity: 0, weight: 70 },
        { rarity: 1, weight: 22 },
        { rarity: 2, weight: 7 },
        { rarity: 3, weight: 1 },
      ],
      pity: [
        { minRarity: 1, every: 10 },
        { minRarity: 2, every: 50 },
      ],
    },
  },
};

export function mergeConfig(base: GameConfig, override: Partial<GameConfig> | null | undefined): GameConfig {
  if (!override) return base;
  return { ...base, ...override, pools: { ...base.pools, ...(override.pools ?? {}) }, quests: { ...base.quests, ...(override.quests ?? {}) } };
}
