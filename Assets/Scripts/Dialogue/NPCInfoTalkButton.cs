using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Button inside the NPCInfoPanel that triggers dialogue with the displayed NPC.
/// Orchestrates: exit computer focus → start dialogue → return to player camera.
/// Replaces ComputerDialogueButton.
/// </summary>
[RequireComponent(typeof(Button))]
public class NPCInfoTalkButton : MonoBehaviour
{
    [Header("Dialogue")]
    [Tooltip("Key that maps to an InfoDialogueEntry on the NPC's NPCDialogueTrigger. " +
             "Each button can have a different key to trigger a different dialogue tree.")]
    [SerializeField] private string dialogueKey = "default";

    [Header("Visual Feedback")]
    [Tooltip("Optional CanvasGroup for fading the button when unavailable.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Alpha when button is unavailable.")]
    [SerializeField] private float unavailableAlpha = 0.4f;

    // ── Runtime ─────────────────────────────────────────────────────
    private Button _button;
    private ComputerScreen _computerScreen;
    private bool _isOrchestrating;
    private float _scanTimer;
    private const float SCAN_INTERVAL = 0.5f;
    private DialogueManager _subscribedDm;

    void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnButtonClicked);
    }

    void Start()
    {
        // Walk up hierarchy to find ComputerScreen
        _computerScreen = GetComponentInParent<ComputerScreen>();
        if (_computerScreen == null)
            _computerScreen = FindFirstObjectByType<ComputerScreen>();

        if (_computerScreen == null)
            Debug.LogError("[NPCInfoTalkButton] Could not find ComputerScreen in hierarchy or scene.");
    }

    void OnDestroy()
    {
        // Unsubscribe from whichever DialogueManager we subscribed to during orchestration
        if (_subscribedDm != null)
            _subscribedDm.OnDialogueEnded -= OnDialogueEnded;
    }

    void OnEnable()
    {
        _scanTimer = SCAN_INTERVAL; // trigger immediate scan
    }

    void Update()
    {
        if (_isOrchestrating) return;

        _scanTimer += Time.deltaTime;
        if (_scanTimer >= SCAN_INTERVAL)
        {
            _scanTimer = 0f;
            UpdateButtonState();
        }
    }

    // ── Button State ────────────────────────────────────────────────

    private void UpdateButtonState()
    {
        bool available = FindMatchingTrigger() != null;

        _button.interactable = available;

        if (canvasGroup != null)
            canvasGroup.alpha = available ? 1f : unavailableAlpha;
    }

    /// <summary>
    /// Finds an NPCDialogueTrigger whose identity matches the one currently
    /// displayed on the NPCInfoPanel, is available for dialogue, and has
    /// a dialogue registered for this button's dialogueKey.
    /// </summary>
    private NPCDialogueTrigger FindMatchingTrigger()
    {
        if (NPCInfoDisplay.Instance == null || !NPCInfoDisplay.Instance.IsDisplaying)
            return null;

        NPCIdentity displayedIdentity = NPCInfoDisplay.Instance.CurrentIdentity;
        if (displayedIdentity == null)
            return null;

        NPCDialogueTrigger[] triggers = FindObjectsByType<NPCDialogueTrigger>(FindObjectsSortMode.None);
        foreach (NPCDialogueTrigger trigger in triggers)
        {
            if (!trigger.IsAvailableForDialogue())
                continue;

            NPCInteractionController ctrl = trigger.GetComponent<NPCInteractionController>();
            if (ctrl != null && ctrl.NpcIdentity == displayedIdentity && trigger.HasInfoDialogue(dialogueKey))
                return trigger;
        }

        return null;
    }

    // ── Orchestration ───────────────────────────────────────────────

    private void OnButtonClicked()
    {
        if (_isOrchestrating) return;
        if (_computerScreen == null) return;

        NPCDialogueTrigger trigger = FindMatchingTrigger();
        if (trigger == null) return;

        DialogueManager dm = PlayerComponents.Local?.Dialogue;
        if (dm == null) return;

        _isOrchestrating = true;
        _button.interactable = false;

        // Subscribe to end event before starting the chain so we don't miss it
        _subscribedDm = dm;
        dm.OnDialogueEnded += OnDialogueEnded;

        Debug.Log($"[NPCInfoTalkButton] Starting dialogue chain with {trigger.gameObject.name}");

        // Phase 1: temporarily exit computer focus
        _computerScreen.TemporaryExitForDialogue(() => OnComputerExitComplete(trigger));
    }

    private void OnComputerExitComplete(NPCDialogueTrigger trigger)
    {
        // Phase 2: focus has fully exited — start info dialogue
        trigger.StartInfoDialogue(dialogueKey);
    }

    private void OnDialogueEnded()
    {
        if (!_isOrchestrating) return;

        // Unsubscribe immediately so we don't get called again
        if (_subscribedDm != null)
        {
            _subscribedDm.OnDialogueEnded -= OnDialogueEnded;
            _subscribedDm = null;
        }

        // Phase 3: dialogue finished — fully deactivate computer and return to player camera
        Debug.Log("[NPCInfoTalkButton] Dialogue ended, deactivating computer screen.");

        if (_computerScreen != null)
            _computerScreen.Deactivate();

        _isOrchestrating = false;
    }
}
