using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Components on a building prefab implement this to react once the Building has been set up.
public interface IBuildingComponent
{
    void OnBuildingInitialized(Building building);
}

// A component on a building prefab implements this to handle clicks on that building.
public interface IBuildingClickHandler
{
    void OnBuildingClicked();
}

// Core of every building: which type it is, where it is, and who works / lives in it.
// Type-specific behaviour lives in components on the prefab (ResourceProducer, Housing, CityHall...).
public class Building : MonoBehaviour, IClickable
{
    public BuildingType buildingType;  // the template this building was created from (name, size, costs, rates...)
    [FormerlySerializedAs("startingCell")]
    public Vector2Int cell = new Vector2Int(0, 0); // anchor cell (in grid cell space)

    // Stable identity, for saving and for referring to a building without holding a reference
    public string Id { get; private set; } = System.Guid.NewGuid().ToString("N");

    public string DisplayName => buildingType != null ? buildingType.buildingName : name;

    // Present only on buildings that produce resources (see ResourceProducer)
    public ResourceProducer Producer { get; private set; }

    // Occupants. Only the Assign/Remove methods below change workers / residents (and the matching
    // OwnedCharacter.WorkBuilding / HomeBuilding), so both sides always agree.
    private readonly List<OwnedCharacter> workers = new List<OwnedCharacter>();     // assigned to work here
    private readonly List<OwnedCharacter> residents = new List<OwnedCharacter>();   // assigned to live here
    // Avatars of assigned workers who are physically here right now. Only these add to the output.
    [SerializeField] private List<Character> workersOnSite = new List<Character>();

    public IReadOnlyList<OwnedCharacter> Workers => workers;
    public IReadOnlyList<OwnedCharacter> Residents => residents;
    public IReadOnlyList<Character> WorkersOnSite => workersOnSite;

    // Capacity comes from the BuildingType
    public int WorkerSlots => buildingType != null ? buildingType.workerSlots : 0;
    public int HousingSlots => buildingType != null ? buildingType.housingSlots : 0;
    public bool HasWorkerSlot => workers.Count < WorkerSlots;
    public bool HasHousingSlot => residents.Count < HousingSlots;

    // Add other properties: health, level, etc.

    private void Awake()
    {
        Producer = GetComponent<ResourceProducer>();
    }

    // Optional: Called when placed
    public void OnPlaced()
    {
        Debug.Log($"{DisplayName} placed at {transform.position}");
    }

    public void OnClicked()
    {
        IBuildingClickHandler handler = GetComponent<IBuildingClickHandler>();
        if (handler != null)
        {
            handler.OnBuildingClicked();
        }
        else
        {
            Debug.Log($"Clicked on {DisplayName}");
        }
    }

    public void Initialize(BuildingType type, Vector2Int anchorCell)
    {
        buildingType = type;
        cell = anchorCell;
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

        if (Producer == null && (type.goldGenerationPerSec > 0 || type.woodGenerationPerSec > 0))
        {
            Debug.LogWarning($"{type.buildingName} has resource output but its prefab has no ResourceProducer component, so it will produce nothing.");
        }

        // Let the type-specific components (Housing, ...) do their setup now that the type is known
        foreach (IBuildingComponent component in GetComponents<IBuildingComponent>())
        {
            component.OnBuildingInitialized(this);
        }
    }

    // --- Workers ---

    // Returns false if the building has no free worker slot or the character already works here.
    // If the character works somewhere else, they are moved (only after a slot here is confirmed).
    public bool TryAssignWorker(OwnedCharacter character)
    {
        if (character == null || workers.Contains(character) || !HasWorkerSlot) return false;

        if (character.WorkBuilding != null) character.WorkBuilding.RemoveWorker(character);

        workers.Add(character);
        character.SetWorkBuilding(this);
        return true;
    }

    public void RemoveWorker(OwnedCharacter character)
    {
        if (!workers.Remove(character)) return;

        workersOnSite.RemoveAll(avatar => avatar == null || avatar.Owned == character);
        if (character.WorkBuilding == this) character.SetWorkBuilding(null);
    }

    // A worker's avatar physically reached / left the building. Only assigned workers count towards output.
    public void WorkerArrived(Character avatar)
    {
        if (!workers.Contains(avatar.Owned) || workersOnSite.Contains(avatar)) return;

        workersOnSite.Add(avatar);
    }

    public void WorkerLeft(Character avatar)
    {
        workersOnSite.Remove(avatar);
    }

    // --- Residents ---

    public bool TryAssignResident(OwnedCharacter character)
    {
        if (character == null || residents.Contains(character) || !HasHousingSlot) return false;

        if (character.HomeBuilding != null) character.HomeBuilding.RemoveResident(character);

        residents.Add(character);
        character.SetHomeBuilding(this);
        return true;
    }

    public void RemoveResident(OwnedCharacter character)
    {
        if (!residents.Remove(character)) return;

        if (character.HomeBuilding == this) character.SetHomeBuilding(null);
    }
}
