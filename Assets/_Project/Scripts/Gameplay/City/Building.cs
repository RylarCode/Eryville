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

    // Occupants. Only the Assign/Remove methods below change these lists (and the matching
    // Character.WorkBuilding / HomeBuilding), so both sides always agree.
    [SerializeField] private List<Character> workers = new List<Character>();         // assigned to work here
    [SerializeField] private List<Character> residents = new List<Character>();       // assigned to live here
    [SerializeField] private List<Character> workersOnSite = new List<Character>();   // workers physically here right now

    public IReadOnlyList<Character> Workers => workers;
    public IReadOnlyList<Character> Residents => residents;
    public IReadOnlyList<Character> WorkersOnSite => workersOnSite;

    // Capacity comes from the BuildingType
    public int WorkerSlots => buildingType != null ? buildingType.workerSlots : 0;
    public int HousingSlots => buildingType != null ? buildingType.housingSlots : 0;
    public bool HasWorkerSlot => workers.Count < WorkerSlots;
    public bool HasHousingSlot => residents.Count < HousingSlots;

    // Add other properties: health, level, etc.

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
        RecalculateOutput();
        ResourceManager.Instance.RegisterBuilding(this);

        // Homes immediately take in any homeless characters
        GetComponent<HomeBuilding>()?.Initialize(type);
    }

    public void RecalculateOutput()
    {
        float totalMultiplier = 1f; // Start at 1 because it's multiplication

        foreach (Character character in workersOnSite)
        {
            totalMultiplier *= character.resourceMultiplier;
        }

        woodGeneration = (int)(baseWoodGeneration*totalMultiplier);//rounds down
        goldGeneration = (int)(baseGoldGeneration*totalMultiplier);//rounds down
    }

    // --- Workers ---

    // Returns false if the building has no free worker slot or the character already works here.
    // If the character works somewhere else, they are moved (only after a slot here is confirmed).
    public bool TryAssignWorker(Character character)
    {
        if (character == null || workers.Contains(character) || !HasWorkerSlot) return false;

        if (character.WorkBuilding != null) character.WorkBuilding.RemoveWorker(character);

        workers.Add(character);
        character.SetWorkBuilding(this);
        return true;
    }

    public void RemoveWorker(Character character)
    {
        if (!workers.Remove(character)) return;

        workersOnSite.Remove(character);
        if (character.WorkBuilding == this) character.SetWorkBuilding(null);
        RecalculateOutput();
    }

    // A worker physically reached / left the building. Only assigned workers count towards output.
    public void WorkerArrived(Character character)
    {
        if (!workers.Contains(character) || workersOnSite.Contains(character)) return;

        workersOnSite.Add(character);
        RecalculateOutput();
    }

    public void WorkerLeft(Character character)
    {
        if (workersOnSite.Remove(character)) RecalculateOutput();
    }

    // --- Residents ---

    public bool TryAssignResident(Character character)
    {
        if (character == null || residents.Contains(character) || !HasHousingSlot) return false;

        if (character.HomeBuilding != null) character.HomeBuilding.RemoveResident(character);

        residents.Add(character);
        character.SetHomeBuilding(this);
        return true;
    }

    public void RemoveResident(Character character)
    {
        if (!residents.Remove(character)) return;

        if (character.HomeBuilding == this) character.SetHomeBuilding(null);
    }
}