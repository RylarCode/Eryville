using System.Collections.Generic;
using UnityEngine;

// Template for a kind of character (a "John", a "Darkie"...). Static data only;
// the live character is created from it.
[CreateAssetMenu(fileName = "NewCharacter", menuName = "Scriptable Objects/Character")]
public class CharacterType : ScriptableObject
{
    public string displayName;
    public int typeId;               // unique per CharacterType asset

    [Header("Art")]
    public Sprite sprite;            // the small picture of the character in the world (its still picture; animations replace it while walking)
    public Sprite portrait;          // the full picture: shown when the character is recruited and when you look at it. Optional.
    public Sprite face;              // just the face, small: for lists and buttons. Optional.
    public CharacterAnimationSet animations; // walking, walking happy / sad, entering and leaving buildings. Optional.

    public int dailyWage = 10;       // gold this character costs every day (paid at the start of each morning)
    public GameObject characterPrefab;
    public enum Role { Resource, Defense, Offense, Others }
    public Role role = Role.Resource;

    [Header("Ownership")]
    public int maxOwned = 10;          // how many of this character the player can own: generic types (villager, guard) allow many,
                                       // a named story character allows 1. 0 or less means no limit.

    [Header("Recruitment")]
    public Rarity rarity = Rarity.Common;   // how rare this character is to pull
    public float pullWeight = 1f;           // chance relative to the other characters of the same rarity
    // What each duplicate pull gives this character. One entry per level: level 1 is the first duplicate, and so on.
    public List<AwarenessLevel> awarenessLevels = new List<AwarenessLevel>();

    [Header("Stats")]
    public float moveSpeed = 3f;       // world units per second
    public float productivity = 1.2f;  // multiplies a building's output while this character works there

    [Header("Speed effects (neutral until you change them)")]
    public StatEffect loyaltySpeed = new StatEffect(); // walking speed multiplier at loyalty 0 .. 100
    public StatEffect moodSpeed = new StatEffect();    // walking speed multiplier at mood 0 .. 100

    [Header("Specialty")]
    [Tooltip("What this character is especially good at: a baker at recipes tagged \"bakery\", a fisherman at \"fishing\". The multiplier applies while it works at a building whose recipe has the tag.")]
    public List<Specialty> specialties = new List<Specialty>();

    [Header("Food")]
    public ItemType favouriteFood;           // eaten first when there is some; a meal of it lifts the mood more. Optional.
    [Tooltip("How fast this character gets hungry compared with an ordinary one (1 = ordinary, 2 = twice as fast).")]
    public float appetite = 1f;

    [Header("Mood")]
    public MoodProfile mood = new MoodProfile();

    // What to show where a picture of the character is needed. Anything left empty falls back to the next best one.
    public Sprite PortraitOrSprite => portrait != null ? portrait : sprite;
    public Sprite FaceOrSprite => face != null ? face : PortraitOrSprite;

    public List<ScheduleEntry> defaultSchedule;

    // Where this character may go for the Rest1 / Rest2 schedule slots, per world state.
    // If nothing matches (no option, or no such building built yet) the character goes home.
    public List<RestOption> restOptions = new List<RestOption>();
}

// "Good at recipes with this tag": a multiplier on the working speed
[System.Serializable]
public class Specialty
{
    public string tag;
    public float multiplier = 1.5f;
}

// How strongly a stat (loyalty, mood) changes something: the multiplier is atZero when the stat is 0
// and atFull when it is 100, and in between it slides smoothly. Both 1 means "no effect".
[System.Serializable]
public class StatEffect
{
    public float atZero = 1f;
    public float atFull = 1f;

    // stat01 is the stat divided by its maximum (0..1)
    public float Evaluate(float stat01)
    {
        float t = stat01 < 0f ? 0f : (stat01 > 1f ? 1f : stat01);
        return atZero + (atFull - atZero) * t;
    }
}

// How a character type's mood behaves. Mood runs from 0 to 100; these numbers are how fast it moves,
// per second, in the situations the MoodSystem's rules know about. Types can differ.
[System.Serializable]
public class MoodProfile
{
    public float baseline = 70f;                     // the mood a newly hired character starts with
    public float homelessDrainPerSecond = 0.1f;      // lost while the character has no home
    public float restRecoveryPerSecond = 0.5f;       // gained while resting (at home or somewhere for leisure)
    public float travelDrainPerSecond = 0.05f;       // lost while walking (so living near work and shops helps)
    public List<MoodWorldEffect> worldStateEffects = new List<MoodWorldEffect>(); // extra change per second in a world state, e.g. War: -0.2

    public float WorldStateChange(WorldState state)
    {
        float total = 0f;
        foreach (MoodWorldEffect effect in worldStateEffects)
        {
            if (effect.state == state) total += effect.perSecond;
        }
        return total;
    }
}

[System.Serializable]
public class MoodWorldEffect
{
    public WorldState state = WorldState.War;
    public float perSecond = -0.2f; // negative drains mood, positive raises it
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
