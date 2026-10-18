using System;
using System.Collections.Generic;
using UnityEngine;

// What happened while the game was closed. Production goes by the real clock, so coming back after a few hours should not feel
// like nothing happened.
//
// It is the same production as while playing, not an estimate: the away time is walked through a minute at a time, and in each
// minute every worker is where their schedule says they would be at that real time of day (at work in the work hours, home or
// resting otherwise). A building runs when its workers would be at work, and the same ProductionNetwork shares out the stock, so
// chains (farm -> mill -> bakery), shortages and full storage all play out as they would have. The characters themselves are not
// simulated; only the answer to "would they be at work?" is taken from their schedules.
public static class OfflineProgress
{
    public static float MaxSeconds = 24f * 3600f;  // at most this much away time counts (one login a day fills it)
    public static float MinSeconds = 60f;          // a short absence (switching apps) is not worth a report
    public static float StepSeconds = 60f;         // the longest slice the away time is walked through in (short absences use finer ones, see Compute)

    public struct Report
    {
        public float seconds;                              // away time that counted (after the cap)
        public Dictionary<string, float> items;            // what changed in the stock, per item (negative = used up)
        public List<string> notes;                         // what held the town back ("Bakery waited for Flour for 40% of the time")
        public int meals;                                  // meals the characters ate while away
        public int hungryAtEnd;                            // how many are hungry when the time away ends
        public Dictionary<OwnedCharacter, HungerState> hunger; // how full everyone is at the end (given to them by Apply)

        public float Change(string item)
        {
            return items != null && items.TryGetValue(item, out float amount) ? amount : 0f;
        }

        public float gold => Change(ResourceManager.Gold);
        public float wood => Change(ResourceManager.Wood);

        public bool Anything
        {
            get
            {
                if (items == null) return false;
                if (meals > 0) return true;
                foreach (float amount in items.Values) if (Mathf.Abs(amount) > 0.5f) return true;
                return false;
            }
        }

        public override string ToString()
        {
            List<string> parts = new List<string>();
            if (items != null)
            {
                foreach (KeyValuePair<string, float> entry in items)
                {
                    if (Mathf.Abs(entry.Value) > 0.5f) parts.Add($"{entry.Value:+0;-0} {entry.Key.ToLowerInvariant()}");
                }
            }
            if (meals > 0) parts.Add($"{meals} meals eaten");
            string text = $"Away {seconds / 3600f:0.#} h: " + (parts.Count > 0 ? string.Join(", ", parts) : "nothing");
            if (notes != null && notes.Count > 0) text += ". " + string.Join(". ", notes);
            return text;
        }
    }

    public static Report LastReport { get; private set; }

    // A copy of the stock to play the time away on, with the same room limits as the real one
    private sealed class ScratchStock : IItemStock
    {
        private readonly Dictionary<string, float> amounts = new Dictionary<string, float>();
        public readonly Dictionary<string, float> start = new Dictionary<string, float>();

        public ScratchStock()
        {
            if (ResourceManager.Instance == null) return;
            foreach (Resource resource in ResourceManager.Instance.resources.Values)
            {
                amounts[resource.resourceName] = resource.currentAmount;
                start[resource.resourceName] = resource.currentAmount;
            }
        }

        public float Get(string item) { amounts.TryGetValue(item, out float v); return v; }

        public float Capacity(string item)
        {
            return ResourceManager.Instance != null ? ResourceManager.Instance.Capacity(item) : ItemCatalog.BaseCapacity(item);
        }

        public void Add(string item, float amount)
        {
            if (!start.ContainsKey(item)) start[item] = 0f;
            float capacity = Capacity(item);
            float result = Get(item) + amount;
            if (capacity > 0f) result = Mathf.Min(result, capacity);
            amounts[item] = Mathf.Max(0f, result);
        }

        public Dictionary<string, float> Changes()
        {
            Dictionary<string, float> changes = new Dictionary<string, float>();
            foreach (KeyValuePair<string, float> entry in amounts)
            {
                start.TryGetValue(entry.Key, out float before);
                if (Mathf.Abs(entry.Value - before) > 0.0001f) changes[entry.Key] = entry.Value - before;
            }
            return changes;
        }
    }

    // What the stock would change by in the given away time (ending now, by the real clock). Nothing is changed.
    public static Report Compute(IEnumerable<Building> buildings, float awaySeconds)
    {
        Report report = new Report { seconds = Mathf.Clamp(awaySeconds, 0f, MaxSeconds), items = new Dictionary<string, float>(), notes = new List<string>() };
        if (awaySeconds < MinSeconds) { report.seconds = 0f; return report; }

        // The producing buildings, and the schedules of the workers they have
        List<Building> producers = new List<Building>();
        foreach (Building building in buildings) if (building.buildingType != null && building.Producer != null && building.buildingType.Produces) producers.Add(building);

        // Everyone's hunger is played through too (copies: nothing real changes until Apply)
        FoodSettings food = FoodSettings.Current;
        List<OwnedCharacter> eaters = new List<OwnedCharacter>();
        Dictionary<OwnedCharacter, HungerState> hunger = new Dictionary<OwnedCharacter, HungerState>();
        if (CharacterManager.Instance != null)
        {
            foreach (OwnedCharacter character in CharacterManager.Instance.Roster) { eaters.Add(character); hunger[character] = character.Hunger.Clone(); }
        }
        if (producers.Count == 0 && eaters.Count == 0) return report;

        Dictionary<CharacterType, CharacterSchedule> schedules = new Dictionary<CharacterType, CharacterSchedule>();
        List<ProductionLine> lines = new List<ProductionLine>();
        int[] staffedSteps = new int[producers.Count];
        int[] waitingSteps = new int[producers.Count];
        int[] fullSteps = new int[producers.Count];
        string[] blocked = new string[producers.Count];
        foreach (Building building in producers) lines.Add(new ProductionLine { Owner = building });

        ScratchStock stock = new ScratchStock();
        DateTime end = GameClock.Instance != null ? GameClock.Instance.LocalNow : DateTime.Now;
        RealTimeCalendar calendar = GameClock.Instance != null ? GameClock.Instance.Calendar : new RealTimeCalendar();
        DateTime time = end - TimeSpan.FromSeconds(report.seconds);
        int stepIndex = 0;

        // What is made in one slice can only be used from the next, so each link of a chain (farm, mill, bakery) delays the next by
        // one slice. Short absences therefore use fine slices (about 180 of them), long ones up to StepSeconds: the delay is always
        // around 1% or less of the time away.
        float slice = Mathf.Clamp(report.seconds / 180f, 5f, Mathf.Max(5f, StepSeconds));
        float done = 0f;
        while (done < report.seconds - 0.001f)
        {
            float step = Mathf.Min(slice, report.seconds - done);
            TimeOfDay phase = calendar.PhaseAt(time + TimeSpan.FromSeconds(step * 0.5f));

            for (int i = 0; i < producers.Count; i++)
            {
                Building building = producers[i];
                lines[i].Recipe = building.Producer.ActiveRecipe;
                lines[i].Multiplier = WorkersAtWork(building, phase, schedules, hunger, food);
            }
            ProductionNetwork.Step(lines, stock, step);
            if (eaters.Count > 0) report.meals += FeedEveryone(eaters, hunger, stock, food, calendar, time + TimeSpan.FromSeconds(step * 0.5f), step, stepIndex);
            stepIndex++;

            for (int i = 0; i < producers.Count; i++)
            {
                if (lines[i].Multiplier <= 0f || lines[i].Recipe == null) continue;
                staffedSteps[i]++;
                if (lines[i].Status == LineStatus.WaitingForInput) { waitingSteps[i]++; blocked[i] = lines[i].BlockedBy; }
                else if (lines[i].Status == LineStatus.StorageFull) { fullSteps[i]++; blocked[i] = lines[i].BlockedBy; }
            }

            done += step;
            time += TimeSpan.FromSeconds(step);
        }

        report.items = stock.Changes();
        report.hunger = hunger;
        foreach (HungerState state in hunger.Values) if (HungerRules.IsHungry(state.Satiety, food)) report.hungryAtEnd++;
        if (report.hungryAtEnd > 0) report.notes.Add($"{report.hungryAtEnd} of {eaters.Count} characters are hungry");
        for (int i = 0; i < producers.Count; i++)
        {
            if (staffedSteps[i] == 0) continue;
            float waiting = waitingSteps[i] / (float)staffedSteps[i];
            float full = fullSteps[i] / (float)staffedSteps[i];
            if (waiting >= 0.1f) report.notes.Add($"{producers[i].DisplayName} waited for {blocked[i]} {waiting * 100f:0}% of the time");
            else if (full >= 0.1f) report.notes.Add($"{producers[i].DisplayName} had no room for {blocked[i]} {full * 100f:0}% of the time");
        }
        return report;
    }

    // How fast the building would be going at this time of day: 0 if none of its paid workers would be at work, else their
    // productivity multiplied together. A building with no worker slots runs all the time.
    private static float WorkersAtWork(Building building, TimeOfDay phase, Dictionary<CharacterType, CharacterSchedule> schedules, Dictionary<OwnedCharacter, HungerState> hunger, FoodSettings food)
    {
        if (building.WorkerSlots <= 0) return 1f;

        float multiplier = 1f;
        int atWork = 0;
        foreach (OwnedCharacter worker in building.Workers)
        {
            if (!worker.WagePaid) continue;
            if (!schedules.TryGetValue(worker.Type, out CharacterSchedule schedule))
            {
                schedule = new CharacterSchedule(worker.Type.defaultSchedule);
                schedules[worker.Type] = schedule;
            }
            if (!schedule.TrySlotDuring(phase, out ScheduleSlot slot) || slot != ScheduleSlot.Work) continue;
            atWork++;
            float fed = hunger.TryGetValue(worker, out HungerState state) ? HungerRules.ProductivityFactor(state, food) : 1f; // hungry workers are a little slower, well fed ones a little faster
            multiplier *= worker.BaseProductivity * fed * worker.SpecialtyMultiplier(building.Producer.ActiveRecipe);
        }
        return atWork > 0 ? multiplier : 0f;
    }

    // Everyone gets hungrier for this slice and, if it is a meal window, eats from the scratch stock (food workers first). Returns the meals eaten.
    private static int FeedEveryone(List<OwnedCharacter> eaters, Dictionary<OwnedCharacter, HungerState> hunger, ScratchStock stock, FoodSettings food, RealTimeCalendar calendar, DateTime middle, float seconds, int stepIndex)
    {
        float hour = (float)middle.TimeOfDay.TotalHours;
        int day = calendar.DayNumber(middle);

        List<OwnedCharacter> first = new List<OwnedCharacter>();
        List<OwnedCharacter> rest = new List<OwnedCharacter>();
        foreach (OwnedCharacter character in eaters) { if (HungerSystem.WorksWithFood(character)) first.Add(character); else rest.Add(character); }
        HungerSystem.Rotate(first, stepIndex);
        HungerSystem.Rotate(rest, stepIndex);
        first.AddRange(rest);

        int meals = 0;
        foreach (OwnedCharacter character in first)
        {
            HungerState state = hunger[character];
            HungerRules.Drain(state, seconds, character.Appetite, food);
            if (!HungerRules.WantsToEat(state, hour, day, food)) continue;
            if (HungerRules.TryEat(state, character.Type.favouriteFood, stock, hour, day, food).Any) meals++;
        }
        return meals;
    }

    // Works out and gives what was earned. Returns the report.
    public static Report Apply(float awaySeconds)
    {
        Report report = Compute(BuildingManager.Instance.Buildings, awaySeconds);
        if (report.Anything || report.hunger != null)
        {
            foreach (KeyValuePair<string, float> change in report.items) ResourceManager.Instance.Add(change.Key, change.Value);
            if (report.hunger != null)
            {
                foreach (KeyValuePair<OwnedCharacter, HungerState> entry in report.hunger)
                {
                    entry.Key.Hunger.Satiety = entry.Value.Satiety;
                    entry.Key.Hunger.LastMealKey = entry.Value.LastMealKey;
                    entry.Key.Hunger.LastForageKey = entry.Value.LastForageKey;
                    entry.Key.Hunger.RecentMeals = new List<string>(entry.Value.RecentMeals);
                }
            }
            UIManager.Instance?.UpdateResourceUI(ResourceManager.Instance.resources);
        }
        LastReport = report;
        Debug.Log("Offline progress: " + report);
        return report;
    }
}
