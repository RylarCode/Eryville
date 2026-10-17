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

    public int ClickPriority => 1; // above buildings, so a character standing on one gets the click

    private CharacterMovement movement;
    private SpriteRenderer spriteRenderer;
    private Collider2D bodyCollider;
    private CharacterSchedule schedule;

    // Drawing order: lower on the screen is nearer, so it is drawn in front of whoever is behind it
    private int sortBase = 1;
    private int sortTieBreak;

    private Building currentPlace;        // the building we are Working / Resting at
    private Building destination;         // the building we are travelling to
    private Activity activityOnArrival;   // what we start doing when we get there

    private CharacterAnimator animator;           // only when the character's type has animations
    private PathService.PathRequest pendingRoute; // a route we asked for and are still waiting for
    private Building routeOrigin;                 // the building that route starts from (null: from where we stand)
    private Activity routeOriginActivity;         // what we were doing there, to carry on with if there is no way out

    // Raised when the player clicks a character; the UI listens, so Character knows nothing about it
    public static event System.Action<Character> Selected;

    private void Awake()
    {
        movement = GetComponent<CharacterMovement>();
        if (movement == null) movement = gameObject.AddComponent<CharacterMovement>();
        movement.Arrived += OnArrived;
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        if (spriteRenderer != null) sortBase = Mathf.Max(1, spriteRenderer.sortingOrder); // always above buildings and roads
    }

    // Characters who stand or walk at the same spot still get a stable front-to-back order
    private void LateUpdate()
    {
        if (spriteRenderer == null || !spriteRenderer.enabled) return;
        spriteRenderer.sortingOrder = sortBase + Mathf.RoundToInt((100f - transform.position.y) * 100f) + sortTieBreak;
    }

    private void OnDestroy()
    {
        if (movement != null) movement.Arrived -= OnArrived;
        if (Owned != null) Owned.StatsChanged -= OnStatsChanged;
        if (BuildingManager.Instance != null) BuildingManager.Instance.BuildingMoved -= OnBuildingMoved;
        if (MapGrid.Existing != null) MapGrid.Existing.ObstaclesChanged -= OnObstaclesChanged;
        pendingRoute?.Cancel();
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

        // Everyone gets a small personal difference in drawing order and in where on the road they walk,
        // so a crowd leaving one building spreads out instead of sitting exactly on top of each other
        int hash = owned.Id.GetHashCode() & 0x7fffffff;
        sortTieBreak = hash % 10;
        movement.offset = new Vector3(((hash >> 4) % 100 / 100f - 0.5f) * 0.3f, ((hash >> 11) % 100 / 100f - 0.5f) * 0.3f, 0f);

        spriteRenderer.sprite = owned.Type.sprite;
        if (owned.Type.animations != null)
        {
            animator = GetComponent<CharacterAnimator>();
            if (animator == null) animator = gameObject.AddComponent<CharacterAnimator>();
            animator.Setup(this, movement, spriteRenderer, owned.Type.animations, owned.Type.sprite);
        }
        movement.speed = owned.MoveSpeed;
        owned.StatsChanged += OnStatsChanged;
        schedule = new CharacterSchedule(owned.Type.defaultSchedule); // own copy, so we never edit the asset's list

        // Follow the buildings we go to if they get moved
        if (BuildingManager.Instance != null) BuildingManager.Instance.BuildingMoved += OnBuildingMoved;
        MapGrid.Instance.ObstaclesChanged += OnObstaclesChanged;

        // Start by doing whatever the schedule says for the current time of day
        ReapplySchedule();
    }

    // A building was picked up and put down elsewhere: anyone on the way there heads for the new spot, and
    // anyone inside (working, resting, even just visiting) has to walk over from where the building used to be.
    // Workers are therefore not producing until they arrive.
    private void OnBuildingMoved(Building building)
    {
        if (destination == building)
        {
            RequestRoute(pendingRoute != null ? routeOrigin : null); // find the way to its new spot
        }
        else if (currentPlace == building)
        {
            Activity activity = CurrentActivity;
            LeaveCurrentActivity(); // step out where the building used to stand
            destination = building;
            activityOnArrival = activity;
            routeOriginActivity = Activity.Idle;
            CurrentActivity = Activity.Traveling;
            Debug.Log($"{DisplayName} follows {building.DisplayName} to its new spot to {activity}");
            RequestRoute(null);
        }
    }

    // Something solid appeared on the map (a building was placed or moved): if it is on our way, find another way
    private void OnObstaclesChanged()
    {
        if (CurrentActivity != Activity.Traveling || destination == null || !movement.IsMoving) return;

        MapGrid map = MapGrid.Instance;
        if (movement.AnyRemainingWaypoint(point => map.IsBlocked(map.WorldToCell(point))))
        {
            RequestRoute(null);
        }
    }

    // Loyalty, mood or a modifier changed: walk at the new speed
    private void OnStatsChanged()
    {
        movement.speed = Owned.MoveSpeed;
    }

    // Called by OwnedCharacter when its job / home changes
    internal void OnWorkBuildingChanged(Building building)
    {
        if (CurrentActivity == Activity.Working && currentPlace != building) LeaveCurrentActivity();
        if (building != null) ReapplySchedule();
    }

    // Paid (or not paid) at payday: an unpaid worker goes home, a paid one goes back to the schedule
    internal void OnWageStatusChanged()
    {
        ReapplySchedule();
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
            pendingRoute?.Cancel();
            pendingRoute = null;
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
                if (Owned != null && !Owned.WagePaid)
                {
                    activity = Activity.Resting; // no wage, no work: stay home
                    return HomeBuilding;
                }
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
        Building origin = currentPlace; // where we are setting off from, if we were inside a building
        routeOriginActivity = CurrentActivity;
        LeaveCurrentActivity();

        destination = place;
        activityOnArrival = activityWhenThere;
        CurrentActivity = Activity.Traveling;
        Debug.Log($"{DisplayName} heading to {place.DisplayName} to {activityWhenThere}");
        RequestRoute(origin);
    }

    // Asks for the way to the destination, from inside a building (origin) or from wherever we stand (null).
    // The walk starts when the answer arrives, which can take a few frames when many characters ask at once.
    private void RequestRoute(Building origin)
    {
        pendingRoute?.Cancel();
        movement.Stop();
        animator?.CancelOneShot(); // an unfinished entering / leaving animation no longer applies

        routeOrigin = origin;
        if (origin != null) SetVisible(false); // still "inside" until the route is known, then step out at the entrance
        pendingRoute = PathService.Instance.Request(transform.position, origin, destination, OnRouteReady);
    }

    private void OnRouteReady(PathService.PathResult route)
    {
        pendingRoute = null;
        if (destination == null || CurrentActivity != Activity.Traveling) return;

        if (!route.Found)
        {
            Debug.Log($"{DisplayName} cannot find a way to {destination.DisplayName}.");
            destination = null;
            if (routeOrigin != null && (routeOriginActivity == Activity.Working || routeOriginActivity == Activity.Resting))
            {
                EnterActivity(routeOriginActivity, routeOrigin); // no way out of the building: carry on inside
            }
            else
            {
                SetVisible(true);
                CurrentActivity = Activity.Idle;
            }
            return;
        }

        SetVisible(true);
        if (route.StartsAtBuilding) transform.position = route.Waypoints[0]; // step out of the building at its entrance

        if (route.StartsAtBuilding && animator != null && animator.Has(AnimKey.ExitBuilding))
        {
            // Come out of the door first, then walk off
            Vector3[] waypoints = route.Waypoints;
            if (waypoints.Length > 1) movement.FaceToward(waypoints[1]); // comes out facing the way it is about to walk
            animator.PlayOnce(AnimKey.ExitBuilding, () =>
            {
                if (CurrentActivity == Activity.Traveling && destination != null) movement.FollowPath(waypoints);
            });
            return;
        }
        movement.FollowPath(route.Waypoints);
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

        Activity activity = activityOnArrival;
        bool goingIn = activity == Activity.Working || activity == Activity.Resting;
        if (goingIn && animator != null && animator.Has(AnimKey.EnterBuilding))
        {
            // Go in through the door first (the character stays in view while it plays), then be inside
            CurrentActivity = Activity.Idle;
            animator.PlayOnce(AnimKey.EnterBuilding, () => FinishArrival(place, activity));
            return;
        }

        FinishArrival(place, activity);
    }

    private void FinishArrival(Building place, Activity activity)
    {
        if (place == null) // the building was removed while we were going in
        {
            CurrentActivity = Activity.Idle;
            return;
        }

        // The assignment may have changed on the way; only start work where we still work
        if (activity == Activity.Working && place != WorkBuilding) activity = Activity.Idle;

        EnterActivity(activity, place);
    }

    // Characters are out of sight while they are inside a building (working or resting). The collider goes
    // too, otherwise an invisible character would swallow clicks meant for the building.
    private void SetVisible(bool visible)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
        if (bodyCollider != null) bodyCollider.enabled = visible;
    }

    private void EnterActivity(Activity activity, Building place)
    {
        CurrentActivity = activity;
        currentPlace = place;
        bool inside = activity == Activity.Working || activity == Activity.Resting;
        SetVisible(!inside);
        if (inside) transform.position = place.transform.position; // we reached its entrance and went in
        if (activity == Activity.Working) place.WorkerArrived(this);
        if (activity == Activity.Resting) place.RestingArrived(this);
        Debug.Log($"{DisplayName} is now {activity} at {place.DisplayName}");
    }

    private void LeaveCurrentActivity()
    {
        animator?.CancelOneShot();
        if (CurrentActivity == Activity.Working && currentPlace != null) currentPlace.WorkerLeft(this);
        if (CurrentActivity == Activity.Resting && currentPlace != null) currentPlace.RestingLeft(this);
        currentPlace = null;
        CurrentActivity = Activity.Idle;
        SetVisible(true);
    }
}
