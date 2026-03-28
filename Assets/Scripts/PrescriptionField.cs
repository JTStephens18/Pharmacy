using TMPro;
using UnityEngine;

/// <summary>
/// Add this component to any TextMeshProUGUI or Button inside a prescription panel.
/// Set the FieldType to declare which piece of PrescriptionData this text displays.
/// PrescriptionDisplay will find and populate all PrescriptionField components automatically —
/// no manual wiring of individual text references needed.
///
/// Works on:
///   - A TextMeshProUGUI object directly
///   - A Button (finds the TMP in its children automatically)
///
/// Same pattern as NPCIdentityField but for prescription data.
/// </summary>
public class PrescriptionField : MonoBehaviour
{
    public enum FieldType
    {
        MedicationName,
        Quantity,
        Dosage,
        PrescriberName,
        PrescriberNPI,
        PrescriberSpecialty,
        PrescriberAddress,
        FillHistory,
    }

    [Tooltip("Which field from PrescriptionData this text element should display.")]
    public FieldType fieldType;

    private TextMeshProUGUI _text;

    void Awake()
    {
        _text = GetComponent<TextMeshProUGUI>();
        if (_text == null)
            _text = GetComponentInChildren<TextMeshProUGUI>();

        if (_text == null)
            Debug.LogWarning($"[PrescriptionField] No TextMeshProUGUI found on '{gameObject.name}' or its children.", this);
    }

    /// <summary>Sets the text to the corresponding field from the given prescription data.</summary>
    public void Populate(PrescriptionData data)
    {
        if (_text == null || data == null) return;

        _text.text = fieldType switch
        {
            FieldType.MedicationName     => data.medicationName,
            FieldType.Quantity           => data.quantity.ToString(),
            FieldType.Dosage             => data.dosage,
            FieldType.PrescriberName     => data.prescriberName,
            FieldType.PrescriberNPI      => data.prescriberNPI,
            FieldType.PrescriberSpecialty => data.prescriberSpecialty,
            FieldType.PrescriberAddress  => data.prescriberAddress,
            FieldType.FillHistory        => FormatFillHistory(data.previousFills),
            _                            => string.Empty,
        };
    }

    /// <summary>
    /// Populates with doppelganger-overridden data. Fake values replace real ones where applicable.
    /// </summary>
    public void Populate(PrescriptionData data, DoppelgangerProfile profile)
    {
        if (_text == null || data == null) return;

        if (profile == null)
        {
            Populate(data);
            return;
        }

        _text.text = fieldType switch
        {
            FieldType.MedicationName     => data.medicationName,
            FieldType.Quantity           => profile.GetQuantity(data.quantity).ToString(),
            FieldType.Dosage             => profile.GetDosage(data.dosage),
            FieldType.PrescriberName     => data.prescriberName,
            FieldType.PrescriberNPI      => profile.GetPrescriberNPI(data.prescriberNPI),
            FieldType.PrescriberSpecialty => profile.GetPrescriberSpecialty(data.prescriberSpecialty),
            FieldType.PrescriberAddress  => data.prescriberAddress,
            FieldType.FillHistory        => profile.HasOverride(DiscrepancyType.NoFillHistory)
                                            ? "No previous fills on record"
                                            : FormatFillHistory(data.previousFills),
            _                            => string.Empty,
        };
    }

    /// <summary>Clears the text.</summary>
    public void Clear()
    {
        if (_text != null)
            _text.text = string.Empty;
    }

    private static string FormatFillHistory(string[] fills)
    {
        if (fills == null || fills.Length == 0)
            return "No previous fills on record";
        return string.Join("\n", fills);
    }
}
