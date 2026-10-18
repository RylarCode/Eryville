using System;
using System.Collections.Generic;
using UnityEngine;

// Fills a CharacterAnimationSet from sprite names, so an artist only has to name the frames of a sliced sprite sheet:
//
//     <anything>_<animation>_<direction>_<frame>      walk_down_0   walk_down_1   walk_left_0   idle_up_0   enter_right_3
//
// - animation: idle, walk, walkHappy (or walk_happy), walkSad (or walk_sad), enter, exit, eat
// - direction: down, up, left, right (also front / back / west / east / south / north)
// - frame: 0, 1, 2... the order they are shown in
// Upper or lower case does not matter, and a prefix such as the character's name is ignored (john_walk_down_0).
// Pure code, so it can be tested without Unity.
public static class AnimationNaming
{
    // Splits a sprite name into what it shows. False if the name does not follow the convention.
    public static bool TryParse(string name, out AnimKey key, out Facing facing, out int frame)
    {
        key = AnimKey.Idle;
        facing = Facing.Down;
        frame = 0;
        if (string.IsNullOrEmpty(name)) return false;

        string[] parts = name.Trim().ToLowerInvariant().Split('_', '-', ' ');
        int n = parts.Length;
        if (n < 3) return false;
        if (!int.TryParse(parts[n - 1], out frame) || frame < 0) return false;
        if (!TryParseFacing(parts[n - 2], out facing)) return false;

        // The animation is the one or two words before the direction ("walk", or "walk" + "happy")
        if (n >= 4 && TryParseKey(parts[n - 4] + parts[n - 3], out key)) return true;
        return TryParseKey(parts[n - 3], out key);
    }

    private static bool TryParseFacing(string word, out Facing facing)
    {
        facing = Facing.Down;
        switch (word)
        {
            case "down": case "front": case "south": facing = Facing.Down; return true;
            case "up": case "back": case "north": facing = Facing.Up; return true;
            case "left": case "west": facing = Facing.Left; return true;
            case "right": case "east": facing = Facing.Right; return true;
            default: return false;
        }
    }

    private static bool TryParseKey(string word, out AnimKey key)
    {
        key = AnimKey.Idle;
        switch (word)
        {
            case "idle": case "stand": key = AnimKey.Idle; return true;
            case "walk": key = AnimKey.Walk; return true;
            case "walkhappy": case "happywalk": key = AnimKey.WalkHappy; return true;
            case "walksad": case "sadwalk": key = AnimKey.WalkSad; return true;
            case "enter": case "enterbuilding": key = AnimKey.EnterBuilding; return true;
            case "exit": case "exitbuilding": key = AnimKey.ExitBuilding; return true;
            case "eat": case "eating": key = AnimKey.Eat; return true;
            default: return false;
        }
    }

    // Puts every sprite whose name follows the convention into the set (replacing the frames of the animations it finds,
    // leaving every other animation and every setting alone). Names that do not follow it are listed in "ignored".
    // Returns how many sprites were used.
    public static int Fill(CharacterAnimationSet set, IEnumerable<Sprite> sprites, List<string> ignored = null)
    {
        Dictionary<(AnimKey, Facing), SortedList<int, Sprite>> found = new Dictionary<(AnimKey, Facing), SortedList<int, Sprite>>();
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null) continue;
            if (!TryParse(sprite.name, out AnimKey key, out Facing facing, out int frame))
            {
                ignored?.Add(sprite.name);
                continue;
            }
            if (!found.TryGetValue((key, facing), out SortedList<int, Sprite> frames))
            {
                frames = new SortedList<int, Sprite>();
                found[(key, facing)] = frames;
            }
            frames[frame] = sprite; // a repeated name keeps the last one
        }

        int used = 0;
        foreach (KeyValuePair<(AnimKey, Facing), SortedList<int, Sprite>> entry in found)
        {
            SpriteAnimation animation = set.Get(entry.Key.Item1).Get(entry.Key.Item2);
            Sprite[] frames = new Sprite[entry.Value.Count];
            entry.Value.Values.CopyTo(frames, 0);
            animation.frames = frames;
            used += frames.Length;
        }
        return used;
    }

    // The picture to use as the character's still picture: the first frame of the front-facing idle (or walk) animation
    public static Sprite StillPicture(CharacterAnimationSet set)
    {
        SpriteAnimation animation = set.Resolve(AnimKey.Idle, Facing.Down) ?? set.Resolve(AnimKey.Walk, Facing.Down);
        return animation != null && !animation.IsEmpty ? animation.frames[0] : null;
    }
}
