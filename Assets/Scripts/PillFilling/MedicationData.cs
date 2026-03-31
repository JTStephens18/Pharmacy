using UnityEngine;

/// <summary>
/// ScriptableObject defining a medication type.
/// Referenced by MedicationBottle (carried item) and loaded into the pill filling hopper.
/// Create via Right-click > Create > NPC > Medication Data.
/// </summary>
[CreateAssetMenu(fileName = "NewMedication", menuName = "NPC/Medication Data")]
public class MedicationData : ScriptableObject
{
    [Tooltip("Display name of the medication (e.g. 'Lisinopril 10mg').")]
    public string medicationName;

    [Tooltip("Pill color — used by the hopper gate window to indicate what's loaded.")]
    public Color pillColor = Color.white;
}
