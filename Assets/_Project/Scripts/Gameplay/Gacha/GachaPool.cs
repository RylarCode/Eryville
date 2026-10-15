using System.Collections.Generic;
using UnityEngine;

// The chance of each rarity when pulling. Weights are relative (70 / 22 / 7 / 1 means 70% / 22% / 7% / 1%
// when all four rarities are in the pool; with fewer rarities present the rest are shared out in proportion).
[System.Serializable]
public class RarityOdds
{
    public Rarity rarity;
    public float weight = 1f;

    public RarityOdds() { }
    public RarityOdds(Rarity rarity, float weight) { this.rarity = rarity; this.weight = weight; }
}

// "Every <every> pulls, one of them is at least <minRarity>": after every - 1 pulls without such a result
// the next pull is guaranteed to be at least that rare.
[System.Serializable]
public class PityRule
{
    public Rarity minRarity = Rarity.Rare;
    public int every = 10;

    public PityRule() { }
    public PityRule(Rarity minRarity, int every) { this.minRarity = minRarity; this.every = every; }
}

// What can be pulled and how likely it is: the list of characters (each character type carries its own
// rarity and pullWeight), the odds of each rarity, and the pity rules. Everything here is data to tune in the editor.
[CreateAssetMenu(fileName = "NewGachaPool", menuName = "Scriptable Objects/Gacha Pool")]
public class GachaPool : ScriptableObject
{
    public string poolName = "Tavern";

    [Tooltip("Characters that can be pulled. Leave empty to use every character type in the game.")]
    public List<CharacterType> characters = new List<CharacterType>();

    public List<RarityOdds> odds = new List<RarityOdds>
    {
        new RarityOdds(Rarity.Common, 70f),
        new RarityOdds(Rarity.Rare, 22f),
        new RarityOdds(Rarity.Epic, 7f),
        new RarityOdds(Rarity.Legendary, 1f),
    };

    public List<PityRule> pity = new List<PityRule>
    {
        new PityRule(Rarity.Rare, 10),
        new PityRule(Rarity.Epic, 50),
    };
}
