using System.Collections.Generic;
using UnityEngine;

// Whether the Map regions the story and quests refer to exist (used by Check Story and Check Quests). Does nothing when the project has no
// Region assets (a game without a Map).
public static class RegionChecks
{
    // known: the ids of the regions that exist. Null: read them from the Region assets in Resources/Regions.
    public static string UnknownRegion(StoryCondition condition, ICollection<string> known = null)
    {
        if (condition == null || condition.kind != StoryCondition.Kind.RegionCleared) return null;
        return Unknown(condition.RegionId, known);
    }

    public static string UnknownRegion(StoryEffect effect, ICollection<string> known = null)
    {
        if (effect == null || effect.kind != StoryEffect.Kind.ClearRegion) return null;
        return Unknown(effect.RegionId, known);
    }

    private static string Unknown(string id, ICollection<string> known)
    {
        if (string.IsNullOrWhiteSpace(id)) return "a region condition or effect without a region id.";
        if (known == null)
        {
            known = new List<string>();
            foreach (Region region in Resources.LoadAll<Region>(MapRegions.AssetsFolder)) known.Add(region.Id);
        }
        if (known.Count == 0 || known.Contains(id.Trim())) return null;
        return $"there is no Map region with the id \"{id}\".";
    }
}
