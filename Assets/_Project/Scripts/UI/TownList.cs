using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TownTab { Characters, Buildings, Stock }
public enum CharacterSort { Name, Mood, Loyalty, Type }
public enum CharacterFilter { All, Homeless, Unemployed, Unhappy }
public enum BuildingSort { Name, Type, Output }
public enum BuildingFilter { All, FreeWorkerSlots, FreeHousing, Producing }
public enum ItemSort { Name, Amount, Net }
public enum ItemFilter { All, Food, Materials }

// One line of the town list: what it says and what it stands for
public sealed class TownRow
{
    public string Key;               // stable identity, so the list is only rebuilt when something really changed
    public string Label;
    public Sprite Sprite;
    public OwnedCharacter Character; // set for a character row
    public Building Building;        // set for a building row (also for a "held back" note in the stock list)
    public bool Note;                // a line of information, not an item
}

// Works out what the Town menu lists: every owned character or placed building, filtered and sorted.
// No UI in here, so it can be tested on its own.
public static class TownList
{
    public const float UnhappyBelow = 30f; // the "Unhappy" filter shows characters below this mood

    // --- Characters ---

    public static List<TownRow> CharacterRows(IEnumerable<OwnedCharacter> roster, CharacterSort sort, CharacterFilter filter)
    {
        IEnumerable<OwnedCharacter> matching = roster.Where(c => Matches(c, filter));
        IOrderedEnumerable<OwnedCharacter> ordered;
        switch (sort)
        {
            case CharacterSort.Mood: ordered = matching.OrderBy(c => c.Mood); break;           // lowest first: who needs help
            case CharacterSort.Loyalty: ordered = matching.OrderBy(c => c.Loyalty); break;
            case CharacterSort.Type: ordered = matching.OrderBy(c => c.Type.displayName, StringComparer.OrdinalIgnoreCase); break;
            default: ordered = matching.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase); break;
        }
        return ordered.ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Id, StringComparer.Ordinal)
            .Select(CharacterRow).ToList();
    }

    public static bool Matches(OwnedCharacter c, CharacterFilter filter)
    {
        switch (filter)
        {
            case CharacterFilter.Homeless: return c.HomeBuilding == null;
            case CharacterFilter.Unemployed: return c.WorkBuilding == null;
            case CharacterFilter.Unhappy: return c.Mood < UnhappyBelow;
            default: return true;
        }
    }

    public static string StatusOf(OwnedCharacter c)
    {
        if (!c.HasAvatar) return "Away";
        switch (c.Avatar.CurrentActivity)
        {
            case Character.Activity.Working: return "Working";
            case Character.Activity.Resting: return "Resting";
            case Character.Activity.Traveling: return "Walking";
            default: return "Idle";
        }
    }

    private static TownRow CharacterRow(OwnedCharacter c)
    {
        string label = $"{c.DisplayName} - {StatusOf(c)} - Mood {c.Mood:0}";
        if (c.HomeBuilding == null) label += " - no home";
        if (c.WorkBuilding == null) label += " - no job";
        if (!c.WagePaid) label += " - unpaid";
        return new TownRow { Key = "c:" + c.Id, Label = label, Sprite = c.Type.FaceOrSprite, Character = c };
    }

    // --- Buildings ---

    public static List<TownRow> BuildingRows(IEnumerable<Building> buildings, BuildingSort sort, BuildingFilter filter)
    {
        IEnumerable<Building> matching = buildings.Where(b => Matches(b, filter));
        IOrderedEnumerable<Building> ordered;
        switch (sort)
        {
            case BuildingSort.Type: ordered = matching.OrderBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase); break;
            case BuildingSort.Output: ordered = matching.OrderByDescending(b => OutputOf(b)); break;   // best producers first
            default: ordered = matching.OrderBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase); break;
        }
        return ordered.ThenBy(b => b.Cell.x).ThenBy(b => b.Cell.y).ThenBy(b => b.Id, StringComparer.Ordinal)
            .Select(BuildingRow).ToList();
    }

    public static bool Matches(Building b, BuildingFilter filter)
    {
        switch (filter)
        {
            case BuildingFilter.FreeWorkerSlots: return b.HasWorkerSlot;
            case BuildingFilter.FreeHousing: return b.HasHousingSlot;
            case BuildingFilter.Producing: return OutputOf(b) > 0f;
            default: return true;
        }
    }

    // Gold plus wood per second at the moment (0 for buildings that produce nothing)
    public static float OutputOf(Building b)
    {
        return b.Producer != null && b.Producer.Status == LineStatus.Working ? b.Producer.TotalOutputPerSec : 0f;
    }

    private static TownRow BuildingRow(Building b)
    {
        string label = $"{b.DisplayName} ({b.Cell.x},{b.Cell.y})";
        if (b.WorkerSlots > 0) label += $" - Workers {b.Workers.Count}/{b.WorkerSlots}";
        if (b.HousingSlots > 0) label += $" - Residents {b.Residents.Count}/{b.HousingSlots}";
        if (b.Producer != null)
        {
            if (!b.Producer.IsRunning) label += " - Not producing";
            if (b.Producer.Status == LineStatus.WaitingForInput || b.Producer.Status == LineStatus.StorageFull) label += " - " + b.Producer.StatusText;
            Recipe recipe = b.Producer.ActiveRecipe;
            if (recipe != null)
            {
                foreach (ItemAmount output in recipe.outputs)
                {
                    float rate = output.item != null ? b.Producer.OutputPerSec(output.Name) : 0f;
                    if (rate > 0f) label += $" - {output.Name} +{ResourceProducer.RateText(rate)}";
                }
            }
        }
        return new TownRow { Key = "b:" + b.Id, Label = label, Sprite = b.buildingType != null ? b.buildingType.sprite : null, Building = b };
    }

    // --- Stock ---

    // The items the town has or makes or uses, sorted and filtered, then a note for every building that is held back
    // (waiting for an input, or with nowhere to put what it makes)
    public static List<TownRow> StockRows(ResourceManager stock, IEnumerable<Building> buildings, ItemSort sort, ItemFilter filter)
    {
        List<ItemType> items = new List<ItemType>();
        foreach (ItemType item in ItemCatalog.All)
        {
            if (item.category == ItemCategory.Currency && item.Name == ResourceManager.Letters) continue; // letters have their own menu
            bool has = stock.Get(item.Name) >= 0.05f || stock.MadePerSecond(item.Name) > 0f || stock.UsedPerSecond(item.Name) > 0f;
            if (!has || !Matches(item, filter)) continue;
            items.Add(item);
        }

        IEnumerable<ItemType> ordered;
        switch (sort)
        {
            case ItemSort.Amount: ordered = items.OrderByDescending(i => stock.Get(i.Name)); break;
            case ItemSort.Net: ordered = items.OrderBy(i => stock.MadePerSecond(i.Name) - stock.UsedPerSecond(i.Name)); break; // running short first
            default: ordered = items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase); break;
        }

        List<TownRow> rows = ordered.ThenByName().Select(i => StockRow(stock, i)).ToList();

        foreach (Building b in buildings.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            if (b.Producer == null) continue;
            LineStatus status = b.Producer.Status;
            if (status != LineStatus.WaitingForInput && status != LineStatus.StorageFull) continue;
            string reason = status == LineStatus.WaitingForInput ? $"waiting for {b.Producer.BlockedBy}" : $"no room for {b.Producer.BlockedBy}";
            rows.Add(new TownRow { Key = "n:" + b.Id, Label = $"{b.DisplayName} ({b.Cell.x},{b.Cell.y}): {reason} ({b.Producer.Efficiency * 100f:0}% speed)", Sprite = b.buildingType != null ? b.buildingType.sprite : null, Building = b });
        }
        return rows;
    }

    private static IEnumerable<ItemType> ThenByName(this IEnumerable<ItemType> ordered)
    {
        return ordered is IOrderedEnumerable<ItemType> o ? o.ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase) : ordered;
    }

    public static bool Matches(ItemType item, ItemFilter filter)
    {
        switch (filter)
        {
            case ItemFilter.Food: return item.edible || item.category == ItemCategory.Meal;
            case ItemFilter.Materials: return item.category == ItemCategory.Material || item.category == ItemCategory.Ingredient;
            default: return true;
        }
    }

    private static TownRow StockRow(ResourceManager stock, ItemType item)
    {
        float amount = stock.Get(item.Name);
        float capacity = stock.Capacity(item.Name);
        float net = (stock.MadePerSecond(item.Name) - stock.UsedPerSecond(item.Name)) * 60f;
        string label = capacity > 0f ? $"{item.Name}: {Mathf.FloorToInt(amount)} / {Mathf.FloorToInt(capacity)}" : $"{item.Name}: {Mathf.FloorToInt(amount)}";
        if (Mathf.Abs(net) >= 0.05f) label += $"  ({net:+0.#;-0.#}/min)";
        if (capacity > 0f && amount >= capacity - 0.05f) label += "  FULL";
        else if (amount < 0.5f && stock.UsedPerSecond(item.Name) > 0f) label += "  EMPTY";
        return new TownRow { Key = "i:" + item.Name, Label = label, Sprite = item.Icon, Note = true };
    }

    // --- Names for the buttons ---

    public static string Describe(CharacterSort sort) { return sort.ToString(); }

    public static string Describe(CharacterFilter filter)
    {
        switch (filter)
        {
            case CharacterFilter.Homeless: return "No home";
            case CharacterFilter.Unemployed: return "No job";
            case CharacterFilter.Unhappy: return "Unhappy";
            default: return "All";
        }
    }

    public static string Describe(ItemSort sort) { return sort == ItemSort.Net ? "Net rate" : sort.ToString(); }

    public static string Describe(ItemFilter filter) { return filter == ItemFilter.Food ? "Food" : filter.ToString(); }

    public static string Describe(BuildingSort sort) { return sort.ToString(); }

    public static string Describe(BuildingFilter filter)
    {
        switch (filter)
        {
            case BuildingFilter.FreeWorkerSlots: return "Free work slots";
            case BuildingFilter.FreeHousing: return "Free housing";
            case BuildingFilter.Producing: return "Producing";
            default: return "All";
        }
    }
}
