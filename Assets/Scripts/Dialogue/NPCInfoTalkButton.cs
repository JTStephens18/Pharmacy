using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Button inside the NPCInfoPanel that triggers dialogue with the displayed NPC.
/// Orchestrates: exit computer focus → start dialogue → return to player camera.
/// Replaces ComputerDialogueButton.
///
/// Also tracks the NPC's question budget. When the budget runs out, the button
/// disables and the remaining-count text updates to reflect it.
/// </summary>
[RequireComponent(typeof(Button))]
public class NPCInfoTalkButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

    [Header("Hover Outline")]
    [Tooltip("Outline UI effect on the Button's Image. Enabled on hover, disabled otherwise. " +
             "Add an Outline component to the Button's Image and assign it here, or leave null to auto-find.")]
    [SerializeField] private Outline hoverOutline;

    [Header("Question Budget")]
    [Tooltip("Optional TMP text to display remaining questions (e.g. '3 questions remaining'). " +
             "Updated automatically by polling the NPC's NPCDialogueTrigger.QuestionsRemaining.")]
    [SerializeField] private TextMeshProUGUI questionsRemainingText;

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

        if (hoverOutline == null)
            hoverOutline = GetComponent<Outline>();
        if (hoverOutline != null)
            hoverOutline.enabled = false;
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
        NPCDialogueTrigger trigger = FindMatchingTrigger(out int questionsLeft);
        bool available = trigger != null && questionsLeft > 0;

        _button.interactable = available;

        if (canvasGroup != null)
            canvasGroup.alpha = available ? 1f : unavailableAlpha;

        // Hide outline if button becomes unavailable while hovered
        if (!available && hoverOutline != null)
            hoverOutline.enabled = false;

        // Update the questions remaining text if assigned
        if (questionsRemainingText != null)
        {
            if (trigger != null || questionsLeft >= 0)
            {
                if (questionsLeft <= 0)
                    questionsRemainingText.text = "No questions remaining";
                else if (questionsLeft == 1)
                    questionsRemainingText.text = "1 question remaining";
                else
                    questionsRemainingText.text = $"{questionsLeft} questions remaining";
            }
            else
            {
                questionsRemainingText.text = "";
            }
        }
    }

    /// <summary>
    /// Finds an NPCDialogueTrigger whose identity matches the one currently
    /// displayed on the NPCInfoPanel, is available for dialogue, and has
    /// a dialogue registered for this button's dialogueKey.
    /// Also outputs the questions remaining for the matched NPC (-1 if no match).
    /// </summary>
    private NPCDialogueTrigger FindMatchingTrigger(out int questionsRemaining)
    {
        questionsRemaining = -1;

        if (NPCInfoDisplay.Instance == null || !NPCInfoDisplay.Instance.IsDisplaying)
            return null;

        NPCIdentity displayedIdentity = NPCInfoDisplay.Instance.CurrentIdentity;
        if (displayedIdentity == null)
            return null;

        NPCDialogueTrigger[] triggers = FindObjectsByType<NPCDialogueTrigger>(FindObjectsSortMode.None);
        foreach (NPCDialogueTrigger trigger in triggers)
        {
            NPCInteractionController ctrl = trigger.GetComponent<NPCInteractionController>();
            if (ctrl == null || ctrl.NpcIdentity != displayedIdentity || !trigger.HasInfoDialogue(dialogueKey))
                continue;

            questionsRemaining = trigger.QuestionsRemaining;

            if (!trigger.IsAvailableForDialogue())
                return null; // NPC matched but not available — still report budget

            return trigger;
        }

        return null;
    }

    // ── Orchestration ───────────────────────────────────────────────

    private void OnButtonClicked()
    {
        if (_isOrchestrating) return;
        if (_computerScreen == null) return;

        NPCDialogueTrigger trigger = FindMatchingTrigger(out int questionsLeft);
        if (trigger == null || questionsLeft <= 0) return;

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

    // ── Hover Outline ────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverOutline != null && _button.interactable)
            hoverOutline.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverOutline != null)
            hoverOutline.enabled = false;
    }
}
