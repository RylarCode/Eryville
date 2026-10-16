using System.Collections.Generic;
using UnityEngine;

// One finger on the screen, as reported by the device
public struct TouchPoint
{
    public int id;
    public Vector2 position; // pixels

    public TouchPoint(int id, Vector2 position) { this.id = id; this.position = position; }
}

// What the fingers did this frame
public struct GestureFrame
{
    public Vector2 panPixels;   // move the view by this many screen pixels (the world follows the fingers)
    public float zoomRatio;     // 1 = no zoom; 2 = the fingers moved twice as far apart (zoom in)
    public bool strokeBegin;    // a one-finger drag started in stroke mode (painting roads): at strokeStart
    public Vector2 strokeStart;
    public bool strokeMove;     // the finger is dragging: now at strokePosition
    public Vector2 strokePosition;
    public bool strokeEnd;      // the finger lifted (or a second finger came down) and the stroke is over
}

// Turns raw touches into camera gestures, without knowing anything about Unity input or the camera, so
// it can be tested exactly:
//   - two fingers: pinch to zoom and drag to pan (always, whatever the game is doing)
//   - one finger, normally: drag to pan the view
//   - one finger, stroke mode (painting roads): drag paints instead of panning
//   - one finger while placing a building: nothing here (the ghost follows the finger by itself)
// A touch that starts on top of UI never does anything here, and after a two-finger gesture the remaining
// finger does nothing until it is lifted (so lifting one finger does not make the view jump).
public class TouchGestures
{
    public float dragThreshold = 14f; // pixels a finger must move before it counts as a drag rather than a tap

    private int singleId = -1;
    private Vector2 singleStart;
    private Vector2 singlePrevious;
    private bool singleDragging;
    private bool singleIgnored;       // started over UI, or is what is left of a two-finger gesture
    private bool strokeActive;

    private bool twoActive;
    private float previousDistance;
    private Vector2 previousMiddle;

    // overUi: whether a screen position is on top of UI. onePanAllowed: a one-finger drag may pan the view.
    // strokeMode: a one-finger drag is a stroke (roads).
    public GestureFrame Update(IReadOnlyList<TouchPoint> touches, System.Func<Vector2, bool> overUi, bool onePanAllowed, bool strokeMode)
    {
        GestureFrame frame = new GestureFrame { zoomRatio = 1f };

        if (touches.Count >= 2)
        {
            if (strokeActive) { frame.strokeEnd = true; strokeActive = false; }
            singleId = -1;
            singleDragging = false;

            Vector2 a = touches[0].position, b = touches[1].position;
            float distance = Vector2.Distance(a, b);
            Vector2 middle = (a + b) * 0.5f;
            if (twoActive)
            {
                if (previousDistance > 1f && distance > 1f) frame.zoomRatio = distance / previousDistance;
                frame.panPixels = middle - previousMiddle;
            }
            twoActive = true;
            previousDistance = distance;
            previousMiddle = middle;
            return frame;
        }

        if (touches.Count == 1)
        {
            TouchPoint touch = touches[0];
            if (touch.id != singleId)
            {
                bool leftover = twoActive; // the other finger of a two-finger gesture is still down
                singleId = touch.id;
                singleStart = touch.position;
                singlePrevious = touch.position;
                singleDragging = false;
                singleIgnored = leftover || overUi(touch.position);
            }
            twoActive = false;

            if (!singleIgnored)
            {
                if (!singleDragging && Vector2.Distance(touch.position, singleStart) > dragThreshold)
                {
                    singleDragging = true;
                    if (strokeMode)
                    {
                        strokeActive = true;
                        frame.strokeBegin = true;
                        frame.strokeStart = singleStart;
                    }
                }

                if (singleDragging)
                {
                    if (strokeMode && strokeActive)
                    {
                        frame.strokeMove = true;
                        frame.strokePosition = touch.position;
                    }
                    else if (!strokeMode && onePanAllowed)
                    {
                        frame.panPixels = touch.position - singlePrevious;
                    }
                }
            }
            singlePrevious = touch.position;
            return frame;
        }

        // No fingers
        if (strokeActive) frame.strokeEnd = true;
        strokeActive = false;
        singleId = -1;
        singleDragging = false;
        singleIgnored = false;
        twoActive = false;
        return frame;
    }
}
