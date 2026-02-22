using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to a "Talk to Customer" button on the computer screen UI.
/// Auto-detects if an NPC is waiting at the counter and enables/disables the button.
/// On click, starts a new conversation with the waiting NPC.
/// </summary>
[RequireComponent(typeof(Button))]
public class ComputerDialogueButton : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Optional text to show when no NPC is available.")]
    [SerializeField] private string unavailableText = "No customer waiting";

    [Tooltip("Text to show when an NPC is available for conversation.")]
    [SerializeField] private string availableText = "Talk to Customer";

    [Header("Visual Feedback")]
    [Tooltip("Optional CanvasGroup for fading the button when unavailable.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Alpha when button is unavailable.")]
    [SerializeField] private float unavailableAlpha = 0.4f;

    // ── Runtime ─────────────────────────────────────────────────────
    private Button _button;
    private TextMeshProUGUI _buttonText;
    private NPCDialogueTrigger _cachedNPCTrigger;
    private float _scanTimer;
    private const float SCAN_INTERVAL = 0.5f; // Don't scan every frame

    void Awake()
    {
        _button = GetComponent<Button>();
        _buttonText = GetComponentInChildren<TextMeshProUGUI>();

        _button.onClick.AddListener(OnButtonClicked);
    }

    void OnEnable()
    {
        // Scan immediately when enabled
        _scanTimer = SCAN_INTERVAL;
    }

    void Update()
    {
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= SCAN_INTERVAL)
        {
            _scanTimer = 0f;
            UpdateButtonState();
        }
    }

    // ── Private ─────────────────────────────────────────────────────

    private void UpdateButtonState()
    {
        _cachedNPCTrigger = FindWaitingNPC();
        bool available = _cachedNPCTrigger != null;

        _button.interactable = available;

        if (_buttonText != null)
            _buttonText.text = available ? availableText : unavailableText;

        if (canvasGroup != null)
            canvasGroup.alpha = available ? 1f : unavailableAlpha;
    }

    /// <summary>
    /// Find an NPC at the counter that is available for dialogue.
    /// </summary>
    private NPCDialogueTrigger FindWaitingNPC()
    {
        // Search all NPCs with dialogue triggers
        NPCDialogueTrigger[] triggers = FindObjectsByType<NPCDialogueTrigger>(FindObjectsSortMode.None);

        foreach (NPCDialogueTrigger trigger in triggers)
        {
            if (trigger.IsAvailableForDialogue())
                return trigger;
        }

        return null;
    }

    private void OnButtonClicked()
    {
        if (_cachedNPCTrigger != null && _cachedNPCTrigger.IsAvailableForDialogue())
        {
            Debug.Log($"[ComputerDialogueButton] Starting new conversation with {_cachedNPCTrigger.gameObject.name}");
            _cachedNPCTrigger.StartNewConversation();
        }
        else
        {
            Debug.Log("[ComputerDialogueButton] No NPC available for dialogue.");
        }
    }
}
