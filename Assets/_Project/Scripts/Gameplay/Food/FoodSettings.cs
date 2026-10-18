using System;
using System.Collections.Generic;
using UnityEngine;

// A stretch of the day when characters sit down to eat
[Serializable]
public class MealWindow
{
    public string name = "Meal";
    [Range(0f, 24f)] public float startHour = 12f;
    [Range(0f, 24f)] public float endHour = 14f;

    public MealWindow() { }
    public MealWindow(string name, float startHour, float endHour) { this.name = name; this.startHour = startHour; this.endHour = endHour; }
}

// Every number of the hunger and eating rules in one place, so the feel can be tuned without touching code.
// The asset lives at Resources/FoodSettings; if it is missing the defaults below are used.
//
// The rules in short: satiety runs from 100 (full) down to 0 over hungerHoursToEmpty hours. A character eats at a meal window when
// it is below eatBelow (and has not eaten in that window yet), or at any time when it falls below emergencyBelow. A meal restores
// about mealRestore. Below hungryBelow a character is "hungry": its mood sinks a little and it works a little slower, never below
// productivityFloor. Nobody ever dies, leaves, or refuses to work because of hunger.
[CreateAssetMenu(fileName = "FoodSettings", menuName = "Scriptable Objects/Food Settings")]
public class FoodSettings : ScriptableObject
{
    [Header("Hunger")]
    [Tooltip("A character of ordinary appetite goes from full to empty in this many real hours.")]
    public float hungerHoursToEmpty = 24f;

    [Header("When they eat")]
    public List<MealWindow> mealWindows = new List<MealWindow>
    {
        new MealWindow("Breakfast", 7f, 9f),
        new MealWindow("Lunch", 12f, 14f),
        new MealWindow("Dinner", 18f, 20f),
    };
    [Tooltip("In a meal window a character eats if its satiety is below this.")]
    public float eatBelow = 65f;
    [Tooltip("Below this a character eats at any time, window or not.")]
    public float emergencyBelow = 20f;
    [Tooltip("How much satiety one meal restores (it eats until it has had this much, or the stock runs out).")]
    public float mealRestore = 35f;
    [Tooltip("With no food in stock at all, a hungry character finds a little to eat by itself once per meal window: this much satiety. So hunger never stays at the very bottom.")]
    public float foragingRestore = 8f;

    [Header("Being hungry (gentle)")]
    [Tooltip("Below this satiety a character is hungry.")]
    public float hungryBelow = 30f;
    [Tooltip("A starving character works at this share of its normal speed at the very least.")]
    [Range(0.3f, 1f)] public float productivityFloor = 0.7f;
    [Tooltip("Mood lost per second by a character that is completely empty (less the fuller it is, nothing at hungryBelow).")]
    public float hungryMoodDrainPerSecond = 0.05f;

    [Header("New game")]
    [Tooltip("What the town starts a new game with besides gold, wood and letters, e.g. 40 bread.")]
    public List<ItemAmount> starterStock = new List<ItemAmount>();

    [Header("Eating")]
    [Tooltip("Mood gained from a meal that is mostly the character's favourite food.")]
    public float favouriteMoodBonus = 6f;
    [Tooltip("Mood gained from any other meal.")]
    public float otherMoodBonus = 2f;
    [Tooltip("How long the eating picture shows over the character, in seconds.")]
    public float eatSeconds = 3f;

    private static FoodSettings current;

    // The settings in use: the Resources/FoodSettings asset, or the defaults
    public static FoodSettings Current
    {
        get
        {
            if (current == null) current = Resources.Load<FoodSettings>("FoodSettings");
            if (current == null) current = CreateInstance<FoodSettings>();
            return current;
        }
        set { current = value; }
    }
}
