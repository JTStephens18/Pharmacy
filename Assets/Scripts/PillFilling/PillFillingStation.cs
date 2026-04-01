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

        // Consume the held bottle (set it down)
        if (_pendingPickup != null && _pendingPickup.IsHoldingObject())
            _pendingPickup.ConsumeHeldObject();
        _pendingPickup = null;

        _isActive = true;

        focus.EnterFocus(focusCameraTarget, OnFocusExited);

        // Auto-load hopper from the current prescription
        AutoLoadHopper();

        // Pull target count from the currently scanned NPC's prescription
        int target = GetPrescriptionTarget();

        if (dispensingController != null)
            dispensingController.Initialize(target);

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
            LastFillCount = dispensingController.CurrentCount;

        _isActive = false;

        if (dispensingController != null)
            dispensingController.Shutdown();

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

    // ── Helpers ──────────────────────────────────────────────────────

    private int GetPrescriptionTarget()
    {
        if (NPCInfoDisplay.Instance != null && NPCInfoDisplay.Instance.CurrentNPC != null)
        {
            var prescription = NPCInfoDisplay.Instance.CurrentNPC.Prescription;
            if (prescription != null)
                return prescription.quantity;
        }
        return 0;
    }

    private void AutoLoadHopper()
    {
        if (hopper == null) return;

        // Auto-load: mark hopper as loaded so the mechanic works immediately.
        // Future: could match loaded MedicationData against prescription for wrong-medication gameplay.
        hopper.SetLoaded();
    }
}
