using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewBuildingType", menuName = "Scriptable Objects/BuildingType")]
public class BuildingType : ScriptableObject
{
    public string buildingName;
    public int typeId;               // unique per BuildingType asset; save files refer to the type by this
    public Sprite sprite;
    public int goldCost;
    public int woodCost;

    [Header("Moving")]
    public bool movable = true;   // can the player pick this building up and put it somewhere else?
    public int moveGoldCost;      // free for now; raise to charge for moving
    public int moveWoodCost;
    public Vector2Int size = new Vector2Int(1, 1); // Size in grid cells (the ground it stands on)
    [FormerlySerializedAs("buildingcategoryprefab")]
    public GameObject buildingPrefab;
    public enum Role { Resource, Home, Special }
    [FormerlySerializedAs("buildingclass")]
    public Role role = Role.Resource;




    [Header("Art")]
    [Tooltip("FitFootprint: the picture is stretched over the building's cells (top-down). ThreeQuarter: the picture keeps its shape, stands on the cells and rises above them, hiding what is behind it.")]
    public BuildingArtStyle artStyle = BuildingArtStyle.FitFootprint;
    [Tooltip("ThreeQuarter only: how wide the picture is, in cells. 0 = as wide as the building's cells.")]
    public float artWidth = 0f;
    [Tooltip("ThreeQuarter only: nudge the picture (in cells) if it does not line up with the ground it stands on.")]
    public Vector2 artOffset = Vector2.zero;
    public BuildingAnimationSet animations;           // closed / open animations. Optional.
    public BuildingOpenRule openRule = BuildingOpenRule.Auto;

    public int goldGenerationPerSec;
    public int woodGenerationPerSec;
    public int workerSlots;   // how many characters can be assigned to work here
    public int housingSlots;  // how many characters can live here
}

