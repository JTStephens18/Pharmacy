using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactable computer screen that uses a World Space Canvas.
/// The player presses E to focus on the monitor; the camera lerps to a target
/// position and the canvas becomes clickable. Press Escape to exit.
///
/// Multiplayer: server-authoritative exclusive-access lock via NetworkVariable.
/// Only one player can use the screen at a time.
/// </summary>
public class ComputerScreen : NetworkBehaviour
{
    [Header("Focus Camera Position")]
    [Tooltip("Empty Transform positioned where the camera should sit when focused on the screen.")]
    [SerializeField] private Transform focusCameraTarget;

    [Header("Canvas")]
    [Tooltip("The World Space Canvas on this monitor.")]
    [SerializeField] private Canvas screenCanvas;

    [Header("Screen Visuals")]
    [Tooltip("Optional background image shown when the screen is 'powered on' but not focused.")]
    [SerializeField] private GameObject idleScreen;
    [Tooltip("Root container for the interactive UI (tabs, buttons, etc.). Shown only when focused.")]
    [SerializeField] private GameObject interactiveUI;

    [Header("Controller")]
    [Tooltip("Manages view switching and tab navigation.")]
    [SerializeField] private ComputerScreenController controller;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip activateSound;

    // ── Networked Lock ────────────────────────────────────────────────
    // ulong.MaxValue = no current user. Server-authoritative.
    private const ulong NoUser = ulong.MaxValue;

    private readonly NetworkVariable<ulong> _currentUserId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Local State ───────────────────────────────────────────────────
    private bool _isActive;
    private GraphicRaycaster _raycaster;
    private bool _suppressDeactivation;
    private Action _onTemporaryExitComplete;

    public bool IsActive  => _isActive;

    /// <summary>True when another player is currently using this screen.</summary>
    public bool IsInUse   => IsSpawned && _currentUserId.Value != NoUser;

    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (screenCanvas != null)
        {
            _raycaster = screenCanvas.GetComponent<GraphicRaycaster>();
            if (_raycaster == null)
                _raycaster = screenCanvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        if (controller == null && interactiveUI != null)
        {
            controller = interactiveUI.GetComponent<ComputerScreenController>();
            if (controller == null)
                controller = interactiveUI.GetComponentInChildren<ComputerScreenController>();
        }

        if (controller != null)
            Debug.Log("[ComputerScreen] Controller found: " + controller.gameObject.name);
        else
            Debug.LogWarning("[ComputerScreen] No ComputerScreenController found! " +
                "Add it to the InteractiveUI GameObject or assign it manually.");

        SetInteractive(false);
    }

    // ── Public API (called by ObjectPickup) ───────────────────────────

    /// <summary>
    /// Called by ObjectPickup when the player presses E while looking at this monitor.
    /// Routes through the server lock in multiplayer; activates directly in single-player.
    /// </summary>
    public void Activate()
    {
        if (_isActive) return;

        if (!IsSpawned)
        {
            // Non-networked fallback (editor / single-player without NGO)
            DoActivate();
            return;
        }

        if (_currentUserId.Value != NoUser) return; // Screen already in use

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
        if (_currentUserId.Value != NoUser) return; // Race condition: someone else just took it

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

    // ── Internal Activate / Deactivate ───────────────────────────────

    private void DoActivate()
    {
        FocusStateManager focus = GetFocusManager();
        if (focus == null)
        {
            Debug.LogError("[ComputerScreen] Cannot activate: PlayerComponents or FocusStateManager not found!");
            if (IsSpawned) ReleaseActivationServerRpc();
            return;
        }

        if (focusCameraTarget == null)
        {
            Debug.LogError("[ComputerScreen] Cannot activate: Focus Camera Target is not assigned!");
            if (IsSpawned) ReleaseActivationServerRpc();
            return;
        }

        _isActive = true;

        Debug.Log("[ComputerScreen] Activating — entering focus mode.");

        EnsureEventCamera();

        if (UnityEngine.EventSystems.EventSystem.current == null)
            Debug.LogError("[ComputerScreen] No EventSystem in scene! UI buttons won't work.");

        focus.EnterFocus(focusCameraTarget, OnFocusExited);

        SetInteractive(true);
        if (controller != null)
            controller.ResetToMain();

        if (audioSource != null && activateSound != null)
            audioSource.PlayOneShot(activateSound);
    }

    private void DoDeactivate()
    {
        _isActive = false;

        Debug.Log("[ComputerScreen] Deactivating — exiting focus mode.");

        SetInteractive(false);
        if (controller != null)
            controller.HideAll();

        FocusStateManager focus = GetFocusManager();
        if (focus != null && focus.IsFocused)
            focus.ExitFocus();
    }

    // ── Focus Callbacks ───────────────────────────────────────────────

    private void OnFocusExited()
    {
        if (_suppressDeactivation)
        {
            _suppressDeactivation = false;
            Action cb = _onTemporaryExitComplete;
            _onTemporaryExitComplete = null;
            cb?.Invoke();
            return;
        }

        Deactivate();
    }

    // ── Dialogue Integration ──────────────────────────────────────────

    /// <summary>
    /// Temporarily exits computer focus to allow dialogue, without fully deactivating.
    /// The onComplete callback fires after the exit transition finishes.
    /// </summary>
    public void TemporaryExitForDialogue(Action onComplete)
    {
        if (!_isActive) return;

        _suppressDeactivation = true;
        _onTemporaryExitComplete = onComplete;

        if (_raycaster != null)
            _raycaster.enabled = false;

        FocusStateManager focus = GetFocusManager();
        if (focus != null)
            focus.ExitFocus();
    }

    /// <summary>
    /// Re-enters computer focus after a temporary dialogue exit.
    /// Does not reset views or call full Activate() — preserves current UI state.
    /// </summary>
    public void ReactivateAfterDialogue()
    {
        if (!_isActive) return;

        FocusStateManager focus = GetFocusManager();
        if (focus == null) return;

        EnsureEventCamera();
        focus.EnterFocus(focusCameraTarget, OnFocusExited);

        focus.OnFocusChanged += OnReactivateFocusChanged;
    }

    private void OnReactivateFocusChanged(bool entered)
    {
        if (!entered) return;

        FocusStateManager focus = GetFocusManager();
        if (focus != null)
            focus.OnFocusChanged -= OnReactivateFocusChanged;

        if (_raycaster != null)
            _raycaster.enabled = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private FocusStateManager GetFocusManager()
    {
        PlayerComponents pc = PlayerComponents.Local;
        return pc != null ? pc.FocusState : null;
    }

    private void EnsureEventCamera()
    {
        if (screenCanvas == null || screenCanvas.renderMode != RenderMode.WorldSpace) return;
        if (screenCanvas.worldCamera != null) return;

        PlayerComponents pc = PlayerComponents.Local;
        Camera cam = pc != null ? pc.PlayerCamera : null;

        if (cam != null)
        {
            screenCanvas.worldCamera = cam;
            Debug.Log("[ComputerScreen] Event Camera assigned: " + cam.gameObject.name);
        }
        else
        {
            Debug.LogError("[ComputerScreen] Cannot find player camera for World Space Canvas!");
        }
    }

    private void SetInteractive(bool interactive)
    {
        if (_raycaster != null)
            _raycaster.enabled = interactive;

        if (idleScreen != null)
            idleScreen.SetActive(!interactive);

        if (interactiveUI != null)
            interactiveUI.SetActive(interactive);
    }
}
