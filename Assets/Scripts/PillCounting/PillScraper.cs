using UnityEngine;

/// <summary>
/// Mouse-driven kinematic scraper that pushes pills across the tray.
/// Uses screen-space raycasting to follow the mouse cursor on the tray plane.
/// Supports hover/contact vertical states, procedural tilt, and a ghost indicator.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PillScraper : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("How quickly the scraper follows the mouse target position.")]
    [SerializeField] private float followSpeed = 15f;
    [SerializeField] private Bounds trayBounds = new Bounds(Vector3.zero, new Vector3(2f, 0.5f, 2f));

    [Header("Vertical States")]
    [SerializeField] private float hoverHeight = 0.5f;
    [SerializeField] private float contactHeight = 0.05f;
    [SerializeField] private float verticalSmoothTime = 0.08f;

    [Header("Tilt")]
    [SerializeField] private float tiltAmount = 15f;
    [SerializeField] private float tiltSmoothSpeed = 8f;

    [Header("Ghost Indicator")]
    [Tooltip("Optional quad/projector shown at hover landing point.")]
    [SerializeField] private GameObject ghostIndicator;
    [SerializeField] private LayerMask trayLayerMask;

    [Header("Layer Collision")]
    [Tooltip("Layer index for 'Tool' layer.")]
    [SerializeField] private int toolLayerIndex = 8;
    [Tooltip("Layer index for 'Physics_Debris' layer (pills).")]
    [SerializeField] private int debrisLayerIndex = 9;

    private Rigidbody _rb;
    private Camera _camera;
    private Plane _trayPlane;
    private float _currentY;
    private int _debugFrameCount;
    private float _yVelocity;
    private Vector3 _lastPosition;
    private Vector3 _velocity;
    private Quaternion _targetTilt;
    private bool _isContact;
    private bool _ready;
    private float _baseY;

    // Reference point: tray center in world space (set by station on activation)
    private Vector3 _trayCenter;
    // Target XZ position from mouse raycast
    private Vector3 _targetXZ;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
    }

    void OnEnable()
    {
        _currentY = hoverHeight;
        _yVelocity = 0f;
        _isContact = false;
        _debugFrameCount = 0;
        _ready = false;

        // Keep the scraper at its current editor-placed position
        _lastPosition = transform.position;
        _targetXZ = transform.position;

        // Use parent position as tray center if available, otherwise own position
        if (_trayCenter == Vector3.zero)
            _trayCenter = transform.parent != null ? transform.parent.position : transform.position;

        // Find the camera
        _camera = Camera.main;
        if (_camera == null) _camera = FindFirstObjectByType<Camera>();

        // Create a horizontal plane at the tray surface height for mouse raycasting
        _trayPlane = new Plane(Vector3.up, new Vector3(0f, _trayCenter.y, 0f));

        // Start with no collision between tool and pills
        Physics.IgnoreLayerCollision(toolLayerIndex, debrisLayerIndex, true);

        if (ghostIndicator != null) ghostIndicator.SetActive(true);

        Debug.Log($"[PillScraper] OnEnable - Camera: {(_camera != null ? _camera.name : "NULL")} " +
            $"| TrayCenter: {_trayCenter} | ScraperPos: {transform.position} | GO active: {gameObject.activeInHierarchy}");
    }

    void OnDisable()
    {
        // Restore collision state
        Physics.IgnoreLayerCollision(toolLayerIndex, debrisLayerIndex, true);
        if (ghostIndicator != null) ghostIndicator.SetActive(false);
    }

    void Update()
    {
        // Don't process any movement until focus mode is fully active
        var focus = FocusStateManager.Instance;
        if (focus == null || !focus.IsFocused || focus.IsTransitioning)
            return;

        // On the first active frame, initialize everything from current position
        if (!_ready)
        {
            _ready = true;
            _baseY = transform.position.y;
            _currentY = 0f; // Start at current height (no offset)
            _lastPosition = transform.position;
            _targetXZ = transform.position;
            Debug.Log($"[PillScraper] Ready! BaseY: {_baseY} | Pos: {transform.position}");
            return; // Skip this frame so we don't jump
        }

        _debugFrameCount++;
        if (_debugFrameCount <= 3)
        {
            Debug.Log($"[PillScraper] Update frame {_debugFrameCount} - " +
                $"Pos: {transform.position} | Camera: {(_camera != null ? _camera.name : "NULL")} " +
                $"| MousePos: {Input.mousePosition}");
        }

        HandleMovement();
        HandleVerticalState();
        HandleTilt();
        HandleGhost();
    }

    private void HandleMovement()
    {
        if (_camera == null)
        {
            if (_debugFrameCount <= 3)
                Debug.LogWarning("[PillScraper] HandleMovement: _camera is NULL, cannot move!");
            return;
        }

        // Raycast from mouse position onto the tray plane
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        bool hitPlane = _trayPlane.Raycast(ray, out float distance);

        if (hitPlane)
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            _targetXZ = hitPoint;
        }

        if (_debugFrameCount <= 3)
        {
            Debug.Log($"[PillScraper] HandleMovement: PlaneHit={hitPlane} | TargetXZ={_targetXZ}");
        }

        // Follow the mouse target directly (tray walls provide physical constraint)
        Vector3 targetPos = new Vector3(_targetXZ.x, _baseY + _currentY, _targetXZ.z);

        // Smoothly move toward mouse target
        Vector3 newPos = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

        // Track velocity for tilt
        _velocity = (newPos - _lastPosition) / Mathf.Max(Time.deltaTime, 0.001f);
        _lastPosition = newPos;

        // Move scraper directly (not via Rigidbody, which delays until FixedUpdate)
        transform.position = newPos;
    }

    private void HandleVerticalState()
    {
        bool wantsContact = Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space);
        float targetY = wantsContact ? contactHeight : hoverHeight;

        _currentY = Mathf.SmoothDamp(_currentY, targetY, ref _yVelocity, verticalSmoothTime);

        // Toggle collision when crossing threshold
        bool wasContact = _isContact;
        _isContact = _currentY < (hoverHeight + contactHeight) * 0.5f;

        if (_isContact != wasContact)
        {
            // Enable collisions between tool and pills only when in contact
            Physics.IgnoreLayerCollision(toolLayerIndex, debrisLayerIndex, !_isContact);
        }
    }

    private void HandleTilt()
    {
        // Tilt based on horizontal velocity
        _targetTilt = Quaternion.Euler(
            _velocity.z * tiltAmount * 0.01f,
            0f,
            -_velocity.x * tiltAmount * 0.01f
        );

        transform.rotation = Quaternion.Slerp(transform.rotation, _targetTilt, Time.deltaTime * tiltSmoothSpeed);
    }

    private void HandleGhost()
    {
        if (ghostIndicator == null) return;

        if (_isContact)
        {
            ghostIndicator.SetActive(false);
            return;
        }

        // Raycast down to show where scraper would land
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, hoverHeight + 0.5f, trayLayerMask))
        {
            ghostIndicator.SetActive(true);
            ghostIndicator.transform.position = hit.point + Vector3.up * 0.005f;
        }
        else
        {
            ghostIndicator.SetActive(false);
        }
    }

    /// <summary>
    /// Call from PillCountingStation to set the tray center reference.
    /// </summary>
    public void SetTrayCenter(Vector3 center)
    {
        _trayCenter = center;
        _trayPlane = new Plane(Vector3.up, new Vector3(0f, center.y, 0f));
    }

    /// <summary>
    /// Update tray bounds at runtime if needed.
    /// </summary>
    public void SetTrayBounds(Bounds bounds)
    {
        trayBounds = bounds;
    }
}
