using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactable computer screen that uses a World Space Canvas.
/// The player presses E to focus on the monitor; the camera lerps to a target
/// position and the canvas becomes clickable. Press Escape to exit.
/// Follows the same pattern as PillCountingStation.
/// </summary>
public class ComputerScreen : MonoBehaviour
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

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip activateSound;

    private bool _isActive;
    private GraphicRaycaster _raycaster;

    public bool IsActive => _isActive;

    void Awake()
    {
        // Cache the raycaster — this is the gate for UI interaction
        if (screenCanvas != null)
        {
            _raycaster = screenCanvas.GetComponent<GraphicRaycaster>();
            if (_raycaster == null)
            {
                _raycaster = screenCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        // Start with interaction disabled but screen visually "on"
        SetInteractive(false);
    }

    /// <summary>
    /// Called by ObjectPickup when the player presses E while looking at this monitor.
    /// </summary>
    public void Activate()
    {
        if (_isActive) return;

        // --- Validate prerequisites ---
        if (FocusStateManager.Instance == null)
        {
            Debug.LogError("[ComputerScreen] Cannot activate: FocusStateManager not found! " +
                "Add a FocusStateManager component to any GameObject in the scene.");
            return;
        }

        if (focusCameraTarget == null)
        {
            Debug.LogError("[ComputerScreen] Cannot activate: Focus Camera Target is not assigned! " +
                "Create an empty child GameObject in front of the monitor and assign it.");
            return;
        }

        _isActive = true;

        Debug.Log("[ComputerScreen] Activating — entering focus mode.");

        // Enter focus mode (disables FPS controls, transitions camera)
        FocusStateManager.Instance.EnterFocus(focusCameraTarget, OnFocusExited);

        // Enable UI interaction
        SetInteractive(true);

        // Play activation sound
        if (audioSource != null && activateSound != null)
        {
            audioSource.PlayOneShot(activateSound);
        }
    }

    /// <summary>
    /// Called when the player exits focus mode (presses Escape).
    /// </summary>
    private void OnFocusExited()
    {
        Deactivate();
    }

    /// <summary>
    /// Cleans up and returns to normal state.
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive) return;

        _isActive = false;

        Debug.Log("[ComputerScreen] Deactivating — exiting focus mode.");

        // Disable UI interaction
        SetInteractive(false);

        // Make sure focus is exited (in case Deactivate was called directly)
        if (FocusStateManager.Instance != null && FocusStateManager.Instance.IsFocused)
        {
            FocusStateManager.Instance.ExitFocus();
        }
    }

    /// <summary>
    /// Toggles between idle (powered-on but non-interactive) and active (interactive) states.
    /// </summary>
    private void SetInteractive(bool interactive)
    {
        // Toggle the GraphicRaycaster — this controls whether clicks reach UI elements
        if (_raycaster != null)
            _raycaster.enabled = interactive;

        // Show/hide the idle screen vs interactive UI
        if (idleScreen != null)
            idleScreen.SetActive(!interactive);

        if (interactiveUI != null)
            interactiveUI.SetActive(interactive);
    }
}
