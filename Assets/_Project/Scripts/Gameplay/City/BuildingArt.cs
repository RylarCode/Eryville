using UnityEngine;

// How a building's picture relates to the cells it stands on
public enum BuildingArtStyle
{
    FitFootprint,   // top-down: the picture is stretched to cover exactly the building's cells (how buildings always were)
    ThreeQuarter,   // 3/4 view: the picture keeps its shape, stands on the building's cells and rises above them
}

// When a building counts as "open" (for its open / closed animations)
public enum BuildingOpenRule
{
    Auto,           // workplaces: someone is working there. Homes: someone is inside. Anything else: it is not night.
    Always,
    WorkersOnSite,  // at least one worker is at the building
    PeopleInside,   // at least one worker or resident is inside
    Daytime,        // morning, afternoon and evening
}

// Where a building's picture goes: its size on screen and where its centre is relative to the centre of its cells.
// Pure maths (no Unity objects), used when a building is created and for the ghost shown while placing.
public struct BuildingLayout
{
    public Vector2 Scale;           // scale for a transform showing the sprite, so it comes out at ArtSize
    public Vector2 ArtSize;         // the picture's size in cells
    public Vector2 ArtOffset;       // the centre of the picture, relative to the centre of the building's cells (world units)
    public bool Raised => ArtOffset.sqrMagnitude > 0f;

    public static BuildingLayout Compute(BuildingArtStyle style, Vector2Int footprint, Vector2 spriteSize, float artWidth, Vector2 artOffset)
    {
        BuildingLayout layout = new BuildingLayout();
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            layout.Scale = Vector2.one;
            layout.ArtSize = new Vector2(footprint.x, footprint.y);
            return layout;
        }

        if (style == BuildingArtStyle.ThreeQuarter)
        {
            // Keep the picture's proportions; its bottom edge is the front edge of the building's cells, so it rises over the cells behind
            float width = artWidth > 0f ? artWidth : footprint.x;
            float height = width * spriteSize.y / spriteSize.x;
            layout.ArtSize = new Vector2(width, height);
            layout.ArtOffset = new Vector2(artOffset.x, (height - footprint.y) * 0.5f + artOffset.y);
        }
        else
        {
            layout.ArtSize = new Vector2(footprint.x, footprint.y);
        }
        layout.Scale = new Vector2(layout.ArtSize.x / spriteSize.x, layout.ArtSize.y / spriteSize.y);
        return layout;
    }

    public static BuildingLayout For(BuildingType type)
    {
        Vector2 spriteSize = type.sprite != null ? (Vector2)type.sprite.bounds.size : Vector2.zero;
        return Compute(type.artStyle, type.size, spriteSize, type.artWidth, type.artOffset);
    }
}

// Drawing order. Things further down the screen are drawn in front of things further up, so a character walking behind a
// building is hidden by it and one walking in front is not. A building counts as being at the front edge of its cells.
public static class DepthSorting
{
    public const int Ghost = 30000; // placement ghosts and cursors: above everything

    // Characters add their own base (at least 1) on top, so that at the same height they are in front of a building
    public static int Order(float y)
    {
        return Mathf.RoundToInt((100f - y) * 100f);
    }
}

// Decides whether a building is open
public static class BuildingOpenState
{
    public static bool IsOpen(BuildingOpenRule rule, int workerSlots, int workersOnSite, int housingSlots, int restingInside, bool night)
    {
        switch (rule)
        {
            case BuildingOpenRule.Always: return true;
            case BuildingOpenRule.WorkersOnSite: return workersOnSite > 0;
            case BuildingOpenRule.PeopleInside: return workersOnSite + restingInside > 0;
            case BuildingOpenRule.Daytime: return !night;
            default:
                if (workerSlots > 0) return workersOnSite > 0;
                if (housingSlots > 0) return restingInside > 0;
                return !night;
        }
    }
}
