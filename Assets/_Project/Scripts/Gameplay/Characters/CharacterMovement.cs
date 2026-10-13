using System;
using UnityEngine;

// Walks a character in a straight line to a point and raises Arrived when it gets there.
// It knows nothing about buildings or schedules; Character decides where to go and what to do on arrival.
public class CharacterMovement : MonoBehaviour
{
    public float speed = 3f;            // world units per second (set from the character type)
    public float speedMultiplier = 1f;  // hook for things that slow / speed up walking (roads, traffic...)

    public bool IsMoving { get; private set; }

    public event Action Arrived;

    private const float ArriveDistance = 0.1f;
    private Vector3 target;

    // Only enabled while walking, so characters that are standing still or inside a building cost no Update calls
    private void Awake()
    {
        enabled = false;
    }

    public void MoveTo(Vector3 destination)
    {
        target = destination;
        IsMoving = true;
        enabled = true;
    }

    public void Stop()
    {
        IsMoving = false;
        enabled = false;
    }

    private void Update()
    {
        if (!IsMoving) return;

        if (Vector3.Distance(transform.position, target) < ArriveDistance)
        {
            transform.position = target; // snap to the exact position
            IsMoving = false;
            enabled = false;
            Arrived?.Invoke();
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, target, speed * speedMultiplier * Time.deltaTime);
    }
}
