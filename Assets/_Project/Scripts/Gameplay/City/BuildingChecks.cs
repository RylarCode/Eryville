using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Looks over the building types for the mistakes that are easy to make when adding one (see docs/NEW_BUILDING.md).
// Pure code: the editor menu "Eryville > Check Buildings" prints what it finds, and tests can call it too.
public static class BuildingChecks
{
    // allTypes: every BuildingType asset in the project. inGame: the ones on the BuildingPlacer's list (what can be built and saved).
    public static List<string> Problems(IReadOnlyList<BuildingType> allTypes, IReadOnlyList<BuildingType> inGame)
    {
        List<string> problems = new List<string>();
        Dictionary<int, BuildingType> byId = new Dictionary<int, BuildingType>();

        foreach (BuildingType type in allTypes)
        {
            if (type == null) continue;
            string who = string.IsNullOrEmpty(type.buildingName) ? $"\"{type.name}\"" : $"{type.buildingName} (\"{type.name}\")";

            if (string.IsNullOrEmpty(type.buildingName)) problems.Add($"{who} has no name.");

            if (byId.TryGetValue(type.typeId, out BuildingType other))
                problems.Add($"{who} has the same typeId ({type.typeId}) as \"{other.name}\": saved games would mix them up. Give each building its own number.");
            else
                byId[type.typeId] = type;

            if (type.size.x < 1 || type.size.y < 1) problems.Add($"{who} has a size below 1x1 cells.");

            if (type.buildingPrefab == null) problems.Add($"{who} has no building prefab, so it cannot be built.");
            else
            {
                if (type.buildingPrefab.GetComponent<Building>() == null) problems.Add($"{who}: its prefab has no Building component.");
                if (type.buildingPrefab.GetComponent<SpriteRenderer>() == null) problems.Add($"{who}: its prefab has no SpriteRenderer.");
                if (type.buildingPrefab.GetComponent<BoxCollider2D>() == null) problems.Add($"{who}: its prefab has no BoxCollider2D, so it cannot be clicked.");
                if (type.Produces && type.buildingPrefab.GetComponent<ResourceProducer>() == null)
                    problems.Add($"{who} makes things but its prefab has no ResourceProducer, so it will produce nothing.");
            }

            if (type.sprite == null) problems.Add($"{who} has no sprite.");
            if (type.artStyle == BuildingArtStyle.ThreeQuarter && type.sprite != null && type.sprite.bounds.size.y <= type.sprite.bounds.size.x * type.size.y / Mathf.Max(1, type.size.x))
                problems.Add($"{who} is set to 3/4 art but its picture is not taller than its cells, so it will not rise above them. (Fine for a flat picture: use FitFootprint.)");

            if (type.animations != null)
            {
                if (type.animations.Resolve(BuildingAnimKey.Closed) == null) problems.Add($"{who}: the animation set has no closed or open frames, so the building will not animate.");
                else if (type.animations.closed.IsEmpty) problems.Add($"{who}: no closed frames; the open ones are shown while it is closed.");
                else if (type.animations.open.IsEmpty) problems.Add($"{who}: no open frames; the closed ones are shown while it is open.");
            }

            // Recipes
            if (type.recipes != null)
            {
                foreach (Recipe recipe in type.recipes)
                {
                    if (recipe == null) { problems.Add($"{who} has an empty slot in its recipes list."); continue; }
                    if (recipe.cycleSeconds <= 0f) problems.Add($"{who}: recipe \"{recipe.DisplayName}\" has a cycle time of 0 or less (it is treated as 1 second).");
                    if (recipe.outputs == null || recipe.outputs.Count == 0) problems.Add($"{who}: recipe \"{recipe.DisplayName}\" makes nothing.");
                    foreach (ItemAmount amount in Amounts(recipe))
                    {
                        if (amount.item == null) problems.Add($"{who}: recipe \"{recipe.DisplayName}\" has an input or output with no item.");
                        else if (amount.amount <= 0f) problems.Add($"{who}: recipe \"{recipe.DisplayName}\" has {amount.Name} with an amount of 0 or less.");
                    }
                }
            }
            if (type.storageBonus != null)
            {
                foreach (ItemAmount bonus in type.storageBonus) if (bonus.item == null) problems.Add($"{who} has a storage bonus with no item.");
            }

            if (inGame != null && !inGame.Contains(type)) problems.Add($"{who} is not on the BuildingPlacer's list (GameScene), so it cannot be built or loaded from a save.");
        }

        // Items that are used up somewhere but made nowhere
        HashSet<string> made = new HashSet<string>();
        foreach (BuildingType type in allTypes)
        {
            if (type == null) continue;
            foreach (Recipe recipe in type.Recipes)
            {
                if (recipe == null || recipe.outputs == null) continue;
                foreach (ItemAmount output in recipe.outputs) if (output.item != null) made.Add(output.Name);
            }
        }
        HashSet<string> reported = new HashSet<string>();
        foreach (BuildingType type in allTypes)
        {
            if (type == null) continue;
            foreach (Recipe recipe in type.Recipes)
            {
                if (recipe == null || recipe.inputs == null) continue;
                foreach (ItemAmount input in recipe.inputs)
                {
                    if (input.item == null || made.Contains(input.Name) || !reported.Add(input.Name)) continue;
                    problems.Add($"{input.Name} is used by {type.buildingName} but no building makes it, so it can only come from cheats or the starting stock.");
                }
            }
        }
        return problems;
    }

    private static IEnumerable<ItemAmount> Amounts(Recipe recipe)
    {
        if (recipe.inputs != null) foreach (ItemAmount a in recipe.inputs) yield return a;
        if (recipe.outputs != null) foreach (ItemAmount a in recipe.outputs) yield return a;
    }
}
