using System;
using System.Collections.Generic;
using UnityEngine;

// What a building can be seen doing
public enum BuildingAnimKey
{
    Closed,     // shut (looping, e.g. smoke-free chimney, dark windows)
    Open,       // open for business (looping, e.g. lit windows, smoke, a door that stands open)
    Opening,    // the change from closed to open (plays once)
    Closing,    // the change from open to closed (plays once)
}

// The animations of one kind of building. Every one is optional: Open falls back to Closed and the other way round,
// a missing Opening / Closing is simply skipped, and with nothing at all the building keeps its still picture.
// Quickest way to fill one in: name the frames of a sliced sprite sheet closed_0, closed_1, open_0, opening_0, closing_0...
// and use "Fill from sprite sheet" (see docs/NEW_BUILDING.md).
[CreateAssetMenu(fileName = "NewBuildingAnimations", menuName = "Scriptable Objects/Building Animations")]
public class BuildingAnimationSet : ScriptableObject
{
    public SpriteAnimation closed = new SpriteAnimation();
    public SpriteAnimation open = new SpriteAnimation();
    public SpriteAnimation opening = new SpriteAnimation { loop = false };
    public SpriteAnimation closing = new SpriteAnimation { loop = false };

    public SpriteAnimation Get(BuildingAnimKey key)
    {
        switch (key)
        {
            case BuildingAnimKey.Open: return open;
            case BuildingAnimKey.Opening: return opening;
            case BuildingAnimKey.Closing: return closing;
            default: return closed;
        }
    }

    // The animation to show, falling back to the other state's; null if there is none (transitions have no fallback)
    public SpriteAnimation Resolve(BuildingAnimKey key)
    {
        SpriteAnimation exact = Get(key);
        if (exact != null && !exact.IsEmpty) return exact;
        switch (key)
        {
            case BuildingAnimKey.Closed: return Resolve(BuildingAnimKey.Open, false);
            case BuildingAnimKey.Open: return Resolve(BuildingAnimKey.Closed, false);
            default: return null;
        }
    }

    private SpriteAnimation Resolve(BuildingAnimKey key, bool allowFallback)
    {
        SpriteAnimation exact = Get(key);
        return exact != null && !exact.IsEmpty ? exact : null;
    }

    // --- Naming ---

    // Splits a sprite name like "open_2" or "bakery_closing_0" into the animation and the frame number
    public static bool TryParse(string name, out BuildingAnimKey key, out int frame)
    {
        key = BuildingAnimKey.Closed;
        frame = 0;
        if (string.IsNullOrEmpty(name)) return false;

        string[] parts = name.Trim().ToLowerInvariant().Split('_', '-', ' ');
        int n = parts.Length;
        if (n < 2 || !int.TryParse(parts[n - 1], out frame) || frame < 0) return false;
        switch (parts[n - 2])
        {
            case "closed": case "close": case "shut": key = BuildingAnimKey.Closed; return true;
            case "open": key = BuildingAnimKey.Open; return true;
            case "opening": key = BuildingAnimKey.Opening; return true;
            case "closing": key = BuildingAnimKey.Closing; return true;
            default: return false;
        }
    }

    // Puts every sprite whose name follows the convention into the set (frames in order of their number). Returns how many were used.
    public int Fill(IEnumerable<Sprite> sprites, List<string> ignored = null)
    {
        Dictionary<BuildingAnimKey, SortedList<int, Sprite>> found = new Dictionary<BuildingAnimKey, SortedList<int, Sprite>>();
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null) continue;
            if (!TryParse(sprite.name, out BuildingAnimKey key, out int frame))
            {
                ignored?.Add(sprite.name);
                continue;
            }
            if (!found.TryGetValue(key, out SortedList<int, Sprite> frames))
            {
                frames = new SortedList<int, Sprite>();
                found[key] = frames;
            }
            frames[frame] = sprite;
        }

        int used = 0;
        foreach (KeyValuePair<BuildingAnimKey, SortedList<int, Sprite>> entry in found)
        {
            Sprite[] frames = new Sprite[entry.Value.Count];
            entry.Value.Values.CopyTo(frames, 0);
            Get(entry.Key).frames = frames;
            used += frames.Length;
        }
        return used;
    }

    // The picture to use as the building's still picture: the first closed (or open) frame
    public Sprite StillPicture()
    {
        SpriteAnimation animation = Resolve(BuildingAnimKey.Closed);
        return animation != null ? animation.frames[0] : null;
    }
}
