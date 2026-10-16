using System.Collections.Generic;
using UnityEngine;

// Fills the running game with lots of buildings and characters (living and working in them, walking
// to and from work like real ones) to see how it holds up. Everything it makes is its own and is removed
// by Clear(). While any of it exists the game cannot be saved, so a stress test never ends up in a save file.
public class StressTest : MonoBehaviour
{
    public static StressTest Instance { get; private set; }

    // True while any stress characters or buildings exist
    public static bool IsActive => Instance != null && (Instance.characters.Count > 0 || Instance.buildings.Count > 0);

    private readonly List<OwnedCharacter> characters = new List<OwnedCharacter>();
    private readonly List<Building> buildings = new List<Building>();
    private int scanIndex;       // where the search for free building spots carries on
    private int buildingTypeTurn; // alternates homes and workplaces

    public int CharacterCount => characters.Count;
    public int BuildingCount => buildings.Count;

    // With churn on, the time of day advances every few seconds, so everybody keeps walking to work and back
    public bool Churn { get; set; }
    public float churnSeconds = 4f;
    private float churnTimer;

    public StressBenchmark Benchmark { get; private set; }
    public System.Action<BenchResult> BenchmarkResult; // raised for each finished level (the caller adds memory figures)
    public System.Action BenchmarkFinished;

    // Whether the game scene is loaded (the managers this needs exist)
    public static bool CanRun => BuildingManager.Instance != null && CharacterManager.Instance != null && MapGrid.Instance != null && BuildingPlacer.Instance != null;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // --- Buildings ---

    // Builds up to count buildings on free spots of the map (homes and workplaces alternately), without charging
    // for them. Returns how many fit.
    public int AddBuildings(int count)
    {
        if (!CanRun) return 0;

        List<BuildingType> homes = new List<BuildingType>();
        List<BuildingType> workplaces = new List<BuildingType>();
        foreach (BuildingType type in BuildingPlacer.Instance.AllBuildings)
        {
            if (type == null || type.buildingPrefab == null) continue;
            if (type.housingSlots > 0) homes.Add(type);
            else if (type.workerSlots > 0) workplaces.Add(type);
        }
        if (homes.Count == 0 && workplaces.Count == 0) return 0;

        MapGrid map = MapGrid.Instance;
        const int stride = 5; // spots are spaced so there is always room to walk between buildings
        int width = Mathf.Max(1, (map.Max.x - map.Min.x) / stride + 1);
        int height = Mathf.Max(1, (map.Max.y - map.Min.y) / stride + 1);
        int built = 0;

        while (built < count && scanIndex < width * height)
        {
            Vector2Int cell = new Vector2Int(map.Min.x + 2 + (scanIndex % width) * stride, map.Min.y + 2 + (scanIndex / width) * stride);
            scanIndex++;

            List<BuildingType> pool = (buildingTypeTurn++ % 2 == 0 ? homes : workplaces);
            if (pool.Count == 0) pool = homes.Count > 0 ? homes : workplaces;
            BuildingType type = pool[buildingTypeTurn / 2 % pool.Count];

            if (!BuildingManager.Instance.CanPlace(type, cell)) continue;

            Vector3 position = map.CellCenterToWorld(cell);
            if (type.size.x % 2 == 0) position.x += 0.5f;
            if (type.size.y % 2 == 0) position.y += 0.5f;

            Building building = BuildingManager.Instance.Build(type, cell, position);
            if (building == null) continue;
            buildings.Add(building);
            built++;
        }
        return built;
    }

    // --- Characters ---

    // Adds count characters, each with a home and a job where there is room (building more as needed), standing at home.
    // Ignores the usual limit on how many of a kind can be owned. Returns how many were added.
    public int AddCharacters(int count)
    {
        if (!CanRun || count <= 0) return 0;

        List<CharacterType> types = new List<CharacterType>();
        foreach (CharacterType type in CharacterManager.Instance.AllCharacters)
        {
            if (type != null && type.characterPrefab != null) types.Add(type);
        }
        if (types.Count == 0) return 0;

        EnsureRoomFor(count);

        int added = 0;
        for (int i = 0; i < count; i++)
        {
            OwnedCharacter owned = new OwnedCharacter(types[(characters.Count + i) % types.Count]);
            CharacterManager.Instance.Restore(owned);
            characters.Add(owned);

            Building home = CharacterManager.Instance.FindHomeWithSpace();
            if (home != null) CharacterManager.Instance.AssignResident(owned, home);
            Building work = FindWorkplaceWithSpace();
            if (work != null) CharacterManager.Instance.AssignWorker(owned, work);

            Vector3 where = home != null ? home.transform.position : (work != null ? work.transform.position : Vector3.zero);
            CharacterManager.Instance.SpawnAvatar(owned, where);
            added++;
        }
        return added;
    }

    private void EnsureRoomFor(int count)
    {
        int guard = 0;
        while (FreeSlots(true) < count || FreeSlots(false) < count)
        {
            if (AddBuildings(2) == 0 || ++guard > 2000) break; // the map is full
        }
    }

    private static int FreeSlots(bool housing)
    {
        int free = 0;
        foreach (Building building in BuildingManager.Instance.Buildings)
        {
            free += housing ? building.HousingSlots - building.Residents.Count : building.WorkerSlots - building.Workers.Count;
        }
        return free;
    }

    private static Building FindWorkplaceWithSpace()
    {
        foreach (Building building in BuildingManager.Instance.Buildings)
        {
            if (building.HasWorkerSlot) return building;
        }
        return null;
    }

    // --- Cleaning up ---

    public void Clear()
    {
        if (CharacterManager.Instance != null)
        {
            foreach (OwnedCharacter owned in characters) CharacterManager.Instance.Dismiss(owned);
        }
        if (BuildingManager.Instance != null)
        {
            foreach (Building building in buildings) BuildingManager.Instance.Remove(building);
        }
        characters.Clear();
        buildings.Clear();
        scanIndex = 0;
        buildingTypeTurn = 0;
        Benchmark = null;
        Churn = false;
        // Churn moved the real-time clock around to keep everybody commuting: put it back to the real time
        if (GameClock.Instance != null && GameClock.Instance.IsRealTime) GameClock.Instance.ResetDebugOffset();
    }

    // --- Benchmark ---

    public void StartBenchmark(int[] levels, float warmupSeconds = 3f, float measureSeconds = 6f)
    {
        if (!CanRun) return;
        Churn = true; // the real load: everybody commuting all the time
        Benchmark = new StressBenchmark(levels, warmupSeconds, measureSeconds);
    }

    private void Update()
    {
        if (Churn && GameClock.Instance != null && IsActive)
        {
            churnTimer += Time.unscaledDeltaTime;
            if (churnTimer >= churnSeconds)
            {
                churnTimer = 0f;
                GameClock.Instance.Advance();
            }
        }

        if (Benchmark != null) StepBenchmark(Time.unscaledDeltaTime);
    }

    // Public so tests can drive it with chosen frame times
    public void StepBenchmark(float frameSeconds)
    {
        StressBenchmark benchmark = Benchmark;
        if (benchmark == null) return;

        if (benchmark.CurrentPhase == StressBenchmark.Phase.Spawning)
        {
            int missing = benchmark.TargetCharacters - characters.Count;
            if (missing > 0) AddCharacters(missing);
            benchmark.SpawnDone();
            return; // this frame was spent spawning: it is not a measurement
        }

        BenchResult result = benchmark.Tick(frameSeconds);
        if (result != null)
        {
            result.buildings = BuildingManager.Instance != null ? BuildingManager.Instance.Buildings.Count : 0;
            BenchmarkResult?.Invoke(result);
        }
        if (benchmark.Finished)
        {
            Churn = false;
            BenchmarkFinished?.Invoke();
        }
    }
}
