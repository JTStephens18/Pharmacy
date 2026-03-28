using UnityEngine;

/// <summary>
/// Types of discrepancies a doppelganger can have.
/// Used by the verification system to determine what's wrong with a fake NPC.
/// </summary>
public enum DiscrepancyType
{
    PhotoMismatch,
    InvalidNPI,
    NoFillHistory,
    WrongPrescriberSpecialty,
    DoseJump,
    NonStandardQuantity,
    PrescriberOutsideArea,
    WrongDOB,
    WrongAddress
}

/// <summary>
/// ScriptableObject defining a doppelganger's fake data and discrepancies.
/// Assigned to an NPC at spawn time to make them a doppelganger.
/// The fake fields override the real NPCIdentity/PrescriptionData when presented to the player.
/// Create via: Right-click in Project → Create → NPC → Doppelganger Profile
/// </summary>
[CreateAssetMenu(fileName = "NewDoppelgangerProfile", menuName = "NPC/Doppelganger Profile")]
public class DoppelgangerProfile : ScriptableObject
{
    [Header("Discrepancies")]
    [Tooltip("Which fields are wrong on this doppelganger. Used for scoring and hint generation.")]
    public DiscrepancyType[] discrepancies;

    [Header("Identity Overrides")]
    [Tooltip("Mismatched photo (e.g. different person). Leave null to use the real photo.")]
    public Sprite fakePhoto;

    [Tooltip("Wrong date of birth. Leave empty to use the real DOB.")]
    public string fakeDOB;

    [Tooltip("Wrong address. Leave empty to use the real address.")]
    public string fakeAddress;

    [Header("Prescription Overrides")]
    [Tooltip("Invalid or wrong NPI number. Leave empty to use the real NPI.")]
    public string fakePrescriberNPI;

    [Tooltip("Wrong prescriber specialty. Leave empty to use the real specialty.")]
    public string fakePrescriberSpecialty;

    [Tooltip("Suspicious dosage (e.g. dose jump). Leave empty to use the real dosage.")]
    public string fakeDosage;

    [Tooltip("Non-standard quantity. 0 = use the real quantity.")]
    public int fakeQuantity;

    // ── Convenience methods ──────────────────────────────────────────

    /// <summary>Returns true if this profile overrides the given field.</summary>
    public bool HasOverride(DiscrepancyType type)
    {
        if (discrepancies == null) return false;
        foreach (var d in discrepancies)
        {
            if (d == type) return true;
        }
        return false;
    }

    /// <summary>Returns the fake DOB if overridden, otherwise the real value.</summary>
    public string GetDOB(string realDOB) => !string.IsNullOrEmpty(fakeDOB) ? fakeDOB : realDOB;

    /// <summary>Returns the fake address if overridden, otherwise the real value.</summary>
    public string GetAddress(string realAddress) => !string.IsNullOrEmpty(fakeAddress) ? fakeAddress : realAddress;

    /// <summary>Returns the fake photo if overridden, otherwise the real value.</summary>
    public Sprite GetPhoto(Sprite realPhoto) => fakePhoto != null ? fakePhoto : realPhoto;

    /// <summary>Returns the fake NPI if overridden, otherwise the real value.</summary>
    public string GetPrescriberNPI(string realNPI) => !string.IsNullOrEmpty(fakePrescriberNPI) ? fakePrescriberNPI : realNPI;

    /// <summary>Returns the fake specialty if overridden, otherwise the real value.</summary>
    public string GetPrescriberSpecialty(string realSpecialty) => !string.IsNullOrEmpty(fakePrescriberSpecialty) ? fakePrescriberSpecialty : realSpecialty;

    /// <summary>Returns the fake dosage if overridden, otherwise the real value.</summary>
    public string GetDosage(string realDosage) => !string.IsNullOrEmpty(fakeDosage) ? fakeDosage : realDosage;

    /// <summary>Returns the fake quantity if overridden, otherwise the real value.</summary>
    public int GetQuantity(int realQuantity) => fakeQuantity > 0 ? fakeQuantity : realQuantity;
}
