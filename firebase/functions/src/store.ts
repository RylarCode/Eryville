import { Firestore } from "firebase-admin/firestore";
import { GameConfig } from "./config";
import { PlayerDoc } from "./player";

export interface SaveDoc { json: string; savedAtMs: number; bytes: number; }

// Where the server keeps things. Firestore in production (FirestoreStore); an in-memory one in the tests.
export interface Store {
  // Reads the player (null if new), lets fn change it, and writes the result: all in one go, so two requests at
  // once cannot both spend the same letters.
  transact<T>(uid: string, fn: (player: PlayerDoc | null) => { player: PlayerDoc; result: T }): Promise<T>;
  getSave(uid: string, slot: string): Promise<SaveDoc | null>;
  putSave(uid: string, slot: string, save: SaveDoc): Promise<void>;
  deleteSave(uid: string, slot: string): Promise<void>;
  deleteAll(uid: string): Promise<void>;
  getConfigOverride(): Promise<Partial<GameConfig> | null>;
}

// Layout in Firestore:
//   players/{uid}                 the PlayerDoc (letters, pity, ...)
//   players/{uid}/saves/{slot}    one saved game as text
//   config/game                   optional overrides of the game rules (see config.ts)
export class FirestoreStore implements Store {
  constructor(private readonly db: Firestore) {}

  async transact<T>(uid: string, fn: (player: PlayerDoc | null) => { player: PlayerDoc; result: T }): Promise<T> {
    const ref = this.db.collection("players").doc(uid);
    return this.db.runTransaction(async (tx) => {
      const snap = await tx.get(ref);
      const { player, result } = fn(snap.exists ? (snap.data() as PlayerDoc) : null);
      tx.set(ref, player);
      return result;
    });
  }

  async getSave(uid: string, slot: string): Promise<SaveDoc | null> {
    const snap = await this.db.collection("players").doc(uid).collection("saves").doc(slot).get();
    return snap.exists ? (snap.data() as SaveDoc) : null;
  }

  async putSave(uid: string, slot: string, save: SaveDoc): Promise<void> {
    await this.db.collection("players").doc(uid).collection("saves").doc(slot).set(save);
  }

  async deleteSave(uid: string, slot: string): Promise<void> {
    await this.db.collection("players").doc(uid).collection("saves").doc(slot).delete();
  }

  async deleteAll(uid: string): Promise<void> {
    await this.db.recursiveDelete(this.db.collection("players").doc(uid));
  }

  async getConfigOverride(): Promise<Partial<GameConfig> | null> {
    const snap = await this.db.collection("config").doc("game").get();
    return snap.exists ? (snap.data() as Partial<GameConfig>) : null;
  }
}
