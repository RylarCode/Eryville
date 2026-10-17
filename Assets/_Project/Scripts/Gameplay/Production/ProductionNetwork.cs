using System.Collections.Generic;
using UnityEngine;

// Where the items are kept. The town's stock implements it; offline progress uses a scratch copy.
public interface IItemStock
{
    float Get(string item);
    float Capacity(string item);          // 0 or less = no limit
    void Add(string item, float amount);  // may be negative; the stock keeps within 0 and the capacity
}

public enum LineStatus
{
    Working,           // making as much as it could
    NoWorker,          // nobody is there to work it
    NoRecipe,          // the building has nothing to make
    WaitingForInput,   // short of an input (see BlockedBy)
    StorageFull,       // no room for an output (see BlockedBy)
}

// One building's production for the solver: what it makes, how fast it could, and what actually happened.
public class ProductionLine
{
    // Set before a step
    public Recipe Recipe;
    public float Multiplier;   // 0 = not staffed; otherwise the speed factor (workers' productivity, 1 for a building that needs nobody)
    public object Owner;       // whatever the caller wants to find the line by (the Building)

    // Filled in by a step
    public LineStatus Status;
    public string BlockedBy;   // the item that held it back
    public float Efficiency;   // 0..1: what it made, as a share of what it could have made
    public float Cycles;       // cycles completed in the last step

    internal float Desired;
}

// Works out what every production building actually makes in a slice of time, given what the town has in stock.
// Pure code with no Unity objects, so it is tested exactly and the same code runs while playing (one second at a time) and
// while the game is closed (a minute at a time).
//
// How it shares things out:
//   1. Every building starts from what it could do: speed x time / cycle length (nothing if not staffed).
//   2. Output room: if the buildings making an item would overfill its storage, they all slow down in proportion.
//   3. Inputs: if the buildings using an item want more than there is, they all slow down in proportion (nobody is served first).
//   4. What was used up is taken from the stock and what was made is added. Things made in this step can be used from the next one.
public static class ProductionNetwork
{
    private const float Epsilon = 0.0001f;

    private static readonly Dictionary<string, float> wanted = new Dictionary<string, float>();
    private static readonly Dictionary<string, float> factor = new Dictionary<string, float>();
    private static readonly List<string> keys = new List<string>();

    // produced / consumed (optional) get what this step made and used up, added to whatever they hold
    public static void Step(IReadOnlyList<ProductionLine> lines, IItemStock stock, float seconds, Dictionary<string, float> produced = null, Dictionary<string, float> consumed = null)
    {
        // 1. What each could do
        foreach (ProductionLine line in lines)
        {
            line.BlockedBy = null;
            line.Efficiency = 0f;
            if (line.Recipe == null) { line.Status = LineStatus.NoRecipe; line.Desired = 0f; line.Cycles = 0f; continue; }
            if (line.Multiplier <= 0f) { line.Status = LineStatus.NoWorker; line.Desired = 0f; line.Cycles = 0f; continue; }
            line.Status = LineStatus.Working;
            line.Desired = line.Multiplier * seconds / line.Recipe.CycleSeconds;
            line.Cycles = line.Desired;
        }

        // 2. Room for the outputs
        wanted.Clear();
        foreach (ProductionLine line in lines)
        {
            if (line.Cycles <= 0f) continue;
            foreach (ItemAmount output in line.Recipe.outputs)
            {
                if (output.item == null || output.amount <= 0f) continue;
                wanted.TryGetValue(output.Name, out float sum);
                wanted[output.Name] = sum + line.Cycles * output.amount;
            }
        }
        BuildFactors(stock, room: true);
        foreach (ProductionLine line in lines)
        {
            if (line.Cycles <= 0f) continue;
            float limit = 1f;
            string item = null;
            foreach (ItemAmount output in line.Recipe.outputs)
            {
                if (output.item == null || output.amount <= 0f) continue;
                if (factor.TryGetValue(output.Name, out float f) && f < limit) { limit = f; item = output.Name; }
            }
            if (limit < 1f - Epsilon) Limit(line, limit, LineStatus.StorageFull, item);
        }

        // 3. Enough of the inputs
        wanted.Clear();
        foreach (ProductionLine line in lines)
        {
            if (line.Cycles <= 0f) continue;
            foreach (ItemAmount input in line.Recipe.inputs)
            {
                if (input.item == null || input.amount <= 0f) continue;
                wanted.TryGetValue(input.Name, out float sum);
                wanted[input.Name] = sum + line.Cycles * input.amount;
            }
        }
        BuildFactors(stock, room: false);
        foreach (ProductionLine line in lines)
        {
            if (line.Cycles <= 0f) continue;
            float limit = 1f;
            string item = null;
            foreach (ItemAmount input in line.Recipe.inputs)
            {
                if (input.item == null || input.amount <= 0f) continue;
                if (factor.TryGetValue(input.Name, out float f) && f < limit) { limit = f; item = input.Name; }
            }
            if (limit < 1f - Epsilon) Limit(line, limit, LineStatus.WaitingForInput, item);
        }

        // 4. Use up and make
        foreach (ProductionLine line in lines)
        {
            line.Efficiency = line.Desired > 0f ? Mathf.Clamp(line.Cycles / line.Desired, 0f, 1f) : 0f;
            if (line.Cycles <= 0f) continue;
            foreach (ItemAmount input in line.Recipe.inputs)
            {
                if (input.item == null || input.amount <= 0f) continue;
                float used = line.Cycles * input.amount;
                stock.Add(input.Name, -used);
                if (consumed != null) { consumed.TryGetValue(input.Name, out float sum); consumed[input.Name] = sum + used; }
            }
            foreach (ItemAmount output in line.Recipe.outputs)
            {
                if (output.item == null || output.amount <= 0f) continue;
                float made = line.Cycles * output.amount;
                stock.Add(output.Name, made);
                if (produced != null) { produced.TryGetValue(output.Name, out float sum); produced[output.Name] = sum + made; }
            }
        }
    }

    private static void Limit(ProductionLine line, float limit, LineStatus status, string item)
    {
        // Several limits can apply; the tightest one is what is reported
        float already = line.Desired > 0f ? line.Cycles / line.Desired : 1f;
        line.Cycles *= limit;
        if (limit <= already + Epsilon)
        {
            line.Status = status;
            line.BlockedBy = item;
        }
    }

    // For every item in "wanted": 1 if there is enough room / stock for all of it, otherwise the share that fits
    private static void BuildFactors(IItemStock stock, bool room)
    {
        factor.Clear();
        keys.Clear();
        keys.AddRange(wanted.Keys);
        foreach (string item in keys)
        {
            float amount = wanted[item];
            float available;
            if (room)
            {
                float capacity = stock.Capacity(item);
                available = capacity > 0f ? Mathf.Max(0f, capacity - stock.Get(item)) : float.MaxValue;
            }
            else
            {
                available = Mathf.Max(0f, stock.Get(item));
            }
            factor[item] = amount > available ? (amount <= 0f ? 1f : available / amount) : 1f;
        }
    }
}
