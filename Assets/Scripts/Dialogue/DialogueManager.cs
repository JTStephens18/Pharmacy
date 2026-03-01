using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton that drives the on-screen dialogue overlay.
/// Shows NPC dialogue text and spawns clickable response buttons.
/// Smoothly lerps the camera to face the NPC when dialogue starts.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Root panel for the dialogue overlay. Enabled/disabled to show/hide.")]
    [SerializeField] private GameObject dialoguePanel;

    [Tooltip("TextMeshPro text for the speaker's name.")]
    [SerializeField] private TextMeshProUGUI speakerNameText;

    [Tooltip("TextMeshPro text for the dialogue body.")]
    [SerializeField] private TextMeshProUGUI dialogueBodyText;

    [Tooltip("Container Transform where response buttons are spawned.")]
    [SerializeField] private Transform responseContainer;

    [Tooltip("Prefab for response buttons. Must have a Button + TextMeshProUGUI child.")]
    [SerializeField] private GameObject responseButtonPrefab;

    [Header("Settings")]
    [Tooltip("Text shown on the close button when dialogue has no more responses.")]
    [SerializeField] private string closeButtonText = "[Continue]";

    [Header("Camera Look-At")]
    [Tooltip("Duration of the camera lerp to face the NPC (seconds).")]
    [SerializeField] private float cameraLerpDuration = 0.5f;

    [Tooltip("Vertical offset from NPC pivot to aim at (roughly head height).")]
    [SerializeField] private float npcHeadOffset = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dialogueOpenSound;
    [SerializeField] private AudioClip responseClickSound;

    // ── Events ──────────────────────────────────────────────────────
    /// <summary>Fires when a dialogue conversation ends (all responses exhausted or closed).</summary>
    public event Action OnDialogueEnded;

    /// <summary>Fires when any dialogue starts.</summary>
    public event Action OnDialogueStarted;

    // ── Runtime State ───────────────────────────────────────────────
    private DialogueData _currentDialogue;
    private Dictionary<string, DialogueNode> _nodeLookup;
    private DialogueNode _currentNode;
    private bool _isActive;
    private List<GameObject> _spawnedButtons = new List<GameObject>();
    private Transform _lookAtTarget;
    private Coroutine _cameraLerpCoroutine;
    private string _speakerNameOverride;
    private bool _suppressEndDialogueReset;

    /// <summary>Whether a dialogue is currently being displayed.</summary>
    public bool IsActive => _isActive;

    // ── Reference to history for recording ──────────────────────────
    private DialogueHistory _history;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DialogueManager] Duplicate instance found — destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _history = GetComponent<DialogueHistory>();
        if (_history == null)
            _history = GetComponentInChildren<DialogueHistory>();

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// Start a dialogue conversation from a TextAsset JSON file.
    /// </summary>
    public void StartDialogue(TextAsset jsonAsset, Transform npcTransform = null)
    {
        if (jsonAsset == null)
        {
            Debug.LogError("[DialogueManager] Cannot start dialogue: TextAsset is null.");
            return;
        }

        DialogueData data = DialogueLoader.Load(jsonAsset, out Dictionary<string, DialogueNode> lookup);
        if (data == null || lookup == null) return;

        StartDialogue(data, lookup, npcTransform);
    }

    /// <summary>
    /// Start a dialogue conversation from pre-loaded data.
    /// Optionally pass the NPC transform to smoothly look at them.
    /// speakerNameOverride replaces the JSON's root speakerName (but node-level overrides still win).
    /// </summary>
    public void StartDialogue(DialogueData data, Dictionary<string, DialogueNode> nodeLookup, Transform npcTransform = null, string speakerNameOverride = null)
    {
        if (_isActive)
        {
            Debug.LogWarning("[DialogueManager] Dialogue already active — ending current before starting new.");
            EndDialogue();
        }

        if (data == null || nodeLookup == null)
        {
            Debug.LogError("[DialogueManager] Cannot start dialogue: data or lookup is null.");
            return;
        }

        _currentDialogue = data;
        _nodeLookup = nodeLookup;
        _lookAtTarget = npcTransform;
        _speakerNameOverride = speakerNameOverride;
        _isActive = true;

        Debug.Log($"[DialogueManager] Starting dialogue '{data.dialogueId}' with {nodeLookup.Count} nodes.");

        // Show the panel
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        // Unlock cursor for button clicking
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;

        // Disable camera look and player movement during dialogue
        if (MouseLook.Instance != null)
            MouseLook.Instance.enabled = false;

        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
            playerMovement.enabled = false;

        // Smoothly look at NPC
        if (_lookAtTarget != null && MouseLook.Instance != null)
        {
            if (_cameraLerpCoroutine != null)
                StopCoroutine(_cameraLerpCoroutine);
            _cameraLerpCoroutine = StartCoroutine(LerpCameraToTarget());
        }

        // Play sound
        if (audioSource != null && dialogueOpenSound != null)
            audioSource.PlayOneShot(dialogueOpenSound);

        // Show the start node
        if (_nodeLookup.TryGetValue(data.startNodeId, out DialogueNode startNode))
        {
            ShowNode(startNode);
        }
        else
        {
            Debug.LogError($"[DialogueManager] Start node '{data.startNodeId}' not found!");
            EndDialogue();
        }

        OnDialogueStarted?.Invoke();
    }

    /// <summary>
    /// When true, EndDialogue will skip relocking the cursor and re-enabling
    /// MouseLook/PlayerMovement. Used when focus will immediately re-enter
    /// (e.g. returning to the computer screen after dialogue).
    /// Automatically cleared after EndDialogue runs.
    /// </summary>
    public void SetSuppressEndReset(bool suppress)
    {
        _suppressEndDialogueReset = suppress;
    }

    /// <summary>
    /// End the current dialogue and hide the overlay.
    /// </summary>
    public void EndDialogue()
    {
        if (!_isActive) return;

        _isActive = false;
        _currentDialogue = null;
        _nodeLookup = null;
        _currentNode = null;
        _lookAtTarget = null;
        _speakerNameOverride = null;

        // Stop camera lerp if still running
        if (_cameraLerpCoroutine != null)
        {
            StopCoroutine(_cameraLerpCoroutine);
            _cameraLerpCoroutine = null;
        }

        // Hide panel
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        // Clear spawned buttons
        ClearResponseButtons();

        if (!_suppressEndDialogueReset)
        {
            // Relock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Sync MouseLook pitch to current camera angle and re-enable
            if (MouseLook.Instance != null)
            {
                // Normalize pitch from 0-360 to -180-180 range
                float pitch = MouseLook.Instance.transform.localEulerAngles.x;
                if (pitch > 180f) pitch -= 360f;
                MouseLook.Instance.SetCurrentPitch(pitch);
                MouseLook.Instance.enabled = true;
            }

            // Re-enable player movement
            PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
            if (playerMovement != null)
                playerMovement.enabled = true;
        }
        _suppressEndDialogueReset = false;

        Debug.Log("[DialogueManager] Dialogue ended.");

        OnDialogueEnded?.Invoke();
    }

    // ── Camera Lerp ─────────────────────────────────────────────────

    /// <summary>
    /// Smoothly rotates the camera (pitch) and player body (yaw) to face the NPC.
    /// </summary>
    private IEnumerator LerpCameraToTarget()
    {
        MouseLook ml = MouseLook.Instance;
        Transform cam = ml.transform;
        Transform body = ml.PlayerBody;

        if (cam == null || body == null || _lookAtTarget == null)
            yield break;

        // Target position: NPC head height
        Vector3 targetPos = _lookAtTarget.position + Vector3.up * npcHeadOffset;

        // Starting rotations
        Quaternion startBodyRot = body.rotation;
        float startPitch = cam.localEulerAngles.x;
        if (startPitch > 180f) startPitch -= 360f;

        float elapsed = 0f;

        while (elapsed < cameraLerpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / cameraLerpDuration);

            // Recalculate target direction each frame (NPC might be animating)
            Vector3 dirToNPC = targetPos - cam.position;

            // Yaw: horizontal rotation on player body
            float targetYaw = Mathf.Atan2(dirToNPC.x, dirToNPC.z) * Mathf.Rad2Deg;
            Quaternion targetBodyRot = Quaternion.Euler(0f, targetYaw, 0f);
            body.rotation = Quaternion.Slerp(startBodyRot, targetBodyRot, t);

            // Pitch: vertical rotation on camera
            float targetPitch = -Mathf.Asin(dirToNPC.normalized.y) * Mathf.Rad2Deg;
            float currentPitch = Mathf.Lerp(startPitch, targetPitch, t);
            cam.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);

            yield return null;
        }

        // Final snap
        Vector3 finalDir = targetPos - cam.position;
        float finalYaw = Mathf.Atan2(finalDir.x, finalDir.z) * Mathf.Rad2Deg;
        body.rotation = Quaternion.Euler(0f, finalYaw, 0f);

        float finalPitch = -Mathf.Asin(finalDir.normalized.y) * Mathf.Rad2Deg;
        cam.localRotation = Quaternion.Euler(finalPitch, 0f, 0f);

        _cameraLerpCoroutine = null;
    }

    // ── Private Helpers ─────────────────────────────────────────────

    /// <summary>
    /// Display a dialogue node: update text and spawn response buttons.
    /// </summary>
    private void ShowNode(DialogueNode node)
    {
        _currentNode = node;

        // Determine speaker name: node-level → identity override → JSON default
        string speaker = !string.IsNullOrEmpty(node.speakerName)
            ? node.speakerName
            : (!string.IsNullOrEmpty(_speakerNameOverride)
                ? _speakerNameOverride
                : (_currentDialogue != null ? _currentDialogue.speakerName : "???"));

        // Update UI text
        if (speakerNameText != null)
            speakerNameText.text = speaker;

        if (dialogueBodyText != null)
            dialogueBodyText.text = node.text;

        // Record in history
        if (_history != null)
            _history.RecordLine(speaker, node.text);

        // Clear old buttons and spawn new ones
        ClearResponseButtons();

        if (node.IsTerminal)
        {
            // No responses — show a close/continue button
            SpawnButton(closeButtonText, () =>
            {
                EndDialogue();
            });
        }
        else
        {
            foreach (DialogueResponse response in node.responses)
            {
                string responseText = response.text;
                string nextNodeId = response.nextNodeId;

                SpawnButton(responseText, () =>
                {
                    OnResponseClicked(responseText, nextNodeId);
                });
            }
        }
    }

    /// <summary>
    /// Handle a response button click: record the choice and navigate to the next node.
    /// </summary>
    private void OnResponseClicked(string responseText, string nextNodeId)
    {
        // Play click sound
        if (audioSource != null && responseClickSound != null)
            audioSource.PlayOneShot(responseClickSound);

        // Record player response in history
        if (_history != null)
            _history.RecordLine("You", responseText);

        Debug.Log($"[DialogueManager] Response chosen: \"{responseText}\" → node '{nextNodeId}'");

        // Navigate to next node
        if (string.IsNullOrEmpty(nextNodeId))
        {
            EndDialogue();
            return;
        }

        if (_nodeLookup != null && _nodeLookup.TryGetValue(nextNodeId, out DialogueNode nextNode))
        {
            ShowNode(nextNode);
        }
        else
        {
            Debug.LogError($"[DialogueManager] Node '{nextNodeId}' not found! Ending dialogue.");
            EndDialogue();
        }
    }

    /// <summary>
    /// Spawn a response button in the container.
    /// </summary>
    private void SpawnButton(string text, Action onClick)
    {
        if (responseButtonPrefab == null || responseContainer == null)
        {
            Debug.LogError("[DialogueManager] Response button prefab or container not assigned!");
            return;
        }

        GameObject buttonObj = Instantiate(responseButtonPrefab, responseContainer);
        _spawnedButtons.Add(buttonObj);

        // Set button text
        TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
            buttonText.text = text;
        else
        {
            // Fallback: try legacy Text
            Text legacyText = buttonObj.GetComponentInChildren<Text>();
            if (legacyText != null)
                legacyText.text = text;
        }

        // Wire click
        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => onClick());
        }
        else
        {
            Debug.LogWarning("[DialogueManager] Response button prefab has no Button component!");
        }
    }

    /// <summary>
    /// Destroy all spawned response buttons.
    /// </summary>
    private void ClearResponseButtons()
    {
        foreach (GameObject btn in _spawnedButtons)
        {
            if (btn != null)
                Destroy(btn);
        }
        _spawnedButtons.Clear();
    }
}
