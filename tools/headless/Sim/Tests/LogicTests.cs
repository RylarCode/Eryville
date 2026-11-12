using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The pure parts of the game (no scene needed): they are checked exactly.
public static class LogicTests
{
    public sealed class Stock : IItemStock
    {
        public readonly Dictionary<string, float> Amounts = new Dictionary<string, float>();
        public readonly Dictionary<string, float> Caps = new Dictionary<string, float>();
        public float Get(string item) => Amounts.TryGetValue(item, out float v) ? v : 0f;
        public float Capacity(string item) => Caps.TryGetValue(item, out float v) ? v : 0f;
        public void Add(string item, float amount) { float cap = Capacity(item); float r = Get(item) + amount; if (cap > 0) r = Math.Min(r, cap); Amounts[item] = Math.Max(0f, r); }
    }

    public static ItemType Item(string name, bool edible = false, float nourishment = 0f, float capacity = 0f)
    {
        ItemType i = ScriptableObject.CreateInstance<ItemType>();
        i.name = name; i.itemName = name; i.edible = edible; i.nourishment = nourishment; i.capacity = capacity;
        i.category = edible ? ItemCategory.Meal : ItemCategory.Ingredient;
        return i;
    }

    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

    public static void Pathfinding()
    {
        T.Section("open ground");
        var none = new HashSet<Vector2Int>();
        GridPathfinder pf = new GridPathfinder(V(-20, -20), V(20, 20), c => none.Contains(c), c => 1f, 1f);
        var path = pf.FindPath(new[] { V(0, 0) }, new[] { V(5, 0) }, out float time);
        T.Check(path != null && path.Count == 6 && T.Near(time, 5f) && path[0] == V(0, 0) && path[5] == V(5, 0), "a straight line on open ground");
        path = pf.FindPath(new[] { V(0, 0) }, new[] { V(3, 3) }, out time);
        T.Check(path != null && path.Count == 7 && path.Zip(path.Skip(1), (a, b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y)).All(d => d == 1), "never diagonal: 7 cells to go 3 across and 3 up");

        T.Section("walls and roads");
        var wall = new HashSet<Vector2Int>();
        for (int y = -20; y <= 20; y++) if (y != 15) wall.Add(V(2, y));
        GridPathfinder walled = new GridPathfinder(V(-20, -20), V(20, 20), c => wall.Contains(c), c => 1f, 1f);
        path = walled.FindPath(new[] { V(0, 0) }, new[] { V(4, 0) }, out time);
        T.Check(path != null && path.All(c => !wall.Contains(c)) && path.Contains(V(2, 15)), "goes round a wall through its gap");
        wall.Add(V(2, 15));
        T.Check(walled.FindPath(new[] { V(0, 0) }, new[] { V(4, 0) }, out time) == null, "a closed wall means no path");
        var road = new Dictionary<Vector2Int, float>();
        for (int x = -20; x <= 20; x++) road[V(x, 3)] = 4f;
        GridPathfinder fast = new GridPathfinder(V(-20, -20), V(20, 20), c => false, c => road.TryGetValue(c, out float s) ? s : 1f, 4f);
        path = fast.FindPath(new[] { V(0, 0) }, new[] { V(10, 0) }, out time);
        T.Check(path != null && path.Count(c => c.y == 3) >= 6 && time < 10f, "a fast road is worth a detour: " + time.ToString("0.00"));
        T.Check(fast.FindPath(new[] { V(0, 0) }, new[] { V(5, 0), V(-2, 0) }, out time).Last() == V(-2, 0), "the nearest of several goals");

        T.Section("nearest free cell");
        var block = new HashSet<Vector2Int>();
        for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++) block.Add(V(x, y));
        GridPathfinder pfb = new GridPathfinder(V(-10, -10), V(10, 10), c => block.Contains(c), c => 1f, 1f);
        T.Check(pfb.TryFindNearestWalkable(V(0, 0), 10, out Vector2Int near) && !block.Contains(near) && Math.Max(Math.Abs(near.x), Math.Abs(near.y)) == 3, "the nearest free cell to one inside a building is just outside it: " + near);
    }

    public static void Production()
    {
        ItemType wheat = Item("Wheat", capacity: 100), flour = Item("Flour"), bread = Item("Bread", true, 30f);
        Recipe grow = Recipe.Create("Grow Wheat", 10f, new ItemAmount[0], new[] { new ItemAmount(wheat, 2) });
        Recipe mill = Recipe.Create("Mill Flour", 10f, new[] { new ItemAmount(wheat, 2) }, new[] { new ItemAmount(flour, 1) });
        Recipe bake = Recipe.Create("Bake Bread", 10f, new[] { new ItemAmount(flour, 1) }, new[] { new ItemAmount(bread, 1) });

        T.Section("one building");
        Stock stock = new Stock();
        var farm = new ProductionLine { Recipe = grow, Multiplier = 1f };
        ProductionNetwork.Step(new[] { farm }, stock, 50f);
        T.Check(T.Near(stock.Get("Wheat"), 10f) && farm.Status == LineStatus.Working, "a farm makes 2 wheat per 10 s: 10 in 50 s");
        farm.Multiplier = 0f; ProductionNetwork.Step(new[] { farm }, stock, 50f);
        T.Check(farm.Status == LineStatus.NoWorker && T.Near(stock.Get("Wheat"), 10f), "nobody working: nothing made");

        T.Section("a chain");
        stock = new Stock(); stock.Amounts["Wheat"] = 4f;
        var miller = new ProductionLine { Recipe = mill, Multiplier = 1f };
        var baker = new ProductionLine { Recipe = bake, Multiplier = 1f };
        ProductionNetwork.Step(new[] { miller, baker }, stock, 50f);
        T.Check(T.Near(stock.Get("Wheat"), 0f) && T.Near(stock.Get("Flour"), 2f) && baker.Status == LineStatus.WaitingForInput && baker.BlockedBy == "Flour", "the mill uses what wheat there is; the bakery waits for flour made this step");
        ProductionNetwork.Step(new[] { miller, baker }, stock, 50f);
        T.Check(T.Near(stock.Get("Bread"), 2f) && miller.Status == LineStatus.WaitingForInput, "next step the bakery bakes it; the mill waits for wheat");

        T.Section("sharing");
        stock = new Stock(); stock.Amounts["Wheat"] = 4f;
        var m1 = new ProductionLine { Recipe = mill, Multiplier = 1f };
        var m2 = new ProductionLine { Recipe = mill, Multiplier = 1f };
        ProductionNetwork.Step(new[] { m1, m2 }, stock, 50f);
        T.Check(T.Near(m1.Efficiency, m2.Efficiency) && T.Near(stock.Get("Flour"), 2f), "two mills short of wheat share it evenly");
        stock = new Stock(); stock.Caps["Wheat"] = 100f; stock.Amounts["Wheat"] = 99f;
        var f1 = new ProductionLine { Recipe = grow, Multiplier = 1f };
        ProductionNetwork.Step(new[] { f1 }, stock, 50f);
        T.Check(T.Near(stock.Get("Wheat"), 100f) && f1.Status == LineStatus.StorageFull && f1.BlockedBy == "Wheat", "a full store stops production and says why");
    }

    public static void Hunger()
    {
        FoodSettings s = ScriptableObject.CreateInstance<FoodSettings>();
        T.Section("drain and states");
        HungerState h = new HungerState();
        HungerRules.Drain(h, s.hungerHoursToEmpty * 3600f / 2f, 1f, s);
        T.Check(T.Near(h.Satiety, 50f, 0.1f), "an ordinary appetite empties in " + s.hungerHoursToEmpty + " h: half in half the time");
        HungerRules.Drain(h, s.hungerHoursToEmpty * 3600f, 1f, s);
        T.Check(h.Satiety == 0f, "never below 0");
        T.Check(HungerRules.IsHungry(s.hungryBelow - 1f, s) && !HungerRules.IsHungry(s.hungryBelow + 1f, s), "hungry below " + s.hungryBelow);
        T.Check(T.Near(HungerRules.ProductivityFactor(0f, s), s.productivityFloor) && HungerRules.ProductivityFactor(100f, s) == 1f, "hunger slows work down, never below the floor");

        T.Section("eating");
        ItemType bread = Item("Bread", true, 30f), fish = Item("Fish", true, 30f);
        ItemCatalog.Register(bread); ItemCatalog.Register(fish);
        Stock stock = new Stock(); stock.Amounts["Bread"] = 5f; stock.Amounts["Fish"] = 5f;
        MealWindow lunch = s.mealWindows.FirstOrDefault(w => w.startHour >= 11f) ?? s.mealWindows[0];
        float hour = lunch.startHour + 0.5f;
        h = new HungerState { Satiety = s.eatBelow - 10f };
        T.Check(HungerRules.WantsToEat(h, hour, 1, s), "in a meal window, below " + s.eatBelow + ": wants to eat");
        T.Check(!HungerRules.WantsToEat(new HungerState { Satiety = s.eatBelow + 5f }, hour, 1, s), "full enough: does not");
        MealResult meal = HungerRules.TryEat(h, fish, stock, hour, 1, s);
        T.Check(meal.Any && meal.Main == fish && stock.Get("Fish") < 5f && h.Satiety > s.eatBelow - 10f, "eats, its favourite first");
        T.Check(!HungerRules.WantsToEat(h, hour, 1, s), "once per meal window");
        HungerState starving = new HungerState { Satiety = s.emergencyBelow - 1f };
        T.Check(HungerRules.WantsToEat(starving, 3f, 1, s), "very hungry: eats at any time");
    }

    public static void Gacha()
    {
        var candidates = new List<GachaRoller.Candidate> { new GachaRoller.Candidate(Rarity.Common, 1), new GachaRoller.Candidate(Rarity.Rare, 1), new GachaRoller.Candidate(Rarity.Epic, 1) };
        var odds = new List<RarityOdds> { new RarityOdds(Rarity.Common, 70), new RarityOdds(Rarity.Rare, 25), new RarityOdds(Rarity.Epic, 5) };
        var pity = new List<PityRule> { new PityRule(Rarity.Rare, 10) };
        T.Section("odds");
        System.Random rng = new System.Random(1);
        int[] counters = new int[1];
        int rareOrBetter = 0;
        for (int i = 0; i < 2000; i++) if (GachaRoller.Pick(candidates, odds, new List<PityRule>(), counters, () => rng.NextDouble()).rarity >= Rarity.Rare) rareOrBetter++;
        T.Check(rareOrBetter > 500 && rareOrBetter < 700, "about 30% Rare or better without pity: " + rareOrBetter + "/2000");
        T.Section("pity");
        counters = new int[1];
        int forced = -1;
        for (int i = 0; i < 10; i++)
        {
            var r = GachaRoller.Pick(candidates, odds, pity, counters, () => 0.0); // always the most common result
            if (r.pityApplied) { forced = i; T.Check(r.rarity >= Rarity.Rare, "the pity pull is at least Rare"); }
        }
        T.Check(forced == 9 && counters[0] == 0, "with the worst luck, pull 10 is guaranteed and the counter starts again");
    }

    public static void TimeAndLight()
    {
        RealTimeCalendar cal = new RealTimeCalendar();
        DateTime day = new DateTime(2026, 11, 12);
        T.Section("times of day (Morning 6-12, Afternoon 12-18, Evening 18-24, Night 0-6)");
        (int h, int m, TimeOfDay want)[] cases = { (0, 0, TimeOfDay.Night), (5, 59, TimeOfDay.Night), (6, 0, TimeOfDay.Morning), (11, 59, TimeOfDay.Morning), (12, 0, TimeOfDay.Afternoon), (17, 59, TimeOfDay.Afternoon), (18, 0, TimeOfDay.Evening), (21, 0, TimeOfDay.Evening), (23, 59, TimeOfDay.Evening) };
        foreach (var (h, m, want) in cases) T.Check(cal.PhaseAt(day.AddHours(h).AddMinutes(m)) == want, $"{h:00}:{m:00} is {want}");
        T.Check(cal.PhaseStart(day.AddHours(22)) == day.AddHours(18) && cal.NextPhaseStart(day.AddHours(22)) == day.AddDays(1), "Evening runs from 18:00 to midnight");
        T.Check(cal.PhaseStart(day.AddHours(3)) == day && cal.NextPhaseStart(day.AddHours(3)) == day.AddHours(6), "Night runs from midnight to 06:00");
        T.Check(cal.DayNumber(day.AddHours(5)) + 1 == cal.DayNumber(day.AddHours(6)), "a new day (payday) starts at 06:00");

        T.Section("lighting (dawn 6-8, dusk 18-20)");
        LightingProfile p = ScriptableObject.CreateInstance<LightingProfile>();
        (float h, DayStage want)[] stages = { (5.9f, DayStage.Night), (6f, DayStage.Dawn), (7.9f, DayStage.Dawn), (8f, DayStage.Day), (17.9f, DayStage.Day), (18f, DayStage.Dusk), (19.9f, DayStage.Dusk), (20f, DayStage.Night), (2f, DayStage.Night) };
        foreach (var (h, want) in stages) T.Check(p.StageAt(h) == want, $"{h}:00 looks like {want}");
        Color noon = p.Evaluate(12f), midnight = p.Evaluate(0f), dawn = p.Evaluate(6.5f);
        T.Check(noon.r == 1f && noon.g == 1f && noon.b > 0.95f && midnight.b > midnight.r && dawn.r > dawn.b, "full daylight at noon, blue at night, warm at dawn");
    }

    public static void Layout()
    {
        T.Section("3/4 and flat");
        BuildingLayout flat = BuildingLayout.Compute(BuildingArtStyle.FitFootprint, new Vector2Int(2, 2), new Vector2(1, 1), 0f, Vector2.zero);
        T.Check(T.Near(flat.ArtSize.x, 2f) && T.Near(flat.ArtSize.y, 2f) && flat.ArtOffset == Vector2.zero, "a top-down picture covers its cells exactly");
        BuildingLayout tall = BuildingLayout.Compute(BuildingArtStyle.ThreeQuarter, new Vector2Int(2, 2), new Vector2(1, 2), 0f, Vector2.zero);
        T.Check(T.Near(tall.ArtSize.x, 2f) && T.Near(tall.ArtSize.y, 4f) && tall.ArtOffset.y > 0f, "a 3/4 picture keeps its shape and rises above its cells");
        T.Section("depth");
        T.Check(DepthSorting.Order(0f) > DepthSorting.Order(1f), "lower on the screen is drawn in front");
    }
}
