using UnityEngine;
using UnityEngine.InputSystem; // Import the new Input System namespace
using System; // Needed for Action event
using UnityEngine.EventSystems; // Needed for IsPointerOverGameObject


public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    private Camera mainCamera;


    private PlayerInputActions playerInputActions;

    // Events other scripts can subscribe to
    public event Action<Vector2> OnPointerClick; // Passes screen position
    public event Action<Vector2> OnPointerPositionUpdate; // Passes screen position
    public event Action<Vector2> OnPointerReleased;       // Passes screen position; the button / finger came up

    // Whether the button is down (or the finger is on the screen) right now
    public bool IsPointerHeld { get; private set; }

    // Everything that currently turns clicks into building (placing, moving, roads...). Each registers
    // itself here; while any is active, clicks go to it instead of selecting what is under the cursor.
    public static readonly System.Collections.Generic.List<IPlacementMode> PlacementModes = new System.Collections.Generic.List<IPlacementMode>();

    private static bool AnyPlacementActive()
    {
        foreach (IPlacementMode mode in PlacementModes)
        {
            if (mode.IsPlacing) return true;
        }
        return false;
    }

    // For the Update frame
    private Vector2? pendingClick = null;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject); // Optional persistence

        playerInputActions = new PlayerInputActions();
        mainCamera = Camera.main;
    }

    public void Update()
    {
        if (pendingClick.HasValue)
        {
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(pendingClick.Value);
            RaycastHit2D[] hits = Physics2D.RaycastAll(worldPosition, Vector2.zero);

            // Of everything under the cursor, the clickable with the highest priority wins
            // (a character standing on a building is clicked, not the building)
            IClickable target = null;
            foreach (RaycastHit2D hit in hits)
            {
                IClickable candidate = hit.collider.gameObject.GetComponent<IClickable>();
                if (candidate != null && (target == null || candidate.ClickPriority > target.ClickPriority)) target = candidate;
            }

            if (EventSystem.current.IsPointerOverGameObject())
            {
                //Debug.Log("Clicked on UI — skip placing.");
            }
            else if (hits.Length > 0 && !AnyPlacementActive())
            {
                // Something solid was clicked: it gets the click (if it can use it) and nothing is placed.
                // (Not while placing or moving a building: then the click is the placer's, even over other buildings.)
                target?.OnClicked();
            }
            else
            {
                // Invoke the event, passing the screen position
                OnPointerClick?.Invoke(pendingClick.Value);
            }

            pendingClick = null;
        }
    }


    private void OnEnable()
    {
        // Enable the Action Map
        playerInputActions.Gameplay.Enable();

        // Subscribe C# methods to the input actions
        playerInputActions.Gameplay.PointerClick.performed += HandlePointerClick;
        playerInputActions.Gameplay.PointerClick.canceled += HandlePointerRelease;
        playerInputActions.Gameplay.PointerPosition.performed += HandlePointerPosition; // Use performed for continuous update
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent errors when disabled/destroyed
        playerInputActions.Gameplay.PointerClick.performed -= HandlePointerClick;
        playerInputActions.Gameplay.PointerClick.canceled -= HandlePointerRelease;
        playerInputActions.Gameplay.PointerPosition.performed -= HandlePointerPosition;

        // Disable the Action Map
        playerInputActions.Gameplay.Disable();
    }

    private void HandlePointerClick(InputAction.CallbackContext context)
    {
        // Read the pointer position when the click occurs
        Vector2 screenPosition = playerInputActions.Gameplay.PointerPosition.ReadValue<Vector2>();

        // Debug.Log($"Pointer Click detected at screen position: {screenPosition}, World Position: {worldPosition}");
        pendingClick = screenPosition;
        IsPointerHeld = true;

    }

    private void HandlePointerRelease(InputAction.CallbackContext context)
    {
        IsPointerHeld = false;
        OnPointerReleased?.Invoke(playerInputActions.Gameplay.PointerPosition.ReadValue<Vector2>());
    }

    private void HandlePointerPosition(InputAction.CallbackContext context)
    {
        Vector2 screenPosition = context.ReadValue<Vector2>(); // Read directly from context for continuous actions
        // Optional: Log only occasionally or remove for performance
        // Debug.Log($"Pointer Position Update: {screenPosition}");

        // Invoke the event for continuous position updates
        OnPointerPositionUpdate?.Invoke(screenPosition);
    }

    // Helper function to get the current pointer position directly if needed
    public Vector2 GetPointerPosition()
    {
        return playerInputActions.Gameplay.PointerPosition.ReadValue<Vector2>();
    }
}