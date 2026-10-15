using System.Collections.Generic;
using UnityEngine;

// The shape of a save file (written with JsonUtility, so: public fields, plain types, lists).
// Things are referred to by id (buildings / characters) or typeId (the ScriptableObject they were made
// from), never by object or prefab name, so a save can be rebuilt without guessing.

[System.Serializable]
public class ResourceData
{
    public string name;
    public float amount;
}

[System.Serializable]
public class BuildingData
{
    public string id;        // Building.Id
    public int typeId;       // BuildingType.typeId
    public int cellX;        // anchor cell
    public int cellY;
    public Vector3 position; // world position it was built at
    // Add other building-specific saved data here (level, health...)
}

[System.Serializable]
public class RoadData
{
    public int cellX;
    public int cellY;
    public int typeId;       // RoadType.typeId
}

[System.Serializable]
public class CharacterData
{
    public string id;             // OwnedCharacter.Id
    public int typeId;            // CharacterType.typeId
    public string displayName;
    public int level;
    public float loyalty;
    public float mood;            // added in version 3
    public bool wagePaid = true;  // added in version 4: whether the latest payday covered this character
    public int awareness;         // added in version 4: self-awareness level (from duplicate pulls)
    public string workBuildingId; // Building.Id, or empty
    public string homeBuildingId; // Building.Id, or empty
    public bool hasAvatar;        // whether it was visible in the world
    public Vector3 position;      // avatar position (when hasAvatar)
    // Activity and destination are not saved: a loaded character re-reads its schedule and walks there.
}

[System.Serializable]
public class GameData
{
    public int version = SaveSystem.CurrentVersion;

    public List<ResourceData> resources = new List<ResourceData>();

    public int timeOfDay;        // TimeOfDay
    public float phaseElapsed;   // seconds into the current phase
    public int worldState;       // WorldState

    public List<BuildingData> buildings = new List<BuildingData>();
    public List<RoadData> roads = new List<RoadData>();               // added in version 2
    public List<CharacterData> characters = new List<CharacterData>();

    public int gachaTotalPulls;                               // added in version 4
    public List<int> gachaPity = new List<int>();             // pulls since the last result of each pity rule's rarity
}
