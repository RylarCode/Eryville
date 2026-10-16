using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Reads the touch screen and drives the camera with it (see TouchGestures for the rules): drag to pan,
// pinch to zoom, and drag to paint while the road tool is active. Does nothing on devices without a touch screen.
public class TouchCameraControls : MonoBehaviour
{
    private readonly TouchGestures gestures = new TouchGestures();
    private readonly List<TouchPoint> touches = new List<TouchPoint>();
    private CameraController cameraController;

    private void Awake()
    {
        cameraController = GetComponent<CameraController>();
    }

    private void Update()
    {
        Touchscreen screen = Touchscreen.current;
        if (screen == null || cameraController == null) return;

        touches.Clear();
        foreach (var touch in screen.touches)
        {
            if (touch.isInProgress) touches.Add(new TouchPoint(touch.touchId.ReadValue(), touch.position.ReadValue()));
        }

        RoadPlacer roads = RoadPlacer.Instance;
        BuildingPlacer buildings = BuildingPlacer.Instance;
        bool strokeMode = roads != null && roads.IsPlacing;
        bool placingBuilding = buildings != null && buildings.IsPlacing;

        GestureFrame frame = gestures.Update(touches, UiHit.IsOver, !placingBuilding, strokeMode);
        Apply(frame, roads);
    }

    // Public so tests can feed a frame in directly
    public void Apply(GestureFrame frame, RoadPlacer roads)
    {
        if (frame.panPixels != Vector2.zero) cameraController.PanByPixels(frame.panPixels);
        if (frame.zoomRatio != 1f) cameraController.ZoomBy(frame.zoomRatio);

        if (roads == null) return;
        if (frame.strokeBegin) roads.BeginTouchStroke(frame.strokeStart);
        if (frame.strokeMove) roads.ContinueTouchStroke(frame.strokePosition);
        if (frame.strokeEnd) roads.EndTouchStroke();
    }
}
