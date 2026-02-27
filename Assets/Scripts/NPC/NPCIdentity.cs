using UnityEngine;

/// <summary>
/// ScriptableObject holding NPC identity data displayed on their ID card and the computer screen.
/// Create via: Right-click in Project → Create → NPC → NPC Identity
/// </summary>
[CreateAssetMenu(fileName = "NewNPCIdentity", menuName = "NPC/NPC Identity")]
public class NPCIdentity : ScriptableObject
{
    [Header("Personal Information")]
    [Tooltip("Full name displayed on the ID card and computer screen.")]
    public string fullName;

    [Tooltip("Date of birth displayed on the ID card.")]
    public string dateOfBirth;

    [Tooltip("Address displayed on the computer screen.")]
    public string address;

    [Tooltip("Unique ID number / barcode number.")]
    public string idNumber;

    [Header("Computer Screen Visuals")]
    [Tooltip("Optional headshot photo displayed on the computer screen.")]
    public Sprite photoSprite;

    [Header("ID Card Overrides")]
    [Tooltip("Photo printed on the physical ID card. " +
             "If unassigned, falls back to photoSprite above.")]
    public Sprite idCardPhotoSprite;

    [Tooltip("Name printed on the physical ID card. " +
             "If empty, falls back to fullName above.")]
    public string idCardName;

    // ── Convenience properties ───────────────────────────────────────

    /// <summary>Photo to display on the physical ID card (falls back to photoSprite if not set).</summary>
    public Sprite IdCardPhoto => idCardPhotoSprite != null ? idCardPhotoSprite : photoSprite;

    /// <summary>Name to print on the physical ID card (falls back to fullName if not set).</summary>
    public string IdCardDisplayName => !string.IsNullOrEmpty(idCardName) ? idCardName : fullName;
}
