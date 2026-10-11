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

    private void Awake()
    {
        input = new PlayerInputActions();
        cam = Camera.main;
        targetPosition = transform.position;
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
