using UnityEngine;

[CreateAssetMenu(fileName = "NewBuildingType", menuName = "Scriptable Objects/BuildingType")]
public class BuildingType : ScriptableObject
{
    public string buildingName;
    public Sprite sprite;
    public int goldCost;
    public int woodCost;
    public Vector2Int size = new Vector2Int(1, 1); // Size in grid cells
    public GameObject buildingcategoryprefab;
    public enum buildingClass { Resource, Home, Special}
    public buildingClass buildingclass = buildingClass.Resource;




    public int goldGenerationPerSec;
    public int woodGenerationPerSec;
    public int maxCharacters;
}

