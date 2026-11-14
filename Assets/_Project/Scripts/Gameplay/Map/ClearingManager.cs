using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// One region being cleared: how far along it is, and the invisible "clearing site" that characters are sent to work at
public class ClearingJob
{
    public Region region;
    public float progress;   // seconds of work done, counted as if every place were filled by an ordinary character (done at region.clearSeconds)
    public Building site;

    public float Fraction => region.clearSeconds <= 0f ? 1f : Mathf.Clamp01(progress / region.clearSeconds);
}

[Serializable]
public class ClearingJobData
{
    public string regionId;
    public float progress;
}

// Clearing land: you pay the gold, characters are assigned to the region from the jobs list ("Clear West Forest"), and while they are at work
// there the job moves along; when it is done the region is cleared and gives its yields.
//
// Each job is an invisible Building (a "clearing site") on the region's edge, next to cleared ground, with as many places as the region needs
// characters. It is not part of the BuildingManager (nobody builds, sees or saves it as a building), but characters treat it like any other
// workplace: the schedule sends them there in work hours, they walk to it, go "inside" (out of sight) and work, and while away they work by
// their schedule like everyone else. Added to the Map's object by MapRegions (nothing to set up in the scene).
public class ClearingManager : MonoBehaviour
{
    // A character good at clearing has a Specialty with this tag (the same mechanism as being good at a recipe)
    public const string SpecialtyTag = "Clearing";

    public static ClearingManager Instance { get; private set; }

    private readonly List<ClearingJob> jobs = new List<ClearingJob>();
    private MapRegions regions;
    private MapGrid mapGrid;

    public IReadOnlyList<ClearingJob> Jobs => jobs;

    // Raised when a job starts, ends or its numbers move a notch
    public event Action Changed;

    public enum StartResult { Started, NotClearable, AlreadyClearing, NotEnoughGold, NoAccess }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        regions = GetComponent<MapRegions>();
        mapGrid = GetComponent<MapGrid>();
        if (regions != null) regions.Changed += OnRegionsChanged;
    }

    private void OnDestroy()
    {
        if (regions != null) regions.Changed -= OnRegionsChanged;
        if (Instance == this) Instance = null;
    }

    public static string SiteIdFor(Region region) => "clear:" + region.Id;

    public ClearingJob JobFor(Region region)
    {
        foreach (ClearingJob job in jobs) if (job.region == region) return job;
        return null;
    }

    // The job a clearing site belongs to (null for any other building)
    public ClearingJob JobAt(Building site)
    {
        if (site == null) return null;
        foreach (ClearingJob job in jobs) if (job.site == site) return job;
        return null;
    }

    // --- Starting ---

    // The ground cell the clearing site stands on: a cell of the region that has the most free, usable ground beside it (that is where the
    // characters walk to). Null when none of its cells touches free ground.
    public Vector2Int? FindSiteCell(Region region)
    {
        if (regions == null || regions.Map == null || mapGrid == null) return null;
        Vector2Int? best = null;
        int bestFree = 0;
        foreach (Vector2Int cell in regions.Map.CellsOf(region))
        {
            int free = 0;
            foreach (Vector2Int step in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                Vector2Int next = cell + step;
                if (mapGrid.InBounds(next) && !mapGrid.IsBlocked(next)) free++;
            }
            bool better = free > bestFree || (free == bestFree && free > 0 && best.HasValue && (cell.y < best.Value.y || (cell.y == best.Value.y && cell.x < best.Value.x)));
            if (better) { best = cell; bestFree = free; }
        }
        return best;
    }

    public StartResult CanStart(Region region)
    {
        if (regions == null || regions.Map == null || region == null || !regions.Active || !regions.Map.CanClear(region)) return StartResult.NotClearable;
        if (JobFor(region) != null) return StartResult.AlreadyClearing;
        if (FindSiteCell(region) == null) return StartResult.NoAccess;
        float gold = ResourceManager.Instance != null ? ResourceManager.Instance.GetResourceAmount(ResourceManager.Gold) : 0f;
        if (gold < region.goldCost) return StartResult.NotEnoughGold;
        return StartResult.Started;
    }

    // Pays the gold and starts the job (characters are assigned afterwards from the jobs list). False, changing nothing, when it cannot start.
    public bool StartClearing(Region region)
    {
        if (CanStart(region) != StartResult.Started) return false;
        if (region.goldCost > 0 && !ResourceManager.Instance.changeResourceAmount(ResourceManager.Gold, -region.goldCost)) return false;
        ClearingJob job = AddJob(region, 0f);
        Notify.Show($"Clearing {region.DisplayName}: assign characters to it", NotifyKind.Info, null, () => UIManager.Instance?.OpenRegion(region), "clearing-start:" + region.Id);
        Changed?.Invoke();
        return job != null;
    }

    private ClearingJob AddJob(Region region, float progress)
    {
        Vector2Int? cell = FindSiteCell(region);
        if (cell == null)
        {
            IReadOnlyList<Vector2Int> all = regions.Map.CellsOf(region);
            if (all.Count == 0) return null;
            cell = all[0]; // (a loaded job whose ground got built over: it stays, though nobody can reach it until there is room)
        }
        ClearingJob job = new ClearingJob { region = region, progress = Mathf.Max(0f, progress), site = MakeSite(region, cell.Value) };
        jobs.Add(job);
        return job;
    }

    private Building MakeSite(Region region, Vector2Int cell)
    {
        GameObject go = new GameObject("Clearing: " + region.DisplayName);
        go.transform.SetParent(transform);
        go.transform.position = mapGrid.CellCenterToWorld(cell);
        Building site = go.AddComponent<Building>();
        BuildingType type = ScriptableObject.CreateInstance<BuildingType>();
        type.buildingName = "Clear " + region.DisplayName;
        type.size = new Vector2Int(1, 1);
        type.workerSlots = Mathf.Max(1, region.charactersNeeded);
        type.role = BuildingType.Role.Special;
        type.typeId = -1;
        site.buildingType = type;
        site.Id = SiteIdFor(region);
        site.SetCell(cell);
        return site;
    }

    // --- Progress ---

    // How fast the job goes right now, in seconds of work per second: each character at work there counts as much as their productivity
    // and what they are especially good at clearing, shared out over the characters the region needs (so half the places filled = half speed)
    public float WorkRate(ClearingJob job)
    {
        float sum = 0f;
        foreach (Character avatar in job.site.WorkersOnSite)
        {
            if (avatar == null || avatar.Owned == null) continue;
            sum += avatar.Productivity * avatar.Owned.SpecialtyMultiplier(SpecialtyTag);
        }
        return sum / Mathf.Max(1, job.region.charactersNeeded);
    }

    private void Update()
    {
        if (jobs.Count == 0) return;
        if (GameManager.Instance != null && GameManager.Instance.SimulationPaused) return;
        float dt = Time.deltaTime;
        foreach (ClearingJob job in new List<ClearingJob>(jobs))
        {
            float rate = WorkRate(job);
            if (rate > 0f) Advance(job, dt * rate);
        }
    }

    // Moves a job on by seconds of work; finishes it when it is done
    public void Advance(ClearingJob job, float workSeconds)
    {
        if (job == null || !jobs.Contains(job)) return;
        job.progress += workSeconds;
        if (job.progress >= job.region.clearSeconds) Complete(job);
    }

    // The job is done: the region is cleared, its yields are given, the characters are free again
    public void Complete(ClearingJob job)
    {
        if (job == null || !jobs.Contains(job)) return;
        Region region = job.region;
        RemoveJob(job);
        regions.Map.Clear(region);

        if (ResourceManager.Instance != null && region.yields != null)
        {
            foreach (RegionYield y in region.yields)
                if (y != null && y.amount > 0 && !string.IsNullOrWhiteSpace(y.item)) ResourceManager.Instance.Add(y.item.Trim(), y.amount);
            UIManager.Instance?.UpdateResourceUI(ResourceManager.Instance.resources);
        }
        string gives = region.YieldText();
        Notify.Show(gives.Length > 0 ? $"{region.DisplayName} cleared: +{gives}" : $"{region.DisplayName} cleared", NotifyKind.Success, null, null, "region-cleared:" + region.Id);
        Changed?.Invoke();
    }

    // Takes a job away, freeing its characters (no yields; the progress is gone with it)
    private void RemoveJob(ClearingJob job)
    {
        jobs.Remove(job);
        if (job.site == null) return;
        foreach (OwnedCharacter worker in new List<OwnedCharacter>(job.site.Workers)) job.site.RemoveWorker(worker);
        BuildingType type = job.site.buildingType;
        Destroy(job.site.gameObject);
        if (type != null) Destroy(type);
    }

    // Regions changed (cleared by a cheat or the story, or the Map was set up again): jobs for land that is no longer clearable are dropped
    private void OnRegionsChanged()
    {
        bool any = false;
        foreach (ClearingJob job in new List<ClearingJob>(jobs))
        {
            if (regions.Map != null && regions.Map.Find(job.region.Id) == job.region && !regions.Map.IsCleared(job.region)) continue;
            RemoveJob(job);
            any = true;
        }
        if (any) Changed?.Invoke();
    }

    // --- While the game was closed ---

    // Plays the time away through: in each slice, every paid character assigned here whose schedule says "work" at that real time of day does
    // their share. Jobs that finish give their yields as usual. Returns how many regions were cleared.
    public int AdvanceAway(float awaySeconds)
    {
        if (jobs.Count == 0) return 0;
        float total = Mathf.Min(awaySeconds, OfflineProgress.MaxSeconds);
        if (total < OfflineProgress.MinSeconds) return 0;

        DateTime end = GameClock.Instance != null ? GameClock.Instance.LocalNow : DateTime.Now;
        RealTimeCalendar calendar = GameClock.Instance != null ? GameClock.Instance.Calendar : new RealTimeCalendar();
        DateTime time = end - TimeSpan.FromSeconds(total);
        float slice = Mathf.Clamp(total / 180f, 5f, Mathf.Max(5f, OfflineProgress.StepSeconds));
        Dictionary<CharacterType, CharacterSchedule> schedules = new Dictionary<CharacterType, CharacterSchedule>();
        int cleared = 0;

        float done = 0f;
        while (done < total - 0.001f && jobs.Count > 0)
        {
            float step = Mathf.Min(slice, total - done);
            TimeOfDay phase = calendar.PhaseAt(time + TimeSpan.FromSeconds(step * 0.5f));
            foreach (ClearingJob job in new List<ClearingJob>(jobs))
            {
                float rate = AwayRate(job, phase, schedules);
                if (rate <= 0f) continue;
                job.progress += step * rate;
                if (job.progress >= job.region.clearSeconds) { Complete(job); cleared++; }
            }
            done += step;
            time += TimeSpan.FromSeconds(step);
        }
        if (jobs.Count > 0) Changed?.Invoke();
        return cleared;
    }

    private static float AwayRate(ClearingJob job, TimeOfDay phase, Dictionary<CharacterType, CharacterSchedule> schedules)
    {
        float sum = 0f;
        foreach (OwnedCharacter worker in job.site.Workers)
        {
            if (!worker.WagePaid) continue;
            if (!schedules.TryGetValue(worker.Type, out CharacterSchedule schedule))
            {
                schedule = new CharacterSchedule(worker.Type.defaultSchedule);
                schedules[worker.Type] = schedule;
            }
            if (!schedule.TrySlotDuring(phase, out ScheduleSlot slot) || slot != ScheduleSlot.Work) continue;
            sum += worker.BaseProductivity * worker.SpecialtyMultiplier(SpecialtyTag);
        }
        return sum / Mathf.Max(1, job.region.charactersNeeded);
    }

    // --- Saving ---

    public List<ClearingJobData> Export()
    {
        List<ClearingJobData> list = new List<ClearingJobData>();
        foreach (ClearingJob job in jobs) list.Add(new ClearingJobData { regionId = job.region.Id, progress = job.progress });
        return list;
    }

    // Drops every job (used when loading a game and on a reset)
    public void ClearAll()
    {
        foreach (ClearingJob job in new List<ClearingJob>(jobs)) RemoveJob(job);
        Changed?.Invoke();
    }

    // Brings the jobs of a save back (their sites get the ids the saved characters refer to). Returns the sites by id.
    public Dictionary<string, Building> Restore(IEnumerable<ClearingJobData> saved)
    {
        ClearAll();
        Dictionary<string, Building> sites = new Dictionary<string, Building>();
        if (saved == null || regions == null || regions.Map == null) return sites;
        foreach (ClearingJobData data in saved)
        {
            Region region = regions.Map.Find(data.regionId);
            if (region == null || !region.Clearable || regions.Map.IsCleared(region) || JobFor(region) != null) continue;
            ClearingJob job = AddJob(region, data.progress);
            if (job != null) sites[job.site.Id] = job.site;
        }
        Changed?.Invoke();
        return sites;
    }

    // --- For the screens ---

    // "Clearing: 35% (1 of 2 places filled, about 4 min left)"
    public string Describe(ClearingJob job)
    {
        int needed = Mathf.Max(1, job.region.charactersNeeded);
        float rate = WorkRate(job);
        StringBuilder text = new StringBuilder($"Clearing: {Mathf.FloorToInt(job.Fraction * 100f)}% done, {job.site.Workers.Count} of {needed} places filled");
        if (rate > 0f) text.Append($", about {RegionMenu.FormatTime((job.region.clearSeconds - job.progress) / rate)} left");
        else if (job.site.Workers.Count == 0) text.Append(", nobody assigned");
        else text.Append(", nobody is at work there right now");
        return text.ToString();
    }
}
