using System;
using System.Collections.Generic;
using Unity.Netcode;
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
///
/// Multiplayer: only one player can dialogue with an NPC at a time.
/// A NetworkVariable lock prevents other players from starting dialogue
/// while one player is already talking. The initial auto-trigger dialogue
/// is synced — once any player completes it, it won't auto-trigger for others.
/// </summary>
[RequireComponent(typeof(NPCInteractionController))]
public class NPCDialogueTrigger : NetworkBehaviour
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

    [Header("Question Budget")]
    [Tooltip("Maximum number of info questions players can ask this NPC before it gets impatient. Shared across all players.")]
    [SerializeField] private int maxQuestions = 5;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // ── Network State ────────────────────────────────────────────────
    // Tracks which client currently "owns" dialogue with this NPC.
    // ulong.MaxValue = nobody. Server-authoritative.
    private NetworkVariable<ulong> _dialogueOwnerId = new NetworkVariable<ulong>(
        ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Once any player completes the initial auto-trigger dialogue, this is set
    // to true so it won't auto-trigger for other players.
    private NetworkVariable<bool> _initialDialogueCompleted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // How many info questions remain. Decremented server-side when any player asks a question.
    // Readable by all clients for UI (NPCInfoTalkButton shows remaining count).
    private NetworkVariable<int> _questionsRemaining = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Dialogue request types for RPC communication ─────────────────
    private const byte REQUEST_AUTO = 0;
    private const byte REQUEST_NEW_CONVERSATION = 1;
    private const byte REQUEST_INFO = 2;

    // ── Runtime State ───────────────────────────────────────────────
    private NPCInteractionController _npcController;
    private bool _hasTriggeredInitialDialogue; // local fallback for non-networked
    private int _dialogueIndex;
    private bool _dialogueInProgress;
    private int _localQuestionsRemaining; // non-networked fallback

    // Pre-loaded dialogue data for current file
    private DialogueData _loadedData;
    private Dictionary<string, DialogueNode> _loadedLookup;

    // Runtime lookup for info dialogues
    private Dictionary<string, TextAsset> _infoDialogueLookup;

    // Tracks which DialogueManager we subscribed to so we can unsubscribe cleanly.
    private DialogueManager _activeDialogueManager;

    // Use the local player's transform for range/LOS checks.
    // Each client checks their own player — avoids cross-client dialogue triggers.
    private Transform PlayerTransform =>
        PlayerComponents.Local?.Movement?.transform;

    /// <summary>
    /// Whether another player currently has the dialogue lock on this NPC.
    /// </summary>
    public bool IsLockedByAnotherPlayer
    {
        get
        {
            if (!IsSpawned) return false;
            ulong owner = _dialogueOwnerId.Value;
            if (owner == ulong.MaxValue) return false;
            return owner != NetworkManager.Singleton.LocalClientId;
        }
    }

    void Awake()
    {
        _npcController = GetComponent<NPCInteractionController>();
        _localQuestionsRemaining = maxQuestions;

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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
            _questionsRemaining.Value = maxQuestions;
    }

    void OnDestroy()
    {
        // Unsubscribe from whichever DialogueManager we last used
        if (_activeDialogueManager != null)
            _activeDialogueManager.OnDialogueEnded -= OnDialogueEnded;
    }

    void Update()
    {
        // Only auto-trigger the first dialogue when NPC reaches WaitingForCheckout
        if (!IsInitialDialogueCompleted() && !_dialogueInProgress && CanTriggerDialogue())
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

        if (IsLockedByAnotherPlayer)
        {
            DebugLog("[NPCDialogueTrigger] Another player is in dialogue with this NPC.");
            return;
        }

        if (dialogueFiles == null || dialogueFiles.Length == 0)
        {
            Debug.LogWarning("[NPCDialogueTrigger] No dialogue files assigned!");
            return;
        }

        // Advance to next dialogue file (cycle)
        _dialogueIndex = (_dialogueIndex + 1) % dialogueFiles.Length;

        if (IsSpawned)
        {
            RequestDialogueLockServerRpc(REQUEST_NEW_CONVERSATION, _dialogueIndex, "");
        }
        else
        {
            // Non-networked fallback
            DoLoadAndStartDialogue(_dialogueIndex);
        }
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

        if (IsLockedByAnotherPlayer)
        {
            DebugLog("[NPCDialogueTrigger] Another player is in dialogue with this NPC.");
            return;
        }

        if (string.IsNullOrEmpty(key) || _infoDialogueLookup == null || !_infoDialogueLookup.ContainsKey(key))
        {
            Debug.LogWarning($"[NPCDialogueTrigger] No info dialogue found for key '{key}'. Falling back to StartNewConversation.");
            StartNewConversation();
            return;
        }

        if (IsSpawned)
        {
            RequestDialogueLockServerRpc(REQUEST_INFO, 0, key);
        }
        else
        {
            // Non-networked fallback — decrement budget locally
            if (_localQuestionsRemaining <= 0)
            {
                Debug.LogWarning("[NPCDialogueTrigger] Question budget exhausted.");
                return;
            }
            _localQuestionsRemaining--;
            if (_localQuestionsRemaining <= 0)
                OnBudgetExhausted?.Invoke();

            DoStartInfoDialogue(key);
        }
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
    /// Checks both local state and the network dialogue lock.
    /// </summary>
    public bool IsAvailableForDialogue()
    {
        if (_npcController == null) return false;
        if (_dialogueInProgress) return false;

        // If another player has the dialogue lock, this NPC is unavailable
        if (IsLockedByAnotherPlayer) return false;

        return _npcController.GetCurrentState() == "WaitingForCheckout";
    }

    /// <summary>
    /// Whether a dialogue is currently in progress with this NPC.
    /// </summary>
    public bool IsDialogueInProgress => _dialogueInProgress;

    /// <summary>
    /// How many info questions remain for this NPC. Shared across all players.
    /// </summary>
    public int QuestionsRemaining => IsSpawned ? _questionsRemaining.Value : _localQuestionsRemaining;

    /// <summary>Maximum questions this NPC allows (inspector value).</summary>
    public int MaxQuestions => maxQuestions;

    /// <summary>Fired when the question budget hits 0.</summary>
    public event Action OnBudgetExhausted;

    // ── Network RPCs ─────────────────────────────────────────────────

    /// <summary>
    /// Client requests the dialogue lock from the server.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RequestDialogueLockServerRpc(byte requestType, int dialogueIndex, string infoKey, ServerRpcParams rpcParams = default)
    {
        ulong requesterId = rpcParams.Receive.SenderClientId;

        // Deny if already locked by someone else
        if (_dialogueOwnerId.Value != ulong.MaxValue)
        {
            DebugLog($"[NPCDialogueTrigger] Lock denied for client {requesterId} — already owned by {_dialogueOwnerId.Value}");
            return;
        }

        // For auto-trigger, deny if initial dialogue already completed
        if (requestType == REQUEST_AUTO && _initialDialogueCompleted.Value)
        {
            DebugLog($"[NPCDialogueTrigger] Auto-trigger denied for client {requesterId} — already completed by another player");
            return;
        }

        // For info requests, check and decrement the question budget
        if (requestType == REQUEST_INFO)
        {
            if (_questionsRemaining.Value <= 0)
            {
                DebugLog($"[NPCDialogueTrigger] Info request denied for client {requesterId} — question budget exhausted");
                return;
            }
            _questionsRemaining.Value--;
            DebugLog($"[NPCDialogueTrigger] Question budget: {_questionsRemaining.Value} remaining");

            if (_questionsRemaining.Value <= 0)
                OnBudgetExhausted?.Invoke();
        }

        // Grant the lock
        _dialogueOwnerId.Value = requesterId;

        if (requestType == REQUEST_AUTO)
            _initialDialogueCompleted.Value = true;

        // Tell the requesting client to start their dialogue locally
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { requesterId } }
        };
        GrantDialogueLockClientRpc(requestType, dialogueIndex, infoKey, clientRpcParams);
    }

    /// <summary>
    /// Server tells the requesting client that the lock was granted — start dialogue locally.
    /// </summary>
    [ClientRpc]
    private void GrantDialogueLockClientRpc(byte requestType, int dialogueIndex, string infoKey, ClientRpcParams clientRpcParams = default)
    {
        switch (requestType)
        {
            case REQUEST_AUTO:
                DoLoadAndStartDialogue(0);
                break;
            case REQUEST_NEW_CONVERSATION:
                DoLoadAndStartDialogue(dialogueIndex);
                break;
            case REQUEST_INFO:
                DoStartInfoDialogue(infoKey);
                break;
        }
    }

    /// <summary>
    /// Client releases the dialogue lock after dialogue ends.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void ReleaseDialogueLockServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (_dialogueOwnerId.Value == senderId)
        {
            _dialogueOwnerId.Value = ulong.MaxValue;
            DebugLog($"[NPCDialogueTrigger] Dialogue lock released by client {senderId}");
        }
    }

    /// <summary>
    /// Server-only: force-releases the dialogue lock if held by the given client (e.g. on disconnect).
    /// </summary>
    public void ForceReleaseLock(ulong clientId)
    {
        if (!IsServer) return;
        if (_dialogueOwnerId.Value == clientId)
        {
            _dialogueOwnerId.Value = ulong.MaxValue;
            DebugLog($"[NPCDialogueTrigger] Dialogue lock force-released for disconnected client {clientId}");
        }
    }

    // ── Private Helpers ─────────────────────────────────────────────

    /// <summary>
    /// Whether the initial auto-trigger dialogue has been completed (by any player if networked).
    /// </summary>
    private bool IsInitialDialogueCompleted()
    {
        if (IsSpawned) return _initialDialogueCompleted.Value;
        return _hasTriggeredInitialDialogue;
    }

    private bool CanTriggerDialogue()
    {
        PlayerComponents pc = PlayerComponents.Local;
        if (pc == null) return false;

        Transform pt = pc.Movement?.transform;
        if (_npcController == null || pt == null) return false;
        if (dialogueFiles == null || dialogueFiles.Length == 0) return false;

        // Don't trigger if local player is in a focused mode (computer, pill station, etc.)
        FocusStateManager focus = pc.FocusState;
        if (focus != null && (focus.IsFocused || focus.IsTransitioning))
            return false;

        // Check NPC state
        if (_npcController.GetCurrentState() != "WaitingForCheckout")
            return false;

        // If another player already has the dialogue lock, don't try
        if (IsLockedByAnotherPlayer)
            return false;

        // Check player range
        float distance = Vector3.Distance(transform.position, pt.position);
        if (distance > playerRange)
            return false;

        // Check line of sight
        if (requireLineOfSight && !HasLineOfSight())
            return false;

        // Check that the local player's DialogueManager exists and isn't busy
        DialogueManager dm = pc.Dialogue;
        if (dm == null || dm.IsActive)
            return false;

        return true;
    }

    private bool HasLineOfSight()
    {
        Transform pt = PlayerTransform;
        if (pt == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.5f; // Eye height
        Vector3 target = pt.position + Vector3.up * 1.5f;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        // Raycast toward player — if it doesn't hit anything (or hits the player), we have LOS
        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, lineOfSightMask))
        {
            // Check if we hit the player
            if (hit.transform == pt || hit.transform.IsChildOf(pt))
                return true;

            DebugLog($"[NPCDialogueTrigger] LOS blocked by {hit.collider.gameObject.name}");
            return false;
        }

        // Nothing blocked the ray
        return true;
    }

    private void TryStartDialogue()
    {
        if (IsSpawned)
        {
            // Request lock from server — dialogue will start in GrantDialogueLockClientRpc
            RequestDialogueLockServerRpc(REQUEST_AUTO, 0, "");
        }
        else
        {
            // Non-networked fallback
            _hasTriggeredInitialDialogue = true;
            DoLoadAndStartDialogue(0);
        }
    }

    /// <summary>
    /// Actually loads and starts a dialogue file locally. Called after lock is granted.
    /// </summary>
    private void DoLoadAndStartDialogue(int index)
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

        DialogueManager dm = PlayerComponents.Local?.Dialogue;
        if (dm == null)
        {
            Debug.LogWarning("[NPCDialogueTrigger] No DialogueManager found on local player.");
            return;
        }

        _loadedData = DialogueLoader.Load(jsonAsset, out _loadedLookup);
        if (_loadedData == null || _loadedLookup == null) return;

        string speakerName = _npcController.NpcIdentity != null ? _npcController.NpcIdentity.fullName : null;

        _dialogueInProgress = true;
        SubscribeToDialogueEnd(dm);
        dm.StartDialogue(_loadedData, _loadedLookup, transform, speakerName);

        DebugLog($"[NPCDialogueTrigger] Started dialogue '{_loadedData.dialogueId}' (file index {index})");
    }

    /// <summary>
    /// Actually starts an info dialogue locally. Called after lock is granted.
    /// </summary>
    private void DoStartInfoDialogue(string key)
    {
        if (string.IsNullOrEmpty(key) || _infoDialogueLookup == null || !_infoDialogueLookup.TryGetValue(key, out TextAsset dialogueFile))
        {
            Debug.LogWarning($"[NPCDialogueTrigger] No info dialogue found for key '{key}'.");
            return;
        }

        DialogueManager dm = PlayerComponents.Local?.Dialogue;
        if (dm == null)
        {
            Debug.LogWarning("[NPCDialogueTrigger] No DialogueManager found on local player.");
            return;
        }

        _loadedData = DialogueLoader.Load(dialogueFile, out _loadedLookup);
        if (_loadedData == null || _loadedLookup == null) return;

        string speakerName = _npcController.NpcIdentity != null ? _npcController.NpcIdentity.fullName : null;

        _dialogueInProgress = true;
        SubscribeToDialogueEnd(dm);
        dm.StartDialogue(_loadedData, _loadedLookup, transform, speakerName);

        DebugLog($"[NPCDialogueTrigger] Started info dialogue '{_loadedData.dialogueId}' (key: '{key}')");
    }

    private void OnDialogueEnded()
    {
        if (_dialogueInProgress)
        {
            _dialogueInProgress = false;
            UnsubscribeFromDialogueEnd();

            // Release the network lock so other players can interact
            if (IsSpawned)
                ReleaseDialogueLockServerRpc();

            DebugLog("[NPCDialogueTrigger] Dialogue ended.");
        }
    }

    private void SubscribeToDialogueEnd(DialogueManager dm)
    {
        // Unsubscribe from any previous DM first to avoid double-callbacks
        if (_activeDialogueManager != null)
            _activeDialogueManager.OnDialogueEnded -= OnDialogueEnded;

        _activeDialogueManager = dm;
        if (dm != null)
            dm.OnDialogueEnded += OnDialogueEnded;
    }

    private void UnsubscribeFromDialogueEnd()
    {
        if (_activeDialogueManager != null)
        {
            _activeDialogueManager.OnDialogueEnded -= OnDialogueEnded;
            _activeDialogueManager = null;
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
