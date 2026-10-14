using UnityEngine;

// Template for a kind of road (dirt, paved...). Static data only.
[CreateAssetMenu(fileName = "NewRoadType", menuName = "Scriptable Objects/RoadType")]
public class RoadType : ScriptableObject
{
    public string displayName;
    public int typeId;                 // unique per RoadType asset; save files refer to the type by this
    public Sprite sprite;              // drawn on each road cell (scaled to fill the cell)

    [Header("Walking")]
    public float speedMultiplier = 2f; // characters walk this many times faster on it; also what routes are weighed by

    [Header("Cost (per cell)")]
    public int goldCost = 1;
    public int woodCost = 0;
}
