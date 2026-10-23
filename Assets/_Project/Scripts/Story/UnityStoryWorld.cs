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
