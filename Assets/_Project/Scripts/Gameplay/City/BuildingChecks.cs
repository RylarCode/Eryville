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
                if ((type.goldGenerationPerSec > 0 || type.woodGenerationPerSec > 0) && type.buildingPrefab.GetComponent<ResourceProducer>() == null)
                    problems.Add($"{who} has resource output but its prefab has no ResourceProducer, so it will produce nothing.");
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

            if (inGame != null && !inGame.Contains(type)) problems.Add($"{who} is not on the BuildingPlacer's list (GameScene), so it cannot be built or loaded from a save.");
        }
        return problems;
    }
}
