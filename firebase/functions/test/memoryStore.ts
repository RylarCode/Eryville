import { GameConfig } from "../src/config";
import { PlayerDoc } from "../src/player";
import { SaveDoc, Store } from "../src/store";

// An in-memory Store for the tests. Calls on the same player run one after another, like Firestore transactions.
export class MemoryStore implements Store {
  players = new Map<string, PlayerDoc>();
  saves = new Map<string, SaveDoc>();
  configOverride: Partial<GameConfig> | null = null;
  private chain: Promise<unknown> = Promise.resolve();

  async transact<T>(uid: string, fn: (player: PlayerDoc | null) => { player: PlayerDoc; result: T }): Promise<T> {
    const run = this.chain.then(() => {
      const existing = this.players.get(uid);
      const { player, result } = fn(existing ? { ...existing, pity: [...existing.pity], claimedQuests: [...(existing.claimedQuests ?? [])] } : null);
      this.players.set(uid, player);
      return result;
    });
    this.chain = run.catch(() => undefined);
    return run;
  }

  async getSave(uid: string, slot: string) { return this.saves.get(`${uid}/${slot}`) ?? null; }
  async putSave(uid: string, slot: string, save: SaveDoc) { this.saves.set(`${uid}/${slot}`, save); }
  async deleteSave(uid: string, slot: string) { this.saves.delete(`${uid}/${slot}`); }
  async deleteAll(uid: string) {
    this.players.delete(uid);
    for (const key of [...this.saves.keys()]) if (key.startsWith(`${uid}/`)) this.saves.delete(key);
  }
  async getConfigOverride() { return this.configOverride; }
}
