using System;
using System.Collections.Generic;
using UnityEngine;

// What one pull gave the player
public class PullResult
{
    public CharacterType Type;
    public Rarity Rarity;
    public bool PityApplied;          // the pity rules forced a minimum rarity
    public OwnedCharacter Character;  // the new character, or (for a duplicate) the one that got stronger
    public bool IsNew;                // a new character joined (otherwise this was a duplicate)
    public int AwarenessLevel;        // for duplicates: the character's self-awareness level afterwards
    public bool ConvertedToLoyalty;   // the duplicate's character was already at max self-awareness, so it gave loyalty instead
    public float LoyaltyGained;       // how much, in that case
}

// Recruitment: spend recruitment letters at the tavern to pull characters. A pull picks a rarity by the pool's
// odds (with pity rules), then a character of that rarity. If the player can still own another of that
// character it joins and appears in front of the tavern; if not (maxOwned reached), the pull is a duplicate
// and raises the self-awareness of the character they already have.
public class GachaSystem : MonoBehaviour
{
    public static GachaSystem Instance { get; private set; }

    [SerializeField] private GachaPool pool;                 // optional: without one, every character type is pullable with default odds
    [SerializeField] private int letterGoldCost = 100;       // price of one recruitment letter in gold
    [SerializeField] private int lettersPerPull = 1;
    [SerializeField] private int tenPullCost = 9;            // a ten-pull costs a little less than ten single pulls
    [SerializeField] private float maxedDuplicateLoyalty = 10f; // loyalty given when a duplicate has nothing left to raise

    public const int TenPull = 10;

    private int[] pityCounters = new int[0];
    private Func<double> random;
    private GachaPool defaultPool;

    public int TotalPulls { get; private set; }
    public IReadOnlyList<int> PityCounters => pityCounters;
    public int LetterGoldCost => letterGoldCost;

    // What the player can read after a failed pull or purchase
    public string LastMessage { get; private set; } = "";

    // Raised with every result of a successful pull action
    public event Action<List<PullResult>> Pulled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        System.Random system = new System.Random();
        random = () => system.NextDouble();
    }

    // Replaces the dice (used by tests to get exact results)
    public void SetRandom(Func<double> source)
    {
        random = source;
    }

    // --- The pool ---

    public GachaPool Pool
    {
        get
        {
            if (pool != null) return pool;
            if (defaultPool == null) defaultPool = ScriptableObject.CreateInstance<GachaPool>();
            return defaultPool;
        }
    }

    // The characters that can be pulled right now
    private List<CharacterType> PullableTypes()
    {
        List<CharacterType> list = new List<CharacterType>();
        IEnumerable<CharacterType> source = Pool.characters != null && Pool.characters.Count > 0
            ? Pool.characters
            : (IEnumerable<CharacterType>)CharacterManager.Instance.AllCharacters;
        foreach (CharacterType type in source)
        {
            if (type != null && !list.Contains(type)) list.Add(type);
        }
        return list;
    }

    // --- Letters ---

    public int Letters => Mathf.FloorToInt(ResourceManager.Instance.GetResourceAmount(ResourceManager.Letters));

    // What a pull of this many costs, in letters (ten at once is cheaper)
    public int CostOf(int count)
    {
        return count == TenPull ? tenPullCost : count * lettersPerPull;
    }

    // Gives letters as a reward (quests, events...)
    public void GrantLetters(int amount)
    {
        if (amount > 0) ResourceManager.Instance.changeResourceAmount(ResourceManager.Letters, amount);
    }

    // Buys one letter for gold. The backend decides (and reports the balances afterwards).
    // Returns true when it went through; with an online backend that answers later it returns true
    // once the request is sent, and the balances change when the answer arrives.
    public bool BuyLetter()
    {
        bool answered = false, success = false;
        Backend.Current.BuyLetter(response =>
        {
            answered = true;
            success = OnBuyLetterResponse(response);
        });
        return answered ? success : true;
    }

    private bool OnBuyLetterResponse(BuyLetterResponse response)
    {
        if (response == null || !response.ok)
        {
            LastMessage = response != null && !string.IsNullOrEmpty(response.error) ? response.error : "Could not buy a letter.";
            return false;
        }
        ResourceManager.Instance.SetResourceAmount(ResourceManager.Gold, response.goldAfter);
        ResourceManager.Instance.SetResourceAmount(ResourceManager.Letters, response.lettersAfter);
        LastMessage = $"Bought a recruitment letter for {letterGoldCost} gold.";
        return true;
    }

    // What an offline backend answers when asked to sell a letter (the game itself plays the part of the server).
    // An online backend does this on its own server instead.
    public BuyLetterResponse LocalBuyLetter()
    {
        float gold = ResourceManager.Instance.GetResourceAmount(ResourceManager.Gold);
        if (gold < letterGoldCost) return BuyLetterResponse.Failed($"A letter costs {letterGoldCost} gold.");
        return new BuyLetterResponse { goldAfter = gold - letterGoldCost, lettersAfter = Letters + 1 };
    }

    // --- Tavern ---

    public Tavern FindTavern()
    {
        foreach (Building building in BuildingManager.Instance.Buildings)
        {
            Tavern tavern = building.GetComponent<Tavern>();
            if (tavern != null) return tavern;
        }
        return null;
    }

    // Whether a pull of this size is possible now; if not, says why
    public bool CanPull(int count, out string reason)
    {
        reason = "";
        if (waitingForBackend) reason = "Waiting for the server...";
        else if (FindTavern() == null) reason = "Build a tavern first: recruits are met there.";
        else if (PullableTypes().Count == 0) reason = "Nobody can be recruited right now.";
        else if (Letters < CostOf(count)) reason = $"You need {CostOf(count)} recruitment letters (you have {Letters}).";
        return reason.Length == 0;
    }

    // --- Pulling ---

    private bool waitingForBackend;

    // Asks the backend for the rolls and applies what it answers. Returns the results when the backend answers at
    // once (the local one does); an online one answers later, and the results then arrive through the Pulled event.
    public List<PullResult> Pull(int count = 1)
    {
        List<PullResult> results = new List<PullResult>();
        if (count < 1) return results;
        if (!CanPull(count, out string reason))
        {
            LastMessage = reason;
            return results;
        }

        waitingForBackend = true;
        bool answered = false;
        List<PullResult> answer = null;
        Backend.Current.Pull(new PullRequest { poolId = Pool.poolName, count = count }, response =>
        {
            answered = true;
            waitingForBackend = false;
            answer = OnPullResponse(response);
        });
        return answered && answer != null ? answer : results;
    }

    private List<PullResult> OnPullResponse(PullResponse response)
    {
        List<PullResult> results = new List<PullResult>();
        if (response == null || !response.ok)
        {
            LastMessage = response != null && !string.IsNullOrEmpty(response.error) ? response.error : "The server did not answer.";
            return results;
        }

        // The server's word is final: balances and pity counters become what it says
        ResourceManager.Instance.SetResourceAmount(ResourceManager.Letters, response.lettersAfter);
        RestoreState(response.totalPulls, response.pity);

        Tavern tavern = FindTavern();
        Vector3 spawnPoint = tavern != null ? tavern.SpawnPoint : Vector3.zero;
        List<CharacterType> types = PullableTypes();
        foreach (PullRollData roll in response.rolls)
        {
            CharacterType type = types.Find(t => t.typeId == roll.typeId);
            if (type == null)
            {
                Debug.LogWarning($"The server gave character type {roll.typeId}, which this version of the game does not know; skipped.");
                continue;
            }

            PullResult result = Grant(type, spawnPoint);
            result.Rarity = (Rarity)roll.rarity;
            result.PityApplied = roll.pityApplied;
            results.Add(result);
        }

        LastMessage = $"Recruited {results.Count}.";
        Pulled?.Invoke(results);
        return results;
    }

    // What an offline backend answers when asked to pull (the game itself plays the part of the server): checks
    // and charges the letters, rolls with the pool's odds and pity rules, and reports the result. An online backend
    // does exactly this on its own server, with its own copy of the odds.
    public PullResponse LocalPull(PullRequest request)
    {
        int count = request.count;
        if (count < 1) return PullResponse.Failed("Nothing to pull.");
        if (Letters < CostOf(count)) return PullResponse.Failed($"You need {CostOf(count)} recruitment letters (you have {Letters}).");

        List<CharacterType> types = PullableTypes();
        if (types.Count == 0) return PullResponse.Failed("Nobody can be recruited right now.");

        List<GachaRoller.Candidate> candidates = new List<GachaRoller.Candidate>();
        foreach (CharacterType type in types) candidates.Add(new GachaRoller.Candidate(type.rarity, type.pullWeight));

        EnsurePityCounters();
        PullResponse response = new PullResponse { lettersAfter = Letters - CostOf(count) };
        for (int i = 0; i < count; i++)
        {
            GachaRoller.Result roll = GachaRoller.Pick(candidates, Pool.odds, Pool.pity, pityCounters, random);
            TotalPulls++;
            if (roll.candidate < 0) continue; // nothing with a chance to be pulled: cannot happen after the checks, but never crash
            response.rolls.Add(new PullRollData { typeId = types[roll.candidate].typeId, rarity = (int)roll.rarity, pityApplied = roll.pityApplied });
        }
        response.totalPulls = TotalPulls;
        response.pity.AddRange(pityCounters);
        return response;
    }

    // A new character joins, or (when none more can be owned) the duplicate makes one stronger
    private PullResult Grant(CharacterType type, Vector3 spawnPoint)
    {
        PullResult result = new PullResult { Type = type };
        CharacterManager characters = CharacterManager.Instance;

        if (characters.CanHire(type))
        {
            result.Character = characters.Hire(type, spawnPoint);
            result.IsNew = result.Character != null;
            return result;
        }

        // Duplicate: give it to whoever of this type has the least self-awareness
        OwnedCharacter target = null;
        foreach (OwnedCharacter owned in characters.OwnedOf(type))
        {
            if (target == null || owned.AwarenessLevel < target.AwarenessLevel) target = owned;
        }

        result.Character = target;
        if (target == null) return result;

        if (!target.AwarenessMaxed)
        {
            target.AwarenessLevel++;
        }
        else
        {
            target.Loyalty += maxedDuplicateLoyalty;
            result.ConvertedToLoyalty = true;
            result.LoyaltyGained = maxedDuplicateLoyalty;
        }
        result.AwarenessLevel = target.AwarenessLevel;
        return result;
    }

    // --- Saving ---

    private void EnsurePityCounters()
    {
        int rules = Pool.pity != null ? Pool.pity.Count : 0;
        if (pityCounters.Length != rules) Array.Resize(ref pityCounters, rules);
    }

    public void RestoreState(int totalPulls, List<int> pity)
    {
        TotalPulls = Mathf.Max(0, totalPulls);
        pityCounters = new int[0];
        EnsurePityCounters();
        if (pity == null) return;
        for (int i = 0; i < pityCounters.Length && i < pity.Count; i++) pityCounters[i] = Mathf.Max(0, pity[i]);
    }
}
