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

    public BuildingType buildingToPlace = null; // The type of building we are currently trying to place (null when not placing)

    // Move mode: an existing building picked up and waiting to be dropped somewhere else
    private Building buildingToMove;
    private bool moveActive;
    private Color movedBuildingOriginalColor = Color.white;

    // True while the player is in placing or moving mode: clicks then go to the placer, not to whatever is under them
    public bool IsPlacing => buildingToPlace != null || moveActive;
    public bool IsMoving => moveActive;

    // Raised when move mode ends, whether the building was dropped or the move cancelled
    public event System.Action MovingEnded;

    private BuildingType ActiveType => moveActive && buildingToMove != null ? buildingToMove.buildingType : buildingToPlace;

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

    private void Update()
    {
        // The building being moved was removed meanwhile: leave move mode so the game does not stay paused
        if (moveActive && buildingToMove == null) EndMove();
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

        if (moveActive) EndMove(); // picking something to build cancels a move in progress

        buildingToPlace = buildingSO;
        ShowIndicator(buildingSO);
    }

    // Activates the ghost and sizes it for the given building type
    private void ShowIndicator(BuildingType type)
    {
        if (currentIndicatorInstance == null) return;

        currentIndicatorInstance.SetActive(true);
        currentIndicatorInstance.GetComponent<SpriteRenderer>().sprite = type.sprite;

        // Scale the ghost so the sprite covers the building's size in tiles
        Vector2 spriteWorldSize = type.sprite.bounds.size; // in Unity units
        currentIndicatorInstance.transform.localScale = new Vector3(
            type.size.x / spriteWorldSize.x,
            type.size.y / spriteWorldSize.y,
            1f
        );
        SetIndicatorColor(Color.white);
    }

    // --- Moving an existing building ---

    // Picks the building up: it is dimmed where it stands and a ghost follows the pointer until the
    // player clicks a free spot (drop) or cancels.
    public void StartMoving(Building building)
    {
        if (building == null || building.buildingType == null || !building.buildingType.movable) return;

        if (moveActive) EndMove();
        buildingToPlace = null;
        buildingToMove = building;
        moveActive = true;

        SpriteRenderer original = building.GetComponent<SpriteRenderer>();
        if (original != null)
        {
            movedBuildingOriginalColor = original.color;
            original.color = new Color(movedBuildingOriginalColor.r, movedBuildingOriginalColor.g, movedBuildingOriginalColor.b, 0.5f);
        }

        ShowIndicator(building.buildingType);
        if (currentIndicatorInstance != null) currentIndicatorInstance.transform.position = building.transform.position;
    }

    public void CancelMoving()
    {
        if (moveActive) EndMove();
    }

    private void EndMove()
    {
        if (buildingToMove != null)
        {
            SpriteRenderer original = buildingToMove.GetComponent<SpriteRenderer>();
            if (original != null) original.color = movedBuildingOriginalColor;
        }
        buildingToMove = null;
        moveActive = false;
        if (currentIndicatorInstance != null) currentIndicatorInstance.SetActive(false);
        MovingEnded?.Invoke();
    }

    // --- Input Handlers ---
    private void HandlePointerMove(Vector2 screenPosition)
    {
        BuildingType type = ActiveType;
        if (type == null || currentIndicatorInstance == null || !currentIndicatorInstance.activeSelf) return;

        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, mainCamera.nearClipPlane + 10)); // +10 to be in front of background
        Vector3Int cellPosition = buildingGrid.WorldToCell(worldPosition);

        currentIndicatorInstance.transform.position = GetBuildingWorldPosition(cellPosition, type.size);

        // Green where the building can go, red where it can not
        Vector2Int anchorCell = new Vector2Int(cellPosition.x, cellPosition.y);
        bool valid = moveActive && buildingToMove != null
            ? BuildingManager.Instance.CanMove(buildingToMove, anchorCell)
            : BuildingManager.Instance.CanPlace(type, anchorCell);
        SetIndicatorColor(valid ? Color.green : Color.red);
    }

    private void HandlePlacementClick(Vector2 screenPosition)
    {
        if (!IsPlacing)
        {
            // Maybe implement deselection or interaction with existing buildings here
            return;
        }

        // Convert screen pos to world pos, then to grid cell
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, mainCamera.nearClipPlane + 10));
        Vector3Int cellPosition = buildingGrid.WorldToCell(worldPosition);

        if (moveActive) TryMoveBuilding(cellPosition);
        else TryPlaceBuilding(cellPosition);
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

    // --- Move Logic ---
    private void TryMoveBuilding(Vector3Int cellPosition)
    {
        Building building = buildingToMove;
        if (building == null) // removed while we were choosing where to put it
        {
            EndMove();
            return;
        }

        BuildingType type = building.buildingType;
        Vector2Int anchorCell = new Vector2Int(cellPosition.x, cellPosition.y);

        // Dropped right where it already stands: nothing to do
        if (anchorCell == building.Cell)
        {
            EndMove();
            return;
        }

        if (!BuildingManager.Instance.CanMove(building, anchorCell))
        {
            Debug.Log($"Cannot move {building.DisplayName} to {cellPosition}. Spot occupied or invalid.");
            return; // stay in move mode so the player can try another spot
        }

        if (type.moveGoldCost > ResourceManager.Instance.GetResourceAmount("Gold") || type.moveWoodCost > ResourceManager.Instance.GetResourceAmount("Wood"))
        {
            Debug.Log($"Cannot afford to move {building.DisplayName}. Cost: {type.moveGoldCost}G, {type.moveWoodCost}W");
            return;
        }

        if (!BuildingManager.Instance.Move(building, anchorCell, GetBuildingWorldPosition(cellPosition, type.size))) return;

        ResourceManager.Instance.changeResourceAmount("Gold", -type.moveGoldCost);
        ResourceManager.Instance.changeResourceAmount("Wood", -type.moveWoodCost);

        Debug.Log($"Moved {building.DisplayName} to {cellPosition}.");
        EndMove();
    }

    // Tints the ghost, keeping its transparency
    private void SetIndicatorColor(Color color)
    {
        if (currentIndicatorInstance == null) return;
        SpriteRenderer indicator = currentIndicatorInstance.GetComponent<SpriteRenderer>();
        if (indicator != null) indicator.color = new Color(color.r, color.g, color.b, 0.5f);
    }
}
