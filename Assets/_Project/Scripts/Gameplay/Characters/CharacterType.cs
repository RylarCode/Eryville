using System.Collections.Generic;
using UnityEngine;

// Template for a kind of character (a "John", a "Darkie"...). Static data only;
// the live character is created from it.
[CreateAssetMenu(fileName = "NewCharacter", menuName = "Scriptable Objects/Character")]
public class CharacterType : ScriptableObject
{
    public string displayName;
    public int typeId;               // unique per CharacterType asset
    public Sprite sprite;
    public int initialWage = 10;
    public GameObject characterPrefab;
    public enum Role { Resource, Defense, Offense, Others }
    public Role role = Role.Resource;

    [Header("Ownership")]
    public int maxOwned = 10;          // how many of this character the player can own: generic types (villager, guard) allow many,
                                       // a named story character allows 1. 0 or less means no limit.

    [Header("Stats")]
    public float moveSpeed = 3f;       // world units per second
    public float productivity = 1.2f;  // multiplies a building's output while this character works there

    public List<ScheduleEntry> defaultSchedule;

    // Where this character may go for the Rest1 / Rest2 schedule slots, per world state.
    // If nothing matches (no option, or no such building built yet) the character goes home.
    public List<RestOption> restOptions = new List<RestOption>();
}

// "In <worldState>, for <slot>, I may rest at a <place>." Add several rows to allow several places;
// the nearest built one is used.
[System.Serializable]
public class RestOption
{
    public ScheduleSlot slot = ScheduleSlot.Rest1;
    public WorldState worldState = WorldState.Peace;
    public BuildingType place;
}
