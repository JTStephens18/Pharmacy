using UnityEngine;

/// <summary>
/// ScriptableObject representing a patient's prescription data.
/// Displayed on the computer screen for verification during checkout.
/// Create via: Right-click in Project → Create → NPC → Prescription Data
/// </summary>
[CreateAssetMenu(fileName = "NewPrescriptionData", menuName = "NPC/Prescription Data")]
public class PrescriptionData : ScriptableObject
{
    [Header("Prescription")]
    [Tooltip("Name of the prescribed medication.")]
    public string medicationName;

    [Tooltip("Number of units prescribed.")]
    public int quantity;

    [Tooltip("Dosage instructions (e.g. '0.5mg twice daily').")]
    public string dosage;

    [Header("Prescriber")]
    [Tooltip("Name of the prescribing doctor.")]
    public string prescriberName;

    [Tooltip("National Provider Identifier number.")]
    public string prescriberNPI;

    [Tooltip("Prescriber's medical specialty (e.g. 'Cardiology').")]
    public string prescriberSpecialty;

    [Tooltip("Prescriber's office address.")]
    public string prescriberAddress;

    [Header("Fill History")]
    [Tooltip("Previous fill records (e.g. '2026-01-15: 30x 0.5mg'). Empty for new prescriptions.")]
    public string[] previousFills;
}
