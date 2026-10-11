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
            RaycastHit2D hit = Physics2D.Raycast(worldPosition, Vector2.zero);

            if (EventSystem.current.IsPointerOverGameObject())
            {
                //Debug.Log("Clicked on UI — skip placing.");
            }
            else if (hit.collider != null)
            {
                Debug.Log("Clicked on: " + hit.collider.gameObject.name);
                hit.collider.gameObject.GetComponent<Character>()?.OnClicked();
                hit.collider.gameObject.GetComponent<Building>()?.OnClicked();
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
        playerInputActions.Gameplay.PointerPosition.performed += HandlePointerPosition; // Use performed for continuous update
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent errors when disabled/destroyed
        playerInputActions.Gameplay.PointerClick.performed -= HandlePointerClick;
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