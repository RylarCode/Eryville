using System;
using System.Collections.Generic;
using UnityEngine;

// Keeps everyone fed while the game runs (the time away is done by OfflineProgress with the same HungerRules).
// Once a second every owned character gets a little hungrier and, if it is a meal window and it is hungry enough (or very
// hungry at any time), eats from the town's stock: its favourite first. Characters that work in food buildings are served
// first, so a shortage cannot grow into a worse one. Eating shows a food picture over the character when it is in view.
public class HungerSystem : MonoBehaviour
{
    public static HungerSystem Instance { get; private set; }

    [SerializeField] private float tickSeconds = 1f;

    // Raised when someone eats (or finds something to eat by itself)
    public event Action<OwnedCharacter, MealResult> Ate;

    // What was eaten since the day began, per item (for the stock list), and how many characters are hungry now
    private readonly Dictionary<string, float> eatenToday = new Dictionary<string, float>();
    private int eatenDay = int.MinValue;
    private float elapsed;
    private int rotation;
    private readonly List<OwnedCharacter> order = new List<OwnedCharacter>();

    public int HungryCount { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.SimulationPaused) return;
        if (CharacterManager.Instance == null || GameClock.Instance == null) return;

        elapsed += Time.deltaTime;
        if (elapsed < tickSeconds) return;
        float seconds = elapsed;
        elapsed = 0f;
        Tick(seconds);
    }

    // Everyone gets hungrier by this many seconds and, if they want to, eats
    public void Tick(float seconds)
    {
        FoodSettings settings = FoodSettings.Current;
        GameClock clock = GameClock.Instance;
        ResourceManager stock = ResourceManager.Instance;
        float hour = clock.HourOfDay;
        int day = clock.MealDay;

        if (day != eatenDay) { eatenToday.Clear(); eatenDay = day; }

        BuildEatingOrder(CharacterManager.Instance.Roster);
        int hungry = 0;
        foreach (OwnedCharacter character in order)
        {
            bool wasHungry = character.IsHungry;
            HungerRules.Drain(character.Hunger, seconds, character.Appetite, settings);

            if (character.Hunger.Dining)
            {
                // On the way to (or at) a table. If it never gets there it eats where it stands instead, next time round.
                character.Hunger.DiningFor += seconds;
                if (character.Hunger.DiningFor > settings.diningTimeout && character.Avatar != null && character.Avatar.CurrentActivity != Character.Activity.Eating) character.Avatar.CancelDining();
                if (character.Avatar == null) character.Hunger.Dining = false;
            }
            else if (stock != null && HungerRules.WantsToEat(character.Hunger, hour, day, settings))
            {
                if (!TryDine(character, hour, day, settings))
                {
                    MealResult meal = HungerRules.TryEat(character.Hunger, character.Type.favouriteFood, stock, hour, day, settings);
                    if (meal.Any) OnAte(character, meal, settings);
                }
            }

            // Mood earned from meals is given out here, with the same clamping as any other change
            if (character.Hunger.PendingMood != 0f)
            {
                character.Mood += character.Hunger.PendingMood;
                character.Hunger.PendingMood = 0f;
            }
            if (character.IsHungry)
            {
                hungry++;
                if (!wasHungry && !StressTest.IsActive) Notify.Show($"{character.DisplayName} is hungry", NotifyKind.Warning, character.Type.FaceOrSprite, NotifyActions.ForCharacter(character), "hungry:" + character.Id, 300f);
            }
        }
        HungryCount = hungry;
        if (stock != null) UIManager.Instance?.UpdateResourceUI(stock.resources);
    }

    // In a meal window, with a free seat at a dining place close by, the character walks there to eat (once per window)
    private bool TryDine(OwnedCharacter character, float hour, int day, FoodSettings settings)
    {
        if (!settings.dineOut || character.Avatar == null) return false;
        int window = HungerRules.WindowAt(hour, settings);
        if (window < 0 || character.Hunger.Satiety < settings.emergencyBelow) return false; // a snack is eaten on the spot
        long key = HungerRules.MealKey(day, window);
        if (character.Hunger.SkipDiningKey == key) return false;

        Building place = FindDiningPlace(character.Avatar.transform.position, settings);
        if (place == null || !place.ReserveSeat(character)) return false;

        character.Hunger.SkipDiningKey = key;
        character.Hunger.Dining = true;
        character.Hunger.DiningFor = 0f;
        character.Hunger.DiningHour = hour;
        character.Hunger.DiningDay = day;
        character.Avatar.StartDining(place);
        return true;
    }

    // The nearest dining place with a free seat, within the settings' distance
    public static Building FindDiningPlace(Vector3 from, FoodSettings settings)
    {
        Building best = null;
        float bestDistance = settings.maxDiningDistance;
        foreach (Building building in BuildingManager.Instance.Buildings)
        {
            if (!building.IsDiningPlace || building.SeatsFree <= 0) continue;
            float distance = Vector3.Distance(from, building.transform.position);
            if (distance <= bestDistance) { best = building; bestDistance = distance; }
        }
        return best;
    }

    // Called by the character when its meal at a table is over: the food comes out of the stock, the place's quality and the
    // dining bonus lift the mood
    public void CompleteMeal(OwnedCharacter character, Building place)
    {
        FoodSettings settings = FoodSettings.Current;
        ResourceManager stock = ResourceManager.Instance;
        if (stock == null) return;

        float quality = place != null && place.buildingType != null ? Mathf.Max(0f, place.buildingType.mealQuality) : 1f;
        MealResult meal = HungerRules.TryEat(character.Hunger, character.Type.favouriteFood, stock, character.Hunger.DiningHour, character.Hunger.DiningDay, settings, quality);
        if (meal.Any && !meal.Foraged) character.Hunger.PendingMood += settings.diningMoodBonus * quality;
        if (meal.Any) OnAte(character, meal, settings);

        character.Mood += character.Hunger.PendingMood;
        character.Hunger.PendingMood = 0f;
        UIManager.Instance?.UpdateResourceUI(stock.resources);
    }

    private void OnAte(OwnedCharacter character, MealResult meal, FoodSettings settings)
    {
        foreach (KeyValuePair<ItemType, float> eaten in meal.Eaten)
        {
            eatenToday.TryGetValue(eaten.Key.Name, out float sum);
            eatenToday[eaten.Key.Name] = sum + eaten.Value;
        }
        if (character.Avatar != null && meal.Main != null) character.Avatar.ShowEating(meal.Main, settings.eatSeconds);
        Ate?.Invoke(character, meal);
    }

    // How many of an item have been eaten today
    public float EatenToday(string item)
    {
        return eatenToday.TryGetValue(item, out float amount) ? amount : 0f;
    }

    // Food workers first (they feed the others), then everyone else; the start moves along each tick so nobody always eats last
    private void BuildEatingOrder(IReadOnlyList<OwnedCharacter> roster)
    {
        order.Clear();
        List<OwnedCharacter> others = new List<OwnedCharacter>();
        foreach (OwnedCharacter character in roster)
        {
            if (WorksWithFood(character)) order.Add(character); else others.Add(character);
        }
        Rotate(order, rotation);
        Rotate(others, rotation);
        order.AddRange(others);
        rotation++;
    }

    public static bool WorksWithFood(OwnedCharacter character)
    {
        Building work = character.WorkBuilding;
        if (work == null || work.buildingType == null) return false;
        foreach (Recipe recipe in work.buildingType.Recipes)
        {
            if (recipe == null || recipe.outputs == null) continue;
            foreach (ItemAmount output in recipe.outputs) if (output.item != null && output.item.edible) return true;
        }
        return false;
    }

    public static void Rotate<T>(List<T> list, int by)
    {
        if (list.Count < 2) return;
        int shift = ((by % list.Count) + list.Count) % list.Count;
        if (shift == 0) return;
        List<T> copy = new List<T>(list);
        for (int i = 0; i < copy.Count; i++) list[i] = copy[(i + shift) % copy.Count];
    }
}
