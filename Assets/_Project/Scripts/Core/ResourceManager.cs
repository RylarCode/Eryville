using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

public class ResourceManager : MonoBehaviour
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
        UIManager.Instance?.UpdateResourceUI(resources); // Update UI
    }

    IEnumerator ResourceLoop()
    {
        while (true)
        {
            if (!GameManager.Instance.SimulationPaused)
            {
                //foreach (var res in resources.Values)
                //{
                //    changeResourceAmount(res.resourceName, res.growthPerSecond);
                //}
                changeResourceAmount("Wood", GetTotalWoodPerSec());
                changeResourceAmount("Gold", GetTotalGoldPerSec());
            }
            yield return new WaitForSeconds(1f);

        }
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
