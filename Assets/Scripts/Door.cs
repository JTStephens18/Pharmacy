using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach to a door GameObject. The player must look at it and press E within range.
/// Rotates <see cref="pivotTransform"/> (or this transform) between closed and open angles.
/// Fully multiplayer-safe: server owns the open/closed state via NetworkVariable;
/// all clients animate locally via OnValueChanged.
///
/// Editor setup:
/// 1. Add this component to the door mesh root (or a parent pivot object).
/// 2. Set <see cref="pivotTransform"/> to the child transform that should physically rotate.
///    Leave null to rotate this object itself.
/// 3. Tune <see cref="openAngle"/> (degrees) and <see cref="rotationAxis"/> (local space).
/// 4. Add a Collider somewhere in the hierarchy so ObjectPickup raycasts can hit it.
/// 5. Add NetworkObject to this GameObject and register it in NetworkManager's prefab list
///    (or mark it as an in-scene placed NetworkObject in the scene).
/// 6. Optional: assign <see cref="highlightObject"/> to a child highlight mesh (disabled by default).
/// </summary>
public class Door : NetworkBehaviour
{
    [Header("Door Settings")]
    [Tooltip("How far the door swings open, in degrees.")]
    [SerializeField] private float openAngle = 90f;

    [Tooltip("Local-space axis around which the door pivots (default: up = Y-axis hinge).")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Tooltip("Time in seconds to animate between open and closed.")]
    [SerializeField] private float animationDuration = 0.4f;

    [Tooltip("Seconds the door stays open after the player looks away before closing.")]
    [SerializeField] private float closeDelay = 2f;

    [Header("References")]
    [Tooltip("The Transform that rotates (e.g. the door mesh child). Leave null to rotate this object.")]
    [SerializeField] private Transform pivotTransform;

    [Tooltip("Optional mesh/object to show when the player is looking at this door.")]
    [SerializeField] private GameObject highlightObject;

    // ── Network state ────────────────────────────────────────────────
    private readonly NetworkVariable<bool> _isOpen = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Local state ──────────────────────────────────────────────────
    private Quaternion _closedRotation;
    private Quaternion _openRotation;
    private Coroutine _animationCoroutine;
    private Coroutine _closeCoroutine;
    private bool _localIsOpen; // used only in non-spawned (editor) play mode

    public bool IsOpen => IsSpawned ? _isOpen.Value : _localIsOpen;

    // ── Lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        CacheRotations();
    }

    public override void OnNetworkSpawn()
    {
        // Re-cache in case Awake ran before the pivot's final transform was set
        CacheRotations();
        _isOpen.OnValueChanged += OnDoorStateChanged;

        // Snap to current network state without animation on late-join
        Transform pivot = pivotTransform != null ? pivotTransform : transform;
        pivot.localRotation = _isOpen.Value ? _openRotation : _closedRotation;
    }

    public override void OnNetworkDespawn()
    {
        _isOpen.OnValueChanged -= OnDoorStateChanged;
    }

    // ── Public API (called by ObjectPickup) ──────────────────────────

    /// <summary>Opens the door immediately and cancels any pending delayed close.</summary>
    public void Open()
    {
        if (_closeCoroutine != null) { StopCoroutine(_closeCoroutine); _closeCoroutine = null; }

        if (IsSpawned)
        {
            if (!_isOpen.Value) SetOpenServerRpc(true);
        }
        else
        {
            if (!_localIsOpen) { _localIsOpen = true; AnimateDoor(true); }
        }
    }

    /// <summary>Schedules the door to close after <see cref="closeDelay"/> seconds.</summary>
    public void Close()
    {
        if (_closeCoroutine != null) StopCoroutine(_closeCoroutine);
        _closeCoroutine = StartCoroutine(CloseAfterDelay());
    }

    private System.Collections.IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(closeDelay);
        _closeCoroutine = null;

        if (IsSpawned)
        {
            if (_isOpen.Value) SetOpenServerRpc(false);
        }
        else
        {
            if (_localIsOpen) { _localIsOpen = false; AnimateDoor(false); }
        }
    }

    public void ShowHighlight()
    {
        if (highlightObject != null) highlightObject.SetActive(true);
    }

    public void HideHighlight()
    {
        if (highlightObject != null) highlightObject.SetActive(false);
    }

    // ── Server RPC ───────────────────────────────────────────────────

    [ServerRpc(RequireOwnership = false)]
    private void SetOpenServerRpc(bool open)
    {
        _isOpen.Value = open;
    }

    // ── Callbacks ────────────────────────────────────────────────────

    private void OnDoorStateChanged(bool previous, bool current)
    {
        AnimateDoor(current);
    }

    // ── Animation ────────────────────────────────────────────────────

    private void AnimateDoor(bool opening)
    {
        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        _animationCoroutine = StartCoroutine(AnimateDoorCoroutine(opening));
    }

    private IEnumerator AnimateDoorCoroutine(bool opening)
    {
        Transform pivot = pivotTransform != null ? pivotTransform : transform;
        Quaternion startRot = pivot.localRotation;
        Quaternion targetRot = opening ? _openRotation : _closedRotation;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);
            pivot.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        pivot.localRotation = targetRot;
        _animationCoroutine = null;
    }

    private void CacheRotations()
    {
        Transform pivot = pivotTransform != null ? pivotTransform : transform;
        _closedRotation = pivot.localRotation;
        _openRotation = _closedRotation * Quaternion.AngleAxis(openAngle, rotationAxis);
    }
}
