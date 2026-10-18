using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

public class ResourceManager : MonoBehaviour, IItemStock
{

    // Singleton pattern: Ensures only one instance exists
    public static ResourceManager Instance { get; private set; }

    public const string Gold = "Gold";
    public const string Wood = "Wood";
    public const string Letters = "Letters"; // recruitment letters: the currency for pulling characters at the tavern

    [SerializeField] public Dictionary<string, Resource> resources = new Dictionary<string, Resource>();



    private void Awake()
    {
        // Singleton implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Destroy duplicate
            return;
        }
        Instance = this;
    }


    private void Start()
    {
        ResetResources();


        StartCoroutine(ResourceLoop());

    }

    // What a new game starts with
    public void ResetResources()
    {
        resources.Clear();
        AddResource(Gold, 100, 0);
        AddResource(Wood, 50, 0);
        AddResource(Letters, 2, 0);
        foreach (ItemAmount starter in FoodSettings.Current.starterStock)
        {
            if (starter.item != null && !resources.ContainsKey(starter.Name)) AddResource(starter.Name, starter.amount, 0);
        }
        UIManager.Instance?.UpdateResourceUI(resources); // Update UI
    }

    IEnumerator ResourceLoop()
    {
        while (true)
        {
            if (!GameManager.Instance.SimulationPaused) ProduceFor(1f);
            yield return new WaitForSeconds(1f);
        }
    }

    // --- Production ---

    private readonly List<ProductionLine> lines = new List<ProductionLine>();
    private readonly Dictionary<string, float> madeNow = new Dictionary<string, float>();
    private readonly Dictionary<string, float> usedNow = new Dictionary<string, float>();
    private readonly Dictionary<string, float> madeAverage = new Dictionary<string, float>();  // per second, smoothed
    private readonly Dictionary<string, float> usedAverage = new Dictionary<string, float>();

    // Every producing building makes (and uses) what it can in this many seconds of play
    public void ProduceFor(float seconds)
    {
        if (BuildingManager.Instance == null || seconds <= 0f) return;

        lines.Clear();
        foreach (Building building in BuildingManager.Instance.Buildings)
        {
            if (building.Producer != null) lines.Add(building.Producer.PrepareLine());
        }

        madeNow.Clear();
        usedNow.Clear();
        ProductionNetwork.Step(lines, this, seconds, madeNow, usedNow);
        Smooth(madeAverage, madeNow, seconds);
        Smooth(usedAverage, usedNow, seconds);
        UIManager.Instance?.UpdateResourceUI(resources);
    }

    // A running average of items per second, so the numbers on screen do not jump about
    private static void Smooth(Dictionary<string, float> average, Dictionary<string, float> now, float seconds)
    {
        const float Keep = 0.8f;
        List<string> names = new List<string>(average.Keys);
        foreach (string name in now.Keys) if (!average.ContainsKey(name)) names.Add(name);
        foreach (string name in names)
        {
            now.TryGetValue(name, out float amount);
            average.TryGetValue(name, out float old);
            average[name] = old * Keep + (amount / seconds) * (1f - Keep);
        }
    }

    // Items per second the town has been making / using up lately (a smoothed figure)
    public float MadePerSecond(string item) { madeAverage.TryGetValue(item, out float v); return v < 0.00001f ? 0f : v; }
    public float UsedPerSecond(string item) { usedAverage.TryGetValue(item, out float v); return v < 0.00001f ? 0f : v; }

    // --- Stock (IItemStock) ---

    public float Get(string item)
    {
        return resources.TryGetValue(item, out Resource resource) ? resource.currentAmount : 0f;
    }

    // How much of the item the town can hold: what the item allows by itself plus what storage buildings add (0 = no limit)
    public float Capacity(string item)
    {
        float baseCapacity = ItemCatalog.BaseCapacity(item);
        if (baseCapacity <= 0f) return 0f;

        float total = baseCapacity;
        if (BuildingManager.Instance != null)
        {
            foreach (Building building in BuildingManager.Instance.Buildings)
            {
                if (building.buildingType == null || building.buildingType.storageBonus == null) continue;
                foreach (ItemAmount bonus in building.buildingType.storageBonus)
                {
                    if (bonus.item != null && bonus.Name == item) total += bonus.amount;
                }
            }
        }
        return total;
    }

    // Adds (or takes) some of an item, staying between 0 and the capacity. Does not redraw the UI (the caller does, once).
    public void Add(string item, float amount)
    {
        if (!resources.TryGetValue(item, out Resource resource))
        {
            resource = new Resource(item, 0f, 0f);
            resources.Add(item, resource);
        }
        float capacity = Capacity(item);
        float result = resource.currentAmount + amount;
        if (capacity > 0f) result = Mathf.Min(result, capacity);
        resource.currentAmount = Mathf.Max(0f, result);
    }

    // Add a new resource
    public void AddResource(string name, float startingAmount = 0, float growthRate = 0)
    {
        if (!resources.ContainsKey(name))
        {
            resources.Add(name, new Resource(name, startingAmount, growthRate));
        }
        else
        {
            Debug.LogWarning($"Resource {name} already exists!");
        }
    }

    // Sets a resource to an exact amount (creating it if needed). Used when loading a game.
    public void SetResourceAmount(string name, float amount)
    {
        if (resources.TryGetValue(name, out Resource resource))
        {
            resource.currentAmount = amount;
        }
        else
        {
            AddResource(name, amount);
        }
        UIManager.Instance?.UpdateResourceUI(resources);
    }

    // Add or remove from a resource, return true if successful, false if failed
    public bool changeResourceAmount(string name, float amount)
    {
        if (resources.ContainsKey(name))
        {
            if (resources[name].currentAmount + amount >= 0)
            {
                resources[name].currentAmount += amount;
                UIManager.Instance?.UpdateResourceUI(resources); // Update UI
                return true;
            }
            else
            {
                Debug.LogWarning($"Not enough {name} to subtract!");
                return false;
            }
            
        }
        else
        {
            Debug.LogWarning($"Resource {name} not found!");
            return false;
        }
    }

    
    // Get current amount
    public float GetResourceAmount(string name)
    {
        if (resources.ContainsKey(name))
        {
            return resources[name].currentAmount;
        }
        Debug.LogWarning($"Resource {name} not found!");
        return 0;
    }

    public float GetTotalWoodPerSec()
    {
        float total = 0f;
        foreach (var building in BuildingManager.Instance.Buildings)
        {
            if (building.Producer != null) total += building.Producer.WoodPerSec;
        }
        return total;
    }

    public float GetTotalGoldPerSec()
    {
        float total = 0f;
        foreach (var building in BuildingManager.Instance.Buildings)
        {
            if (building.Producer != null) total += building.Producer.GoldPerSec;
        }
        return total;
    }
}
