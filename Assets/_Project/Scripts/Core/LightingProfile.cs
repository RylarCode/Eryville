using System;
using System.Collections.Generic;
using UnityEngine;

// The four looks of the day
public enum DayStage { Dawn, Day, Dusk, Night }

// One moment of the day and how the world is lit then
[Serializable]
public class LightKey
{
    public float hour;      // 0..24 on the clock (6.5 = half past six in the morning)
    public Color color;     // the colour the whole world is multiplied by: white is full daylight, dark blue is night

    public LightKey() { }
    public LightKey(float hour, float r, float g, float b) { this.hour = hour; color = new Color(r, g, b, 1f); }
}

// How the world is lit through the day: a list of colours at hours, blended smoothly between them (and around midnight).
// Make one with Create > Scriptable Objects > Lighting Profile and give it to the DayNightCycle to change the look;
// without one the built-in default is used. Pure maths, so it can be tested exactly.
[CreateAssetMenu(fileName = "NewLightingProfile", menuName = "Scriptable Objects/Lighting Profile")]
public class LightingProfile : ScriptableObject
{
    public List<LightKey> keys = DefaultKeys();

    [Header("Names of the four looks (for labels; the colours come from the keys)")]
    public float dawnStart = 5f;
    public float dayStart = 8f;
    public float duskStart = 17.5f;
    public float nightStart = 20.5f;

    public static List<LightKey> DefaultKeys()
    {
        return new List<LightKey>
        {
            new LightKey(0f, 0.22f, 0.26f, 0.45f),   // deep night
            new LightKey(5f, 0.24f, 0.28f, 0.48f),   // still night
            new LightKey(6f, 0.95f, 0.62f, 0.55f),   // dawn: pink
            new LightKey(7f, 1.00f, 0.80f, 0.62f),   // dawn: warm gold
            new LightKey(9f, 1.00f, 1.00f, 1.00f),   // day
            new LightKey(16.5f, 1.00f, 1.00f, 0.96f),
            new LightKey(18f, 1.00f, 0.72f, 0.45f),  // dusk: orange
            new LightKey(19.5f, 0.70f, 0.45f, 0.58f),// dusk: purple
            new LightKey(21f, 0.35f, 0.35f, 0.58f),  // evening blue
            new LightKey(22f, 0.22f, 0.26f, 0.45f),  // night
        };
    }

    // The colour at an hour. Between two keys it blends in a straight line; after the last key it blends on to the first one (the next morning).
    public Color Evaluate(float hour)
    {
        if (keys == null || keys.Count == 0) return Color.white;
        if (keys.Count == 1) return keys[0].color;

        hour = Wrap(hour);
        List<LightKey> sorted = Sorted();
        LightKey first = sorted[0];
        LightKey last = sorted[sorted.Count - 1];

        // Before the first key or after the last: between the last (yesterday) and the first (today)
        if (hour < first.hour || hour >= last.hour)
        {
            float span = first.hour + 24f - last.hour;
            float since = hour >= last.hour ? hour - last.hour : hour + 24f - last.hour;
            return Blend(last.color, first.color, span <= 0f ? 0f : since / span);
        }

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            LightKey from = sorted[i];
            LightKey to = sorted[i + 1];
            if (hour >= from.hour && hour < to.hour)
            {
                float span = to.hour - from.hour;
                return Blend(from.color, to.color, span <= 0f ? 0f : (hour - from.hour) / span);
            }
        }
        return last.color;
    }

    // Which of the four looks it is at an hour
    public DayStage StageAt(float hour)
    {
        hour = Wrap(hour);
        if (hour >= nightStart || hour < dawnStart) return DayStage.Night;
        if (hour >= duskStart) return DayStage.Dusk;
        if (hour >= dayStart) return DayStage.Day;
        return DayStage.Dawn;
    }

    // How dark it is: 0 in full daylight, towards 1 at night (used to switch lamps and windows on)
    public static float Darkness(Color color)
    {
        float brightness = color.r * 0.3f + color.g * 0.59f + color.b * 0.11f;
        return Mathf.Clamp(1f - brightness, 0f, 1f);
    }

    public static float Wrap(float hour)
    {
        hour %= 24f;
        return hour < 0f ? hour + 24f : hour;
    }

    private static Color Blend(Color a, Color b, float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);
    }

    private List<LightKey> Sorted()
    {
        List<LightKey> sorted = new List<LightKey>(keys);
        sorted.Sort((x, y) => x.hour.CompareTo(y.hour));
        return sorted;
    }
}
