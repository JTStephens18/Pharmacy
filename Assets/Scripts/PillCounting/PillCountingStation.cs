using UnityEngine;

/// <summary>
/// The sorting station that the player interacts with to start the pill counting mini-game.
/// Manages the full mini-game lifecycle: activation, pill spawning, completion, and cleanup.
/// </summary>
public class PillCountingStation : MonoBehaviour
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

    private bool _isActive;

    public bool IsActive => _isActive;

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

    /// <summary>
    /// Called by ObjectPickup when the player presses E while looking at this station.
    /// </summary>
    public void Activate()
    {
        if (_isActive) return;

        // --- Validate prerequisites BEFORE doing anything ---
        if (FocusStateManager.Instance == null)
        {
            Debug.LogError("[PillCountingStation] Cannot activate: FocusStateManager not found! " +
                "Add a FocusStateManager component to any GameObject in the scene (e.g., the Player).");
            return;
        }

        if (focusCameraTarget == null)
        {
            Debug.LogError("[PillCountingStation] Cannot activate: Focus Camera Target is not assigned! " +
                "Create an empty GameObject positioned over the tray and assign it to the 'Focus Camera Target' field.");
            return;
        }

        _isActive = true;

        Debug.Log($"[PillCountingStation] Activating with target: {targetPillCount} pills.");

        // Enter focus mode FIRST (disables FPS controls, transitions camera)
        FocusStateManager.Instance.EnterFocus(focusCameraTarget, OnFocusExited);

        // Enable mini-game components
        SetMiniGameActive(true);

        // Initialize the chute
        if (chute != null)
        {
            chute.Initialize(targetPillCount);
            chute.OnTargetReached += OnTargetReached;
        }

        // Bind UI to chute
        if (countUI != null)
        {
            countUI.Bind(chute);
        }

        // Set scraper tray center
        if (scraper != null)
        {
            scraper.SetTrayCenter(transform.position);
        }

        // Spawn pills
        if (spawner != null)
        {
            spawner.SpawnPills(targetPillCount);
        }

        // Play activation sound
        if (audioSource != null && activateSound != null)
        {
            audioSource.PlayOneShot(activateSound);
        }
    }

    /// <summary>
    /// Called when the target pill count is reached.
    /// </summary>
    private void OnTargetReached()
    {
        Debug.Log("[PillCountingStation] Mini-game complete!");
        // The UI handles showing completion state.
        // Player can press Escape to exit, or we could auto-exit after a delay.
    }

    /// <summary>
    /// Called when the player exits focus mode (presses Escape).
    /// </summary>
    private void OnFocusExited()
    {
        Deactivate();
    }

    /// <summary>
    /// Cleans up the mini-game and restores normal state.
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive) return;

        _isActive = false;

        Debug.Log("[PillCountingStation] Deactivating.");

        // Unsubscribe
        if (chute != null)
        {
            chute.OnTargetReached -= OnTargetReached;
            chute.ResetChute();
        }

        // Clean up pills
        if (spawner != null)
        {
            spawner.ClearPills();
        }

        // Reset UI
        if (countUI != null)
        {
            countUI.ResetUI();
        }

        // Disable mini-game components
        SetMiniGameActive(false);

        // Make sure focus is exited (in case Deactivate was called directly)
        if (FocusStateManager.Instance != null && FocusStateManager.Instance.IsFocused)
        {
            FocusStateManager.Instance.ExitFocus();
        }
    }

    private void SetMiniGameActive(bool active)
    {
        if (scraper != null) scraper.enabled = active;
        if (chute != null) chute.enabled = active;
        if (countUI != null) countUI.gameObject.SetActive(active);
    }
}
