using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Whether a screen position is on top of some UI. Asked with the position itself, so it works the same for
// a mouse and for a finger (a lifted finger is no longer "over" anything as far as the event system knows).
public static class UiHit
{
    private static readonly List<RaycastResult> results = new List<RaycastResult>();

    public static bool IsOver(Vector2 screenPosition)
    {
        EventSystem system = EventSystem.current;
        if (system == null) return false;

        PointerEventData data = new PointerEventData(system) { position = screenPosition };
        results.Clear();
        system.RaycastAll(data, results);
        return results.Count > 0;
    }
}
