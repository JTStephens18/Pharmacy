using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton that populates the computer screen with prescription and prescriber data
/// after an NPC's ID is scanned. Lives on the computer screen's InteractiveUI alongside
/// NPCInfoDisplay.
///
/// Text fields are populated automatically: add a PrescriptionField component to any
/// TextMeshProUGUI inside prescriptionPanel, set its FieldType, and it will be found at runtime.
///
/// When a doppelganger is scanned, fake data overrides are applied automatically.
///
/// Usage:
///   PrescriptionDisplay.Instance.ShowPrescription(npc);  // After barcode scan
///   PrescriptionDisplay.Instance.ClearPrescription();     // When NPC exits
/// </summary>
public class PrescriptionDisplay : MonoBehaviour
{
    public static PrescriptionDisplay Instance { get; private set; }

    [Header("Prescription Panel")]
    [Tooltip("The panel GameObject that shows prescription data. " +
             "Text elements inside it with PrescriptionField components are populated automatically.")]
    [SerializeField] private GameObject prescriptionPanel;

    // ── Runtime State ───────────────────────────────────────────────
    private PrescriptionData _currentPrescription;
    private bool _isDisplaying;

    /// <summary>Whether prescription info is currently being displayed.</summary>
    public bool IsDisplaying => _isDisplaying;

    /// <summary>The currently displayed prescription data (null if none).</summary>
    public PrescriptionData CurrentPrescription => _currentPrescription;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PrescriptionDisplay] Duplicate instance found — destroying this one.");
            Destroy(this);
            return;
        }
        Instance = this;

        if (prescriptionPanel != null)
            prescriptionPanel.SetActive(false);
    }

    /// <summary>
    /// Populates prescription fields from the given NPC controller.
    /// Automatically applies doppelganger overrides if the NPC has a DoppelgangerProfile.
    /// Called by NPCInfoDisplay after a successful barcode scan.
    /// </summary>
    public void ShowPrescription(NPCInteractionController npc)
    {
        if (npc == null || npc.Prescription == null)
        {
            // No prescription data — hide the panel
            if (prescriptionPanel != null)
                prescriptionPanel.SetActive(false);
            return;
        }

        _currentPrescription = npc.Prescription;
        _isDisplaying = true;

        DoppelgangerProfile profile = npc.DoppelgangerData;

        if (prescriptionPanel != null)
        {
            foreach (PrescriptionField field in prescriptionPanel.GetComponentsInChildren<PrescriptionField>())
                field.Populate(_currentPrescription, profile);

            prescriptionPanel.SetActive(true);
        }

        Debug.Log($"[PrescriptionDisplay] Showing prescription for '{npc.NpcIdentity?.fullName ?? "unknown"}'.");
    }

    /// <summary>
    /// Clears the prescription display and hides the panel.
    /// Called when the NPC exits the store or is destroyed.
    /// </summary>
    public void ClearPrescription()
    {
        if (!_isDisplaying) return;

        _currentPrescription = null;
        _isDisplaying = false;

        if (prescriptionPanel != null)
        {
            foreach (PrescriptionField field in prescriptionPanel.GetComponentsInChildren<PrescriptionField>())
                field.Clear();

            prescriptionPanel.SetActive(false);
        }

        Debug.Log("[PrescriptionDisplay] Cleared prescription data.");
    }
}
