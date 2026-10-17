using System.Collections.Generic;
using UnityEngine;

// All the items of the game, found by name. Items are the assets in any Resources/Items folder. Gold, Wood and Letters always
// exist: if there is no asset for them a plain built-in one is used, so older projects keep working.
public static class ItemCatalog
{
    private static readonly Dictionary<string, ItemType> byName = new Dictionary<string, ItemType>();
    private static readonly List<ItemType> all = new List<ItemType>();
    private static bool loaded;

    public static IReadOnlyList<ItemType> All
    {
        get { EnsureLoaded(); return all; }
    }

    // The item with this name; null if there is none
    public static ItemType Get(string name)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(name) && byName.TryGetValue(name, out ItemType item) ? item : null;
    }

    // Adds (or replaces) an item. Used by tests and by anything that makes items at run time.
    public static void Register(ItemType item)
    {
        if (item == null) return;
        EnsureLoaded();
        string key = item.Name;
        if (byName.TryGetValue(key, out ItemType old)) all.Remove(old);
        byName[key] = item;
        all.Add(item);
    }

    // Forgets everything and loads again from the assets next time (tests)
    public static void Reset()
    {
        byName.Clear();
        all.Clear();
        loaded = false;
    }

    // How many of the item the town can hold without storage buildings (0 = no limit; unknown items are unlimited)
    public static float BaseCapacity(string name)
    {
        ItemType item = Get(name);
        return item != null ? Mathf.Max(0f, item.capacity) : 0f;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        ItemType[] assets = Resources.LoadAll<ItemType>("Items");
        if (assets != null)
        {
            foreach (ItemType item in assets)
            {
                if (item == null || byName.ContainsKey(item.Name)) continue;
                byName[item.Name] = item;
                all.Add(item);
            }
        }

        AddBuiltIn(ResourceManager.Gold, ItemCategory.Currency, new Color(0.95f, 0.8f, 0.2f, 1f));
        AddBuiltIn(ResourceManager.Wood, ItemCategory.Material, new Color(0.55f, 0.38f, 0.2f, 1f));
        AddBuiltIn(ResourceManager.Letters, ItemCategory.Currency, new Color(0.9f, 0.9f, 0.8f, 1f));
    }

    private static void AddBuiltIn(string name, ItemCategory category, Color color)
    {
        if (byName.ContainsKey(name)) return;
        ItemType item = ScriptableObject.CreateInstance<ItemType>();
        item.itemName = name;
        item.name = name;
        item.category = category;
        item.placeholderColor = color;
        byName[name] = item;
        all.Add(item);
    }
}
