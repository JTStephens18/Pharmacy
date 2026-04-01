using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The pill filling station where the player dispenses medication into a bottle.
/// Manages the full lifecycle: focus mode transition, hopper activation,
/// dispensing controller initialization, and cleanup.
///
/// Multiplayer: server-authoritative exclusive-access lock via NetworkVariable.
/// Only one player can use the station at a time. All dispensing state is local
/// to the using client (hopper rotation, gate input, pill count).
/// </summary>
public class PillFillingStation : NetworkBehaviour
{
    [Header("Focus Camera Position")]
    [Tooltip("Empty Transform positioned where the camera looks at the station during focus.")]
    [SerializeField] private Transform focusCameraTarget;

    [Header("Child Components")]
    [SerializeField] private RotatingHopper hopper;
    [SerializeField] private DispensingController dispensingController;
    [SerializeField] private FillCounterUI counterUI;

    [Tooltip("Where the bottle sits while pills are being dispensed.")]
    [SerializeField] private Transform bottleOutputPoint;
    [Tooltip("Used when no NPC prescription is loaded (e.g. testing). 0 = count-only mode.")]
    [SerializeField] private int fallbackTargetCount = 30;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip activateSound;

    // ── Networked Lock ──────────────────────────────────────────────
    private const ulong NoUser = ulong.MaxValue;

    private readonly NetworkVariable<ulong> _currentUserId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Local State ─────────────────────────────────────────────────
    private bool _isActive;
    private ObjectPickup _pendingPickup;
    private MedicationBottle _activeBottle;
    private ObjectPickup _activePickup;

    public bool IsActive => _isActive;

    /// <summary>True when another player is currently using this station.</summary>
    public bool IsInUse => IsSpawned && _currentUserId.Value != NoUser;

    /// <summary>The hopper component.</summary>
    public RotatingHopper Hopper => hopper;

    /// <summary>Last fill count when the player exited. -1 if station was never used.</summary>
    public int LastFillCount { get; private set; } = -1;

    void Awake()
    {
        if (hopper == null) hopper = GetComponentInChildren<RotatingHopper>(true);
        if (dispensingController == null) dispensingController = GetComponentInChildren<DispensingController>(true);
        if (counterUI == null) counterUI = GetComponentInChildren<FillCounterUI>(true);
    }

    // ── Public API (called by ObjectPickup) ─────────────────────────

    /// <summary>
    /// Activate the filling station. If the player is holding a medication bottle,
    /// it is consumed (set down) and the hopper auto-loads from the prescription.
    /// Routes through server lock in multiplayer.
    /// </summary>
    public void Activate(ObjectPickup pickup = null)
    {
        if (_isActive) return;

        _pendingPickup = pickup;

        if (!IsSpawned)
        {
            DoActivate();
            return;
        }

        if (_currentUserId.Value != NoUser) return;

        RequestActivationServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    /// <summary>
    /// Deactivate the station and release the server lock.
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive) return;

        DoDeactivate();

        if (IsSpawned)
            ReleaseActivationServerRpc();
    }

    // ── Networked Lock RPCs ─────────────────────────────────────────

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

    [ServerRpc(RequireOwnership = false)]
    private void DestroyBottleServerRpc(ulong networkObjectId)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
            netObj.Despawn(true);
    }

    /// <summary>
    /// Server-only: force-releases the lock if held by the given client (e.g. on disconnect).
    /// </summary>
    public void ForceReleaseLock(ulong clientId)
    {
        if (!IsServer) return;
        if (_currentUserId.Value == clientId)
            _currentUserId.Value = NoUser;
    }

    // ── Internal Activate / Deactivate ──────────────────────────────

    private void DoActivate()
    {
        PlayerComponents pc = PlayerComponents.Local;
        FocusStateManager focus = pc != null ? pc.FocusState : null;
        if (focus == null)
        {
            Debug.LogError("[PillFillingStation] Cannot activate: FocusStateManager not found!");
            if (IsSpawned) ReleaseActivationServerRpc();
            return;
        }

        if (focusCameraTarget == null)
        {
            Debug.LogError("[PillFillingStation] Cannot activate: focusCameraTarget not assigned!");
            if (IsSpawned) ReleaseActivationServerRpc();
            return;
        }

        // Place the bottle at the output point and store both it and the pickup reference.
        // On fill completion the bottle is returned directly to the player's hand.
        _activeBottle = null;
        _activePickup = _pendingPickup;
        if (_pendingPickup != null && _pendingPickup.IsHoldingObject())
        {
            Vector3    pos = bottleOutputPoint != null ? bottleOutputPoint.position : transform.position;
            Quaternion rot = bottleOutputPoint != null ? bottleOutputPoint.rotation : Quaternion.identity;
            GameObject placed = _pendingPickup.PlaceHeldObjectAt(pos, rot);
            _activeBottle = placed != null ? placed.GetComponent<MedicationBottle>() : null;
        }
        _pendingPickup = null;

        _isActive = true;

        focus.EnterFocus(focusCameraTarget, OnFocusExited);

        // Auto-load hopper from the current prescription
        AutoLoadHopper();

        // Pull target count from the currently scanned NPC's prescription
        int target = GetPrescriptionTarget();

        if (dispensingController != null)
        {
            dispensingController.OnTargetReached += OnFillTargetReached;
            dispensingController.Initialize(target);
        }

        if (counterUI != null)
        {
            counterUI.gameObject.SetActive(true);
            counterUI.Bind(dispensingController);
        }

        if (hopper != null)
            hopper.Activate();

        if (audioSource != null && activateSound != null)
            audioSource.PlayOneShot(activateSound);

        Debug.Log($"[PillFillingStation] Activated. Target: {target}");
    }

    private void DoDeactivate()
    {
        // Capture the fill count before resetting
        if (dispensingController != null)
        {
            LastFillCount = dispensingController.CurrentCount;
            dispensingController.OnTargetReached -= OnFillTargetReached;
            dispensingController.Shutdown();
        }

        // If the bottle is still at the output point (player exited early), destroy it.
        // DelayedComplete clears _activeBottle before calling Deactivate so this only
        // fires on early exits, not normal completions.
        if (_activeBottle != null)
        {
            NetworkObject netObj = _activeBottle.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                DestroyBottleServerRpc(netObj.NetworkObjectId);
            else
                Destroy(_activeBottle.gameObject);
        }

        _activeBottle = null;
        _activePickup = null;
        _isActive = false;

        if (hopper != null)
            hopper.Deactivate();

        if (counterUI != null)
        {
            counterUI.ResetUI();
            counterUI.gameObject.SetActive(false);
        }

        PlayerComponents pc = PlayerComponents.Local;
        FocusStateManager focus = pc != null ? pc.FocusState : null;
        if (focus != null && focus.IsFocused)
            focus.ExitFocus();

        Debug.Log($"[PillFillingStation] Deactivated. Final count: {LastFillCount}");
    }

    // ── Callbacks ───────────────────────────────────────────────────

    private void OnFocusExited()
    {
        Deactivate();
    }

    private void OnFillTargetReached()
    {
        if (_activeBottle != null)
            _activeBottle.SetFilled();

        StartCoroutine(DelayedComplete(0f));
    }

    private IEnumerator DelayedComplete(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Return the filled bottle to the player's hand before exiting focus.
        // Clear _activeBottle first so DoDeactivate doesn't treat it as an abandoned bottle.
        if (_activeBottle != null && _activePickup != null)
        {
            ObjectPickup pickup = _activePickup;
            MedicationBottle bottle = _activeBottle;
            _activeBottle = null;
            _activePickup = null;
            pickup.ForcePickup(bottle.gameObject);
        }
        else
        {
            _activeBottle = null;
            _activePickup = null;
        }

        Deactivate();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private int GetPrescriptionTarget()
    {
        if (NPCInfoDisplay.Instance != null && NPCInfoDisplay.Instance.CurrentNPC != null)
        {
            var prescription = NPCInfoDisplay.Instance.CurrentNPC.Prescription;
            if (prescription != null && prescription.quantity > 0)
                return prescription.quantity;
        }
        return fallbackTargetCount;
    }

    private void AutoLoadHopper()
    {
        if (hopper == null) return;

        // Auto-load: mark hopper as loaded so the mechanic works immediately.
        // Future: could match loaded MedicationData against prescription for wrong-medication gameplay.
        hopper.SetLoaded();
    }
}
