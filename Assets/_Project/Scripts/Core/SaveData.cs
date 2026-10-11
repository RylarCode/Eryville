using System.Collections.Generic; // For Lists
using UnityEngine; // For Vector3 etc.

[System.Serializable]
public class BuildingData
{
    public string prefabName; // Or an ID to know which prefab to load
    public Vector3 position;
    public float currentHealth; // Example
    // Add other building-specific saved data (level, inventory?)
}

[System.Serializable]
public class CharacterData
{
    public string prefabName; // Or an ID
    public Vector3 position;
    public float currentHealth; // Example
    public Character.State currentState; // Save state enum
    // Add other character-specific saved data (target position?, inventory?)
}


[System.Serializable]
public class GameData
{
    // Game Manager Data
    public int gold;
    public int wood;
    // Add other global game state

    // Lists of placed objects
    public List<BuildingData> buildings = new List<BuildingData>();
    public List<CharacterData> characters = new List<CharacterData>();

    // Could add Tilemap data here too if needed
}