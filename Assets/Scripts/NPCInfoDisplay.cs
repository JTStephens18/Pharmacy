using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton that populates the computer screen with NPC identity data after barcode scanning.
/// Lives on the computer screen's InteractiveUI. Shows/hides a panel inside the main view
/// rather than switching to a separate view.
///
/// Text fields are populated automatically: add an NPCIdentityField component to any
/// TextMeshProUGUI inside npcInfoPanel, set its FieldType, and it will be found at runtime.
/// No manual TMP wiring needed.
///
/// When the scanned NPC is a doppelganger, fake data overrides (DOB, address, photo) are
/// applied automatically. Also bridges to PrescriptionDisplay if one exists in the scene.
///
/// Usage:
///   NPCInfoDisplay.Instance.ShowNPCInfo(npcIdentity);  // After barcode scan
///   NPCInfoDisplay.Instance.ClearNPCInfo();             // When NPC exits
/// </summary>
public class NPCInfoDisplay : MonoBehaviour
{
    public static NPCInfoDisplay Instance { get; private set; }

    [Header("NPC Info Panel")]
    [Tooltip("The panel GameObject inside the main view that shows NPC info. " +
             "Text elements inside it with NPCIdentityField components are populated automatically.")]
    [SerializeField] private GameObject npcInfoPanel;

    [Header("UI Image")]
    [Tooltip("Optional: Displays the NPC's photo. Assign the Image component directly.")]
    [SerializeField] private Image photoImage;

    // ── Runtime State ───────────────────────────────────────────────
    private NPCIdentity _currentIdentity;
    private NPCInteractionController _currentNPC;
    private bool _isDisplaying;

    /// <summary>Whether NPC info is currently being displayed.</summary>
    public bool IsDisplaying => _isDisplaying;

    /// <summary>The currently displayed NPC identity (null if none).</summary>
    public NPCIdentity CurrentIdentity => _currentIdentity;

    /// <summary>The NPC controller for the currently displayed NPC (null if none).</summary>
    public NPCInteractionController CurrentNPC => _currentNPC;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NPCInfoDisplay] Duplicate instance found — destroying this one.");
            Destroy(this);
            return;
        }
        Instance = this;

        // Hide the info panel by default until an ID is scanned
        if (npcInfoPanel != null)
            npcInfoPanel.SetActive(false);
    }

    /// <summary>
    /// Populates the NPC info fields and shows the info panel on the main view.
    /// Called by IDCardInteraction after a successful barcode scan.
    /// Finds the matching NPC controller to apply doppelganger overrides and prescription data.
    /// </summary>
    public void ShowNPCInfo(NPCIdentity identity)
    {
        if (identity == null)
        {
            Debug.LogWarning("[NPCInfoDisplay] Cannot show info: NPCIdentity is null.");
            return;
        }

        _currentIdentity = identity;
        _isDisplaying = true;

        // Find the NPC controller that owns this identity (for prescription + doppelganger data)
        _currentNPC = FindNPCByIdentity(identity);
        DoppelgangerProfile profile = _currentNPC?.DoppelgangerData;

        // Populate all NPCIdentityField components inside the panel automatically.
        // If a doppelganger profile exists, fake overrides are applied.
        if (npcInfoPanel != null)
        {
            foreach (NPCIdentityField field in npcInfoPanel.GetComponentsInChildren<NPCIdentityField>())
                field.Populate(identity, profile);

            // Also populate any PrescriptionField components inside the same panel.
            // This allows prescription data to live alongside identity data in one panel.
            PrescriptionData rx = _currentNPC?.Prescription;
            if (rx != null)
            {
                foreach (PrescriptionField field in npcInfoPanel.GetComponentsInChildren<PrescriptionField>())
                    field.Populate(rx, profile);
            }
        }

        // Set photo — doppelganger may have a fake photo
        Sprite displayPhoto = profile != null ? profile.GetPhoto(identity.photoSprite) : identity.photoSprite;
        if (photoImage != null)
        {
            if (displayPhoto != null)
            {
                photoImage.sprite = displayPhoto;
                photoImage.enabled = true;
            }
            else
            {
                photoImage.enabled = false;
            }
        }

        // Show the info panel within the main view
        if (npcInfoPanel != null)
            npcInfoPanel.SetActive(true);

        // Bridge to PrescriptionDisplay if a separate prescription panel exists
        if (_currentNPC != null && PrescriptionDisplay.Instance != null)
            PrescriptionDisplay.Instance.ShowPrescription(_currentNPC);

        Debug.Log($"[NPCInfoDisplay] Showing info for '{identity.fullName}'" +
                  (profile != null ? " (doppelganger)" : "") + " on main view.");
    }

    /// <summary>
    /// Clears the NPC info display and hides the info panel.
    /// Called when the NPC exits the store or is destroyed.
    /// </summary>
    public void ClearNPCInfo()
    {
        if (!_isDisplaying) return;

        _currentIdentity = null;
        _currentNPC = null;
        _isDisplaying = false;

        // Clear all identity and prescription field components inside the panel
        if (npcInfoPanel != null)
        {
            foreach (NPCIdentityField field in npcInfoPanel.GetComponentsInChildren<NPCIdentityField>())
                field.Clear();
            foreach (PrescriptionField field in npcInfoPanel.GetComponentsInChildren<PrescriptionField>())
                field.Clear();
        }

        // Hide photo
        if (photoImage != null)
            photoImage.enabled = false;

        // Hide the info panel
        if (npcInfoPanel != null)
            npcInfoPanel.SetActive(false);

        // Clear prescription display
        if (PrescriptionDisplay.Instance != null)
            PrescriptionDisplay.Instance.ClearPrescription();

        Debug.Log("[NPCInfoDisplay] Cleared NPC info, panel hidden.");
    }

    /// <summary>
    /// Finds the active NPC controller that has the given identity.
    /// Returns null if no matching NPC is found.
    /// </summary>
    private NPCInteractionController FindNPCByIdentity(NPCIdentity identity)
    {
        if (identity == null) return null;

        foreach (var npc in FindObjectsByType<NPCInteractionController>(FindObjectsSortMode.None))
        {
            if (npc.NpcIdentity == identity)
                return npc;
        }

        return null;
    }
}
