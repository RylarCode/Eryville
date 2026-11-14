using UnityEngine;

// What the story can see of the real town, and how it gives rewards
public class UnityStoryWorld : IStoryWorld
{
    public int BuildingCount(BuildingType type)
    {
        if (BuildingManager.Instance == null) return 0;
        int count = 0;
        foreach (Building building in BuildingManager.Instance.Buildings)
        {
            if (type == null || building.buildingType == type) count++;
        }
        return count;
    }

    public int CharacterCount(CharacterType type)
    {
        if (CharacterManager.Instance == null) return 0;
        return type == null ? CharacterManager.Instance.Roster.Count : CharacterManager.Instance.OwnedCount(type);
    }

    public float ItemAmount(string item)
    {
        return ResourceManager.Instance != null ? ResourceManager.Instance.Get(item) : 0f;
    }

    public QuestStatus QuestStatus(string questId)
    {
        return QuestManager.Instance != null ? QuestManager.Instance.StatusOf(questId) : global::QuestStatus.Locked;
    }

    public void StartQuest(string questId)
    {
        if (QuestManager.Instance != null) QuestManager.Instance.StartById(questId);
    }

    // Story characters are unique (Max Owned 1), so "the" owned character of a type is the first one
    public static OwnedCharacter OwnedOf(CharacterType type)
    {
        if (type == null || CharacterManager.Instance == null) return null;
        foreach (OwnedCharacter owned in CharacterManager.Instance.Roster) if (owned.Type == type) return owned;
        return null;
    }

    public int Loyalty(CharacterType type)
    {
        OwnedCharacter owned = OwnedOf(type);
        return owned != null ? Mathf.FloorToInt(owned.Loyalty) : -1;
    }

    public void AddLoyalty(CharacterType type, int amount)
    {
        OwnedCharacter owned = OwnedOf(type);
        if (owned != null) owned.Loyalty += amount;
    }

    public bool RegionCleared(string regionId)
    {
        MapRegions regions = MapRegions.Existing;
        if (regions == null || regions.Map == null) return false;
        Region region = regions.Map.Find(regionId);
        return region != null && regions.Map.IsCleared(region);
    }

    // What the player has cleared: every cleared region except the ones that start cleared
    public int RegionsCleared()
    {
        MapRegions regions = MapRegions.Existing;
        if (regions == null || regions.Map == null) return 0;
        int count = 0;
        foreach (Region region in regions.Map.Regions) if (!region.startsCleared && regions.Map.IsCleared(region)) count++;
        return count;
    }

    public int CellsCleared()
    {
        MapRegions regions = MapRegions.Existing;
        if (regions == null || regions.Map == null) return 0;
        int cells = 0;
        foreach (Region region in regions.Map.Regions) if (!region.startsCleared && regions.Map.IsCleared(region)) cells += regions.Map.CellsOf(region).Count;
        return cells;
    }

    // Clears the region at once: free, no yields, any clearing job for it is dropped (ClearingManager sees the change)
    public void ClearRegion(string regionId)
    {
        MapRegions regions = MapRegions.Existing;
        if (regions == null || regions.Map == null) return;
        Region region = regions.Map.Find(regionId);
        if (region == null) { Debug.LogWarning($"The story clears the region \"{regionId}\", but there is no such region."); return; }
        if (region.Clearable) regions.Map.Clear(region);
    }

    public void Give(string item, float amount)
    {
        if (ResourceManager.Instance == null) return;
        if (item == ResourceManager.Letters && Backend.Current.ServerOwnsBalances)
        {
            Debug.LogWarning("A story reward of recruitment letters was skipped: the server owns the letters, so only it can grant them.");
            return;
        }
        ResourceManager.Instance.Add(item, amount);
        if (UIManager.Instance != null) UIManager.Instance.UpdateResourceUI(ResourceManager.Instance.resources);
    }
}
