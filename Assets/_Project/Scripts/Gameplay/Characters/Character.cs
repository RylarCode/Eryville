using UnityEngine;
using System.Collections.Generic;

public class Character : MonoBehaviour
{
    public string characterName = "Villager";
    public CharacterType characterType;
    public float MoveSpeed => characterType != null ? characterType.moveSpeed : 3f;
    public float Productivity => characterType != null ? characterType.productivity : 1f;
    public float Loyalty = 0f;
    // Changed only through Building.TryAssign*/Remove* so the building lists stay in sync
    [SerializeField] private Building workingBuilding;
    [SerializeField] private Building homeBuilding;
    public Building WorkBuilding => workingBuilding;
    public Building HomeBuilding => homeBuilding;
    public Building rest1Building;
    public Building rest2Building;
    private CharacterSchedule schedule;

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
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, MoveSpeed * Time.deltaTime);
    }

    // Called only by Building (TryAssignWorker / RemoveWorker); pass null to clear.
    internal void SetWorkBuilding(Building building)
    {
        workingBuilding = building;
        Debug.Log(building != null
            ? $"{characterName} assigned work at {building.DisplayName}"
            : $"{characterName} unassigned from work");
    }

    // Called only by Building (TryAssignResident / RemoveResident); pass null to clear.
    internal void SetHomeBuilding(Building building)
    {
        homeBuilding = building;
        Debug.Log(building != null
            ? $"{characterName} now lives at {building.DisplayName}"
            : $"{characterName} is now homeless");
    }


    public void StartWork()
    {   
        if (workingBuilding)
        {
            Debug.Log($"{characterName} starting work at {workingBuilding.DisplayName}");
            currentState = State.Working;
            // Implement work logic (timer, resource generation link, animation)
        }

    }

    public void StopWork()
    {
        if (currentState == State.Working && workingBuilding)
        {
            Debug.Log($"{characterName} stopped work at {workingBuilding.DisplayName}");
            currentState = State.Idle;
            // Implement work logic (timer, resource generation link, animation)
        }

    }

    

    

    public void StartResting(Building building)
    {
        if (building == homeBuilding || building == rest1Building || building == rest2Building)
        {
            Debug.Log($"{characterName} starting rest at {building.DisplayName}");
            currentState = State.Resting;
        }
        
    }

    public void StopResting(Building building)
    {
        if (currentState == State.Resting)
        {
            Debug.Log($"{characterName} stopped resting at {building?.DisplayName}");
            currentState = State.Idle;
        }

    }

    public void Initialize(CharacterType type)
    {
        characterType = type;
        characterName = characterType.displayName;

        GetComponent<SpriteRenderer>().sprite = characterType.sprite;

        schedule = new CharacterSchedule(characterType.defaultSchedule); // own copy, so we never edit the asset's list
        Building potentialHome = CharacterManager.Instance.FindHomeWithSpace();
        if (potentialHome != null)
        {
            CharacterManager.Instance.assignCharacterHouse(this, potentialHome);
        }
        else
        {
            Debug.Log($"No Home");
        }



    }

    public void ScheduleMoving(TimeOfDay newTime)
    {
        if (schedule == null || !schedule.TryGetSlot(newTime, out ScheduleSlot slot)) return;

        Vector3? targetLocation = slot switch
        {
            ScheduleSlot.Home => homeBuilding != null ? homeBuilding.transform.position : (Vector3?)null,
            ScheduleSlot.Work => workingBuilding != null ? workingBuilding.transform.position : (Vector3?)null,
            ScheduleSlot.Rest1 => rest1Building != null ? rest1Building.transform.position : (Vector3?)null,
            ScheduleSlot.Rest2 => rest2Building != null ? rest2Building.transform.position : (Vector3?)null,
            _ => null
        };

        if (targetLocation.HasValue)
        {
            SetDestination(targetLocation.Value);
        }
        else
        {
            Debug.LogWarning("No valid building found for: " + slot);
        }
    }

}
