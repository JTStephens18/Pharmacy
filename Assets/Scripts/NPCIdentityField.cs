using TMPro;
using UnityEngine;

/// <summary>
/// Add this component to any TextMeshProUGUI or Button inside an NPCInfoPanel.
/// Set the FieldType to declare which piece of NPCIdentity data this text displays.
/// NPCInfoDisplay will find and populate all NPCIdentityField components automatically —
/// no manual wiring of individual text references needed.
///
/// Works on:
///   - A TextMeshProUGUI object directly
///   - A Button (finds the TMP in its children automatically)
/// </summary>
public class NPCIdentityField : MonoBehaviour
{
    public enum FieldType
    {
        FullName,
        DateOfBirth,
        Address,
        IDNumber,
    }

    [Tooltip("Which field from NPCIdentity this text element should display.")]
    public FieldType fieldType;

    private TextMeshProUGUI _text;

    void Awake()
    {
        // Support both direct TMP objects and Button GameObjects (TMP is a child of the button)
        _text = GetComponent<TextMeshProUGUI>();
        if (_text == null)
            _text = GetComponentInChildren<TextMeshProUGUI>();

        if (_text == null)
            Debug.LogWarning($"[NPCIdentityField] No TextMeshProUGUI found on '{gameObject.name}' or its children.", this);
    }

    /// <summary>Sets the text to the corresponding field from the given identity.</summary>
    public void Populate(NPCIdentity identity)
    {
        if (_text == null || identity == null) return;

        _text.text = fieldType switch
        {
            FieldType.FullName    => identity.fullName,
            FieldType.DateOfBirth => identity.dateOfBirth,
            FieldType.Address     => identity.address,
            FieldType.IDNumber    => identity.idNumber,
            _                    => string.Empty,
        };
    }

    /// <summary>
    /// Populates with doppelganger-overridden data. Fake values replace real ones where applicable.
    /// </summary>
    public void Populate(NPCIdentity identity, DoppelgangerProfile profile)
    {
        if (_text == null || identity == null) return;

        if (profile == null)
        {
            Populate(identity);
            return;
        }

        _text.text = fieldType switch
        {
            FieldType.FullName    => identity.fullName,
            FieldType.DateOfBirth => profile.GetDOB(identity.dateOfBirth),
            FieldType.Address     => profile.GetAddress(identity.address),
            FieldType.IDNumber    => identity.idNumber,
            _                    => string.Empty,
        };
    }

    /// <summary>Clears the text.</summary>
    public void Clear()
    {
        if (_text != null)
            _text.text = string.Empty;
    }
}
