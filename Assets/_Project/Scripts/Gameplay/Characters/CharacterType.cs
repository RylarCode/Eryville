using System.Collections.Generic;
using UnityEngine;
// using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Scriptable Objects/Character")]
public class CharacterType : ScriptableObject
{
    public string Name;
    public int CharacterID;
    public Sprite sprite;
    public int initialWage = 10;
    public GameObject characterCategoryPrefab;
    public enum Type { Resource, Defense, Offense, Others }
    public Type type = Type.Resource;

    
    

    public List<WaypointEntry> defaultSchedule;

}

[System.Serializable]
public class WaypointEntry
{
    public enum personalLocation { Home, Work, Rest1, Rest2, Others }
    public GameManager.timeOfDay time;
    public personalLocation destination;

}