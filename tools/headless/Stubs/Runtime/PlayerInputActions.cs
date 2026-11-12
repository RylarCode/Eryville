// Stands in for the generated Assets/_Project/InputActions/PlayerInputActions.cs (which needs the real Input System):
// the same maps and actions, as plain actions tests can fire (InputAction.Perform / Cancel).
using System;
using UnityEngine.InputSystem;

public class PlayerInputActions : IDisposable
{
    public readonly InputActionMap GameplayMap = new InputActionMap { name = "Gameplay" };
    public readonly InputActionMap CameraMap = new InputActionMap { name = "Camera" };
    private readonly InputAction pointerClick = new InputAction("PointerClick"), pointerPosition = new InputAction("PointerPosition"), gameplayCameraMove = new InputAction("CameraMove"), cameraPanActive = new InputAction("CameraPanActive");
    private readonly InputAction cameraMove = new InputAction("CameraMove"), cameraDrag = new InputAction("CameraDrag"), cameraDragButton = new InputAction("CameraDragButton"), cameraZoom = new InputAction("CameraZoom");

    // Every PlayerInputActions made, so tests can reach the game's own (InputManager and CameraController each make one)
    public static readonly System.Collections.Generic.List<PlayerInputActions> Made = new System.Collections.Generic.List<PlayerInputActions>();

    public PlayerInputActions()
    {
        GameplayMap.actions.AddRange(new[] { pointerClick, pointerPosition, gameplayCameraMove, cameraPanActive });
        CameraMap.actions.AddRange(new[] { cameraMove, cameraDrag, cameraDragButton, cameraZoom });
        pointerPosition.Value = UnityEngine.Vector2.zero;
        cameraMove.Value = UnityEngine.Vector2.zero;
        Made.Add(this);
    }

    public void Enable() { GameplayMap.Enable(); CameraMap.Enable(); }
    public void Disable() { GameplayMap.Disable(); CameraMap.Disable(); }
    public void Dispose() { }

    public struct GameplayActions
    {
        private readonly PlayerInputActions w;
        public GameplayActions(PlayerInputActions wrapper) { w = wrapper; }
        public InputAction PointerClick => w.pointerClick;
        public InputAction PointerPosition => w.pointerPosition;
        public InputAction CameraMove => w.gameplayCameraMove;
        public InputAction CameraPanActive => w.cameraPanActive;
        public void Enable() => w.GameplayMap.Enable();
        public void Disable() => w.GameplayMap.Disable();
        public bool enabled => w.GameplayMap.enabled;
    }
    public GameplayActions Gameplay => new GameplayActions(this);

    public struct CameraActions
    {
        private readonly PlayerInputActions w;
        public CameraActions(PlayerInputActions wrapper) { w = wrapper; }
        public InputAction CameraMove => w.cameraMove;
        public InputAction CameraDrag => w.cameraDrag;
        public InputAction CameraDragButton => w.cameraDragButton;
        public InputAction CameraZoom => w.cameraZoom;
        public void Enable() => w.CameraMap.Enable();
        public void Disable() => w.CameraMap.Disable();
        public bool enabled => w.CameraMap.enabled;
    }
    public CameraActions Camera => new CameraActions(this);
}
