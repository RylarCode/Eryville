using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TownTab { Characters, Buildings }
public enum CharacterSort { Name, Mood, Loyalty, Type }
public enum CharacterFilter { All, Homeless, Unemployed, Unhappy }
public enum BuildingSort { Name, Type, Output }
public enum BuildingFilter { All, FreeWorkerSlots, FreeHousing, Producing }

// One line of the town list: what it says and what it stands for
public sealed class TownRow
{
    public string Key;               // stable identity, so the list is only rebuilt when something really changed
    public string Label;
    public Sprite Sprite;
    public OwnedCharacter Character; // set for a character row
    public Building Building;        // set for a building row
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
        return b.Producer != null ? b.Producer.GoldPerSec + b.Producer.WoodPerSec : 0f;
    }

    private static TownRow BuildingRow(Building b)
    {
        string label = $"{b.DisplayName} ({b.Cell.x},{b.Cell.y})";
        if (b.WorkerSlots > 0) label += $" - Workers {b.Workers.Count}/{b.WorkerSlots}";
        if (b.HousingSlots > 0) label += $" - Residents {b.Residents.Count}/{b.HousingSlots}";
        if (b.Producer != null)
        {
            if (!b.Producer.IsRunning) label += " - Not producing";
            if (b.Producer.GoldPerSec > 0f) label += $" - Gold +{b.Producer.GoldPerSec:0.#}/s";
            if (b.Producer.WoodPerSec > 0f) label += $" - Wood +{b.Producer.WoodPerSec:0.#}/s";
        }
        return new TownRow { Key = "b:" + b.Id, Label = label, Sprite = b.buildingType != null ? b.buildingType.sprite : null, Building = b };
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
