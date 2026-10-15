using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// A character the player owns: the record that hiring (and later gacha pulls), the roster and saving
// work with. It exists whether or not the character currently has a visible body in the world.
// Character (the MonoBehaviour) is only the avatar of one of these.
//
// Not [Serializable] on purpose: it is shared by reference (buildings hold the same instance),
// and Unity would copy it by value.
public class OwnedCharacter
{
    public string Id { get; }
    public CharacterType Type { get; }
    public string DisplayName { get; set; }
    public int Level { get; set; } = 1;

    // Loyalty and mood both run from 0 to MaxStat. Changing either (or a speed modifier) updates MoveSpeed.
    public const float MaxStat = 100f;
    private float loyalty;
    private float mood;
    public float Loyalty { get { return loyalty; } set { SetStat(ref loyalty, value); } }
    public float Mood { get { return mood; } set { SetStat(ref mood, value); } }

    // Raised when loyalty, mood or a speed modifier changes
    public event Action StatsChanged;

    // How fast this character walks on open ground, in world units per second. Worked out as
    //   type's base speed x (loyalty effect) x (mood effect) x every named modifier
    // and only recalculated when one of those changes. The terrain underfoot (roads) is applied on top, while walking.
    public float MoveSpeed { get; private set; }
    private readonly Dictionary<string, float> speedModifiers = new Dictionary<string, float>();

    // Changed only through Building.TryAssign*/Remove*, so the building lists stay in sync
    public Building WorkBuilding { get; private set; }
    public Building HomeBuilding { get; private set; }

    // The visible body, or null when this character is not currently in the world
    public Character Avatar { get; internal set; }
    public bool HasAvatar => Avatar != null;

    // What this character costs per day, and whether the latest payday covered it. An unpaid character
    // refuses to work (stays home) until the next payday pays them.
    public int DailyWage => Type.dailyWage;
    private bool wagePaid = true;
    public bool WagePaid
    {
        get { return wagePaid; }
        set
        {
            if (wagePaid == value) return;
            wagePaid = value;
            if (HasAvatar) Avatar.OnWageStatusChanged();
        }
    }

    // Multiplies a building's output while this character works there (level / loyalty bonuses can go here later)
    public float Productivity => Type.productivity;

    public OwnedCharacter(CharacterType type) : this(type, null) { }

    // Pass an id only when restoring a saved character, so it keeps the id other save data refers to
    public OwnedCharacter(CharacterType type, string id)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
        Type = type;
        DisplayName = type.displayName;
        mood = Clamp(type.mood != null ? type.mood.baseline : MaxStat * 0.7f);
        RecalculateSpeed();
    }

    private static float Clamp(float value) { return value < 0f ? 0f : (value > MaxStat ? MaxStat : value); }

    private void SetStat(ref float field, float value)
    {
        value = Clamp(value);
        if (value == field) return;
        field = value;
        RecalculateSpeed();
        StatsChanged?.Invoke();
    }

    // --- Speed ---

    public float LoyaltyFactor => Type.loyaltySpeed != null ? Type.loyaltySpeed.Evaluate(loyalty / MaxStat) : 1f;
    public float MoodFactor => Type.moodSpeed != null ? Type.moodSpeed.Evaluate(mood / MaxStat) : 1f;

    // Adds (or changes) a named speed multiplier, e.g. "story:inLove" 1.1. Anything can add one: the key says who did.
    public void SetSpeedModifier(string source, float multiplier)
    {
        if (multiplier < 0f) multiplier = 0f;
        if (speedModifiers.TryGetValue(source, out float existing) && existing == multiplier) return;
        speedModifiers[source] = multiplier;
        RecalculateSpeed();
        StatsChanged?.Invoke();
    }

    public void RemoveSpeedModifier(string source)
    {
        if (!speedModifiers.Remove(source)) return;
        RecalculateSpeed();
        StatsChanged?.Invoke();
    }

    private void RecalculateSpeed()
    {
        float speed = Type.moveSpeed * LoyaltyFactor * MoodFactor;
        foreach (float multiplier in speedModifiers.Values) speed *= multiplier;
        MoveSpeed = speed;
    }

    // Shows where the speed comes from, e.g. "3.3 (base 3 x loyalty 1.1 x mood 1)"
    public string DescribeSpeed()
    {
        StringBuilder text = new StringBuilder();
        text.Append($"{MoveSpeed:0.##} (base {Type.moveSpeed:0.##} x loyalty {LoyaltyFactor:0.##} x mood {MoodFactor:0.##}");
        foreach (KeyValuePair<string, float> modifier in speedModifiers) text.Append($" x {modifier.Key} {modifier.Value:0.##}");
        text.Append(")");
        return text.ToString();
    }

    // Called by Building (TryAssignWorker / RemoveWorker); pass null to clear.
    internal void SetWorkBuilding(Building building)
    {
        WorkBuilding = building;
        Debug.Log(building != null
            ? $"{DisplayName} assigned work at {building.DisplayName}"
            : $"{DisplayName} unassigned from work");
        if (HasAvatar) Avatar.OnWorkBuildingChanged(building);
    }

    // Called by Building (TryAssignResident / RemoveResident); pass null to clear.
    internal void SetHomeBuilding(Building building)
    {
        HomeBuilding = building;
        Debug.Log(building != null
            ? $"{DisplayName} now lives at {building.DisplayName}"
            : $"{DisplayName} is now homeless");
        if (HasAvatar) Avatar.OnHomeBuildingChanged(building);
    }
}
