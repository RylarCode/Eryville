using UnityEngine;

[RequireComponent(typeof(CharacterMovement))]
public class Character : MonoBehaviour
{
    public enum Activity { Idle, Traveling, Working, Resting }

    public string characterName = "Villager";
    public CharacterType characterType;
    public float Loyalty = 0f;

    public float Productivity => characterType != null ? characterType.productivity : 1f;

    // Changed only through Building.TryAssign*/Remove* so the building lists stay in sync
    [SerializeField] private Building workingBuilding;
    [SerializeField] private Building homeBuilding;
    public Building WorkBuilding => workingBuilding;
    public Building HomeBuilding => homeBuilding;

    public Activity CurrentActivity { get; private set; } = Activity.Idle;

    private CharacterMovement movement;
    private CharacterSchedule schedule;

    private Building currentPlace;        // the building we are Working / Resting at
    private Building destination;         // the building we are travelling to
    private Activity activityOnArrival;   // what we start doing when we get there

    private void Awake()
    {
        movement = GetComponent<CharacterMovement>();
        if (movement == null) movement = gameObject.AddComponent<CharacterMovement>();
        movement.Arrived += OnArrived;
    }

    private void OnDestroy()
    {
        if (movement != null) movement.Arrived -= OnArrived;
        LeaveCurrentActivity(); // don't leave a ghost worker behind in a building
    }

    public void OnClicked()
    {
        UIManager.Instance.showCharacterDetails(this);
    }

    public void Initialize(CharacterType type)
    {
        characterType = type;
        characterName = characterType.displayName;

        GetComponent<SpriteRenderer>().sprite = characterType.sprite;
        movement.speed = characterType.moveSpeed;

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

        // Start by doing whatever the schedule says for the current time of day
        ReapplySchedule();
    }

    // Called by Building (TryAssignWorker / RemoveWorker); pass null to clear.
    internal void SetWorkBuilding(Building building)
    {
        workingBuilding = building;
        if (CurrentActivity == Activity.Working && currentPlace != building) LeaveCurrentActivity();
        Debug.Log(building != null
            ? $"{characterName} assigned work at {building.DisplayName}"
            : $"{characterName} unassigned from work");
        if (building != null) ReapplySchedule();
    }

    // Called by Building (TryAssignResident / RemoveResident); pass null to clear.
    internal void SetHomeBuilding(Building building)
    {
        homeBuilding = building;
        Debug.Log(building != null
            ? $"{characterName} now lives at {building.DisplayName}"
            : $"{characterName} is now homeless");
        if (building != null) ReapplySchedule();
    }

    // --- Schedule ---

    // Decide where to be for this time of day and head there. If the schedule has no entry for it,
    // the character just carries on with what it was doing.
    // A new job / home may change where we should be right now (e.g. hired mid-shift)
    private void ReapplySchedule()
    {
        ApplySchedule(GameClock.Instance != null ? GameClock.Instance.Current : TimeOfDay.Morning);
    }

    public void ApplySchedule(TimeOfDay time)
    {
        if (schedule == null || !schedule.TryGetSlot(time, out ScheduleSlot slot)) return;

        Building place = ResolvePlace(slot, out Activity activity);
        if (place == null)
        {
            // Nowhere to go for this slot (no job, no home...): stop what we were doing and stay put
            Debug.Log($"{characterName} has nowhere to go for {slot}.");
            movement.Stop();
            destination = null;
            LeaveCurrentActivity();
            return;
        }

        bool alreadyThere = CurrentActivity == activity && currentPlace == place;
        bool alreadyOnTheWay = CurrentActivity == Activity.Traveling && destination == place && activityOnArrival == activity;
        if (alreadyThere || alreadyOnTheWay) return;

        GoTo(place, activity);
    }

    private Building ResolvePlace(ScheduleSlot slot, out Activity activity)
    {
        switch (slot)
        {
            case ScheduleSlot.Work:
                activity = Activity.Working;
                return workingBuilding;
            case ScheduleSlot.Home:
                activity = Activity.Resting;
                return homeBuilding;
            case ScheduleSlot.Rest1:
            case ScheduleSlot.Rest2:
                activity = Activity.Resting;
                return CharacterManager.Instance.ResolveRestPlace(this, slot);
            default:
                activity = Activity.Idle;
                return null;
        }
    }

    // --- Moving and activities ---

    private void GoTo(Building place, Activity activityWhenThere)
    {
        LeaveCurrentActivity();

        destination = place;
        activityOnArrival = activityWhenThere;
        CurrentActivity = Activity.Traveling;
        movement.MoveTo(place.transform.position);
        Debug.Log($"{characterName} heading to {place.DisplayName} to {activityWhenThere}");
    }

    // We know why we came here, so there is no need to work it out from our position
    private void OnArrived()
    {
        Building place = destination;
        destination = null;

        if (place == null) // the building was removed while we were on the way
        {
            CurrentActivity = Activity.Idle;
            return;
        }

        // The assignment may have changed on the way; only start work where we still work
        Activity activity = activityOnArrival;
        if (activity == Activity.Working && place != workingBuilding) activity = Activity.Idle;

        EnterActivity(activity, place);
    }

    private void EnterActivity(Activity activity, Building place)
    {
        CurrentActivity = activity;
        currentPlace = place;
        if (activity == Activity.Working) place.WorkerArrived(this);
        Debug.Log($"{characterName} is now {activity} at {place.DisplayName}");
    }

    private void LeaveCurrentActivity()
    {
        if (CurrentActivity == Activity.Working && currentPlace != null) currentPlace.WorkerLeft(this);
        currentPlace = null;
        CurrentActivity = Activity.Idle;
    }
}
