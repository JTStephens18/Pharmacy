using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps a string key to a dialogue file. Used by NPCInfoTalkButton to trigger
/// different dialogue trees from different buttons in the NPCInfoPanel.
/// </summary>
[Serializable]
public class InfoDialogueEntry
{
    [Tooltip("Key that NPCInfoTalkButton uses to select this dialogue (e.g. 'verify_id', 'dob', 'prescription').")]
    public string key;

    [Tooltip("The dialogue JSON file for this key.")]
    public TextAsset dialogueFile;
}

/// <summary>
/// Attach to an NPC alongside NPCInteractionController.
/// Monitors NPC state and automatically triggers dialogue when the NPC
/// is waiting for checkout and the player is within range + has line of sight.
/// Also supports starting new conversations from the computer screen.
/// </summary>
[RequireComponent(typeof(NPCInteractionController))]
public class NPCDialogueTrigger : MonoBehaviour
{
    [Header("Dialogue Data")]
    [Tooltip("JSON dialogue files for this NPC. Cycles through them for repeat conversations.")]
    [SerializeField] private TextAsset[] dialogueFiles;

    [Tooltip("Keyed dialogue files triggered from NPCInfoPanel buttons. Each entry maps a key to a dialogue file.")]
    [SerializeField] private InfoDialogueEntry[] infoDialogues;

    [Header("Player Detection")]
    [Tooltip("Maximum distance to the player for dialogue to trigger automatically.")]
    [SerializeField] private float playerRange = 5f;

    [Tooltip("If true, requires line of sight to the player (raycast must not be blocked).")]
    [SerializeField] private bool requireLineOfSight = true;

    [Tooltip("Layer mask for line-of-sight check (should include obstacles, not the player).")]
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // ── Runtime State ───────────────────────────────────────────────
    private NPCInteractionController _npcController;
    private Transform _playerTransform;
    private bool _hasTriggeredInitialDialogue;
    private int _dialogueIndex;
    private bool _dialogueInProgress;

    // Pre-loaded dialogue data for current file
    private DialogueData _loadedData;
    private Dictionary<string, DialogueNode> _loadedLookup;

    // Runtime lookup for info dialogues
    private Dictionary<string, TextAsset> _infoDialogueLookup;

    void Awake()
    {
        _npcController = GetComponent<NPCInteractionController>();

        // Build lookup from serialized array
        _infoDialogueLookup = new Dictionary<string, TextAsset>();
        if (infoDialogues != null)
        {
            foreach (InfoDialogueEntry entry in infoDialogues)
            {
                if (string.IsNullOrEmpty(entry.key) || entry.dialogueFile == null) continue;
                _infoDialogueLookup[entry.key] = entry.dialogueFile;
            }
        }
    }

    void Start()
    {
        // Find player via PlayerComponents
        PlayerComponents pc = PlayerComponents.Local;
        if (pc != null && pc.Movement != null)
            _playerTransform = pc.Movement.transform;
        else
            Debug.LogWarning("[NPCDialogueTrigger] Could not find PlayerComponents/PlayerMovement in scene.");

        // Subscribe to dialogue end events
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueEnded += OnDialogueEnded;
        }
    }

    void OnDestroy()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueEnded -= OnDialogueEnded;
        }
    }

    void Update()
    {
        // Only auto-trigger the first dialogue when NPC reaches WaitingForCheckout
        if (!_hasTriggeredInitialDialogue && !_dialogueInProgress && CanTriggerDialogue())
        {
            TryStartDialogue();
        }
    }

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// Start a new conversation with this NPC.
    /// Called by the computer screen "Talk to Customer" button.
    /// </summary>
    public void StartNewConversation()
    {
        if (_dialogueInProgress)
        {
            DebugLog("[NPCDialogueTrigger] Dialogue already in progress.");
            return;
        }

        if (dialogueFiles == null || dialogueFiles.Length == 0)
        {
            Debug.LogWarning("[NPCDialogueTrigger] No dialogue files assigned!");
            return;
        }

        // Advance to next dialogue file (cycle)
        _dialogueIndex = (_dialogueIndex + 1) % dialogueFiles.Length;
        LoadAndStartDialogue(_dialogueIndex);
    }

    /// <summary>
    /// Start an info-specific dialogue by key (triggered from NPCInfoTalkButton).
    /// Looks up the key in the infoDialogues list. Falls back to StartNewConversation
    /// if the key is not found.
    /// </summary>
    public void StartInfoDialogue(string key)
    {
        if (_dialogueInProgress)
        {
            DebugLog("[NPCDialogueTrigger] Dialogue already in progress.");
            return;
        }

        if (string.IsNullOrEmpty(key) || _infoDialogueLookup == null || !_infoDialogueLookup.TryGetValue(key, out TextAsset dialogueFile))
        {
            Debug.LogWarning($"[NPCDialogueTrigger] No info dialogue found for key '{key}'. Falling back to StartNewConversation.");
            StartNewConversation();
            return;
        }

        _loadedData = DialogueLoader.Load(dialogueFile, out _loadedLookup);
        if (_loadedData == null || _loadedLookup == null) return;

        string speakerName = _npcController.NpcIdentity != null ? _npcController.NpcIdentity.fullName : null;

        _dialogueInProgress = true;
        DialogueManager.Instance.StartDialogue(_loadedData, _loadedLookup, transform, speakerName);

        DebugLog($"[NPCDialogueTrigger] Started info dialogue '{_loadedData.dialogueId}' (key: '{key}')");
    }

    /// <summary>
    /// Whether this NPC has an info dialogue registered for the given key.
    /// </summary>
    public bool HasInfoDialogue(string key)
    {
        return !string.IsNullOrEmpty(key) && _infoDialogueLookup != null && _infoDialogueLookup.ContainsKey(key);
    }

    /// <summary>
    /// Whether this NPC is currently in a state where dialogue can occur.
    /// </summary>
    public bool IsAvailableForDialogue()
    {
        if (_npcController == null) return false;
        if (_dialogueInProgress) return false;

        return _npcController.GetCurrentState() == "WaitingForCheckout";
    }

    /// <summary>
    /// Whether a dialogue is currently in progress with this NPC.
    /// </summary>
    public bool IsDialogueInProgress => _dialogueInProgress;

    // ── Private Helpers ─────────────────────────────────────────────

    private bool CanTriggerDialogue()
    {
        if (_npcController == null || _playerTransform == null) return false;
        if (dialogueFiles == null || dialogueFiles.Length == 0) return false;

        // Don't trigger if player is in a focused mode (computer, pill station, etc.)
        PlayerComponents pc = PlayerComponents.Local;
        FocusStateManager focus = pc != null ? pc.FocusState : null;
        if (focus != null && (focus.IsFocused || focus.IsTransitioning))
            return false;

        // Check NPC state
        if (_npcController.GetCurrentState() != "WaitingForCheckout")
            return false;

        // Check player range
        float distance = Vector3.Distance(transform.position, _playerTransform.position);
        if (distance > playerRange)
            return false;

        // Check line of sight
        if (requireLineOfSight && !HasLineOfSight())
            return false;

        // Check that DialogueManager exists and isn't busy
        if (DialogueManager.Instance == null || DialogueManager.Instance.IsActive)
            return false;

        return true;
    }

    private bool HasLineOfSight()
    {
        if (_playerTransform == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.5f; // Eye height
        Vector3 target = _playerTransform.position + Vector3.up * 1.5f;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        // Raycast toward player — if it doesn't hit anything (or hits the player), we have LOS
        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, lineOfSightMask))
        {
            // Check if we hit the player
            if (hit.transform == _playerTransform || hit.transform.IsChildOf(_playerTransform))
                return true;

            DebugLog($"[NPCDialogueTrigger] LOS blocked by {hit.collider.gameObject.name}");
            return false;
        }

        // Nothing blocked the ray
        return true;
    }

    private void TryStartDialogue()
    {
        _hasTriggeredInitialDialogue = true;
        LoadAndStartDialogue(0);
    }

    private void LoadAndStartDialogue(int index)
    {
        if (index < 0 || index >= dialogueFiles.Length)
        {
            Debug.LogWarning($"[NPCDialogueTrigger] Dialogue index {index} out of range.");
            return;
        }

        TextAsset jsonAsset = dialogueFiles[index];
        if (jsonAsset == null)
        {
            Debug.LogWarning($"[NPCDialogueTrigger] Dialogue file at index {index} is null.");
            return;
        }

        _loadedData = DialogueLoader.Load(jsonAsset, out _loadedLookup);
        if (_loadedData == null || _loadedLookup == null) return;

        string speakerName = _npcController.NpcIdentity != null ? _npcController.NpcIdentity.fullName : null;

        _dialogueInProgress = true;
        DialogueManager.Instance.StartDialogue(_loadedData, _loadedLookup, transform, speakerName);

        DebugLog($"[NPCDialogueTrigger] Started dialogue '{_loadedData.dialogueId}' (file index {index})");
    }

    private void OnDialogueEnded()
    {
        if (_dialogueInProgress)
        {
            _dialogueInProgress = false;
            DebugLog("[NPCDialogueTrigger] Dialogue ended.");
        }
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs) Debug.Log(message);
    }

    // ── Gizmos ──────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Draw player detection range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, playerRange);
    }
}
