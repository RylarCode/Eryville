using System;
using System.Collections.Generic;
using UnityEngine;

// What a region is made of. (Saved by number in assets: only ever add at the end.)
public enum RegionTerrain
{
    Forest,   // trees: cleared by felling them
    Rocks,    // boulders
    Debris,   // rubble and junk
    Mountain, // very costly: expected to be left for last, or built around
    River,    // can never be cleared (it blocks the way until there is a bridge)
    Quarry    // cleared land where a quarry can be built (later)
}

// One thing a cleared region gives: an item and how much (e.g. Wood 40, Stone 25)
[Serializable]
public class RegionYield
{
    public string item = ResourceManager.Wood;
    public int amount = 10;
}

// A hand-drawn part of the Map. The Map's layout is one small image (Resources/Regions/RegionMap) where each region is a flat colour and
// one pixel is one cell; a Region asset says which colour is which region and what it costs to clear. Make one with
// Assets > Create > Scriptable Objects > Region (or Eryville > New Region) in a Resources/Regions folder. See docs/MAP.md.
//
// Land starts locked (hidden under fog) except the regions that start cleared. A region can be cleared once it touches a cleared one
// along an edge. Clearing costs gold, takes characters some time, and gives its yields.
[CreateAssetMenu(fileName = "NewRegion", menuName = "Scriptable Objects/Region")]
public class Region : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Short unique name used by saves and stories (e.g. 'west-forest'). Empty: the asset's name. Do not change it once players have saves.")]
    public string id;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public RegionTerrain terrain = RegionTerrain.Forest;

    [Header("On the Map")]
    [Tooltip("The colour of this region in the region image. Every region needs its own, and the image must use it exactly (no blending or anti-aliasing at the edges).")]
    public Color color = Color.red;
    [Tooltip("Cleared from the start of a new game (the land around the Town Hall).")]
    public bool startsCleared;

    [Header("Clearing it")]
    public int goldCost = 100;
    [Tooltip("How many characters work on clearing it (the jobs list shows this many places).")]
    public int charactersNeeded = 2;
    [Tooltip("Seconds it takes with every place filled by an ordinary character (faster with good ones).")]
    public float clearSeconds = 120f;
    [Tooltip("What clearing it gives, e.g. Wood 40 or Stone 25.")]
    public List<RegionYield> yields = new List<RegionYield>();

    [Header("Pictures (optional)")]
    [Tooltip("Your own picture for a special region (a mountain, a ruin): shown while it is locked, instead of the random trees / rocks / debris. Drawn standing, its bottom centre at the bottom centre of the region.")]
    public Sprite picture;
    [Tooltip("Shown in its place once cleared (empty: nothing).")]
    public Sprite clearedPicture;
    [Tooltip("Moves the picture from the bottom centre of the region, in cells.")]
    public Vector2 pictureOffset;

    public string Id => !string.IsNullOrWhiteSpace(id) ? id.Trim() : name;
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : Id;

    // Rivers stay as they are; everything else can be cleared
    public bool Clearable => terrain != RegionTerrain.River;

    public Color32 Color32 => color;

    public string YieldText()
    {
        List<string> parts = new List<string>();
        foreach (RegionYield y in yields) if (y != null && y.amount > 0 && !string.IsNullOrWhiteSpace(y.item)) parts.Add($"{y.amount} {y.item.Trim()}");
        return string.Join(", ", parts);
    }
}
