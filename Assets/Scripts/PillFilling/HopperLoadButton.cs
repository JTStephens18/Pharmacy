using UnityEngine;

/// <summary>
/// Physical load button on the pill filling station housing.
/// When the player presses E while holding a MedicationBottle and looking at this button,
/// the medication is tipped into the hopper. The bottle is consumed.
///
/// The hopper holds one medication type at a time. Loading a different bottle replaces the current load.
/// Loading the wrong medication produces no error — the station accepts whatever is put in.
/// </summary>
public class HopperLoadButton : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The filling station this button loads into. Auto-found in parent if left empty.")]
    [SerializeField] private PillFillingStation station;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Sound played when medication is poured into the hopper.")]
    [SerializeField] private AudioClip loadSound;

    [Header("Visual")]
    [Tooltip("Optional highlight mesh shown when the player looks at the button.")]
    [SerializeField] private GameObject highlightObject;

    void Awake()
    {
        if (station == null)
            station = GetComponentInParent<PillFillingStation>();

        if (highlightObject != null)
            highlightObject.SetActive(false);
    }

    /// <summary>
    /// Attempt to load the held object's medication into the hopper.
    /// Returns true if successful (held object was a MedicationBottle).
    /// The caller (ObjectPickup) is responsible for consuming the held object on success.
    /// </summary>
    public bool TryLoad(GameObject heldObject, ObjectPickup pickup)
    {
        if (heldObject == null || station == null) return false;

        RotatingHopper hopper = station.Hopper;
        if (hopper == null) return false;

        MedicationBottle bottle = heldObject.GetComponent<MedicationBottle>();
        if (bottle == null || bottle.MedicationData == null)
        {
            Debug.Log("[HopperLoadButton] Held object is not a medication bottle.");
            return false;
        }

        // Load (or replace) the hopper's medication
        hopper.LoadMedication(bottle.MedicationData);

        // Consume the bottle
        pickup.ConsumeHeldObject();

        if (audioSource != null && loadSound != null)
            audioSource.PlayOneShot(loadSound);

        Debug.Log($"[HopperLoadButton] Loaded '{bottle.MedicationData.medicationName}' into hopper.");
        return true;
    }

    /// <summary>Show or hide the highlight mesh (called by ObjectPickup during raycast detection).</summary>
    public void ShowHighlight(bool show)
    {
        if (highlightObject != null)
            highlightObject.SetActive(show);
    }
}
