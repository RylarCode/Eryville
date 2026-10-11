using System;
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
    public float Loyalty { get; set; }

    // Changed only through Building.TryAssign*/Remove*, so the building lists stay in sync
    public Building WorkBuilding { get; private set; }
    public Building HomeBuilding { get; private set; }

    // The visible body, or null when this character is not currently in the world
    public Character Avatar { get; internal set; }
    public bool HasAvatar => Avatar != null;

    // Multiplies a building's output while this character works there (level / loyalty bonuses can go here later)
    public float Productivity => Type.productivity;

    public OwnedCharacter(CharacterType type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        Id = Guid.NewGuid().ToString("N");
        Type = type;
        DisplayName = type.displayName;
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
