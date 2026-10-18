using System;
using System.Collections.Generic;
using UnityEngine;

// An amount of an item: "2 wheat"
[Serializable]
public class ItemAmount
{
    public ItemType item;
    public float amount = 1f;

    public string Name => item != null ? item.Name : "";

    public ItemAmount() { }
    public ItemAmount(ItemType item, float amount) { this.item = item; this.amount = amount; }
}

// What a building does: every cycle it uses up its inputs and makes its outputs.
//   Farm:   (nothing)         -> 6 wheat                  every 60 s
//   Mill:   2 wheat           -> 1 flour                  every 30 s
//   Bakery: 1 flour           -> 3 bread                  every 45 s
// A building with a recipe works while it is staffed (a building without worker slots just works) and there are enough inputs
// and room for the outputs; its workers' productivity makes the cycles come faster. Everything is data: a new production path
// (clay -> bricks, logs -> planks) is a new recipe asset on a building, no code.
[CreateAssetMenu(fileName = "NewRecipe", menuName = "Scriptable Objects/Recipe")]
public class Recipe : ScriptableObject
{
    public string recipeName;
    public List<ItemAmount> inputs = new List<ItemAmount>();
    public List<ItemAmount> outputs = new List<ItemAmount>();
    [Tooltip("Seconds one cycle takes with one ordinary worker (or none needed).")]
    public float cycleSeconds = 30f;
    [Tooltip("Labels such as \"bakery\" or \"fishing\": characters with a matching specialty work faster here.")]
    public List<string> tags = new List<string>();

    public string DisplayName => string.IsNullOrEmpty(recipeName) ? name : recipeName;
    public float CycleSeconds => cycleSeconds > 0f ? cycleSeconds : 1f;

    // How many of an item go in or out per second at full speed (speed 1 = no productivity bonus)
    public float PerSecond(ItemAmount amount, float speed = 1f)
    {
        return amount.amount * speed / CycleSeconds;
    }

    // A recipe made in code (the Gold Mine and Lumberyard's old gold / wood fields become one)
    public static Recipe Create(string name, float cycleSeconds, IEnumerable<ItemAmount> inputs, IEnumerable<ItemAmount> outputs)
    {
        Recipe recipe = CreateInstance<Recipe>();
        recipe.name = name;
        recipe.recipeName = name;
        recipe.cycleSeconds = cycleSeconds;
        if (inputs != null) recipe.inputs = new List<ItemAmount>(inputs);
        if (outputs != null) recipe.outputs = new List<ItemAmount>(outputs);
        return recipe;
    }

    // One short line for the UI: "2 Wheat -> 1 Flour every 30 s"
    public string Describe()
    {
        return $"{List(inputs, "nothing")} -> {List(outputs, "nothing")} every {CycleSeconds:0.#} s";
    }

    private static string List(List<ItemAmount> amounts, string empty)
    {
        if (amounts == null || amounts.Count == 0) return empty;
        List<string> parts = new List<string>();
        foreach (ItemAmount a in amounts) parts.Add($"{a.amount:0.##} {a.Name}");
        return string.Join(" + ", parts);
    }
}
