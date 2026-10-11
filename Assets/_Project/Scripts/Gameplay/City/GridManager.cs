using System.Collections.Generic;
using UnityEngine;


// Dont know if this script is necessary yet
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; } // Singleton (optional but common)

    private Dictionary<Vector2Int, Building> grid = new Dictionary<Vector2Int, Building>();


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

    public bool IsCellOccupied(Vector2Int cell)
    {
        return grid.ContainsKey(cell);
    }

    public void PlaceBuilding(Vector2Int cell, Building building)
    {
        grid[cell] = building;
    }

    public void RemoveBuilding(Vector2Int cell)
    {
        if (grid.ContainsKey(cell))
            grid.Remove(cell);
    }

    public Building GetBuildingAt(Vector2Int cell)
    {
        grid.TryGetValue(cell, out var building);
        return building;
    }
}