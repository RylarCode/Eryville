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

    public event Action Arrived;

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
        while (budget > 0f && nextWaypoint < path.Count)
        {
            Vector3 target = path[nextWaypoint] + offset;
            float distance = Vector3.Distance(position, target);
            if (distance <= budget)
            {
                position = target; // reached this waypoint, carry on towards the next with what is left
                budget -= distance;
                nextWaypoint++;
            }
            else
            {
                position = Vector3.MoveTowards(position, target, budget);
                budget = 0f;
            }
        }
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
