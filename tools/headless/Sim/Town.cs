using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A small version of GameScene, put together the way the scene is: the managers on one object, a grid, a camera, the input manager,
// a canvas with the panels and buttons the UI looks for by name, and a few building and character types.
// Each test suite builds its own (T.Suite starts from an empty world).
public class Town
{
    public GameManager gm;
    public BuildingPlacer placer;
    public BuildingManager bm;
    public MapGrid map;
    public RoadManager roads;
    public RoadPlacer roadPlacer;
    public ResourceManager rm;
    public CharacterManager cm;
    public GameClock clock;
    public MoodSystem mood;
    public WageSystem wages;
    public GachaSystem gacha;
    public UIManager ui;
    public InputManager input;
    public PlayerInputActions actions;
    public Grid grid;
    public Camera camera;
    public Transform canvas;
    public GameObject detailsPanel, assignPanel, listContent;
    public TextMeshProUGUI detailsText;
    public GameObject buttonPrefab;

    public BuildingType Mine, Lumberyard, Home, TownHall, Tavern;
    public CharacterType Firsy, Condo, Baker;
    public RoadType DirtRoad;

    // --- Content ---

    public static GameObject BuildingPrefab(string name, bool produces)
    {
        GameObject go = new GameObject(name + "Prefab", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Building));
        if (produces) go.AddComponent<ResourceProducer>();
        return World.MakePrefab(go);
    }

    public static BuildingType NewBuilding(string name, int id, int size, int workers = 0, int housing = 0, int gold = 0, int wood = 0, int goldCost = 0, int woodCost = 0)
    {
        BuildingType t = ScriptableObject.CreateInstance<BuildingType>();
        t.name = name; t.buildingName = name; t.typeId = id; t.size = new Vector2Int(size, size); t.sprite = new Sprite(100 * size, 100 * size);
        t.workerSlots = workers; t.housingSlots = housing; t.goldGenerationPerSec = gold; t.woodGenerationPerSec = wood;
        t.goldCost = goldCost; t.woodCost = woodCost;
        t.buildingPrefab = BuildingPrefab(name, gold > 0 || wood > 0);
        return t;
    }

    public static GameObject CharacterPrefab()
    {
        GameObject go = new GameObject("CharacterPrefab", typeof(SpriteRenderer), typeof(CapsuleCollider2D), typeof(Character));
        return World.MakePrefab(go);
    }

    public static CharacterType NewCharacter(string name, int id, Rarity rarity = Rarity.Common, int maxOwned = 10)
    {
        CharacterType t = ScriptableObject.CreateInstance<CharacterType>();
        t.name = name; t.displayName = name; t.typeId = id; t.rarity = rarity; t.maxOwned = maxOwned;
        t.sprite = new Sprite(64, 64); t.characterPrefab = CharacterPrefab();
        t.moveSpeed = 3f; t.productivity = 1.2f;
        t.defaultSchedule = new List<ScheduleEntry>
        {
            new ScheduleEntry { time = TimeOfDay.Morning, destination = ScheduleSlot.Work },
            new ScheduleEntry { time = TimeOfDay.Afternoon, destination = ScheduleSlot.Work },
            new ScheduleEntry { time = TimeOfDay.Evening, destination = ScheduleSlot.Rest1 },
            new ScheduleEntry { time = TimeOfDay.Night, destination = ScheduleSlot.Home },
        };
        return t;
    }

    // Sets a private or serialized field (what the inspector does in the scene)
    public static void Set(object target, string field, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f != null) { f.SetValue(target, value); return; }
        }
        throw new Exception($"{target.GetType().Name} has no field {field}");
    }

    public static T Get<T>(object target, string field)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f != null) return (T)f.GetValue(target);
        }
        throw new Exception($"{target.GetType().Name} has no field {field}");
    }

    // The centre of a building of this size anchored at a cell (where the placer puts it)
    public static Vector3 Center(int cx, int cy, int size) { float o = size % 2 == 0 ? 0.5f : 0f; return new Vector3(cx + 0.5f + o, cy + 0.5f + o, 0); }

    // --- The scene ---

    public static Town Build(bool withUi = true)
    {
        Town t = new Town();
        PlayerPrefs.SetInt(GameClock.ModePreference, (int)ClockMode.InGame); // the game's own clock: tests move time themselves

        t.Mine = NewBuilding("Gold Mine", 1, 3, workers: 3, gold: 5, goldCost: 50);
        t.Lumberyard = NewBuilding("Lumberyard", 2, 2, workers: 3, wood: 2, goldCost: 30);
        t.Home = NewBuilding("Home", 3, 2, housing: 2, goldCost: 20, woodCost: 10);
        t.TownHall = NewBuilding("Town Hall", 4, 3); t.TownHall.movable = false;
        t.Tavern = NewBuilding("Tavern", 5, 2); t.Tavern.diningSlots = 6;
        t.Firsy = NewCharacter("Firsy", 1);
        t.Condo = NewCharacter("Condo", 2, Rarity.Epic, maxOwned: 1);
        t.Baker = NewCharacter("Baker", 3, Rarity.Rare, maxOwned: 3);
        t.DirtRoad = ScriptableObject.CreateInstance<RoadType>();
        t.DirtRoad.name = "Dirt Road"; t.DirtRoad.displayName = "Dirt Road"; t.DirtRoad.typeId = 1; t.DirtRoad.sprite = new Sprite(100, 100);

        // Camera and grid
        GameObject cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
        cam.transform.position = new Vector3(0, 0, -10);
        t.camera = cam.GetComponent<Camera>();
        t.grid = new GameObject("Grid").AddComponent<Grid>();
        t.input = new GameObject("InputManager").AddComponent<InputManager>();
        t.actions = Get<PlayerInputActions>(t.input, "playerInputActions");

        // The managers, on one object like in the scene
        GameObject mgr = new GameObject("GameManager");
        t.gm = mgr.AddComponent<GameManager>();
        t.placer = mgr.AddComponent<BuildingPlacer>();
        t.placer.AllBuildings = new List<BuildingType> { t.Mine, t.Lumberyard, t.Home, t.TownHall, t.Tavern };
        Set(t.placer, "buildingGrid", t.grid);
        Set(t.placer, "mainCamera", t.camera);
        GameObject indicator = new GameObject("PlacementIndicator", typeof(SpriteRenderer));
        indicator.SetActive(false);
        t.placer.currentIndicatorInstance = indicator;
        t.bm = mgr.AddComponent<BuildingManager>();
        t.map = mgr.AddComponent<MapGrid>();
        Set(t.map, "grid", t.grid);
        mgr.AddComponent<PathService>();
        t.roads = mgr.AddComponent<RoadManager>();
        Set(t.roads, "roadTypes", new List<RoadType> { t.DirtRoad });
        t.roadPlacer = mgr.AddComponent<RoadPlacer>();
        t.mood = mgr.AddComponent<MoodSystem>();
        t.rm = mgr.AddComponent<ResourceManager>();
        t.cm = mgr.AddComponent<CharacterManager>();
        t.cm.AllCharacters = new List<CharacterType> { t.Firsy, t.Condo, t.Baker };
        Set(mgr.AddComponent<GameClock>(), "autoAdvance", false);
        t.clock = GameClock.Instance;

        if (withUi) t.BuildUi();
        World.Tick(0.02f); // runs every Start
        return t;
    }

    private GameObject Ui(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, bool label = true, Transform parent = null)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent ?? canvas, false);
        RectTransform r = (RectTransform)g.transform;
        r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.anchoredPosition = position; r.sizeDelta = size;
        if (label) { GameObject l = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI)); l.transform.SetParent(g.transform, false); }
        return g;
    }

    private void BuildUi()
    {
        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        buttonPrefab = new GameObject("ButtonPrefab", typeof(RectTransform), typeof(Image), typeof(Button));
        GameObject buttonLabel = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        buttonLabel.transform.SetParent(buttonPrefab.transform, false);
        World.MakePrefab(buttonPrefab);

        GameObject canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas = canvasGO.transform;
        Vector2 mid = new Vector2(0.5f, 0.5f);

        Ui("ResourceText", mid, mid, new Vector2(-454, 187), new Vector2(200, 50));
        Ui("OpenBuildButton", mid, mid, new Vector2(-509, -48), new Vector2(88, 30)).AddComponent<Button>();
        GameObject hire = Ui("OpenHireButton", mid, mid, new Vector2(-393, -48), new Vector2(93, 30));
        hire.AddComponent<Button>();
        hire.GetComponentInChildren<TMP_Text>().text = "Hire";
        Ui("OpenMenuButton", Vector2.one, Vector2.one, new Vector2(-30, -30), new Vector2(116, 35)).AddComponent<Button>();
        GameObject menuPanel = Ui("MenuPanel", new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(180, 404), false);
        GameObject quit = Ui("QuitButton", new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, -279), new Vector2(180, 65), true, menuPanel.transform);
        quit.AddComponent<Button>();
        quit.GetComponentInChildren<TMP_Text>().text = "Quit";
        Ui("TestButtonforFunctions", mid, mid, new Vector2(13, 178), new Vector2(264, 31));
        GameObject buildMenu = Ui("BuildingMenuPanel", Vector2.zero, Vector2.one, new Vector2(1, -156), new Vector2(0, -313), false);
        buildMenu.AddComponent<Image>();
        buildMenu.AddComponent<GridLayoutGroup>().cellSize = new Vector2(100, 140);
        buildMenu.SetActive(false);
        GameObject hireMenu = Ui("HireMenuPanel", Vector2.zero, Vector2.one, new Vector2(1, -156), new Vector2(0, -313), false);
        hireMenu.AddComponent<Image>();
        hireMenu.AddComponent<GridLayoutGroup>().cellSize = new Vector2(100, 140);
        hireMenu.SetActive(false);

        detailsPanel = Ui("CharacterPanel", Vector2.zero, Vector2.one, new Vector2(370, 0), new Vector2(-740, 0), false);
        detailsPanel.AddComponent<Image>();
        GameObject info = Ui("CharacterInfo", mid, mid, Vector2.zero, new Vector2(365, 441), true, detailsPanel.transform);
        detailsText = info.GetComponentInChildren<TextMeshProUGUI>();
        GameObject assign = Ui("AssignButton", mid, mid, new Vector2(-88, -182), new Vector2(144, 39), true, detailsPanel.transform);
        assign.AddComponent<Image>();
        assign.AddComponent<Button>();
        assign.GetComponentInChildren<TMP_Text>().text = "Assign";
        assignPanel = Ui("AssignPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, detailsPanel.transform);
        GameObject scroll = Ui("Scroll View", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, assignPanel.transform);
        scroll.AddComponent<ScrollRect>();
        GameObject viewport = Ui("Viewport", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, scroll.transform);
        listContent = Ui("Content", new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero, false, viewport.transform);
        assignPanel.SetActive(false);
        detailsPanel.SetActive(false);

        GameObject uiGO = new GameObject("UI");
        uiGO.SetActive(false); // wired up before it wakes, like a scene object
        ui = uiGO.AddComponent<UIManager>();
        ui.buttonPrefab = buttonPrefab;
        ui.buildingButtonContainer = buildMenu.transform;
        ui.hireButtonContainer = hireMenu.transform;
        ui.assignPanel = assignPanel;
        ui.assignButtonContainer = listContent.transform;
        ui.characterPanel = detailsPanel;
        Set(ui, "characterText", detailsText);
        Set(ui, "GridlinesGO", new GameObject("Gridlines"));
        uiGO.SetActive(true);
    }

    // --- Helpers for the tests ---

    public Building Place(BuildingType type, int cx, int cy) => bm.Build(type, new Vector2Int(cx, cy), Center(cx, cy, type.size.x));
    public OwnedCharacter Hire(CharacterType type, float x = 0, float y = 0) => cm.Hire(type, new Vector3(x, y, 0));

    // The rows of the list beside the details panel (jobs, occupants)
    public List<string> ListRows() => listContent.transform.children.Where(c => !c.destroyed).Select(c => c.GetComponentInChildren<TMP_Text>()?.text ?? "").ToList();

    public static List<Transform> Rows(Transform content) => content.children.Where(c => !c.destroyed).ToList();
    public static string TextOf(Transform t) => t.GetComponentInChildren<TMP_Text>()?.text ?? "";
    public static Transform RowStartingWith(Transform content, string start) => Rows(content).FirstOrDefault(c => TextOf(c).StartsWith(start));

    public Transform Panel(string name) => canvas.Find(name);
    public Transform Content(string panel) => canvas.Find(panel + "/Scroll/Viewport/Content");
    public List<string> PanelRows(string panel) { World.EndFrame(); return Rows(Content(panel)).Select(TextOf).ToList(); }

    public static void Click(Transform t) => t.GetComponent<Button>().onClick.Invoke();

    // A click on the map at a world position (what is under it is found by its collider)
    public void ClickWorld(Vector3 world)
    {
        Vector3 screen = camera.WorldToScreenPoint(world);
        actions.Gameplay.PointerPosition.Perform(new Vector2(screen.x, screen.y));
        actions.Gameplay.PointerClick.Perform();
        World.Tick(0.016f);
        actions.Gameplay.PointerClick.Cancel();
    }
}
