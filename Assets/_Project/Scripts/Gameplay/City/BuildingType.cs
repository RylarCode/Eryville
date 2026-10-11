using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewBuildingType", menuName = "Scriptable Objects/BuildingType")]
public class BuildingType : ScriptableObject
{
    public string buildingName;
    public Sprite sprite;
    public int goldCost;
    public int woodCost;
    public Vector2Int size = new Vector2Int(1, 1); // Size in grid cells
    [FormerlySerializedAs("buildingcategoryprefab")]
    public GameObject buildingPrefab;
    public enum Role { Resource, Home, Special }
    [FormerlySerializedAs("buildingclass")]
    public Role role = Role.Resource;




    public int goldGenerationPerSec;
    public int woodGenerationPerSec;
    public int maxCharacters;
}

