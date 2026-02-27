using TMPro;
using UnityEngine;

/// <summary>
/// Add this component to any TextMeshProUGUI inside an NPCInfoPanel.
/// Set the FieldType to declare which piece of NPCIdentity data this text displays.
/// NPCInfoDisplay will find and populate all NPCIdentityField components automatically —
/// no manual wiring of individual text references needed.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
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
        _text = GetComponent<TextMeshProUGUI>();
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

    /// <summary>Clears the text.</summary>
    public void Clear()
    {
        if (_text != null)
            _text.text = string.Empty;
    }
}
