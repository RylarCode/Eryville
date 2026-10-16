using System;
using System.Collections.Generic;
using UnityEngine;

// Lets the player paint roads (press and drag over cells) or erase them. A cursor shows the cell under
// the pointer: green where a click will do something, red where it will not. Each new road cell costs
// what the RoadType says; painting stops when the player cannot afford the next cell.
public class RoadPlacer : MonoBehaviour, IPlacementMode
{
    public static RoadPlacer Instance { get; private set; }

    private enum Mode { None, Paint, Erase }

    private Mode mode = Mode.None;
    private RoadType paintType;

    private bool stroking;          // a press has started a stroke that has not been released yet
    private bool touchStroke;       // the stroke is a finger drag (a touch screen has no "button held" to ask the InputManager about)
    private Vector2Int lastCell;
    private bool outOfMoneyLogged;

    private GameObject cursor;
    private SpriteRenderer cursorRenderer;

    public bool IsPlacing => mode != Mode.None;
    public bool IsPainting => mode == Mode.Paint;
    public bool IsErasing => mode == Mode.Erase;

    // Raised when painting / erasing mode ends (cancelled, or another building mode took over)
    public event Action ModeEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InputManager.PlacementModes.Add(this);
    }

    // Subscribed in Start, not OnEnable, so the InputManager is certain to exist already
    private void Start()
    {
        InputManager input = InputManager.Instance;
        if (input == null)
        {
            Debug.LogWarning("InputManager not found. Road building will not work.");
            return;
        }
        input.OnPointerClick += HandlePress;
        input.OnPointerPositionUpdate += HandleMove;
        input.OnPointerReleased += HandleRelease;
    }

    private void OnDestroy()
    {
        InputManager.PlacementModes.Remove(this);
        InputManager input = InputManager.Instance;
        if (input != null)
        {
            input.OnPointerClick -= HandlePress;
            input.OnPointerPositionUpdate -= HandleMove;
            input.OnPointerReleased -= HandleRelease;
        }
        if (cursor != null) Destroy(cursor);
    }

    // --- Modes ---

    public void StartPainting(RoadType type)
    {
        if (type == null) return;
        BeginMode(Mode.Paint);
        paintType = type;
    }

    public void StartErasing()
    {
        BeginMode(Mode.Erase);
    }

    public void Cancel()
    {
        if (mode == Mode.None) return;
        mode = Mode.None;
        stroking = false;
        touchStroke = false;
        if (cursor != null) cursor.SetActive(false);
        ModeEnded?.Invoke();
    }

    private void BeginMode(Mode newMode)
    {
        if (BuildingPlacer.Instance != null) BuildingPlacer.Instance.CancelPlacing(); // one building mode at a time
        mode = newMode;
        stroking = false;
        outOfMoneyLogged = false;
        EnsureCursor();
        cursor.SetActive(false); // shown once the pointer moves over the map
    }

    // --- Input ---

    private void HandlePress(Vector2 screenPosition)
    {
        if (!IsPlacing) return;

        Vector2Int cell = CellAt(screenPosition);
        stroking = true;
        lastCell = cell;
        outOfMoneyLogged = false;
        MoveCursor(cell);
        ApplyTo(cell);
    }

    private void HandleMove(Vector2 screenPosition)
    {
        if (!IsPlacing) return;

        Vector2Int cell = CellAt(screenPosition);
        MoveCursor(cell);

        InputManager input = InputManager.Instance;
        if (stroking && ((input != null && input.IsPointerHeld) || touchStroke) && cell != lastCell)
        {
            // Fast drags jump over cells: fill in the straight line from where the last one was
            foreach (Vector2Int onTheWay in Line(lastCell, cell))
            {
                if (onTheWay != lastCell) ApplyTo(onTheWay);
            }
            lastCell = cell;
        }
    }

    private void HandleRelease(Vector2 screenPosition)
    {
        stroking = false;
    }

    // --- Finger strokes (a touch drag paints or erases like a held mouse button does) ---

    public void BeginTouchStroke(Vector2 screenPosition)
    {
        if (!IsPlacing) return;
        touchStroke = true;
        HandlePress(screenPosition);
    }

    public void ContinueTouchStroke(Vector2 screenPosition)
    {
        if (!IsPlacing || !touchStroke) return;
        HandleMove(screenPosition);
    }

    public void EndTouchStroke()
    {
        touchStroke = false;
        stroking = false;
    }

    private static Vector2Int CellAt(Vector2 screenPosition)
    {
        Camera cam = Camera.main;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, cam.nearClipPlane + 10));
        return MapGrid.Instance.WorldToCell(world);
    }

    // --- Painting and erasing ---

    private void ApplyTo(Vector2Int cell)
    {
        if (mode == Mode.Erase)
        {
            RoadManager.Instance.Remove(cell);
            return;
        }

        if (!RoadManager.Instance.CanBuild(paintType, cell)) return; // off the map, under a building, or already there

        if (!CanAfford(paintType))
        {
            if (!outOfMoneyLogged) Debug.Log($"Not enough resources for another {paintType.displayName} cell ({paintType.goldCost}G, {paintType.woodCost}W).");
            outOfMoneyLogged = true;
            stroking = false; // stop painting for this stroke
            return;
        }

        if (!RoadManager.Instance.Build(paintType, cell)) return;
        ResourceManager.Instance.changeResourceAmount("Gold", -paintType.goldCost);
        ResourceManager.Instance.changeResourceAmount("Wood", -paintType.woodCost);
    }

    private static bool CanAfford(RoadType type)
    {
        return ResourceManager.Instance.GetResourceAmount("Gold") >= type.goldCost
            && ResourceManager.Instance.GetResourceAmount("Wood") >= type.woodCost;
    }

    // Every cell on the straight line between two cells, both ends included
    public static IEnumerable<Vector2Int> Line(Vector2Int from, Vector2Int to)
    {
        int dx = Math.Abs(to.x - from.x), dy = Math.Abs(to.y - from.y);
        int stepX = from.x < to.x ? 1 : -1, stepY = from.y < to.y ? 1 : -1;
        int error = dx - dy;
        int x = from.x, y = from.y;
        while (true)
        {
            yield return new Vector2Int(x, y);
            if (x == to.x && y == to.y) yield break;
            int doubled = 2 * error;
            if (doubled > -dy) { error -= dy; x += stepX; }
            if (doubled < dx) { error += dx; y += stepY; }
        }
    }

    // --- Cursor ---

    private void EnsureCursor()
    {
        if (cursor != null) return;

        cursor = new GameObject("RoadCursor");
        cursorRenderer = cursor.AddComponent<SpriteRenderer>();
        Texture2D white = Texture2D.whiteTexture;
        cursorRenderer.sprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f), white.width); // exactly one cell
        cursorRenderer.sortingOrder = 5;
        cursor.SetActive(false);
    }

    private void MoveCursor(Vector2Int cell)
    {
        if (cursor == null) return;

        cursor.SetActive(true);
        cursor.transform.position = MapGrid.Instance.CellCenterToWorld(cell);

        bool useful = mode == Mode.Erase ? MapGrid.Instance.RoadAt(cell) != null : RoadManager.Instance.CanBuild(paintType, cell);
        Color tint = useful ? Color.green : Color.red;
        cursorRenderer.color = new Color(tint.r, tint.g, tint.b, 0.5f);
    }
}
