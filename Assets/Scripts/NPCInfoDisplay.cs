using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Singleton that populates the computer screen with NPC identity data after barcode scanning.
/// Lives on the computer screen's InteractiveUI and manages the NPC info view.
///
/// Usage:
///   NPCInfoDisplay.Instance.ShowNPCInfo(npcIdentity);  // After barcode scan
///   NPCInfoDisplay.Instance.ClearNPCInfo();             // When NPC exits
/// </summary>
public class NPCInfoDisplay : MonoBehaviour
{
    public static NPCInfoDisplay Instance { get; private set; }

    [Header("Computer Screen Reference")]
    [Tooltip("The ComputerScreenController to switch views on.")]
    [SerializeField] private ComputerScreenController screenController;

    [Tooltip("The name of the NPC Info view in the ComputerScreenController views array.")]
    [SerializeField] private string npcInfoViewName = "NPCInfo";

    [Header("UI Text Fields")]
    [Tooltip("Displays the NPC's full name.")]
    [SerializeField] private TextMeshProUGUI nameText;

    [Tooltip("Displays the NPC's date of birth.")]
    [SerializeField] private TextMeshProUGUI dobText;

    [Tooltip("Displays the NPC's address.")]
    [SerializeField] private TextMeshProUGUI addressText;

    [Tooltip("Displays the NPC's ID number.")]
    [SerializeField] private TextMeshProUGUI idNumberText;

    [Header("UI Image")]
    [Tooltip("Optional: Displays the NPC's photo.")]
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

        // Auto-find ComputerScreenController if not assigned
        if (screenController == null)
        {
            screenController = GetComponentInParent<ComputerScreenController>();
            if (screenController == null)
                screenController = FindFirstObjectByType<ComputerScreenController>();
        }
    }

    /// <summary>
    /// Populates the NPC info fields and switches the computer to the NPC Info view.
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

        // Populate text fields
        if (nameText != null)
            nameText.text = identity.fullName;

        if (dobText != null)
            dobText.text = identity.dateOfBirth;

        if (addressText != null)
            addressText.text = identity.address;

        if (idNumberText != null)
            idNumberText.text = identity.idNumber;

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

        // Switch computer screen to NPC info view
        if (screenController != null)
        {
            screenController.ShowView(npcInfoViewName);
            Debug.Log($"[NPCInfoDisplay] Showing info for '{identity.fullName}' on computer screen.");
        }
        else
        {
            Debug.LogWarning("[NPCInfoDisplay] No ComputerScreenController assigned — cannot switch view.");
        }
    }

    /// <summary>
    /// Clears the NPC info display and reverts the computer to its main view.
    /// Called when the NPC exits the store or is destroyed.
    /// </summary>
    public void ClearNPCInfo()
    {
        if (!_isDisplaying) return;

        _currentIdentity = null;
        _isDisplaying = false;

        // Clear text fields
        if (nameText != null) nameText.text = "";
        if (dobText != null) dobText.text = "";
        if (addressText != null) addressText.text = "";
        if (idNumberText != null) idNumberText.text = "";

        // Hide photo
        if (photoImage != null)
            photoImage.enabled = false;

        // Revert computer screen to main view
        if (screenController != null)
        {
            screenController.ResetToMain();
            Debug.Log("[NPCInfoDisplay] Cleared NPC info, computer reverted to main view.");
        }
    }
}
