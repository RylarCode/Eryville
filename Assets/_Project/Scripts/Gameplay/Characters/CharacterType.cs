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

    [Header("Stats")]
    public float moveSpeed = 3f;       // world units per second
    public float productivity = 1.2f;  // multiplies a building's output while this character works there

    public List<ScheduleEntry> defaultSchedule;
}
