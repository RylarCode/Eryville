using System.Collections.Generic;
using UnityEngine;

// Makes things in a building: its recipe (see Recipe and ProductionNetwork). Base speed comes from the recipe; a building that
// has worker slots only works while at least one assigned worker is physically on site (not while they are walking there),
// and every such worker multiplies the speed by their productivity.
// The actual making is done by the ResourceManager's once-a-second step; this holds the building's side of it: which recipe is
// chosen, how fast the building could go, and what happened last time (working, waiting for an input...).
[RequireComponent(typeof(Building))]
public class ResourceProducer : MonoBehaviour
{
    private Building building;
    private readonly ProductionLine line = new ProductionLine();
    private int activeRecipe;

    private void Awake()
    {
        building = GetComponent<Building>();
    }

    // Whether the building is staffed right now. A building with no worker slots cannot be staffed, so it just runs.
    public bool IsRunning => building.WorkerSlots <= 0 || building.WorkersOnSite.Count > 0;

    public float WorkerMultiplier
    {
        get
        {
            float multiplier = 1f; // Start at 1 because it's multiplication
            foreach (Character worker in building.WorkersOnSite)
            {
                multiplier *= worker.Productivity;
            }
            return multiplier;
        }
    }

    // --- Recipes ---

    public IReadOnlyList<Recipe> Recipes => building.buildingType != null ? building.buildingType.Recipes : new List<Recipe>();

    // Which of the building's recipes it makes (saved with the game)
    public int ActiveRecipeIndex
    {
        get { return Mathf.Clamp(activeRecipe, 0, Mathf.Max(0, Recipes.Count - 1)); }
        set { activeRecipe = Mathf.Clamp(value, 0, Mathf.Max(0, Recipes.Count - 1)); }
    }

    public Recipe ActiveRecipe => Recipes.Count > 0 ? Recipes[ActiveRecipeIndex] : null;

    // --- What it does ---

    // Gets the line ready for the next production step
    public ProductionLine PrepareLine()
    {
        line.Owner = building;
        line.Recipe = ActiveRecipe;
        line.Multiplier = IsRunning ? WorkerMultiplier : 0f;
        return line;
    }

    // What happened in the latest step (Working until the first step has run)
    public LineStatus Status
    {
        get
        {
            if (ActiveRecipe == null) return LineStatus.NoRecipe;
            if (!IsRunning) return LineStatus.NoWorker;                          // always up to date: people come and go between steps
            return line.Recipe == null || line.Status == LineStatus.NoWorker ? LineStatus.Working : line.Status;
        }
    }
    public string BlockedBy => line.BlockedBy;
    public float Efficiency => line.Efficiency;

    // How much of an item the building makes / uses per second when it is staffed (at its speed now), ignoring shortages
    public float OutputPerSec(string item)
    {
        return PerSecond(ActiveRecipe != null ? ActiveRecipe.outputs : null, item);
    }

    public float InputPerSec(string item)
    {
        return PerSecond(ActiveRecipe != null ? ActiveRecipe.inputs : null, item);
    }

    private float PerSecond(List<ItemAmount> amounts, string item)
    {
        if (amounts == null || !IsRunning) return 0f;
        float total = 0f;
        foreach (ItemAmount a in amounts)
        {
            if (a.item != null && a.Name == item) total += ActiveRecipe.PerSecond(a, WorkerMultiplier);
        }
        return total;
    }

    // All outputs together per second (0 when not staffed), for sorting and filtering
    public float TotalOutputPerSec
    {
        get
        {
            if (ActiveRecipe == null || !IsRunning) return 0f;
            float total = 0f;
            foreach (ItemAmount a in ActiveRecipe.outputs) if (a.item != null) total += ActiveRecipe.PerSecond(a, WorkerMultiplier);
            return total;
        }
    }

    // "12/s" for fast things, "18/min" for slow ones (a farm makes a handful a minute)
    public static string RateText(float perSecond)
    {
        return perSecond >= 1f || perSecond <= 0f ? $"{perSecond:0.#}/s" : $"{perSecond * 60f:0.#}/min";
    }

    public float GoldPerSec => OutputPerSec(ResourceManager.Gold);
    public float WoodPerSec => OutputPerSec(ResourceManager.Wood);

    // One line for the UI about how it is doing: "Working", "Waiting for Flour", "Storage full: Bread", "Nobody is working here right now"
    public string StatusText
    {
        get
        {
            switch (Status)
            {
                case LineStatus.NoWorker: return "Not producing: nobody is working here right now";
                case LineStatus.WaitingForInput: return $"Waiting for {BlockedBy}";
                case LineStatus.StorageFull: return $"Storage full: {BlockedBy}";
                case LineStatus.NoRecipe: return "Nothing to make";
                default: return "Working";
            }
        }
    }
}
