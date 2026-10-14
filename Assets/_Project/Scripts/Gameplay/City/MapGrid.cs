using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// The single source of truth for what is on each cell of the map: the playable bounds, which cells
// buildings occupy, and (later) which cells have roads. Placing, road building and pathfinding all
// ask here, so they can never disagree about what is where.
//
// Buildings are put on the map and taken off it by BuildingManager (and roads by RoadManager);
// everything else only reads.
public class MapGrid : MonoBehaviour
{
    private static MapGrid instance;

    // Created on demand (with default bounds) if the scene does not have one
    public static MapGrid Instance
    {
        get
        {
            if (instance == null)
            {
                Debug.LogWarning("No MapGrid in the scene; creating one with the default bounds.");
                instance = new GameObject("MapGrid").AddComponent<MapGrid>();
            }
            return instance;
        }
    }

    [SerializeField] private Grid grid;               // the scene's Grid: cell size and cell <-> world conversion
    [SerializeField] private Tilemap boundsTilemap;   // a tilemap covering the playable area (the background)
    [SerializeField] private Vector2Int fallbackMin = new Vector2Int(-28, -17); // used when there is no tilemap
    [SerializeField] private Vector2Int fallbackMax = new Vector2Int(28, 11);

    // The playable area, inclusive on both ends
    public Vector2Int Min { get; private set; }
    public Vector2Int Max { get; private set; }

    // Goes up on every change to the map, so caches (like saved paths) can tell they are out of date
    public int Version { get; private set; }

    public event Action Changed;            // anything on the map changed
    public event Action ObstaclesChanged;   // something that blocks walking appeared or disappeared (buildings)

    private readonly Dictionary<Vector2Int, Building> buildingCells = new Dictionary<Vector2Int, Building>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        ComputeBounds();
    }

    private void ComputeBounds()
    {
        Min = fallbackMin;
        Max = fallbackMax;
        if (boundsTilemap != null)
        {
            BoundsInt bounds = boundsTilemap.cellBounds;
            if (bounds.size.x > 0 && bounds.size.y > 0)
            {
                Min = new Vector2Int(bounds.xMin, bounds.yMin);
                Max = new Vector2Int(bounds.xMax - 1, bounds.yMax - 1);
            }
        }
    }

    // Overrides the playable area (tests, or a map that changes size)
    public void SetBounds(Vector2Int min, Vector2Int max)
    {
        Min = min;
        Max = max;
        Touch(true);
    }

    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= Min.x && cell.x <= Max.x && cell.y >= Min.y && cell.y <= Max.y;
    }

    // --- Cells <-> world ---

    public Vector3 CellCenterToWorld(Vector2Int cell)
    {
        if (grid != null) return grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        if (grid != null)
        {
            Vector3Int cell = grid.WorldToCell(world);
            return new Vector2Int(cell.x, cell.y);
        }
        return new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
    }

    // --- Buildings ---

    public bool IsBuildingCell(Vector2Int cell)
    {
        return buildingCells.ContainsKey(cell);
    }

    public Building BuildingAt(Vector2Int cell)
    {
        buildingCells.TryGetValue(cell, out Building building);
        return building;
    }

    // Walking is not possible here (a building stands on it)
    public bool IsBlocked(Vector2Int cell)
    {
        return buildingCells.ContainsKey(cell);
    }

    // --- Terrain speed ---

    // How much faster (or slower) than normal a character walks on this cell: 1 on open ground.
    // This is also the cost the pathfinder weighs routes with, so anything that changes it
    // (roads, later traffic) changes both how fast people walk and which way they choose.
    public float SpeedMultiplierAt(Vector2Int cell)
    {
        return 1f;
    }

    public float SpeedMultiplierAt(Vector3 world)
    {
        return SpeedMultiplierAt(WorldToCell(world));
    }

    // The highest speed multiplier any cell can have (the pathfinder needs it for its estimates)
    public float MaxSpeedMultiplier => 1f;

    internal void PlaceBuilding(IEnumerable<Vector2Int> cells, Building building)
    {
        foreach (Vector2Int cell in cells) buildingCells[cell] = building;
        Touch(true);
    }

    internal void RemoveBuilding(IEnumerable<Vector2Int> cells, Building building)
    {
        foreach (Vector2Int cell in cells)
        {
            if (buildingCells.TryGetValue(cell, out Building occupant) && occupant == building) buildingCells.Remove(cell);
        }
        Touch(true);
    }

    internal void ClearBuildings()
    {
        buildingCells.Clear();
        Touch(true);
    }

    private void Touch(bool obstacles)
    {
        Version++;
        Changed?.Invoke();
        if (obstacles) ObstaclesChanged?.Invoke();
    }
}
