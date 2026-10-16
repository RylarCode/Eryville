using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float dragSpeed = 0.5f;
    public float zoomSpeed = 5f;
    public Vector2 zoomLimits = new Vector2(5f, 20f);

    private Vector2 dragOrigin;
    private Vector3 targetPosition;
    private Camera cam;
    private PlayerInputActions input;
    private Vector2 moveInput;
    private bool isDragging;

    // The camera rig that the player moves around; anything that wants to look somewhere asks it
    public static CameraController Instance { get; private set; }

    // Jumps the camera so the given world position is in the middle of the view
    public void FocusOn(Vector3 world)
    {
        FocusOn(world, 0f);
    }

    // Same, but the target ends up higher on the screen: screenLift is the share of the screen height above the centre
    // (0.2 puts it 20% of the screen above the middle, clear of a menu sheet covering the bottom of the screen)
    public void FocusOn(Vector3 world, float screenLift)
    {
        if (cam == null) cam = Camera.main;
        float lift = cam != null ? screenLift * 2f * cam.orthographicSize : 0f;
        transform.position = new Vector3(world.x, world.y - lift, transform.position.z);
    }

    private void Awake()
    {
        Instance = this;
        input = new PlayerInputActions();
        cam = Camera.main;
        targetPosition = transform.position;
        if (GetComponent<TouchCameraControls>() == null) gameObject.AddComponent<TouchCameraControls>(); // drag / pinch on touch screens
    }

    // Moves the view so the world follows the fingers: dragging right by N pixels shows what is N pixels further left
    public void PanByPixels(Vector2 pixels)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || Screen.height <= 0) return;

        float worldPerPixel = 2f * cam.orthographicSize / Screen.height;
        transform.position += new Vector3(-pixels.x * worldPerPixel, -pixels.y * worldPerPixel, 0f);
    }

    // Zooms in when ratio is above 1 (the fingers moved apart), out when below
    public void ZoomBy(float ratio)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || ratio <= 0f) return;

        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize / ratio, zoomLimits.x, zoomLimits.y);
    }

    private void OnEnable()
    {
        input.Enable();
        input.Camera.CameraMove.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Camera.CameraMove.canceled += _ => moveInput = Vector2.zero;

        input.Camera.CameraDragButton.performed += _ => StartDrag();
        input.Camera.CameraDragButton.canceled += _ => EndDrag();

        input.Camera.CameraZoom.performed += ctx => Zoom(ctx.ReadValue<float>());
    }

    private void OnDisable()
    {
        input.Disable();
    }

    private void Update()
    {
        HandleMovement();
        HandleMouseDrag();
    }

    private void HandleMovement()
    {
        if (moveInput != Vector2.zero)
        {
            Vector3 move = new Vector3(moveInput.x, moveInput.y, 0f) * moveSpeed * Time.deltaTime;
            transform.position += move;
        }
    }

    private void StartDrag()
    {
        isDragging = true;
        dragOrigin = Mouse.current.position.ReadValue();
    }

    private void EndDrag()
    {
        isDragging = false;
    }

    private void HandleMouseDrag()
    {
        if (isDragging)
        {
            Vector2 currentMousePos = Mouse.current.position.ReadValue();
            Vector2 delta = currentMousePos - dragOrigin;

            Vector3 move = new Vector3(-delta.x, -delta.y, 0) * dragSpeed * Time.deltaTime;
            transform.Translate(move);
            dragOrigin = currentMousePos;
        }
    }

    private void Zoom(float scroll)
    {
        float size = cam.orthographicSize;
        size -= scroll * zoomSpeed * Time.deltaTime;
        size = Mathf.Clamp(size, zoomLimits.x, zoomLimits.y);
        cam.orthographicSize = size;
    }
}
