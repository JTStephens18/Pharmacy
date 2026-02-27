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
    private bool _isDisplaying;

    /// <summary>Whether NPC info is currently being displayed.</summary>
    public bool IsDisplaying => _isDisplaying;

    /// <summary>The currently displayed NPC identity (null if none).</summary>
    public NPCIdentity CurrentIdentity => _currentIdentity;

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

        // Populate all NPCIdentityField components inside the panel automatically
        if (npcInfoPanel != null)
        {
            foreach (NPCIdentityField field in npcInfoPanel.GetComponentsInChildren<NPCIdentityField>())
                field.Populate(identity);
        }

        // Set photo
        if (photoImage != null)
        {
            if (identity.photoSprite != null)
            {
                photoImage.sprite = identity.photoSprite;
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

        Debug.Log($"[NPCInfoDisplay] Showing info for '{identity.fullName}' on main view.");
    }

    /// <summary>
    /// Clears the NPC info display and hides the info panel.
    /// Called when the NPC exits the store or is destroyed.
    /// </summary>
    public void ClearNPCInfo()
    {
        if (!_isDisplaying) return;

        _currentIdentity = null;
        _isDisplaying = false;

        // Clear all NPCIdentityField components inside the panel
        if (npcInfoPanel != null)
        {
            foreach (NPCIdentityField field in npcInfoPanel.GetComponentsInChildren<NPCIdentityField>())
                field.Clear();
        }

        // Hide photo
        if (photoImage != null)
            photoImage.enabled = false;

        // Hide the info panel
        if (npcInfoPanel != null)
            npcInfoPanel.SetActive(false);

        Debug.Log("[NPCInfoDisplay] Cleared NPC info, panel hidden.");
    }
}
