using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Portable container that holds a fixed number of items for shelf restocking.
/// Items are category-agnostic — the ItemPlacementManager decides what to spawn.
/// Attach to a pickupable object (requires Rigidbody + NetworkObject).
///
/// Networked behaviour:
///   - _networkRemainingItems and _networkIsDestroying are server-authoritative.
///   - Decrement() is safe to call only on the server (or in the non-spawned fallback path).
///   - When the box empties, the server sends ShrinkAndForceDropClientRpc() so all clients
///     play the shrink animation and the holder force-drops it, then the server despawns.
///   - Visual open/close animations are local-only (only the holding player sees them).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class InventoryBox : NetworkBehaviour
{
    [Header("Inventory")]
    [Tooltip("Total number of items this box can dispense before being destroyed.")]
    [SerializeField] private int totalItems = 8;

    [Header("Box Visuals")]
    [Tooltip("Reference to the closed box child mesh.")]
    [SerializeField] private GameObject closedModel;

    [Tooltip("Reference to the open box child mesh.")]
    [SerializeField] private GameObject openModel;

    [Tooltip("Duration of the open/close scale animation.")]
    [SerializeField] private float openCloseDuration = 0.3f;

    [Header("Shrink Animation")]
    [Tooltip("Duration of the shrink animation when the box is emptied.")]
    [SerializeField] private float shrinkDuration = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool logOperations = false;

    // ── Networked State ───────────────────────────────────────────────
    // Server-authoritative. All clients can read these values.

    private readonly NetworkVariable<int> _networkRemainingItems = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> _networkIsDestroying = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Local Fallback State ──────────────────────────────────────────
    // Used when the box is not yet spawned as a NetworkObject (editor / pre-networking path).

    private int _localRemainingItems;
    private bool _localIsDestroying;

    // ── Visual State (local only) ─────────────────────────────────────
    // Only the holding player triggers open/close — no need to sync.

    private bool _isOpen;
    private Coroutine _openCloseCoroutine;
    private Vector3 _closedModelOriginalScale;
    private Vector3 _openModelOriginalScale;

    public bool IsOpen => _isOpen;

    // ─────────────────────────────────────────────────────────────────
    // Unity / NGO lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _localRemainingItems = totalItems;

        if (closedModel != null) _closedModelOriginalScale = closedModel.transform.localScale;
        if (openModel != null)   _openModelOriginalScale   = openModel.transform.localScale;

        if (closedModel != null) closedModel.SetActive(true);
        if (openModel != null)   openModel.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _networkRemainingItems.Value = totalItems;
            _networkIsDestroying.Value   = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Stock queries — read NetworkVariables when spawned, local fields otherwise
    // ─────────────────────────────────────────────────────────────────

    public bool HasAnyStock()
    {
        if (!IsSpawned)
            return _localRemainingItems > 0 && !_localIsDestroying;

        return _networkRemainingItems.Value > 0 && !_networkIsDestroying.Value;
    }

    public int GetRemainingCount()
    {
        if (!IsSpawned) return _localRemainingItems;
        return _networkRemainingItems.Value;
    }

    public int GetTotalCapacity() => totalItems;

    // ─────────────────────────────────────────────────────────────────
    // Decrement
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes one item from the box.
    /// Networked: only the server executes this — call from PlaceItemOnShelfServerRpc.
    /// Non-networked fallback: runs locally for editor/pre-networking testing.
    /// </summary>
    public void Decrement()
    {
        if (!IsSpawned)
        {
            // ── Non-networked fallback ────────────────────────────────
            if (_localRemainingItems <= 0 || _localIsDestroying) return;

            _localRemainingItems--;

            if (logOperations)
                Debug.Log($"[InventoryBox] Decremented (local). Remaining: {_localRemainingItems}/{totalItems}");

            if (_localRemainingItems <= 0)
                StartCoroutine(ShrinkAndDestroyLocal());

            return;
        }

        // ── Networked path ────────────────────────────────────────────
        if (!IsServer) return;
        if (_networkRemainingItems.Value <= 0 || _networkIsDestroying.Value) return;

        _networkRemainingItems.Value--;

        if (logOperations)
            Debug.Log($"[InventoryBox] Decremented. Remaining: {_networkRemainingItems.Value}/{totalItems}");

        if (_networkRemainingItems.Value <= 0)
            StartCoroutine(ShrinkAndDestroyOnServer());
    }

    // ─────────────────────────────────────────────────────────────────
    // Networked shrink & despawn
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Server-side: notifies all clients to play the shrink animation and force-drop
    /// if held, then waits for the animation to finish before despawning.
    /// </summary>
    private IEnumerator ShrinkAndDestroyOnServer()
    {
        _networkIsDestroying.Value = true;

        if (logOperations)
            Debug.Log("[InventoryBox] Box empty — starting networked shrink.");

        // Runs on all clients (and the host): plays visual shrink + force-drops if held
        ShrinkAndForceDropClientRpc();

        // Give clients enough time to finish the animation before the GO is destroyed
        yield return new WaitForSeconds(shrinkDuration + 0.2f);

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    /// <summary>
    /// Runs on every client when the box empties.
    /// Plays the shrink animation and force-drops the box if this client is holding it.
    /// </summary>
    [ClientRpc]
    private void ShrinkAndForceDropClientRpc()
    {
        // Force-drop on the client that is holding this box
        PlayerComponents pc = PlayerComponents.Local;
        ObjectPickup pickup  = pc != null ? pc.Pickup : null;
        if (pickup != null && pickup.GetHeldObject() == gameObject)
            pickup.ForceDropObject();

        StartCoroutine(ShrinkAnimation());
    }

    private IEnumerator ShrinkAnimation()
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shrinkDuration);
            transform.localScale = startScale * Mathf.SmoothStep(1f, 0f, t);
            yield return null;
        }

        transform.localScale = Vector3.zero;
    }

    // ─────────────────────────────────────────────────────────────────
    // Local fallback shrink & destroy (non-networked path)
    // ─────────────────────────────────────────────────────────────────

    private IEnumerator ShrinkAndDestroyLocal()
    {
        _localIsDestroying = true;

        if (logOperations)
            Debug.Log("[InventoryBox] Box empty — starting local shrink.");

        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shrinkDuration);
            transform.localScale = startScale * Mathf.SmoothStep(1f, 0f, t);
            yield return null;
        }

        transform.localScale = Vector3.zero;

        PlayerComponents pc = PlayerComponents.Local;
        ObjectPickup pickup  = pc != null ? pc.Pickup : null;
        if (pickup != null && pickup.GetHeldObject() == gameObject)
            pickup.ForceDropObject();

        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────
    // Box visuals — local only, driven by ItemPlacementManager on the owner
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Transitions to the open visual state with a scale-up animation.
    /// </summary>
    public void OpenBox()
    {
        if (_isOpen) return;
        bool destroying = IsSpawned ? _networkIsDestroying.Value : _localIsDestroying;
        if (destroying) return;

        _isOpen = true;
        StopAndResetOpenClose();
        _openCloseCoroutine = StartCoroutine(AnimateOpenClose(open: true));
    }

    /// <summary>
    /// Transitions to the closed visual state with a scale-up animation.
    /// </summary>
    public void CloseBox()
    {
        if (!_isOpen) return;
        bool destroying = IsSpawned ? _networkIsDestroying.Value : _localIsDestroying;
        if (destroying) return;

        _isOpen = false;
        StopAndResetOpenClose();
        _openCloseCoroutine = StartCoroutine(AnimateOpenClose(open: false));
    }

    private void StopAndResetOpenClose()
    {
        if (_openCloseCoroutine != null)
        {
            StopCoroutine(_openCloseCoroutine);
            _openCloseCoroutine = null;
        }

        if (closedModel != null) closedModel.transform.localScale = _closedModelOriginalScale;
        if (openModel != null)   openModel.transform.localScale   = _openModelOriginalScale;
    }

    private IEnumerator AnimateOpenClose(bool open)
    {
        GameObject showModel  = open ? openModel   : closedModel;
        GameObject hideModel  = open ? closedModel : openModel;
        Vector3    targetScale = open ? _openModelOriginalScale : _closedModelOriginalScale;

        if (hideModel != null)
            hideModel.SetActive(false);

        if (showModel != null)
        {
            showModel.SetActive(true);
            showModel.transform.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < openCloseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / openCloseDuration));
                showModel.transform.localScale = targetScale * t;
                yield return null;
            }

            showModel.transform.localScale = targetScale;
        }

        _openCloseCoroutine = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (totalItems < 1) totalItems = 1;
    }
#endif
}
