using System;
using System.Collections.Generic;
using UnityEngine;

// Single source of truth for which buildings exist. Everything that needs "all buildings"
// (production, assign UI, home lookup) asks here, and every building is created / moved /
// destroyed through Build / Move / Remove. The cells buildings occupy are kept on the MapGrid.
public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    public event Action<Building> BuildingAdded;
    public event Action<Building> BuildingRemoved;
    public event Action<Building> BuildingMoved;

    private readonly List<Building> buildings = new List<Building>();

    public IReadOnlyList<Building> Buildings => buildings;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // The cells a building covers, given the cell the player clicked (its anchor).
    // Odd sizes are centred on the anchor; even sizes extend one extra cell up/right,
    // which matches how BuildingPlacer positions the ghost and the building.
    public static IEnumerable<Vector2Int> GetFootprint(Vector2Int anchorCell, Vector2Int size)
    {
        Vector2Int start = new Vector2Int(anchorCell.x - (size.x - 1) / 2, anchorCell.y - (size.y - 1) / 2);
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                yield return new Vector2Int(start.x + x, start.y + y);
            }
        }
    }

    public bool IsCellOccupied(Vector2Int cell)
    {
        return MapGrid.Instance.IsBuildingCell(cell);
    }

    public Building GetBuildingAt(Vector2Int cell)
    {
        return MapGrid.Instance.BuildingAt(cell);
    }

    // The closest built building of the given type to a world position, or null if there is none.
    public Building FindNearest(BuildingType type, Vector3 from)
    {
        Building nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Building building in buildings)
        {
            if (building.buildingType != type) continue;
            float distance = Vector3.Distance(building.transform.position, from);
            if (distance < nearestDistance)
            {
                nearest = building;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    // Whether a building of this type fits with its anchor at anchorCell. When checking a spot for a building
    // that already exists (moving it), pass it as ignore so its own current cells do not count as taken.
    public bool CanPlace(BuildingType type, Vector2Int anchorCell, Building ignore = null)
    {
        MapGrid map = MapGrid.Instance;
        foreach (Vector2Int cell in GetFootprint(anchorCell, type.size))
        {
            if (!map.InBounds(cell)) return false; // off the map
            Building occupant = map.BuildingAt(cell);
            if (occupant != null && occupant != ignore) return false;
        }
        return true;
    }

    public bool CanMove(Building building, Vector2Int newAnchorCell)
    {
        return building != null && building.buildingType != null && building.buildingType.movable
            && buildings.Contains(building) && CanPlace(building.buildingType, newAnchorCell, building);
    }

    // Moves a building to a new anchor cell: frees its old cells, takes the new ones and tells everyone
    // (characters heading there or inside it follow). Returns false, changing nothing, if it cannot go there.
    // Cost is NOT handled here, like Build.
    public bool Move(Building building, Vector2Int newAnchorCell, Vector3 newWorldPosition)
    {
        if (!CanMove(building, newAnchorCell)) return false;
        if (newAnchorCell == building.Cell) return true; // already there

        MapGrid map = MapGrid.Instance;
        map.RemoveBuilding(GetFootprint(building.Cell, building.buildingType.size), building);
        map.PlaceBuilding(GetFootprint(newAnchorCell, building.buildingType.size), building);

        building.transform.position = newWorldPosition;
        building.SetCell(newAnchorCell);

        BuildingMoved?.Invoke(building);
        return true;
    }

    // Creates, initializes and registers a building. Returns null if the spot is taken or the type is invalid.
    // Cost is NOT handled here; the caller pays after a successful build.
    // Pass an id only when restoring a saved building, so it keeps the id other save data refers to.
    public Building Build(BuildingType type, Vector2Int anchorCell, Vector3 worldPosition, string id = null)
    {
        if (type == null || type.buildingPrefab == null)
        {
            Debug.LogError("BuildingManager.Build: building type or its prefab is missing.");
            return null;
        }
        if (!CanPlace(type, anchorCell)) return null;

        GameObject go = Instantiate(type.buildingPrefab, worldPosition, Quaternion.identity);
        go.name = $"{type.buildingName} ({anchorCell.x},{anchorCell.y})"; // unique name in the hierarchy

        Building building = go.GetComponent<Building>();
        if (building == null)
        {
            Debug.LogError($"Prefab for {type.buildingName} has no Building component.");
            Destroy(go);
            return null;
        }

        if (!string.IsNullOrEmpty(id)) building.Id = id;
        building.Initialize(type, anchorCell);

        buildings.Add(building);
        MapGrid.Instance.PlaceBuilding(GetFootprint(anchorCell, type.size), building);

        BuildingAdded?.Invoke(building);
        return building;
    }

    // Removes every building (used when loading a game)
    public void ClearAll()
    {
        foreach (Building building in new List<Building>(buildings)) Remove(building);
        MapGrid.Instance.ClearBuildings();
    }

    // Removes a building, frees its cells and sends its workers/residents back to being unassigned.
    public void Remove(Building building)
    {
        if (building == null || !buildings.Remove(building)) return;

        MapGrid.Instance.RemoveBuilding(GetFootprint(building.Cell, building.buildingType.size), building);

        foreach (OwnedCharacter worker in new List<OwnedCharacter>(building.Workers)) building.RemoveWorker(worker);
        foreach (OwnedCharacter resident in new List<OwnedCharacter>(building.Residents)) building.RemoveResident(resident);

        BuildingRemoved?.Invoke(building);
        Destroy(building.gameObject);
    }
}
