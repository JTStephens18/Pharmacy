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

    [Tooltip("Assign a BoxCollider (Is Trigger) that defines the area the scraper can move within.")]
    [SerializeField] private BoxCollider trayBoundsCollider;

    [Header("Screen-to-Tray Mapping")]
    [Tooltip("Screen Y fraction (from bottom) that maps to the near edge of the tray.")]
    [SerializeField, Range(0f, 1f)] private float screenBottomFraction = 0.333f;
    [Tooltip("Screen Y fraction (from bottom) that maps to the far edge of the tray.")]
    [SerializeField, Range(0f, 1f)] private float screenTopFraction = 0.75f;
    [Tooltip("Screen X fraction (from left) that maps to the left edge of the tray.")]
    [SerializeField, Range(0f, 1f)] private float screenLeftFraction = 0.15f;
    [Tooltip("Screen X fraction (from left) that maps to the right edge of the tray.")]
    [SerializeField, Range(0f, 1f)] private float screenRightFraction = 0.85f;

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
    private float _yVelocity;
    private Vector3 _lastPosition;
    private Vector3 _velocity;
    private Quaternion _targetTilt;
    private bool _isContact;
    private bool _ready;
    private float _baseY;

    // Reference point: tray center in world space (set by station on activation)
    private Vector3 _trayCenter;
    // Target XZ position from mouse
    private Vector3 _targetXZ;

    // Cached world-space corners for screen-to-tray mapping
    private Vector3 _cornerBL, _cornerBR, _cornerTL, _cornerTR;
    private bool _cornersComputed;

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
        _ready = false;
        _cornersComputed = false;

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
            return; // Skip this frame so we don't jump
        }

        HandleMovement();
        HandleVerticalState();
        HandleTilt();
        HandleGhost();
    }

    private void HandleMovement()
    {
        if (_camera == null) return;

        // Compute reference corners once (raycasts from screen-fraction positions onto tray plane)
        if (!_cornersComputed)
            ComputeScreenCorners();

        if (_cornersComputed)
        {
            // Map mouse screen position to [0,1] within the defined screen zone
            float normX = Input.mousePosition.x / Screen.width;
            float normY = Input.mousePosition.y / Screen.height;

            float tX = Mathf.Clamp01(Mathf.InverseLerp(screenLeftFraction, screenRightFraction, normX));
            float tY = Mathf.Clamp01(Mathf.InverseLerp(screenBottomFraction, screenTopFraction, normY));

            // Bilinear interpolation across the four world-space corners
            Vector3 bottom = Vector3.Lerp(_cornerBL, _cornerBR, tX);
            Vector3 top = Vector3.Lerp(_cornerTL, _cornerTR, tX);
            _targetXZ = Vector3.Lerp(bottom, top, tY);

            // Safety clamp to bounding box
            if (trayBoundsCollider != null)
            {
                Bounds b = trayBoundsCollider.bounds;
                _targetXZ.x = Mathf.Clamp(_targetXZ.x, b.min.x, b.max.x);
                _targetXZ.z = Mathf.Clamp(_targetXZ.z, b.min.z, b.max.z);
            }
        }
        else
        {
            // Fallback: raw raycast onto tray plane
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (_trayPlane.Raycast(ray, out float distance))
                _targetXZ = ray.GetPoint(distance);
        }

        // Follow the mouse target directly
        Vector3 targetPos = new Vector3(_targetXZ.x, _baseY + _currentY, _targetXZ.z);

        // Smoothly move toward mouse target
        Vector3 newPos = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

        // Track velocity for tilt
        _velocity = (newPos - _lastPosition) / Mathf.Max(Time.deltaTime, 0.001f);
        _lastPosition = newPos;

        // Move scraper directly (not via Rigidbody, which delays until FixedUpdate)
        transform.position = newPos;
    }

    /// <summary>
    /// Raycasts from the four screen-fraction corners onto the tray plane
    /// to establish the world-space quad that the screen zone maps to.
    /// </summary>
    private void ComputeScreenCorners()
    {
        // Use the bounding box center Y for the tray plane (much more accurate
        // than _trayCenter.y which can come from a parent far below the tray surface)
        if (trayBoundsCollider != null)
        {
            float planeY = trayBoundsCollider.bounds.center.y;
            _trayPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        }

        _cornerBL = ScreenFractionToTray(screenLeftFraction, screenBottomFraction);
        _cornerBR = ScreenFractionToTray(screenRightFraction, screenBottomFraction);
        _cornerTL = ScreenFractionToTray(screenLeftFraction, screenTopFraction);
        _cornerTR = ScreenFractionToTray(screenRightFraction, screenTopFraction);
        _cornersComputed = true;
    }

    private Vector3 ScreenFractionToTray(float fracX, float fracY)
    {
        Vector3 screenPos = new Vector3(fracX * Screen.width, fracY * Screen.height, 0f);
        Ray ray = _camera.ScreenPointToRay(screenPos);
        if (_trayPlane.Raycast(ray, out float dist))
            return ray.GetPoint(dist);
        return _trayCenter;
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
    /// Assign a BoxCollider to constrain scraper movement at runtime.
    /// </summary>
    public void SetTrayBoundsCollider(BoxCollider collider)
    {
        trayBoundsCollider = collider;
        if (trayBoundsCollider != null)
            trayBoundsCollider.isTrigger = true;
    }

    void OnDrawGizmosSelected()
    {
        if (trayBoundsCollider != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireCube(trayBoundsCollider.bounds.center, trayBoundsCollider.bounds.size);
        }
    }
}
