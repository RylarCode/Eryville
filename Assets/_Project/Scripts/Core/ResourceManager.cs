using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{

    // Singleton pattern: Ensures only one instance exists
    public static ResourceManager Instance { get; private set; }

    [SerializeField] public Dictionary<string, Resource> resources = new Dictionary<string, Resource>();
    [SerializeField] public List<Building> activeBuildings = new List<Building>();



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
        resources.Clear();
        AddResource("Gold", 100, 0);
        AddResource("Wood", 50, 0);
        UIManager.Instance?.UpdateResourceUI(resources); // Update UI


        StartCoroutine(ResourceLoop());

    }

    IEnumerator ResourceLoop()
    {
        while (true)
        {
            if (GameManager.Instance.currentGameState == GameManager.gameState.Normal)
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

    public void RegisterBuilding(Building building)
    {
        activeBuildings.Add(building);
    }

    public void UnregisterBuilding(Building building)
    {
        activeBuildings.Remove(building);
    }

    public float GetTotalWoodPerSec()
    {
        float total = 0f;
        foreach (var building in activeBuildings)
        {
            total += building.woodGeneration;
        }
        return total;
    }

    public float GetTotalGoldPerSec()
    {
        float total = 0f;
        foreach (var building in activeBuildings)
        {
            total += building.goldGeneration;
        }
        return total;
    }
}
