using UnityEngine;

// Marks a building as the tavern: the place where recruitment letters are used. Characters who are
// pulled turn up in front of it. Put this component on the Tavern prefab.
[RequireComponent(typeof(Building))]
public class Tavern : MonoBehaviour
{
    private Building building;

    private void Awake()
    {
        building = GetComponent<Building>();
    }

    // Where new recruits appear: the free cell just in front of (below) the tavern, or the nearest free
    // cell around it when that one is taken.
    public Vector3 SpawnPoint
    {
        get
        {
            if (building == null) building = GetComponent<Building>();
            MapGrid map = MapGrid.Instance;
            if (building == null || building.buildingType == null || map == null) return transform.position;

            Vector2Int size = building.buildingType.size;
            Vector2Int start = new Vector2Int(building.Cell.x - (size.x - 1) / 2, building.Cell.y - (size.y - 1) / 2);
            Vector2Int frontCenter = new Vector2Int(start.x + (size.x - 1) / 2, start.y - 1);

            bool found = false;
            Vector2Int best = frontCenter;
            int bestScore = int.MaxValue;
            for (int x = start.x - 1; x <= start.x + size.x; x++)
            {
                for (int y = start.y - 1; y <= start.y + size.y; y++)
                {
                    bool onRing = x < start.x || x >= start.x + size.x || y < start.y || y >= start.y + size.y;
                    if (!onRing) continue;
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!map.InBounds(cell) || map.IsBlocked(cell)) continue;

                    // Closest to the middle of the front wall wins; the front row beats the sides and back
                    int score = Mathf.Abs(x - frontCenter.x) + Mathf.Abs(y - frontCenter.y) * 3;
                    if (score < bestScore) { bestScore = score; best = cell; found = true; }
                }
            }
            return found ? map.CellCenterToWorld(best) : transform.position;
        }
    }
}
