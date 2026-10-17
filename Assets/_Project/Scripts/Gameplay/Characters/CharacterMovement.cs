using System;
using System.Collections.Generic;
using UnityEngine;

// Walks a character along a list of waypoints and raises Arrived when it reaches the last one.
// It knows nothing about buildings or schedules; Character decides where to go and what to do on arrival,
// and PathService decides which way to walk.
public class CharacterMovement : MonoBehaviour
{
    public float speed = 3f;            // world units per second on open ground (set from the character type)
    public Vector3 offset;              // where on the road this character walks, relative to the cell centres (so crowds spread out a little)
    public float speedMultiplier = 1f;  // extra hook for things that slow / speed up walking (e.g. being tired)

    public bool IsMoving { get; private set; }

    // Which way the character moved in the latest frame (always straight up, down, left or right), which of the four it
    // faces (kept while standing still), and how fast it is going (world units per second, roads included)
    public Vector2 Direction { get; private set; }
    public Facing Facing { get; private set; }
    public float EffectiveSpeed { get; private set; }

    public event Action Arrived;

    private const float AxisTolerance = 0.001f; // closer than this to a line counts as on it

    private IReadOnlyList<Vector3> path;
    private int nextWaypoint;
    private readonly Vector3[] singlePoint = new Vector3[1];

    // Only enabled while walking, so characters that are standing still or inside a building cost no Update calls
    private void Awake()
    {
        enabled = false;
    }

    // Walks in a straight line to one point
    public void MoveTo(Vector3 destination)
    {
        singlePoint[0] = destination;
        FollowPath(singlePoint);
    }

    // Walks through the points in order. The list is only read, never changed (routes are shared).
    public void FollowPath(IReadOnlyList<Vector3> waypoints)
    {
        path = waypoints;
        nextWaypoint = 0;
        IsMoving = true;
        enabled = true;
    }

    // Turns to look at something without moving (e.g. towards the first step of a walk)
    public void FaceToward(Vector3 target)
    {
        Vector3 d = target - transform.position;
        // Walking goes along x first, then y, so that is the way the first step points
        Facing = FacingExtensions.FromDirection(Mathf.Abs(d.x) > AxisTolerance ? new Vector2(d.x, 0f) : new Vector2(0f, d.y), Facing);
    }

    public void Stop()
    {
        IsMoving = false;
        path = null;
        enabled = false;
    }

    // Whether any of the waypoints still ahead matches (used to spot a route that has been blocked)
    public bool AnyRemainingWaypoint(Predicate<Vector3> match)
    {
        if (!IsMoving || path == null) return false;
        for (int i = nextWaypoint; i < path.Count; i++)
        {
            if (match(path[i])) return true;
        }
        return false;
    }

    private void Update()
    {
        if (!IsMoving || path == null) return;

        // How far we can go this frame: our speed, times what the ground under us allows (roads are faster)
        float terrain = MapGrid.Instance.SpeedMultiplierAt(transform.position);
        float budget = speed * speedMultiplier * terrain * Time.deltaTime;

        Vector3 position = transform.position;
        Vector2 lastStep = Vector2.zero;
        while (budget > 0f && nextWaypoint < path.Count)
        {
            Vector3 target = path[nextWaypoint] + offset;

            // Never diagonal: along x first, then along y. (Roads are made of straight legs already; this only
            // matters for the first step, from wherever the character stands onto the road.)
            Vector3 step = target;
            bool corner = Mathf.Abs(target.x - position.x) > AxisTolerance && Mathf.Abs(target.y - position.y) > AxisTolerance;
            if (corner) step = new Vector3(target.x, position.y, target.z);

            float distance = Vector3.Distance(position, step);
            Vector3 before = position;
            if (distance <= budget)
            {
                position = step; // reached the end of this leg, carry on with what is left
                budget -= distance;
                if (!corner) nextWaypoint++;
            }
            else
            {
                position = Vector3.MoveTowards(position, step, budget);
                budget = 0f;
            }
            Vector3 leg = position - before;
            if (leg.sqrMagnitude > 0f) lastStep = new Vector2(leg.x, leg.y);
        }
        if (lastStep.sqrMagnitude > 0f)
        {
            Direction = lastStep;
            Facing = FacingExtensions.FromDirection(lastStep, Facing);
        }
        EffectiveSpeed = speed * speedMultiplier * terrain;
        transform.position = position;

        if (nextWaypoint >= path.Count)
        {
            IsMoving = false;
            path = null;
            enabled = false;
            Arrived?.Invoke();
        }
    }
}
