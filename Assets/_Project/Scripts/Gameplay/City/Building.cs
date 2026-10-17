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
    [SerializeField, FormerlySerializedAs("startingCell")]
    private Vector2Int cell = new Vector2Int(0, 0); // anchor cell (in grid cell space)

    // Where the building is on the grid. Only BuildingManager changes it (Build / Move), together with the cell map.
    public Vector2Int Cell => cell;
    internal void SetCell(Vector2Int anchorCell) { cell = anchorCell; UpdateDepth(); }

    // Stable identity, for saving and for referring to a building without holding a reference
    public string Id { get; internal set; } = System.Guid.NewGuid().ToString("N");

    public string DisplayName => buildingType != null ? buildingType.buildingName : name;

    // The renderer that shows the building: the one on the prefab itself, or (3/4 art, which is raised above the building's
    // cells) one on a child. Use this, not GetComponent<SpriteRenderer>(), to colour or hide the picture.
    public SpriteRenderer Body { get; private set; }

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

    // Avatars resting inside right now (residents at home, or anyone at a place of leisure)
    private readonly List<Character> resting = new List<Character>();
    public int RestingInside
    {
        get
        {
            resting.RemoveAll(avatar => avatar == null);
            return resting.Count;
        }
    }
    public void RestingArrived(Character avatar) { if (!resting.Contains(avatar)) resting.Add(avatar); }
    public void RestingLeft(Character avatar) { resting.Remove(avatar); }

    // Capacity comes from the BuildingType
    public int WorkerSlots => buildingType != null ? buildingType.workerSlots : 0;
    public int HousingSlots => buildingType != null ? buildingType.housingSlots : 0;
    public bool HasWorkerSlot => workers.Count < WorkerSlots;
    public bool HasHousingSlot => residents.Count < HousingSlots;

    // Add other properties: health, level, etc.

    private void Awake()
    {
        Producer = GetComponent<ResourceProducer>();
        if (Body == null) Body = GetComponent<SpriteRenderer>();
    }

    // Drawing order: a building is at the front edge of its cells, so characters further down the screen are drawn in front
    // of it and characters further up (behind it) are hidden by its picture.
    public void UpdateDepth()
    {
        if (buildingType == null) return;
        int order = DepthSorting.Order(transform.position.y - buildingType.size.y * 0.5f);
        if (Body != null) Body.sortingOrder = order;
        SpriteRenderer root = GetComponent<SpriteRenderer>();
        if (root != null) root.sortingOrder = order; // the click handling reads the order from here
    }

    // Optional: Called when placed
    public void OnPlaced()
    {
        Debug.Log($"{DisplayName} placed at {transform.position}");
    }

    public int ClickPriority => 0;

    // Raised when the player clicks a building; the UI listens, so Building knows nothing about it
    public static event System.Action<Building> Selected;

    public void OnClicked()
    {
        Selected?.Invoke(this);

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
        SpriteRenderer root = GetComponent<SpriteRenderer>();
        BuildingLayout layout = BuildingLayout.For(type);
        Body = root;
        BoxCollider2D collider = GetComponent<BoxCollider2D>();

        if (layout.Raised)
        {
            // 3/4 art: the picture rises above the cells, so it lives on a child that is shifted up; the building itself stays
            // centred on its cells (workers walk to its position, entrances are worked out from its cells)
            Transform art = transform.Find("Art");
            if (art == null)
            {
                GameObject child = new GameObject("Art");
                child.transform.SetParent(transform, false);
                art = child.transform;
                SpriteRenderer childRenderer = child.AddComponent<SpriteRenderer>();
                if (root != null)
                {
                    childRenderer.sharedMaterial = root.sharedMaterial;
                    childRenderer.sortingLayerID = root.sortingLayerID;
                    childRenderer.color = root.color;
                }
            }
            Body = art.GetComponent<SpriteRenderer>();
            Body.sprite = buildingType.sprite;
            if (root != null) root.enabled = false;
            transform.localScale = Vector3.one;
            art.localPosition = new Vector3(layout.ArtOffset.x, layout.ArtOffset.y, 0f);
            art.localScale = new Vector3(layout.Scale.x, layout.Scale.y, 1f);

            // Clicking anywhere on the picture counts
            if (collider != null)
            {
                collider.size = layout.ArtSize;
                collider.offset = layout.ArtOffset;
            }
        }
        else
        {
            if (root != null) root.sprite = buildingType.sprite;
            transform.localScale = new Vector3(layout.Scale.x, layout.Scale.y, 1f);

            // Adjust the collider to match the intended size
            if (collider != null)
            {
                // Since the object is already scaled, divide to counteract it
                collider.size = new Vector2(
                    buildingType.size.x / layout.Scale.x,
                    buildingType.size.y / layout.Scale.y
                );
                collider.offset = Vector2.zero; // Optional: reset offset
            }
        }
        UpdateDepth();

        if (type.animations != null && Body != null)
        {
            BuildingAnimator animator = GetComponent<BuildingAnimator>();
            if (animator == null) animator = gameObject.AddComponent<BuildingAnimator>();
            animator.Setup(this, Body, type.animations, type.sprite);
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

    // Whether this assigned worker's avatar is physically at the building right now
    public bool IsWorkerOnSite(OwnedCharacter worker)
    {
        foreach (Character avatar in workersOnSite)
        {
            if (avatar != null && avatar.Owned == worker) return true;
        }
        return false;
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
