using UnityEngine;

// The four ways a character can face. Characters only ever walk up, down, left or right, so these are all there is.
public enum Facing { Down, Up, Left, Right }

public static class FacingExtensions
{
    // Which way a movement points. Along one axis it is plain; for a slanted one the longer part decides (a tie goes to left / right).
    public static Facing FromDirection(Vector2 direction, Facing fallback)
    {
        float ax = Mathf.Abs(direction.x);
        float ay = Mathf.Abs(direction.y);
        if (ax < 0.0001f && ay < 0.0001f) return fallback;
        if (ax >= ay) return direction.x < 0f ? Facing.Left : Facing.Right;
        return direction.y < 0f ? Facing.Down : Facing.Up;
    }
}
