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

    [Header("Controller")]
    [Tooltip("Manages view switching and tab navigation.")]
    [SerializeField] private ComputerScreenController controller;

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

        // Auto-find the controller if not manually assigned
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

        // Ensure the World Space Canvas has an Event Camera — this MUST happen
        // here (not Awake) because Camera.main may not exist during Awake.
        EnsureEventCamera();

        // Check for EventSystem (required for any UI interaction)
        if (UnityEngine.EventSystems.EventSystem.current == null)
            Debug.LogError("[ComputerScreen] No EventSystem in scene! UI buttons won't work. " +
                "Add one via: GameObject → UI → Event System.");

        // Enter focus mode (disables FPS controls, transitions camera)
        FocusStateManager.Instance.EnterFocus(focusCameraTarget, OnFocusExited);

        // Enable UI interaction and show main view
        SetInteractive(true);
        if (controller != null)
            controller.ResetToMain();

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

        // Disable UI interaction and hide all views
        SetInteractive(false);
        if (controller != null)
            controller.HideAll();

        // Make sure focus is exited (in case Deactivate was called directly)
        if (FocusStateManager.Instance != null && FocusStateManager.Instance.IsFocused)
        {
            FocusStateManager.Instance.ExitFocus();
        }
    }

    /// <summary>
    /// Ensures the World Space Canvas has a valid Event Camera.
    /// Called at activation time because Camera.main may be null during Awake.
    /// </summary>
    private void EnsureEventCamera()
    {
        if (screenCanvas == null || screenCanvas.renderMode != RenderMode.WorldSpace) return;

        // Already assigned and valid?
        if (screenCanvas.worldCamera != null) return;

        // Try Camera.main first
        Camera cam = Camera.main;

        // Fallback: find any camera in the scene
        if (cam == null)
            cam = FindFirstObjectByType<Camera>();

        if (cam != null)
        {
            screenCanvas.worldCamera = cam;
            Debug.Log("[ComputerScreen] Event Camera assigned: " + cam.gameObject.name);
        }
        else
        {
            Debug.LogError("[ComputerScreen] Cannot find any camera for World Space Canvas! " +
                "Button clicks will not work.");
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
