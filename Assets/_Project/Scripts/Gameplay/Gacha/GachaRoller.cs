using System;
using System.Collections.Generic;

// The dice of the gacha, kept free of Unity so it can be tested exactly. Given what can be pulled, the odds
// and the pity rules, it picks one result. The randomness comes in as a function returning 0 <= x < 1.
public static class GachaRoller
{
    public struct Candidate
    {
        public Rarity rarity;
        public float weight; // relative chance among candidates of the same rarity

        public Candidate(Rarity rarity, float weight) { this.rarity = rarity; this.weight = weight; }
    }

    public struct Result
    {
        public int candidate;     // index into the candidates list, or -1 if nothing could be picked
        public Rarity rarity;
        public bool pityApplied;  // the pity rules forced a minimum rarity for this pull
    }

    // pityCounters has one entry per rule: how many pulls in a row have been below that rule's rarity.
    // It is updated here, so the caller only has to keep it between pulls.
    public static Result Pick(IList<Candidate> candidates, IList<RarityOdds> odds, IList<PityRule> rules, int[] pityCounters, Func<double> random)
    {
        Result result = new Result { candidate = -1 };

        // Pity: if any rule is due, the result must be at least that rare
        Rarity minimum = Rarity.Common;
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].every > 0 && pityCounters[i] + 1 >= rules[i].every && rules[i].minRarity > minimum) minimum = rules[i].minRarity;
        }

        List<Rarity> allowed = RaritiesPresent(candidates, minimum);
        if (allowed.Count == 0)
        {
            minimum = Rarity.Common; // nothing that rare to give: fall back to whatever exists
            allowed = RaritiesPresent(candidates, minimum);
            if (allowed.Count == 0) return result;
        }
        result.pityApplied = minimum > Rarity.Common;

        // Pick a rarity by its odds among the ones that can be given
        float total = 0f;
        foreach (Rarity rarity in allowed) total += OddsOf(odds, rarity);
        Rarity chosen = allowed[allowed.Count - 1];
        if (total <= 0f)
        {
            chosen = allowed[Math.Min((int)(Next(random) * allowed.Count), allowed.Count - 1)]; // no odds given: all equal
        }
        else
        {
            double roll = Next(random) * total;
            foreach (Rarity rarity in allowed)
            {
                roll -= OddsOf(odds, rarity);
                if (roll < 0) { chosen = rarity; break; }
            }
        }

        // Pick a character of that rarity by its weight
        float weightTotal = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].rarity == chosen && candidates[i].weight > 0f) weightTotal += candidates[i].weight;
        }
        double pick = Next(random) * weightTotal;
        int picked = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].rarity != chosen || candidates[i].weight <= 0f) continue;
            picked = i;
            pick -= candidates[i].weight;
            if (pick < 0) break;
        }

        result.candidate = picked;
        result.rarity = chosen;

        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].every <= 0) continue;
            pityCounters[i] = chosen >= rules[i].minRarity ? 0 : pityCounters[i] + 1;
        }
        return result;
    }

    private static double Next(Func<double> random)
    {
        double value = random();
        return value < 0 ? 0 : (value >= 1 ? 0.999999999 : value);
    }

    private static float OddsOf(IList<RarityOdds> odds, Rarity rarity)
    {
        float total = 0f;
        foreach (RarityOdds entry in odds)
        {
            if (entry.rarity == rarity && entry.weight > 0f) total += entry.weight;
        }
        return total;
    }

    // The rarities (from the minimum up) that have at least one candidate that can be pulled
    private static List<Rarity> RaritiesPresent(IList<Candidate> candidates, Rarity minimum)
    {
        List<Rarity> present = new List<Rarity>();
        foreach (Rarity rarity in (Rarity[])Enum.GetValues(typeof(Rarity)))
        {
            if (rarity < minimum) continue;
            foreach (Candidate candidate in candidates)
            {
                if (candidate.rarity == rarity && candidate.weight > 0f) { present.Add(rarity); break; }
            }
        }
        return present;
    }
}
