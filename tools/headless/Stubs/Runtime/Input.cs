// Headless stand-ins for the Input System: devices whose buttons tests can press, and input actions tests can fire.
// "Pressed this frame" flags are cleared at the end of every World.Tick, like a real frame.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.InputSystem
{
    public class InputControl { public string name = ""; }

    public class ButtonControl : InputControl
    {
        public bool isPressed { get; set; }
        public bool wasPressedThisFrame { get; set; }
        public bool wasReleasedThisFrame { get; set; }
        public float ReadValue() => isPressed ? 1f : 0f;

        // Not an Input System API: press (and release) in one go, as one frame's input
        public void Tap() { wasPressedThisFrame = true; InputDevices.Touched(this); }
        public void Press() { isPressed = true; wasPressedThisFrame = true; InputDevices.Touched(this); }
        public void Release() { isPressed = false; wasReleasedThisFrame = true; InputDevices.Touched(this); }
        internal void EndFrame() { wasPressedThisFrame = false; wasReleasedThisFrame = false; }
    }

    public class KeyControl : ButtonControl { }
    public class Vector2Control : InputControl { public Vector2 value; public Vector2 ReadValue() => value; }
    public class AxisControl : InputControl { public float value; public float ReadValue() => value; }
    public class IntegerControl : InputControl { public int value; public int ReadValue() => value; }
    public class TouchPhaseControl : InputControl { public TouchPhase value; public TouchPhase ReadValue() => value; }
    public enum TouchPhase { None, Began, Moved, Ended, Canceled, Stationary }

    internal static class InputDevices
    {
        private static readonly HashSet<ButtonControl> touched = new HashSet<ButtonControl>();
        static InputDevices() { World.InputSystemFrame += EndFrame; }
        public static void Touched(ButtonControl b) => touched.Add(b);
        private static void EndFrame() { foreach (ButtonControl b in touched) b.EndFrame(); touched.Clear(); }
    }

    public class InputDevice { public bool added = true; public string displayName = ""; }

    public enum Key { Escape, F3, Space, Enter, A, D, S, W }

    public class Keyboard : InputDevice
    {
        public static Keyboard current { get; set; } // null until a test makes one
        public KeyControl escapeKey { get; } = new KeyControl();
        public KeyControl f3Key { get; } = new KeyControl();
        public KeyControl spaceKey { get; } = new KeyControl();
        public KeyControl enterKey { get; } = new KeyControl();
        public ButtonControl anyKey { get; } = new ButtonControl();
        public KeyControl this[Key k] => k == Key.Escape ? escapeKey : k == Key.F3 ? f3Key : k == Key.Space ? spaceKey : enterKey;
    }

    public class Pointer : InputDevice
    {
        public Vector2Control position { get; } = new Vector2Control();
        public Vector2Control delta { get; } = new Vector2Control();
        public ButtonControl press { get; } = new ButtonControl();
    }

    public class Mouse : Pointer
    {
        public static Mouse current { get; set; }
        public ButtonControl leftButton { get; } = new ButtonControl();
        public ButtonControl rightButton { get; } = new ButtonControl();
        public ButtonControl middleButton { get; } = new ButtonControl();
        public Vector2Control scroll { get; } = new Vector2Control();
    }

    public class TouchControl : InputControl
    {
        public IntegerControl touchId { get; } = new IntegerControl();
        public Vector2Control position { get; } = new Vector2Control();
        public Vector2Control delta { get; } = new Vector2Control();
        public ButtonControl press { get; } = new ButtonControl();
        public TouchPhaseControl phase { get; } = new TouchPhaseControl();
        public bool isInProgress { get; set; }
    }

    public class Touchscreen : Pointer
    {
        public static Touchscreen current { get; set; }
        public TouchControl primaryTouch { get; } = new TouchControl();
        public List<TouchControl> touches { get; } = new List<TouchControl>();
    }

    public enum InputActionPhase { Disabled, Waiting, Started, Performed, Canceled }
    public enum InputActionType { Value, Button, PassThrough }

    public class InputActionMap : IDisposable
    {
        public string name = "";
        public bool enabled { get; private set; }
        public readonly List<InputAction> actions = new List<InputAction>();
        public void Enable() { enabled = true; foreach (InputAction a in actions) a.Enable(); }
        public void Disable() { enabled = false; foreach (InputAction a in actions) a.Disable(); }
        public void Dispose() { }
    }

    public class InputAction
    {
        public struct CallbackContext
        {
            internal InputAction action;
            public InputAction Action => action;
            public InputActionPhase phase;
            public bool performed => phase == InputActionPhase.Performed;
            public bool canceled => phase == InputActionPhase.Canceled;
            public bool started => phase == InputActionPhase.Started;
            public TValue ReadValue<TValue>() where TValue : struct => action.ReadValue<TValue>();
            public object ReadValueAsObject() => action.Value;
            public bool ReadValueAsButton() => action.Value is float f ? f > 0.5f : action.Value is bool b && b;
            public double time => Time.time;
        }

        public string name;
        public bool enabled { get; private set; }
        public object Value = 0f;
        public event Action<CallbackContext> started;
        public event Action<CallbackContext> performed;
        public event Action<CallbackContext> canceled;

        public InputAction(string name = "") { this.name = name; }
        public void Enable() => enabled = true;
        public void Disable() => enabled = false;
        public TValue ReadValue<TValue>() where TValue : struct => Value is TValue v ? v : default;
        public bool IsPressed() => Value is float f ? f > 0.5f : Value is bool b && b;
        public bool WasPressedThisFrame() => false;
        public bool WasReleasedThisFrame() => false;
        public bool triggered => false;

        // Not Input System APIs: tests set a value and raise the callbacks (only while enabled, like the real thing)
        public void Perform(object value) { Value = value; if (enabled) performed?.Invoke(new CallbackContext { action = this, phase = InputActionPhase.Performed }); }
        public void Perform() => Perform(1f);
        public void Cancel(object value) { Value = value; if (enabled) canceled?.Invoke(new CallbackContext { action = this, phase = InputActionPhase.Canceled }); }
        public void Cancel() => Cancel(0f);
        public void Start() { if (enabled) started?.Invoke(new CallbackContext { action = this, phase = InputActionPhase.Started }); }
    }
}

namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule { }
}

namespace UnityEngine.InputSystem.Controls
{
    public class ButtonControl : UnityEngine.InputSystem.ButtonControl { }
}
