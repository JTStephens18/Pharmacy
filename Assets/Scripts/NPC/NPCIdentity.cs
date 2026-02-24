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

    [Header("Visuals")]
    [Tooltip("Optional headshot photo displayed on the computer screen.")]
    public Sprite photoSprite;
}
