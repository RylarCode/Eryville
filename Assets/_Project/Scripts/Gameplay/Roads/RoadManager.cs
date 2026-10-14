using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Builds and removes roads. Which cells have a road is kept on the MapGrid (so walking, routing and
// building placement all see the same thing); this adds the rules (what may be built where) and the
// picture: roads are drawn on a tilemap made at runtime, one renderer for any number of road cells.
public class RoadManager : MonoBehaviour
{
    private static RoadManager instance;

    // Created on demand (with no road types) if the scene does not have one
    public static RoadManager Instance
    {
        get
        {
            if (instance == null)
            {
                Debug.LogWarning("No RoadManager in the scene; creating one.");
                instance = new GameObject("RoadManager").AddComponent<RoadManager>();
            }
            return instance;
        }
    }

    [SerializeField] private List<RoadType> roadTypes = new List<RoadType>(); // the kinds of road the player can build
    [SerializeField] private int sortingOrder = -3; // above the ground (-5) and grid lines (-4), below buildings and characters (0)

    public IReadOnlyList<RoadType> RoadTypes => roadTypes;

    public event Action RoadsChanged;

    private Tilemap tilemap;
    private readonly Dictionary<RoadType, Tile> tiles = new Dictionary<RoadType, Tile>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        CreateTilemap();
    }

    // The tilemap goes under the scene's Grid so its cells line up with everything else
    private void CreateTilemap()
    {
        Grid grid = MapGrid.Instance.Grid;
        if (grid == null) grid = new GameObject("RoadGrid").AddComponent<Grid>(); // no scene Grid assigned: make one

        GameObject go = new GameObject("Roads");
        go.transform.SetParent(grid.transform);
        tilemap = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortingOrder = sortingOrder;
    }

    // Whether a road of this type can be built on the cell: on the map, not under a building,
    // and not already that road
    public bool CanBuild(RoadType type, Vector2Int cell)
    {
        MapGrid map = MapGrid.Instance;
        return type != null && map.InBounds(cell) && !map.IsBuildingCell(cell) && map.RoadAt(cell) != type;
    }

    // Builds (or replaces) the road on a cell. Cost is NOT handled here; the caller pays.
    public bool Build(RoadType type, Vector2Int cell)
    {
        if (!CanBuild(type, cell)) return false;

        MapGrid.Instance.SetRoad(cell, type);
        tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), TileFor(type));
        RoadsChanged?.Invoke();
        return true;
    }

    // Removes the road on a cell, if there is one
    public bool Remove(Vector2Int cell)
    {
        if (!MapGrid.Instance.ClearRoad(cell)) return false;

        tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), null);
        RoadsChanged?.Invoke();
        return true;
    }

    // Removes every road (used when loading a game)
    public void ClearAll()
    {
        MapGrid.Instance.ClearRoads();
        tilemap.ClearAllTiles();
        RoadsChanged?.Invoke();
    }

    // One tile per road type, made when first needed. The tile is scaled so the sprite fills its cell
    // whatever size the sprite is.
    private Tile TileFor(RoadType type)
    {
        if (tiles.TryGetValue(type, out Tile tile)) return tile;

        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = type.sprite;
        tile.colliderType = Tile.ColliderType.None;
        if (type.sprite != null)
        {
            Vector3 size = type.sprite.bounds.size;
            if (size.x > 0f && size.y > 0f) tile.transform = Matrix4x4.Scale(new Vector3(1f / size.x, 1f / size.y, 1f));
        }
        tiles[type] = tile;
        return tile;
    }

    public RoadType FindType(int typeId)
    {
        foreach (RoadType type in roadTypes)
        {
            if (type != null && type.typeId == typeId) return type;
        }
        return null;
    }
}
