using UnityEngine;

// The visible body of an OwnedCharacter: walks around, follows the schedule and turns up at buildings.
// Identity, stats and assignments live on the OwnedCharacter (Owned); this only handles the world side.
[RequireComponent(typeof(CharacterMovement))]
public class Character : MonoBehaviour, IClickable
{
    public enum Activity { Idle, Traveling, Working, Resting }

    // The character this body belongs to
    public OwnedCharacter Owned { get; private set; }

    public string DisplayName => Owned != null ? Owned.DisplayName : name;
    public CharacterType Type => Owned?.Type;
    public float Productivity => Owned != null ? Owned.Productivity : 1f;
    public Building WorkBuilding => Owned?.WorkBuilding;
    public Building HomeBuilding => Owned?.HomeBuilding;

    public Activity CurrentActivity { get; private set; } = Activity.Idle;

    private CharacterMovement movement;
    private CharacterSchedule schedule;

    private Building currentPlace;        // the building we are Working / Resting at
    private Building destination;         // the building we are travelling to
    private Activity activityOnArrival;   // what we start doing when we get there

    // Raised when the player clicks a character; the UI listens, so Character knows nothing about it
    public static event System.Action<Character> Selected;

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
        if (Owned != null && Owned.Avatar == this) Owned.Avatar = null;
    }

    public void OnClicked()
    {
        Selected?.Invoke(this);
    }

    // Gives this body to an owned character and starts it following its schedule
    public void Bind(OwnedCharacter owned)
    {
        Owned = owned;
        owned.Avatar = this;

        GetComponent<SpriteRenderer>().sprite = owned.Type.sprite;
        movement.speed = owned.Type.moveSpeed;
        schedule = new CharacterSchedule(owned.Type.defaultSchedule); // own copy, so we never edit the asset's list

        // Start by doing whatever the schedule says for the current time of day
        ReapplySchedule();
    }

    // Called by OwnedCharacter when its job / home changes
    internal void OnWorkBuildingChanged(Building building)
    {
        if (CurrentActivity == Activity.Working && currentPlace != building) LeaveCurrentActivity();
        if (building != null) ReapplySchedule();
    }

    internal void OnHomeBuildingChanged(Building building)
    {
        if (building != null) ReapplySchedule();
    }

    // --- Schedule ---

    // A new job / home may change where we should be right now (e.g. hired mid-shift)
    private void ReapplySchedule()
    {
        ApplySchedule(GameClock.Instance != null ? GameClock.Instance.Current : TimeOfDay.Morning);
    }

    // Decide where to be for this time of day and head there. If the schedule has no entry for it,
    // the character just carries on with what it was doing.
    public void ApplySchedule(TimeOfDay time)
    {
        if (schedule == null || !schedule.TryGetSlot(time, out ScheduleSlot slot)) return;

        Building place = ResolvePlace(slot, out Activity activity);
        if (place == null)
        {
            // Nowhere to go for this slot (no job, no home...): stop what we were doing and stay put
            Debug.Log($"{DisplayName} has nowhere to go for {slot}.");
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
                return WorkBuilding;
            case ScheduleSlot.Home:
                activity = Activity.Resting;
                return HomeBuilding;
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
        Debug.Log($"{DisplayName} heading to {place.DisplayName} to {activityWhenThere}");
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
        if (activity == Activity.Working && place != WorkBuilding) activity = Activity.Idle;

        EnterActivity(activity, place);
    }

    private void EnterActivity(Activity activity, Building place)
    {
        CurrentActivity = activity;
        currentPlace = place;
        if (activity == Activity.Working) place.WorkerArrived(this);
        Debug.Log($"{DisplayName} is now {activity} at {place.DisplayName}");
    }

    private void LeaveCurrentActivity()
    {
        if (CurrentActivity == Activity.Working && currentPlace != null) currentPlace.WorkerLeft(this);
        currentPlace = null;
        CurrentActivity = Activity.Idle;
    }
}
