using System.Collections.Generic;
using UnityEngine;

// How full a character is, and when it last ate. A plain object so the same rules run on a real character and on a copy for the time away.
public class HungerState
{
    public const float Full = 100f;

    public float Satiety = Full;       // 0 (empty) to 100 (full)
    public long LastMealKey = -1;      // the meal window (day and which one) of the latest meal, so it eats once per window
    public long LastForageKey = -1;    // the window in which it last found something by itself
    public float PendingMood;          // mood earned by meals, waiting to be given to the character

    public HungerState Clone()
    {
        return new HungerState { Satiety = Satiety, LastMealKey = LastMealKey, LastForageKey = LastForageKey, PendingMood = PendingMood };
    }
}

// What a meal was
public class MealResult
{
    public readonly List<KeyValuePair<ItemType, float>> Eaten = new List<KeyValuePair<ItemType, float>>(); // item and how many units
    public float Gained;           // satiety gained
    public bool Favourite;         // mostly the character's favourite food
    public bool Foraged;           // there was no food in stock: it found a little by itself
    public ItemType Main;          // what most of it was (for the picture)
    public bool Any => Gained > 0f;
}

// The rules of hunger and eating, with no Unity objects in them: used one second at a time while playing (HungerSystem) and in
// slices for the time away (OfflineProgress), so a town is fed the same either way.
public static class HungerRules
{
    // --- Time ---

    // Which meal window the hour is in (index into the settings' list), or -1
    public static int WindowAt(float hour, FoodSettings settings)
    {
        hour = LightingProfile.Wrap(hour);
        for (int i = 0; i < settings.mealWindows.Count; i++)
        {
            MealWindow window = settings.mealWindows[i];
            if (hour >= window.startHour && hour < window.endHour) return i;
        }
        return -1;
    }

    public static long MealKey(int day, int window)
    {
        return (long)day * 16L + window;
    }

    // --- Hunger ---

    public static float DrainPerSecond(float appetite, FoodSettings settings)
    {
        if (settings.hungerHoursToEmpty <= 0f) return 0f;
        return HungerState.Full / (settings.hungerHoursToEmpty * 3600f) * Mathf.Max(0f, appetite);
    }

    public static void Drain(HungerState state, float seconds, float appetite, FoodSettings settings)
    {
        state.Satiety = Mathf.Clamp(state.Satiety - DrainPerSecond(appetite, settings) * seconds, 0f, HungerState.Full);
    }

    // 0 when not hungry, up to 1 when completely empty
    public static float HungryFactor(float satiety, FoodSettings settings)
    {
        if (settings.hungryBelow <= 0f || satiety >= settings.hungryBelow) return 0f;
        return Mathf.Clamp((settings.hungryBelow - satiety) / settings.hungryBelow, 0f, 1f);
    }

    public static bool IsHungry(float satiety, FoodSettings settings)
    {
        return satiety < settings.hungryBelow;
    }

    // How much of its normal speed a character works at: 1 until hungry, then down to the floor (never below it)
    public static float ProductivityFactor(float satiety, FoodSettings settings)
    {
        return Mathf.Lerp(1f, Mathf.Clamp(settings.productivityFloor, 0.3f, 1f), HungryFactor(satiety, settings));
    }

    // --- Eating ---

    public static bool WantsToEat(HungerState state, float hour, int day, FoodSettings settings)
    {
        if (state.Satiety < settings.emergencyBelow) return true;
        int window = WindowAt(hour, settings);
        if (window < 0) return false;
        return state.Satiety < settings.eatBelow && state.LastMealKey != MealKey(day, window);
    }

    // One go at a meal: takes food from the stock (the favourite first, then whatever else is edible) until the character has had
    // about a meal's worth or the stock has nothing more. Without any food it may find a little by itself, once per window.
    // Returns what happened (Any is false if nothing was eaten).
    public static MealResult TryEat(HungerState state, ItemType favourite, IItemStock stock, float hour, int day, FoodSettings settings)
    {
        MealResult meal = new MealResult();
        int window = WindowAt(hour, settings);
        long key = MealKey(day, window < 0 ? 15 : window); // an emergency meal outside the windows counts as its own slot of the day

        float need = Mathf.Min(settings.mealRestore, HungerState.Full - state.Satiety);
        if (need <= 0.01f) return meal;

        float favouriteGained = 0f;
        foreach (ItemType item in FoodOrder(favourite))
        {
            if (need <= 0.01f) break;
            float available = stock.Get(item.Name);
            if (available <= 0.0001f) continue;
            float units = Mathf.Min(available, need / item.nourishment);
            if (units <= 0f) continue;

            stock.Add(item.Name, -units);
            float gained = units * item.nourishment;
            meal.Eaten.Add(new KeyValuePair<ItemType, float>(item, units));
            meal.Gained += gained;
            need -= gained;
            if (item == favourite) favouriteGained += gained;
            if (meal.Main == null || gained > MainGain(meal)) meal.Main = item;
        }

        if (meal.Gained <= 0f && settings.foragingRestore > 0f && state.LastForageKey != key)
        {
            // No food in the town at all: it finds a little by itself, so being hungry never gets worse than a point
            meal.Gained = Mathf.Min(settings.foragingRestore, need);
            meal.Foraged = true;
            state.LastForageKey = key;
        }

        if (!meal.Any) return meal;

        state.Satiety = Mathf.Clamp(state.Satiety + meal.Gained, 0f, HungerState.Full);
        meal.Favourite = !meal.Foraged && meal.Gained > 0f && favouriteGained >= meal.Gained * 0.5f;
        state.PendingMood += meal.Foraged ? 0f : (meal.Favourite ? settings.favouriteMoodBonus : settings.otherMoodBonus);
        if (window >= 0 || !meal.Foraged) state.LastMealKey = key; // having eaten (or foraged) in this window, it does not try again
        return meal;
    }

    private static float MainGain(MealResult meal)
    {
        float best = 0f;
        foreach (KeyValuePair<ItemType, float> e in meal.Eaten) best = Mathf.Max(best, e.Value * e.Key.nourishment);
        return best;
    }

    // The edible items, favourite first and then by name (so who eats what is the same every time)
    private static IEnumerable<ItemType> FoodOrder(ItemType favourite)
    {
        if (favourite != null && favourite.edible && favourite.nourishment > 0f) yield return favourite;

        List<ItemType> others = new List<ItemType>();
        foreach (ItemType item in ItemCatalog.All)
        {
            if (item == favourite || !item.edible || item.nourishment <= 0f) continue;
            others.Add(item);
        }
        others.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        foreach (ItemType item in others) yield return item;
    }
}
