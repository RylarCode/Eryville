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
            HungerRules.Drain(character.Hunger, seconds, character.Appetite, settings);

            if (stock != null && HungerRules.WantsToEat(character.Hunger, hour, day, settings))
            {
                MealResult meal = HungerRules.TryEat(character.Hunger, character.Type.favouriteFood, stock, hour, day, settings);
                if (meal.Any) OnAte(character, meal, settings);
            }

            // Mood earned from meals is given out here, with the same clamping as any other change
            if (character.Hunger.PendingMood != 0f)
            {
                character.Mood += character.Hunger.PendingMood;
                character.Hunger.PendingMood = 0f;
            }
            if (character.IsHungry) hungry++;
        }
        HungryCount = hungry;
        if (stock != null) UIManager.Instance?.UpdateResourceUI(stock.resources);
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
