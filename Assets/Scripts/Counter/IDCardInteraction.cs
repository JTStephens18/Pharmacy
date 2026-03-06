using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach to the ID card prefab. Handles:
/// 1. Player pressing E to enter focus mode (zoom camera to the ID card)
/// 2. Player clicking the barcode area to scan the ID
/// 3. Showing a highlight outline when hovering over the barcode
/// 4. Sending scanned data to NPCInfoDisplay to update the computer screen
///
/// Detected by ObjectPickup via raycast, same as ComputerScreen / PillCountingStation.
///
/// Multiplayer: server-authoritative exclusive-access lock via NetworkVariable.
/// Only one player can focus on the card at a time.
/// </summary>
public class IDCardInteraction : NetworkBehaviour
{
    [Header("Barcode Zone")]
    [Tooltip("Child collider covering the barcode area of the ID card. Player clicks this to scan.")]
    [SerializeField] private Collider barcodeZone;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Sound played when the barcode is successfully scanned.")]
    [SerializeField] private AudioClip scanSound;

    [Header("Scan Feedback")]
    [Tooltip("Optional visual effect spawned at the barcode when scanned (e.g., a scan line flash).")]
    [SerializeField] private GameObject scanEffectPrefab;
    [Tooltip("How long to wait after scanning before auto-exiting focus mode.")]
    [SerializeField] private float autoExitDelay = 1.0f;

    [Header("Focus Settings")]
    [Tooltip("Distance above the ID card for the auto-generated focus camera position.")]
    [SerializeField] private float focusHeight = 0.3f;
    [Tooltip("How far back from the card center the camera should sit.")]
    [SerializeField] private float focusDistance = 0.15f;

    [Header("Hover Highlight")]
    [Tooltip("Color of the highlight outline when hovering over the barcode.")]
    [SerializeField] private Color highlightColor = new Color(0f, 1f, 0.5f, 0.9f);
    [Tooltip("Width of the highlight outline.")]
    [SerializeField] private float highlightWidth = 0.003f;

    // ── Networked Lock ────────────────────────────────────────────────
    private const ulong NoUser = ulong.MaxValue;

    private readonly NetworkVariable<ulong> _currentUserId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Runtime State ───────────────────────────────────────────────
    private NPCIdentity _identity;
    private Transform _focusCameraTarget;
    private bool _isActive;
    private bool _hasBeenScanned;
    private float _autoExitTimer;
    private Camera _cachedCamera;
    private LineRenderer _highlightRenderer;
    private bool _isHovering;

    /// <summary>Whether the player is currently focused on this ID card.</summary>
    public bool IsActive => _isActive;

    /// <summary>Whether this card's barcode has been scanned.</summary>
    public bool HasBeenScanned => _hasBeenScanned;

    /// <summary>The NPC identity data on this card.</summary>
    public NPCIdentity Identity => _identity;

    /// <summary>
    /// Called by IDCardSlot after spawning to provide identity data and camera target.
    /// If focusCameraTarget is null, one will be auto-generated above the card.
    /// </summary>
    public void Initialize(NPCIdentity identity, Transform focusCameraTarget)
    {
        _identity = identity;
        _focusCameraTarget = focusCameraTarget;

        if (_identity == null)
            Debug.LogWarning("[IDCardInteraction] Initialized with null NPCIdentity!");

        // Auto-generate a focus camera target if none was provided
        if (_focusCameraTarget == null)
        {
            CreateAutoFocusTarget();
        }

        // Populate physical card visuals (photo + printed name)
        IDCardVisuals visuals = GetComponent<IDCardVisuals>();
        if (visuals == null)
            visuals = GetComponentInChildren<IDCardVisuals>();

        if (visuals != null && identity != null)
            visuals.Initialize(identity);
    }

    void Start()
    {
        // Create the highlight renderer for the barcode zone
        if (barcodeZone != null)
        {
            CreateHighlightRenderer();
        }
    }

    /// <summary>
    /// Creates a focus camera target Transform above the ID card, looking down at it.
    /// Used as a fallback when IDCardSlot doesn't have a focusCameraTarget assigned.
    /// </summary>
    private void CreateAutoFocusTarget()
    {
        GameObject focusObj = new GameObject("IDCard_AutoFocusTarget");
        focusObj.transform.SetParent(transform);
        // Position above and slightly in front of the card
        focusObj.transform.localPosition = new Vector3(0f, focusHeight, -focusDistance);
        // Look down at the card
        focusObj.transform.LookAt(transform.position, transform.up);
        _focusCameraTarget = focusObj.transform;
        Debug.Log("[IDCardInteraction] Auto-generated focus camera target above ID card.");
    }

    // ── Highlight Outline ─────────────────────────────────────────

    /// <summary>
    /// Creates a LineRenderer that draws an outline around the barcode zone's bounding box.
    /// </summary>
    private void CreateHighlightRenderer()
    {
        GameObject highlightObj = new GameObject("BarcodeHighlight");
        highlightObj.transform.SetParent(barcodeZone.transform);
        highlightObj.transform.localPosition = Vector3.zero;
        highlightObj.transform.localRotation = Quaternion.identity;
        highlightObj.transform.localScale = Vector3.one;

        _highlightRenderer = highlightObj.AddComponent<LineRenderer>();
        _highlightRenderer.useWorldSpace = true;
        _highlightRenderer.loop = true;
        _highlightRenderer.startWidth = highlightWidth;
        _highlightRenderer.endWidth = highlightWidth;
        _highlightRenderer.positionCount = 4;

        // Use a simple unlit material
        _highlightRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _highlightRenderer.startColor = highlightColor;
        _highlightRenderer.endColor = highlightColor;

        // Start hidden
        _highlightRenderer.enabled = false;
    }

    /// <summary>
    /// Updates the highlight outline to match the barcode collider's world-space bounding box.
    /// </summary>
    private void UpdateHighlightPositions()
    {
        if (_highlightRenderer == null || barcodeZone == null) return;

        Bounds bounds = barcodeZone.bounds;

        // Get the 4 corners of the top face of the bounding box
        // (the barcode is likely a flat surface, so we use the top face)
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        // Determine which axis is the "thin" one (the collider's flat direction)
        // and draw the outline on the other two axes
        float minExtent = Mathf.Min(extents.x, extents.y, extents.z);

        Vector3 c0, c1, c2, c3;

        if (Mathf.Approximately(minExtent, extents.y) || extents.y <= extents.x && extents.y <= extents.z)
        {
            // Flat along Y — draw on XZ plane (top-down view, most common for a card on a counter)
            float y = center.y + extents.y + 0.001f; // Slightly above
            c0 = new Vector3(center.x - extents.x, y, center.z - extents.z);
            c1 = new Vector3(center.x + extents.x, y, center.z - extents.z);
            c2 = new Vector3(center.x + extents.x, y, center.z + extents.z);
            c3 = new Vector3(center.x - extents.x, y, center.z + extents.z);
        }
        else if (Mathf.Approximately(minExtent, extents.z) || extents.z <= extents.x)
        {
            // Flat along Z — draw on XY plane
            float z = center.z + extents.z + 0.001f;
            c0 = new Vector3(center.x - extents.x, center.y - extents.y, z);
            c1 = new Vector3(center.x + extents.x, center.y - extents.y, z);
            c2 = new Vector3(center.x + extents.x, center.y + extents.y, z);
            c3 = new Vector3(center.x - extents.x, center.y + extents.y, z);
        }
        else
        {
            // Flat along X — draw on YZ plane
            float x = center.x + extents.x + 0.001f;
            c0 = new Vector3(x, center.y - extents.y, center.z - extents.z);
            c1 = new Vector3(x, center.y + extents.y, center.z - extents.z);
            c2 = new Vector3(x, center.y + extents.y, center.z + extents.z);
            c3 = new Vector3(x, center.y - extents.y, center.z + extents.z);
        }

        _highlightRenderer.SetPosition(0, c0);
        _highlightRenderer.SetPosition(1, c1);
        _highlightRenderer.SetPosition(2, c2);
        _highlightRenderer.SetPosition(3, c3);
    }

    private void SetHighlightVisible(bool visible)
    {
        if (_highlightRenderer != null)
        {
            _highlightRenderer.enabled = visible;
            if (visible) UpdateHighlightPositions();
        }
    }

    // ── Camera Helper ───────────────────────────────────────────────

    /// <summary>
    /// Gets the active camera, handling the case where Camera.main is null
    /// because FocusStateManager unparents the camera during focus.
    /// </summary>
    private Camera GetActiveCamera()
    {
        if (_cachedCamera != null) return _cachedCamera;

        PlayerComponents pc = PlayerComponents.Local;
        _cachedCamera = pc != null ? pc.PlayerCamera : null;

        return _cachedCamera;
    }

    // ── Core Logic ──────────────────────────────────────────────────

    void Update()
    {
        if (!_isActive) return;

        // Hover detection for barcode highlight
        bool hoveringBarcode = IsMouseOverBarcode();
        if (hoveringBarcode != _isHovering)
        {
            _isHovering = hoveringBarcode;
            SetHighlightVisible(_isHovering && !_hasBeenScanned);
        }

        // Handle auto-exit after scan
        if (_hasBeenScanned && _autoExitTimer > 0f)
        {
            _autoExitTimer -= Time.deltaTime;
            if (_autoExitTimer <= 0f)
            {
                Deactivate();
                return;
            }
        }

        // Detect barcode click (left mouse button)
        if (!_hasBeenScanned && Input.GetMouseButtonDown(0))
        {
            TryBarcodeScan();
        }
    }

    /// <summary>
    /// Checks if the mouse is currently hovering over the barcode zone.
    /// </summary>
    private bool IsMouseOverBarcode()
    {
        if (barcodeZone == null) return false;

        Camera cam = GetActiveCamera();
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // Use QueryTriggerInteraction.Collide so trigger colliders are detected
        RaycastHit[] hits = Physics.RaycastAll(ray, 5f, ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == barcodeZone || hit.collider.transform.IsChildOf(barcodeZone.transform))
            {
                return true;
            }
        }

        return false;
    }

    // ── Public API (called by ObjectPickup) ───────────────────────────

    /// <summary>
    /// Called by ObjectPickup when the player presses E while looking at this ID card.
    /// Routes through the server lock in multiplayer; activates directly in single-player.
    /// </summary>
    public void Activate()
    {
        if (_isActive) return;

        if (!IsSpawned)
        {
            DoActivate();
            return;
        }

        if (_currentUserId.Value != NoUser) return; // Card already being viewed

        RequestActivationServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    /// <summary>
    /// Cleans up and returns to normal state. Also releases the server-side lock.
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive) return;

        DoDeactivate();

        if (IsSpawned)
            ReleaseActivationServerRpc();
    }

    // ── Networked Lock RPCs ───────────────────────────────────────────

    [ServerRpc(RequireOwnership = false)]
    private void RequestActivationServerRpc(ulong requestingClientId)
    {
        if (_currentUserId.Value != NoUser) return;

        _currentUserId.Value = requestingClientId;
        ActivateClientRpc(requestingClientId);
    }

    [ClientRpc]
    private void ActivateClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;
        DoActivate();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReleaseActivationServerRpc()
    {
        _currentUserId.Value = NoUser;
    }

    // ── Internal Activate / Deactivate ───────────────────────────────

    private void DoActivate()
    {
        PlayerComponents pc = PlayerComponents.Local;
        FocusStateManager focus = pc != null ? pc.FocusState : null;
        if (focus == null)
        {
            Debug.LogError("[IDCardInteraction] Cannot activate: FocusStateManager not found!");
            if (IsSpawned) ReleaseActivationServerRpc();
            return;
        }

        if (_focusCameraTarget == null)
        {
            Debug.Log("[IDCardInteraction] No focus camera target — auto-generating one.");
            CreateAutoFocusTarget();
        }

        _isActive = true;
        _hasBeenScanned = false;
        _isHovering = false;

        _cachedCamera = pc.PlayerCamera;

        Debug.Log("[IDCardInteraction] Activating — entering focus mode on ID card.");

        focus.EnterFocus(_focusCameraTarget, OnFocusExited);
    }

    private void DoDeactivate()
    {
        _isActive = false;
        _isHovering = false;
        SetHighlightVisible(false);
        _cachedCamera = null;

        Debug.Log("[IDCardInteraction] Deactivating — exiting ID card focus.");

        PlayerComponents pc = PlayerComponents.Local;
        FocusStateManager focus = pc != null ? pc.FocusState : null;
        if (focus != null && focus.IsFocused)
            focus.ExitFocus();
    }

    // ── Focus Callback ────────────────────────────────────────────────

    private void OnFocusExited()
    {
        Deactivate();
    }

    /// <summary>
    /// Attempts to scan the barcode by raycasting from the mouse position.
    /// </summary>
    private void TryBarcodeScan()
    {
        if (barcodeZone == null)
        {
            Debug.LogWarning("[IDCardInteraction] No barcode zone collider assigned! Scanning anyway.");
            OnBarcodeScanned();
            return;
        }

        Camera cam = GetActiveCamera();
        if (cam == null)
        {
            Debug.LogError("[IDCardInteraction] No camera found for barcode raycast!");
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // Use RaycastAll with QueryTriggerInteraction.Collide to detect trigger colliders
        RaycastHit[] hits = Physics.RaycastAll(ray, 5f, ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == barcodeZone || hit.collider.transform.IsChildOf(barcodeZone.transform))
            {
                Debug.Log("[IDCardInteraction] Barcode zone hit detected!");
                OnBarcodeScanned();
                return;
            }
        }

        Debug.Log("[IDCardInteraction] Click detected but did not hit barcode zone.");
    }

    /// <summary>
    /// Called when the barcode is successfully scanned.
    /// Sends NPC data to the computer screen and triggers feedback.
    /// </summary>
    private void OnBarcodeScanned()
    {
        if (_hasBeenScanned) return;
        _hasBeenScanned = true;

        // Hide hover highlight after scan
        SetHighlightVisible(false);

        Debug.Log($"[IDCardInteraction] Barcode scanned! NPC: {_identity?.fullName ?? "Unknown"}");

        // Play scan sound
        if (audioSource != null && scanSound != null)
        {
            audioSource.PlayOneShot(scanSound);
        }

        // Spawn scan effect
        if (scanEffectPrefab != null && barcodeZone != null)
        {
            GameObject effect = Instantiate(scanEffectPrefab, barcodeZone.transform.position, Quaternion.identity);
            Destroy(effect, 2f); // Auto-cleanup
        }

        // Send data to computer screen
        if (_identity != null && NPCInfoDisplay.Instance != null)
        {
            NPCInfoDisplay.Instance.ShowNPCInfo(_identity);
        }
        else
        {
            if (_identity == null)
                Debug.LogWarning("[IDCardInteraction] Cannot display info: no NPCIdentity assigned.");
            if (NPCInfoDisplay.Instance == null)
                Debug.LogWarning("[IDCardInteraction] Cannot display info: NPCInfoDisplay not found in scene.");
        }

        // Start auto-exit timer
        _autoExitTimer = autoExitDelay;
    }
}
