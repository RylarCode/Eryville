using UnityEngine;
using UnityEngine.Tilemaps; // Needed for Tilemap interaction
using UnityEngine.InputSystem; // If using new Input System directly here
using System.Collections.Generic;

public class BuildingPlacer : MonoBehaviour
{
    public static BuildingPlacer Instance { get; private set; }

    public List<BuildingType> AllBuildings; // Drag all ScriptableObjects here OR load them dynamically



    [SerializeField] private Grid buildingGrid; // Assign the Grid GameObject in Inspector
    [SerializeField] private Tilemap placementTilemap; // Assign PlacementTilemap
    [SerializeField] private Camera mainCamera; // Assign Main Camera

    public BuildingType buildingToPlace = null; // The prefab of the building we are currently trying to place

    // Optional: Visual feedback for placement
    [SerializeField] private GameObject placementIndicator; // A semi-transparent sprite matching building size
    public GameObject currentIndicatorInstance;

    private void Awake()
    {
        // Singleton implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Destroy duplicate
            return;
        }
        Instance = this;


        if (mainCamera == null) mainCamera = Camera.main; // Auto-find if not assigned
        if (buildingGrid == null) Debug.LogError("Building Grid not assigned!");
        if (placementTilemap == null) Debug.LogError("Placement Tilemap not assigned!");

        // Create indicator instance if prefab is assigned
        if (placementIndicator != null)
        {
            currentIndicatorInstance = Instantiate(placementIndicator);
            var sr = currentIndicatorInstance.GetComponent<SpriteRenderer>();
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0.5f);
            currentIndicatorInstance.SetActive(false); // Start hidden
        }
    }

    private void OnEnable()
    {
        // Subscribe to input events from InputManager
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPointerClick += HandlePlacementClick;
            InputManager.Instance.OnPointerPositionUpdate += HandlePointerMove; // For indicator
        }
        else
        {
            Debug.LogWarning("InputManager not found. Placement might not work.");
            // Fallback or alternative input handling could go here
        }
    }

    private void OnDisable()
    {
        // Unsubscribe
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPointerClick -= HandlePlacementClick;
            InputManager.Instance.OnPointerPositionUpdate -= HandlePointerMove;
        }
    }

    // --- Public method called by UI Button to select a building ---
    public void SelectBuildingToPlace(BuildingType buildingSO)
    {
        if (buildingSO == null)
        {
            Debug.LogWarning("Tried to select a null building prefab.");
            return;
        }



        // buildingToPlace = buildingPrefab;
        buildingToPlace = buildingSO;

        // Activate and size the placement indicator
        if (currentIndicatorInstance != null && buildingToPlace != null)
        {
            currentIndicatorInstance.SetActive(true);
            // currentIndicatorInstance.GetComponent<SpriteRenderer>().sprite = buildingPrefab.gameObject.GetComponent<SpriteRenderer>().sprite;
            currentIndicatorInstance.GetComponent<SpriteRenderer>().sprite = buildingSO.sprite;

            // 2. Get the size in tiles
            //Vector2 buildingSize = buildingPrefab.size; // (e.g., (2, 3))
            Vector2 buildingSize = buildingSO.size;

            // 3. Get sprite world size (how big 1 sprite is)
            // Vector2 spriteWorldSize = buildingPrefab.gameObject.GetComponent<SpriteRenderer>().sprite.bounds.size; // in Unity units
            Vector2 spriteWorldSize = buildingSO.sprite.bounds.size; // in Unity units

            // 4. Calculate required scale
            Vector3 scale = new Vector3(
                buildingSize.x / spriteWorldSize.x,
                buildingSize.y / spriteWorldSize.y,
                1f
            );

            // 5. Apply the scale
            currentIndicatorInstance.transform.localScale = scale;

        }
        else if (currentIndicatorInstance != null)
        {
            currentIndicatorInstance.SetActive(false); // Hide if no building selected
        }
    }

    // --- Input Handlers ---
    private void HandlePointerMove(Vector2 screenPosition)
    {
        if (buildingToPlace == null || currentIndicatorInstance == null || !currentIndicatorInstance.activeSelf) return;

        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, mainCamera.nearClipPlane + 10)); // +10 to be in front of background
        Vector3Int cellPosition = buildingGrid.WorldToCell(worldPosition);
        Vector3 cellCenterWorld = buildingGrid.GetCellCenterWorld(cellPosition);

        Vector2 size = buildingToPlace.size; // e.g., (2,3)
        Vector3 offset = Vector3.zero;
        if ((size.x % 2) == 0)
        {
            offset.x = buildingGrid.cellSize.x / 2f;
        }
        if ((size.y % 2) == 0)
        {
            offset.y = buildingGrid.cellSize.y / 2f;
        }

        // 4. Final set
        currentIndicatorInstance.transform.position = cellCenterWorld + offset;


        // Optional: Add visual feedback for validity (e.g., change indicator color)
        // bool canPlace = BuildingManager.Instance.CanPlace(buildingToPlace, new Vector2Int(cellPosition.x, cellPosition.y));
        // SetIndicatorColor(canPlace ? Color.green : Color.red);
    }


    private void HandlePlacementClick(Vector2 screenPosition)
    {
        if (buildingToPlace == null)
        {
            // Maybe implement deselection or interaction with existing buildings here
            // Debug.Log("No building selected to place.");
            return;
        }

        // Convert screen pos to world pos, then to grid cell
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, mainCamera.nearClipPlane + 10));
        Vector3Int cellPosition = buildingGrid.WorldToCell(worldPosition);

        // Try to place the building
        TryPlaceBuilding(cellPosition);
    }


    // --- Placement Logic ---
    private void TryPlaceBuilding(Vector3Int cellPosition)
    {
        if (buildingToPlace == null) return; // Should not happen if called from HandlePlacementClick correctly

        Vector2Int anchorCell = new Vector2Int(cellPosition.x, cellPosition.y);

        // 1. Check Validity (is any cell of the footprint already taken?)
        if (!BuildingManager.Instance.CanPlace(buildingToPlace, anchorCell))
        {
            Debug.Log($"Cannot place {buildingToPlace.buildingName} at {cellPosition}. Spot occupied or invalid.");
            // Add player feedback (e.g., sound effect, visual cue)
            return;
        }

        // 2. Check Cost
        if (buildingToPlace.goldCost > ResourceManager.Instance.resources["Gold"].currentAmount || buildingToPlace.woodCost > ResourceManager.Instance.resources["Wood"].currentAmount)
        {
            Debug.Log($"Cannot afford {buildingToPlace.buildingName}. Cost: {buildingToPlace.goldCost}G, {buildingToPlace.woodCost}W");
            return; // Exit placement
        }

        // 3. Build it (BuildingManager instantiates, initializes and registers it)
        Building newBuilding = BuildingManager.Instance.Build(buildingToPlace, anchorCell, GetBuildingWorldPosition(cellPosition, buildingToPlace.size));
        if (newBuilding == null) return;

        // 4. Pay for it, now that it exists
        ResourceManager.Instance.changeResourceAmount("Gold", -buildingToPlace.goldCost);
        ResourceManager.Instance.changeResourceAmount("Wood", -buildingToPlace.woodCost);

        // 5. Deselect building after placement (optional, maybe allow multi-place)
        buildingToPlace = null;
        if (currentIndicatorInstance != null) currentIndicatorInstance.SetActive(false);
        Debug.Log($"Placed {newBuilding.name} successfully.");
    }

    // World position of a building's centre: the anchor cell's centre, shifted half a cell on
    // axes where the size is even. BuildingManager.GetFootprint mirrors this when listing cells.
    private Vector3 GetBuildingWorldPosition(Vector3Int cellPosition, Vector2Int size)
    {
        Vector3 position = buildingGrid.GetCellCenterWorld(cellPosition);
        if ((size.x % 2) == 0) position.x += buildingGrid.cellSize.x / 2f;
        if ((size.y % 2) == 0) position.y += buildingGrid.cellSize.y / 2f;
        return position;
    }

    // Optional: Helper to change indicator color based on placement validity
    // private void SetIndicatorColor(Color color) {
    //     if (currentIndicatorInstance == null) return;
    //     var renderer = currentIndicatorInstance.GetComponent<SpriteRenderer>();
    //     if (renderer != null) {
    //         color.a = renderer.color.a; // Keep original transparency
    //         renderer.color = color;
    //     }
    // }
}