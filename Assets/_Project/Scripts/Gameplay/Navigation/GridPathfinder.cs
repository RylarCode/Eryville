using System;
using System.Collections.Generic;
using UnityEngine;

// Finds the quickest way between cells of the map grid (A*). Plain code with no scene dependencies:
// it only asks two questions about a cell, "can you walk here?" and "how fast can you walk here?",
// so it can be tested on its own and does not care whether the speed comes from roads, mud or traffic.
//
// - 4 directions (up, down, left, right): characters never walk diagonally
// - a step takes distance / speed, so a faster road can beat a shorter straight line
// - any number of start cells and goal cells (e.g. every cell next to a building's walls)
// - its working arrays are reused between searches, so searching a lot does not create garbage
public sealed class GridPathfinder
{
    public delegate bool BlockedFunc(Vector2Int cell);
    public delegate float SpeedFunc(Vector2Int cell);

    private const float MinSpeed = 0.01f;

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
    };

    private readonly Vector2Int min;
    private readonly int width;
    private readonly int height;
    private readonly BlockedFunc isBlocked;
    private readonly SpeedFunc speedAt;
    private readonly float maxSpeed; // fastest speed any cell can have; keeps the estimate honest

    // Per-cell search state. A cell's data only counts if its stamp equals the current search's stamp,
    // so nothing needs clearing between searches.
    private readonly int[] seenStamp;
    private readonly int[] closedStamp;
    private readonly int[] goalStamp;
    private readonly float[] cost;
    private readonly int[] cameFrom;
    private int stamp;

    private readonly Heap open = new Heap();
    private readonly List<Vector2Int> goalCells = new List<Vector2Int>();

    public GridPathfinder(Vector2Int min, Vector2Int max, BlockedFunc isBlocked, SpeedFunc speedAt, float maxSpeed)
    {
        this.min = min;
        width = max.x - min.x + 1;
        height = max.y - min.y + 1;
        this.isBlocked = isBlocked;
        this.speedAt = speedAt;
        this.maxSpeed = Math.Max(maxSpeed, 1f);

        int cells = width * height;
        seenStamp = new int[cells];
        closedStamp = new int[cells];
        goalStamp = new int[cells];
        cost = new float[cells];
        cameFrom = new int[cells];
    }

    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= min.x && cell.x < min.x + width && cell.y >= min.y && cell.y < min.y + height;
    }

    private int IndexOf(Vector2Int cell) { return (cell.y - min.y) * width + (cell.x - min.x); }

    private Vector2Int CellOf(int index) { return new Vector2Int(min.x + index % width, min.y + index / width); }

    private float Speed(Vector2Int cell) { return Math.Max(speedAt(cell), MinSpeed); }

    // The quickest path from any of the start cells to any of the goal cells, as a list of cells
    // (first = where it starts, last = which goal it reaches), or null if there is no way.
    // totalTime is how long walking it takes at speed 1.
    public List<Vector2Int> FindPath(IReadOnlyList<Vector2Int> starts, IReadOnlyList<Vector2Int> goals, out float totalTime)
    {
        totalTime = 0f;
        stamp++;
        open.Clear();
        goalCells.Clear();

        foreach (Vector2Int goal in goals)
        {
            if (!InBounds(goal) || isBlocked(goal)) continue;
            goalStamp[IndexOf(goal)] = stamp;
            goalCells.Add(goal);
        }
        if (goalCells.Count == 0) return null;

        foreach (Vector2Int start in starts)
        {
            if (!InBounds(start) || isBlocked(start)) continue;
            int index = IndexOf(start);
            if (seenStamp[index] == stamp) continue;
            seenStamp[index] = stamp;
            cost[index] = 0f;
            cameFrom[index] = -1;
            open.Push(Estimate(start), index);
        }

        while (open.Count > 0)
        {
            int currentIndex = open.Pop();
            if (closedStamp[currentIndex] == stamp) continue; // an outdated duplicate entry
            closedStamp[currentIndex] = stamp;

            Vector2Int current = CellOf(currentIndex);
            if (goalStamp[currentIndex] == stamp)
            {
                totalTime = cost[currentIndex];
                return Rebuild(currentIndex);
            }

            float currentSpeed = Speed(current);
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;
                if (!InBounds(next) || isBlocked(next)) continue;

                int nextIndex = IndexOf(next);
                if (closedStamp[nextIndex] == stamp) continue;

                float stepTime = 1f / Math.Min(currentSpeed, Speed(next));
                float newCost = cost[currentIndex] + stepTime;
                if (seenStamp[nextIndex] == stamp && newCost >= cost[nextIndex]) continue;

                seenStamp[nextIndex] = stamp;
                cost[nextIndex] = newCost;
                cameFrom[nextIndex] = currentIndex;
                open.Push(newCost + Estimate(next), nextIndex);
            }
        }

        return null; // every reachable cell was tried and none is a goal
    }

    // Lower bound on the time still needed: walking distance (up/down/left/right) to the nearest goal at top speed
    private float Estimate(Vector2Int cell)
    {
        float best = float.MaxValue;
        foreach (Vector2Int goal in goalCells)
        {
            int dx = Math.Abs(goal.x - cell.x);
            int dy = Math.Abs(goal.y - cell.y);
            float distance = dx + dy;
            if (distance < best) best = distance;
        }
        return best / maxSpeed;
    }

    private List<Vector2Int> Rebuild(int goalIndex)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        for (int index = goalIndex; index != -1; index = cameFrom[index]) path.Add(CellOf(index));
        path.Reverse();
        return path;
    }

    // The closest cell you can stand on, searching outward from a cell (which may be blocked or off the map).
    // Returns false if nothing walkable is found within the radius.
    public bool TryFindNearestWalkable(Vector2Int from, int maxRadius, out Vector2Int found)
    {
        from = new Vector2Int(Math.Min(Math.Max(from.x, min.x), min.x + width - 1), Math.Min(Math.Max(from.y, min.y), min.y + height - 1));
        for (int radius = 0; radius <= maxRadius; radius++)
        {
            bool any = false;
            float bestDistance = float.MaxValue;
            found = from;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue; // only the ring at this radius
                    Vector2Int cell = new Vector2Int(from.x + dx, from.y + dy);
                    if (!InBounds(cell) || isBlocked(cell)) continue;
                    float distance = dx * dx + dy * dy;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        found = cell;
                        any = true;
                    }
                }
            }
            if (any) return true;
        }
        found = from;
        return false;
    }

    // Minimal binary min-heap of (priority, cell index)
    private sealed class Heap
    {
        private float[] priorities = new float[256];
        private int[] items = new int[256];
        public int Count { get; private set; }

        public void Clear() { Count = 0; }

        public void Push(float priority, int item)
        {
            if (Count == items.Length)
            {
                Array.Resize(ref priorities, Count * 2);
                Array.Resize(ref items, Count * 2);
            }
            int i = Count++;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (priorities[parent] <= priority) break;
                priorities[i] = priorities[parent];
                items[i] = items[parent];
                i = parent;
            }
            priorities[i] = priority;
            items[i] = item;
        }

        public int Pop()
        {
            int top = items[0];
            Count--;
            if (Count > 0)
            {
                float priority = priorities[Count];
                int item = items[Count];
                int i = 0;
                while (true)
                {
                    int child = i * 2 + 1;
                    if (child >= Count) break;
                    if (child + 1 < Count && priorities[child + 1] < priorities[child]) child++;
                    if (priorities[child] >= priority) break;
                    priorities[i] = priorities[child];
                    items[i] = items[child];
                    i = child;
                }
                priorities[i] = priority;
                items[i] = item;
            }
            return top;
        }
    }
}
