using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The sorting station that the player interacts with to start the pill counting mini-game.
/// Manages the full mini-game lifecycle: activation, pill spawning, completion, and cleanup.
///
/// Multiplayer: server-authoritative exclusive-access lock via NetworkVariable.
/// Only one player can use the station at a time.
/// Pills are spawned locally on the using client — they are not networked objects.
/// PillScraper, PillCountingChute, and PillCountUI are enabled only on the using client.
/// </summary>
public class PillCountingStation : NetworkBehaviour
{
    [Header("Mini-Game Settings")]
    [SerializeField] private int targetPillCount = 30;

    [Header("Focus Camera Position")]
    [Tooltip("An empty Transform positioned where the camera should look at the tray (top-down or 45-degree angle).")]
    [SerializeField] private Transform focusCameraTarget;

    [Header("Child Components")]
    [SerializeField] private PillScraper scraper;
    [SerializeField] private PillSpawner spawner;
    [SerializeField] private PillCountingChute chute;
    [SerializeField] private PillCountUI countUI;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip activateSound;

    // ── Networked Lock ────────────────────────────────────────────────
    private const ulong NoUser = ulong.MaxValue;

    private readonly NetworkVariable<ulong> _currentUserId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Local State ───────────────────────────────────────────────────
    private bool _isActive;

    public bool IsActive  => _isActive;

    /// <summary>True when another player is currently using this station.</summary>
    public bool IsInUse   => IsSpawned && _currentUserId.Value != NoUser;

    void Awake()
    {
        // Auto-find child components if not assigned
        if (scraper == null) scraper = GetComponentInChildren<PillScraper>(true);
        if (spawner == null) spawner = GetComponentInChildren<PillSpawner>(true);
        if (chute == null) chute = GetComponentInChildren<PillCountingChute>(true);
        if (countUI == null) countUI = GetComponentInChildren<PillCountUI>(true);

        // Start with mini-game components disabled
        SetMiniGameActive(false);
    }

    // ── Public API (called by ObjectPickup) ───────────────────────────

    /// <summary>
    /// Called by ObjectPickup when the player presses E while looking at this station.
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

        if (_currentUserId.Value != NoUser) return; // Station already in use

        RequestActivationServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    /// <summary>
    /// Cleans up the mini-game and restores normal state. Also releases the server-side lock.
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
            Debug.LogError("[PillCountingStation] Cannot activate: FocusStateManager not found!");
            if (IsSpawned) ReleaseActivationServerRpc();
            return;
        }

        if (focusCameraTarget == null)
        {
            Debug.LogError("[PillCountingStation] Cannot activate: Focus Camera Target is not assigned!");
            if (IsSpawned) ReleaseActivationServerRpc();
            return;
        }

        _isActive = true;

        Debug.Log($"[PillCountingStation] Activating with target: {targetPillCount} pills.");

        focus.EnterFocus(focusCameraTarget, OnFocusExited);

        // Enable mini-game components (local to this client only)
        SetMiniGameActive(true);

        if (chute != null)
        {
            chute.Initialize(targetPillCount);
            chute.OnTargetReached += OnTargetReached;
        }

        if (countUI != null)
            countUI.Bind(chute);

        if (scraper != null)
            scraper.SetTrayCenter(transform.position);

        // Pills are spawned locally — not networked objects
        if (spawner != null)
            spawner.SpawnPills(targetPillCount);

        if (audioSource != null && activateSound != null)
            audioSource.PlayOneShot(activateSound);
    }

    private void DoDeactivate()
    {
        _isActive = false;

        Debug.Log("[PillCountingStation] Deactivating.");

        if (chute != null)
        {
            chute.OnTargetReached -= OnTargetReached;
            chute.ResetChute();
        }

        if (spawner != null)
            spawner.ClearPills();

        if (countUI != null)
            countUI.ResetUI();

        SetMiniGameActive(false);

        PlayerComponents pc = PlayerComponents.Local;
        FocusStateManager focus = pc != null ? pc.FocusState : null;
        if (focus != null && focus.IsFocused)
            focus.ExitFocus();
    }

    // ── Focus Callbacks ───────────────────────────────────────────────

    private void OnTargetReached()
    {
        Debug.Log("[PillCountingStation] Mini-game complete!");
        Invoke(nameof(Deactivate), 1.5f);
    }

    private void OnFocusExited()
    {
        Deactivate();
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void SetMiniGameActive(bool active)
    {
        if (scraper != null) scraper.enabled = active;
        if (chute   != null) chute.enabled   = active;
        if (countUI != null) countUI.gameObject.SetActive(active);
    }
}
