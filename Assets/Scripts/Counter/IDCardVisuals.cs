using TMPro;
using UnityEngine;

/// <summary>
/// Attach to the ID card prefab alongside IDCardInteraction.
/// Controls the physical visuals of the card: a photo (SpriteRenderer) and a printed name
/// (TextMeshPro — the 3D world-space version, not UGUI).
///
/// Reads from NPCIdentity.IdCardPhoto and NPCIdentity.IdCardDisplayName, which fall back
/// to photoSprite and fullName respectively if no card-specific overrides are set.
///
/// Editor setup:
///   1. Add a child GameObject to your card prefab — place and size it over the photo area.
///      Add a SpriteRenderer to it, then assign it to photoRenderer.
///   2. Add another child GameObject over the name area.
///      Add a TextMeshPro (3D) component to it, then assign it to nameText.
/// </summary>
public class IDCardVisuals : MonoBehaviour
{
    [Header("Card Visual Elements")]
    [Tooltip("SpriteRenderer on a child object positioned over the photo area of the card.")]
    [SerializeField] private SpriteRenderer photoRenderer;

    [Tooltip("TextMeshPro (3D) on a child object positioned over the name area of the card.")]
    [SerializeField] private TextMeshPro nameText;

    /// <summary>
    /// Populates the card's photo and name from the given NPCIdentity.
    /// Called automatically by IDCardInteraction.Initialize().
    /// </summary>
    public void Initialize(NPCIdentity identity)
    {
        if (identity == null)
        {
            Debug.LogWarning("[IDCardVisuals] Cannot initialize: NPCIdentity is null.");
            return;
        }

        // Photo: use card-specific override, falls back to computer screen photo
        if (photoRenderer != null)
        {
            Sprite photo = identity.IdCardPhoto;
            if (photo != null)
            {
                photoRenderer.sprite = photo;
                photoRenderer.gameObject.SetActive(true);
            }
            else
            {
                photoRenderer.gameObject.SetActive(false);
            }
        }

        // Name: use card-specific override, falls back to full name
        if (nameText != null)
        {
            nameText.text = identity.IdCardDisplayName;
        }
    }
}
