using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class Character : MonoBehaviour
{
    public string characterName = "Villager";
    public float moveSpeed = 3f;
    public CharacterType characterType;
    public float resourceMultiplier = 1.2f;
    public float Loyalty = 0f;
    public Building workingBuilding;
    public Building homeBuilding;
    public Building rest1Building;
    public Building rest2Building;
    public List<WaypointEntry> Schedule;

    // State machine or simple state variable could go here
    public enum State { Idle, Moving, Working, Resting}
    public State currentState = State.Idle;

    private Vector3 targetPosition;
    private bool isMoving = false;

    void Start()
    {
        int randomNumber = Random.Range(-10, 10);
        int randomNumber2 = Random.Range(-10, 10);
        SetDestination(new Vector3(randomNumber, randomNumber2, 0));
    }
    void Update()
    {
        if (isMoving)
        {
            MoveTowardsTarget();
        }
    }

    public void OnClicked()
    {
        UIManager.Instance.showCharacterDetails(this);
    }

    public void SetDestination(Vector3 destination)
    {
        targetPosition = destination;
        isMoving = true;
        currentState = State.Moving;
        Debug.Log($"{characterName} moving towards {targetPosition}");
        // Optional: Rotate to face destination
        // Vector2 direction = (destination - transform.position).normalized;
        // if (direction.x != 0) // Basic facing direction
        //     transform.localScale = new Vector3(Mathf.Sign(direction.x), 1, 1);
    }

    private void MoveTowardsTarget()
    {
        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            // Reached destination
            transform.position = targetPosition; // Snap to exact position
            isMoving = false;
            currentState = State.Idle;
            Debug.Log($"{characterName} reached destination.");
            // Trigger arrival logic (e.g., start working)
            if (Vector3.Distance(transform.position, workingBuilding != null ? workingBuilding.transform.position : transform.position) < 0.1f)
            {
                CharacterManager.Instance.CharacterStartsWorkinginBuilding(this, workingBuilding);
            }
            else if (Vector3.Distance(transform.position, homeBuilding != null ? homeBuilding.transform.position : transform.position) < 0.1f)
            {
                CharacterManager.Instance.CharacterStartsRestinginBuilding(this, homeBuilding);
            }
            else if (Vector3.Distance(transform.position, rest1Building != null ? rest1Building.transform.position : transform.position) < 0.1f)
            {
                CharacterManager.Instance.CharacterStartsRestinginBuilding(this, rest1Building);
            }
            else if (Vector3.Distance(transform.position, rest2Building != null ? rest2Building.transform.position : transform.position) < 0.1f)
            {
                CharacterManager.Instance.CharacterStartsRestinginBuilding(this, rest2Building);
            }
            return;
        }

        // Move towards the target position
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    }

    public void AssignedWork(Building targetBuilding)
    {
        Debug.Log($"{characterName} assigned work at {targetBuilding.buildingName}");
        workingBuilding = targetBuilding;
    }

    public void UnassignedWork(Building targetBuilding)
    {
        Debug.Log($"{characterName} unassigned work from {targetBuilding.buildingName}");
        workingBuilding = null;
    }

    public void AssignedHome(Building targetBuilding)
    {
        homeBuilding = targetBuilding;
        Debug.Log($"House went to {this.name}");
    }

    public void UnassignedHome(Building targetBuilding)
    {
        homeBuilding = null;
        Debug.Log($"House unassigned from {this.name}");
    }


    public void StartWork()
    {   
        if (workingBuilding)
        {
            Debug.Log($"{characterName} starting work at {workingBuilding.buildingName}");
            currentState = State.Working;
            // Implement work logic (timer, resource generation link, animation)
        }

    }

    public void StopWork()
    {
        if (currentState == State.Working && workingBuilding)
        {
            Debug.Log($"{characterName} stopped work at {workingBuilding.buildingName}");
            currentState = State.Idle;
            // Implement work logic (timer, resource generation link, animation)
        }

    }

    

    

    public void StartResting(Building building)
    {
        if (building == homeBuilding || building == rest1Building || building == rest2Building)
        {
            Debug.Log($"{characterName} starting rest at {building.buildingName}");
            currentState = State.Resting;
        }
        
    }

    public void StopResting(Building building)
    {
        if (currentState == State.Resting)
        {
            Debug.Log($"{characterName} stopped resting at {building.buildingName}");
            currentState = State.Idle;
        }

    }

    public void Initialize(CharacterType type)
    {
        characterType = type;
        characterName = characterType.Name;

        GetComponent<SpriteRenderer>().sprite = characterType.sprite;

        Schedule = characterType.defaultSchedule;
        Building potentialHome = CharacterManager.Instance.getHome();
        if (potentialHome != null)
        {
            AssignedHome(potentialHome);
        }
        else
        {
            Debug.Log($"No Home");
        }



    }

    public void ScheduleMoving(GameManager.timeOfDay newTime)
    {
        foreach (WaypointEntry waypoint in Schedule)
        {
            if (waypoint.time == newTime)
            {
                WaypointEntry.personalLocation newLocation =  waypoint.destination;

                Vector3? targetLocation = newLocation switch
                {
                    WaypointEntry.personalLocation.Home => homeBuilding != null ? homeBuilding.transform.position : (Vector3?)null,
                    WaypointEntry.personalLocation.Work => workingBuilding != null ? workingBuilding.transform.position : (Vector3?)null,
                    WaypointEntry.personalLocation.Rest1 => rest1Building != null ? rest1Building.transform.position : (Vector3?)null,
                    WaypointEntry.personalLocation.Rest2 => rest2Building != null ? rest2Building.transform.position : (Vector3?)null,
                    _ => null
                };

                if (targetLocation.HasValue)
                {
                    SetDestination(targetLocation.Value);
                }
                else
                {
                    Debug.LogWarning("No valid building found for: " + newLocation);
                }

            }
        }
    }

}