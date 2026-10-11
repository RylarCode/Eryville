using System;
using System.Collections.Generic;
using UnityEngine;

// Single source of truth for which buildings exist and which grid cells they occupy.
// Everything that needs "all buildings" (production, assign UI, home lookup) asks here,
// and every building is created / destroyed through Build / Remove.
public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    public event Action<Building> BuildingAdded;
    public event Action<Building> BuildingRemoved;

    private readonly List<Building> buildings = new List<Building>();
    private readonly Dictionary<Vector2Int, Building> occupiedCells = new Dictionary<Vector2Int, Building>();

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
        return occupiedCells.ContainsKey(cell);
    }

    public Building GetBuildingAt(Vector2Int cell)
    {
        occupiedCells.TryGetValue(cell, out Building building);
        return building;
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

    public bool CanPlace(BuildingType type, Vector2Int anchorCell)
    {
        foreach (Vector2Int cell in GetFootprint(anchorCell, type.size))
        {
            if (occupiedCells.ContainsKey(cell)) return false;
        }
        return true;
    }

    // Creates, initializes and registers a building. Returns null if the spot is taken or the type is invalid.
    // Cost is NOT handled here; the caller pays after a successful build.
    public Building Build(BuildingType type, Vector2Int anchorCell, Vector3 worldPosition)
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

        building.Initialize(type, anchorCell);

        buildings.Add(building);
        foreach (Vector2Int cell in GetFootprint(anchorCell, type.size))
        {
            occupiedCells[cell] = building;
        }

        BuildingAdded?.Invoke(building);
        return building;
    }

    // Removes a building, frees its cells and sends its workers/residents back to being unassigned.
    public void Remove(Building building)
    {
        if (building == null || !buildings.Remove(building)) return;

        foreach (Vector2Int cell in GetFootprint(building.cell, building.buildingType.size))
        {
            if (occupiedCells.TryGetValue(cell, out Building occupant) && occupant == building)
            {
                occupiedCells.Remove(cell);
            }
        }

        foreach (OwnedCharacter worker in new List<OwnedCharacter>(building.Workers)) building.RemoveWorker(worker);
        foreach (OwnedCharacter resident in new List<OwnedCharacter>(building.Residents)) building.RemoveResident(resident);

        BuildingRemoved?.Invoke(building);
        Destroy(building.gameObject);
    }
}
