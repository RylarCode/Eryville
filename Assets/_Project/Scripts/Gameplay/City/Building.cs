using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Building : MonoBehaviour
{
    // Default Values, Adjust actual values in Hierarchy
    public string buildingName = "Generic Building";
    public BuildingType buildingType;
    public Vector2Int size = new Vector2Int(1, 1); // Size in grid cells
    [FormerlySerializedAs("startingCell")]
    public Vector2Int cell = new Vector2Int(0, 0); // anchor cell (in grid cell space)
    public float baseWoodGeneration = 0;
    public float baseGoldGeneration = 0;
    public int woodGeneration = 0;
    public int goldGeneration = 0;
    [FormerlySerializedAs("buildingclass")]
    public BuildingType.Role role;

    public List<Character> workingCharacters = new List<Character>();
    public List<Character> assignedCharacters = new List<Character>();
    public int maxCharacters;

    // Add other properties: resource generation rate, capacity, health, etc.

    // Optional: Called when placed
    public void OnPlaced()
    {
        Debug.Log($"{buildingName} placed at {transform.position}");
    }

    public void OnClicked()
    {
        if (role == BuildingType.Role.Special)
        {
            gameObject.GetComponent<CityHall>().OnClicked();
        }
        else
        {
            Debug.Log($"Clicked on {buildingName}");
        }
            
    }

    public void Initialize(BuildingType type, Vector2Int anchorCell)
    {
        buildingType = type;
        cell = anchorCell;
        buildingName = type.buildingName;
        role = type.role;
        GetComponent<SpriteRenderer>().sprite = buildingType.sprite;
        Vector2 spriteWorldSize = buildingType.sprite.bounds.size; // in Unity units
        Vector3 scale = new Vector3(
            buildingType.size.x / spriteWorldSize.x,
            buildingType.size.y / spriteWorldSize.y,
            1f
        );
        transform.localScale = scale;

        // Adjust the collider to match the intended size
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            // Since the object is already scaled, divide to counteract it
            collider.size = new Vector2(
                buildingType.size.x / scale.x,
                buildingType.size.y / scale.y
            );
            collider.offset = Vector2.zero; // Optional: reset offset
        }

        // Resource Generation (If applicable)
        baseWoodGeneration = buildingType.woodGenerationPerSec;
        baseGoldGeneration = buildingType.goldGenerationPerSec;
        maxCharacters = buildingType.maxCharacters;
        updateResourceGeneration();
        ResourceManager.Instance.RegisterBuilding(this);


    }

    public void updateResourceGeneration()
    {
        float totalMultiplier = 1f; // Start at 1 because it's multiplication

        foreach (Character character in workingCharacters)
        {
            totalMultiplier *= character.resourceMultiplier;
        }

        woodGeneration = (int)(baseWoodGeneration*totalMultiplier);//rounds down
        goldGeneration = (int)(baseGoldGeneration*totalMultiplier);//rounds down
    }


    public void registerCharacter(Character character)
    {
        if (assignedCharacters.Contains(character))
        {
            Debug.Log("Already Registered!");
        }
        assignedCharacters.Add(character);
        updateResourceGeneration();
    }

    public void unregisterCharacter(Character character)
    {
        assignedCharacters.Remove(character);
        workingCharacters.Remove(character);
        updateResourceGeneration();
    }

    public void WorkerArrived(Character character)
    {
        workingCharacters.Add(character);
        updateResourceGeneration();
    }

    public void WorkerLeft(Character character)
    {
        workingCharacters.Remove(character);
        updateResourceGeneration();
    }
}