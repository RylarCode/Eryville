using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// The Map's regions in the running game: finds the Region assets and the region image (Resources/Regions), keeps what is cleared, tells
// the Map which cells are usable, and draws the fog. Added to the MapGrid's object by the MapGrid itself (nothing to set up in the scene).
//
// With no region image and Region assets the Map has no regions and every cell inside its bounds is usable, as before.
// The rules themselves are in RegionMap.
public class MapRegions : MonoBehaviour
{
    public const string AssetsFolder = "Regions";
    public const string ImageName = "Regions/RegionMap";
    public const string OnPreference = "MapRegionsOn";
    public const int OverlaySortingOrder = -2; // above the ground (-5), grid lines (-4) and roads (-3), below buildings and characters

    private static MapRegions instance;

    // The regions, or null before the Map exists (never creates one)
    public static MapRegions Existing => instance;

    public RegionMap Map { get; private set; }

    // Raised when what is cleared changes, or regions are switched on or off
    public event Action Changed;

    private MapGrid mapGrid;
    private Tilemap overlay;
    private Tile fogTile, clearableTile, riverTile;

    // Whether the Map has regions at all
    public bool HasRegions => Map != null && Map.Regions.Count > 0;

    // The player's (tester's) switch: off, every cell is usable and there is no fog. Cheats can flip it.
    public bool Enabled
    {
        get { return PlayerPrefs.GetInt(OnPreference, 1) == 1; }
        set
        {
            if (Enabled == value) return;
            PlayerPrefs.SetInt(OnPreference, value ? 1 : 0);
            PlayerPrefs.Save();
            OnMapChanged();
        }
    }

    // Whether regions are in force now
    public bool Active => Enabled && HasRegions;

    // Whether buildings, roads and walking are allowed on the cell (true everywhere when there are no regions)
    public bool IsUsable(Vector2Int cell)
    {
        return !Active || Map.IsUsable(cell);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
        mapGrid = GetComponent<MapGrid>();
        LoadFromAssets();
    }

    private void OnDestroy()
    {
        if (Map != null) Map.Changed -= OnMapChanged;
        if (instance == this) instance = null;
    }

    // Reads the Region assets and the image from Resources/Regions
    private void LoadFromAssets()
    {
        Region[] regions = Resources.LoadAll<Region>(AssetsFolder);
        Texture2D image = Resources.Load<Texture2D>(ImageName);
        if (image == null || regions.Length == 0) return;

        Color32[] pixels;
        try { pixels = image.GetPixels32(); }
        catch (Exception)
        {
            Debug.LogError("The region image (Resources/" + ImageName + ") cannot be read: select it in Unity and turn on Read/Write in its import settings (and set Compression to None).");
            return;
        }
        Configure(regions, pixels, image.width, image.height);
    }

    // Sets up the regions from an image's pixels (what the assets load into; tests use it directly). The image's lower left pixel is the
    // Map's lowest cell.
    public void Configure(IEnumerable<Region> regions, Color32[] pixels, int width, int height)
    {
        if (Map != null) Map.Changed -= OnMapChanged;
        Vector2Int min = mapGrid != null ? mapGrid.Min : new Vector2Int(-28, -17);
        List<string> problems = new List<string>();
        Map = RegionMap.FromPixels(min, width, height, pixels, regions, problems);
        foreach (string problem in problems) Debug.LogWarning("Region image: " + problem);
        if (mapGrid != null && (mapGrid.Max.x - mapGrid.Min.x + 1 != width || mapGrid.Max.y - mapGrid.Min.y + 1 != height))
            Debug.LogWarning($"The region image is {width}x{height} but the Map is {mapGrid.Max.x - mapGrid.Min.x + 1}x{mapGrid.Max.y - mapGrid.Min.y + 1} cells: cells outside the image are never usable.");
        Map.ResetToStart();
        Map.Changed += OnMapChanged;
        OnMapChanged();
    }

    private void OnMapChanged()
    {
        if (mapGrid != null) mapGrid.RegionsChanged();
        RebuildOverlay();
        Changed?.Invoke();
    }

    // --- The fog ---

    private static readonly Color FogColour = new Color(0.04f, 0.06f, 0.09f, 0.985f);       // very dense: you cannot see into it
    private static readonly Color ClearableColour = new Color(0.98f, 0.82f, 0.35f, 0.25f);   // a region you can clear
    private static readonly Color RiverColour = new Color(0.35f, 0.55f, 0.85f, 0.3f);        // a revealed region that cannot be cleared

    private Tile MakeTile(Color colour)
    {
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f); // exactly one cell
        tile.color = colour;
        return tile;
    }

    private void EnsureOverlay()
    {
        if (overlay != null || mapGrid == null || mapGrid.Grid == null) return;
        GameObject go = new GameObject("RegionOverlay", typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(mapGrid.Grid.transform);
        overlay = go.GetComponent<Tilemap>();
        go.GetComponent<TilemapRenderer>().sortingOrder = OverlaySortingOrder;
        fogTile = MakeTile(FogColour);
        clearableTile = MakeTile(ClearableColour);
        riverTile = MakeTile(RiverColour);
    }

    // The tile (fog or a tint) a cell shows, or null for a cleared cell
    public TileBase OverlayTileAt(Vector2Int cell)
    {
        if (!Active) return null;
        Region region = Map.RegionAt(cell);
        if (region == null || !Map.IsRevealed(region)) return fogTile;
        if (Map.IsCleared(region)) return null;
        return region.Clearable ? (TileBase)clearableTile : riverTile;
    }

    private void RebuildOverlay()
    {
        EnsureOverlay();
        if (overlay == null) return;
        overlay.ClearAllTiles();
        if (!Active || mapGrid == null) return;
        for (int y = mapGrid.Min.y; y <= mapGrid.Max.y; y++)
            for (int x = mapGrid.Min.x; x <= mapGrid.Max.x; x++)
            {
                TileBase tile = OverlayTileAt(new Vector2Int(x, y));
                if (tile != null) overlay.SetTile(new Vector3Int(x, y, 0), tile);
            }
    }

    // --- For the rest of the game ---

    public int ClearedCount => Map != null ? Map.ClearedCount : 0;

    public void ResetToStart()
    {
        if (Map != null) Map.ResetToStart();
    }

    public void ClearEverything()
    {
        if (Map != null) Map.ClearEverything();
    }
}
