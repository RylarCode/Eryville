using System;
using System.Collections.Generic;
using UnityEngine;

// Works out routes for characters: "from here (or from inside this building) to that building".
//
// - Buildings are solid: a route ends at a free cell touching the destination's walls (its entrance) and,
//   leaving a building, starts at one touching the origin's walls. Any side works.
// - Routes between two buildings are remembered while the map stays the same, because many characters
//   make the same trips (home -> mine every morning).
// - Searches that are not remembered are spread over several frames, so a whole town setting off at
//   the same moment does not stall the game.
public class PathService : MonoBehaviour
{
    private static PathService instance;

    // Created on demand if the scene does not have one
    public static PathService Instance
    {
        get
        {
            if (instance == null)
            {
                Debug.LogWarning("No PathService in the scene; creating one.");
                instance = new GameObject("PathService").AddComponent<PathService>();
            }
            return instance;
        }
    }

    [SerializeField] private int maxSearchesPerFrame = 25;

    // What a request ends up with. Shared between characters, so never modify it.
    public sealed class PathResult
    {
        public bool Found;
        public Vector3[] Waypoints;      // cell centres, first = where the walk starts, last = the entrance
        public bool StartsAtBuilding;    // the walk starts at the origin building's entrance (step out there)
    }

    public sealed class PathRequest
    {
        public bool Cancelled { get; private set; }
        public bool Done { get; internal set; }
        internal Vector3 FromWorld;
        internal Building FromBuilding;
        internal Building ToBuilding;
        internal Action<PathResult> Callback;
        public void Cancel() { Cancelled = true; }
    }

    private static readonly PathResult NoPath = new PathResult { Found = false, Waypoints = new Vector3[0] };

    private readonly Queue<PathRequest> queue = new Queue<PathRequest>();
    private readonly Dictionary<string, PathResult> cache = new Dictionary<string, PathResult>();
    private int cacheVersion = -1;

    private GridPathfinder pathfinder;
    private Vector2Int pathfinderMin, pathfinderMax;
    private float pathfinderMaxSpeed;

    private readonly List<Vector2Int> startCells = new List<Vector2Int>();
    private readonly List<Vector2Int> goalCells = new List<Vector2Int>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    // Asks for a route. onDone is called with the result: straight away if it is already known,
    // otherwise within a few frames. Cancel the returned request to stop caring about the answer.
    public PathRequest Request(Vector3 fromWorld, Building fromBuilding, Building toBuilding, Action<PathResult> onDone)
    {
        PathRequest request = new PathRequest { FromWorld = fromWorld, FromBuilding = fromBuilding, ToBuilding = toBuilding, Callback = onDone };

        if (fromBuilding != null && toBuilding != null)
        {
            RefreshCache();
            if (cache.TryGetValue(CacheKey(fromBuilding, toBuilding), out PathResult known))
            {
                request.Done = true;
                onDone(known);
                return request;
            }
        }

        queue.Enqueue(request);
        return request;
    }

    private void Update()
    {
        int searches = 0;
        while (queue.Count > 0 && searches < maxSearchesPerFrame)
        {
            PathRequest request = queue.Dequeue();
            if (request.Cancelled) continue;

            searches++;
            PathResult result = Solve(request);
            request.Done = true;
            request.Callback(result);
        }
    }

    private static string CacheKey(Building from, Building to) { return from.Id + ">" + to.Id; }

    private void RefreshCache()
    {
        int version = MapGrid.Instance.Version;
        if (version != cacheVersion)
        {
            cache.Clear();
            cacheVersion = version;
        }
    }

    private PathResult Solve(PathRequest request)
    {
        MapGrid map = MapGrid.Instance;
        RefreshCache();
        EnsurePathfinder(map);

        Building from = request.FromBuilding;
        Building to = request.ToBuilding;
        if (to == null) return NoPath; // the destination disappeared while waiting

        // Where the walk can start: the origin building's entrances, or the free cell nearest to the character
        startCells.Clear();
        bool fromBuilding = from != null;
        if (fromBuilding)
        {
            AddEntranceCells(map, from, startCells);
        }
        else if (pathfinder.TryFindNearestWalkable(map.WorldToCell(request.FromWorld), 60, out Vector2Int nearest))
        {
            startCells.Add(nearest);
        }

        goalCells.Clear();
        AddEntranceCells(map, to, goalCells);

        PathResult result = NoPath;
        if (startCells.Count > 0 && goalCells.Count > 0)
        {
            List<Vector2Int> cells = pathfinder.FindPath(startCells, goalCells, out float _);
            if (cells != null)
            {
                Vector3[] waypoints = new Vector3[cells.Count];
                for (int i = 0; i < cells.Count; i++) waypoints[i] = map.CellCenterToWorld(cells[i]);
                result = new PathResult { Found = true, Waypoints = waypoints, StartsAtBuilding = fromBuilding };
            }
        }

        if (fromBuilding) cache[CacheKey(from, to)] = result; // remembered until the map changes
        return result;
    }

    // The pathfinder is built once and rebuilt only if the map size or the fastest terrain changes
    private void EnsurePathfinder(MapGrid map)
    {
        if (pathfinder != null && pathfinderMin == map.Min && pathfinderMax == map.Max && pathfinderMaxSpeed == map.MaxSpeedMultiplier) return;

        pathfinderMin = map.Min;
        pathfinderMax = map.Max;
        pathfinderMaxSpeed = map.MaxSpeedMultiplier;
        pathfinder = new GridPathfinder(map.Min, map.Max, map.IsBlocked, map.SpeedMultiplierAt, pathfinderMaxSpeed);
    }

    // The free cells that touch the building's walls (sides, not corners): where people go in and out
    public static void AddEntranceCells(MapGrid map, Building building, List<Vector2Int> into)
    {
        if (building == null || building.buildingType == null) return;

        HashSet<Vector2Int> footprint = new HashSet<Vector2Int>(BuildingManager.GetFootprint(building.Cell, building.buildingType.size));
        HashSet<Vector2Int> added = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in footprint)
        {
            AddIfEntrance(map, footprint, added, new Vector2Int(cell.x + 1, cell.y), into);
            AddIfEntrance(map, footprint, added, new Vector2Int(cell.x - 1, cell.y), into);
            AddIfEntrance(map, footprint, added, new Vector2Int(cell.x, cell.y + 1), into);
            AddIfEntrance(map, footprint, added, new Vector2Int(cell.x, cell.y - 1), into);
        }
    }

    private static void AddIfEntrance(MapGrid map, HashSet<Vector2Int> footprint, HashSet<Vector2Int> added, Vector2Int cell, List<Vector2Int> into)
    {
        if (footprint.Contains(cell) || !map.InBounds(cell) || map.IsBlocked(cell)) return;
        if (added.Add(cell)) into.Add(cell);
    }
}
