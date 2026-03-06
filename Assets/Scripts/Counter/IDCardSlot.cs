using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A designated spot on the counter where an NPC places their ID card.
/// Similar to CounterSlot but holds a single ID card instead of store items.
/// The NPC spawns the ID card prefab here during item placement.
/// </summary>
public class IDCardSlot : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Local position offset for the ID card.")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;

    [Tooltip("Local rotation (euler angles) for the ID card.")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    [Header("Focus Camera")]
    [Tooltip("Empty Transform positioned where the camera should sit when the player focuses on the ID card.")]
    [SerializeField] private Transform focusCameraTarget;

    // ── Runtime State ───────────────────────────────────────────────
    private GameObject _placedIDCard;
    private IDCardInteraction _placedInteraction;

    /// <summary>Whether an ID card is currently placed in this slot.</summary>
    public bool HasIDCard => _placedIDCard != null;

    /// <summary>The IDCardInteraction on the currently placed card (null if empty).</summary>
    public IDCardInteraction PlacedInteraction => _placedInteraction;

    /// <summary>The focus camera target for this slot.</summary>
    public Transform FocusCameraTarget => focusCameraTarget;

    /// <summary>
    /// Spawns an ID card prefab in this slot and initializes it with the NPC's identity.
    /// Called by NPCInteractionController during item placement.
    /// </summary>
    /// <param name="idCardPrefab">The ID card prefab to instantiate.</param>
    /// <param name="identity">The NPC identity data to assign to the card.</param>
    /// <returns>The spawned IDCardInteraction component, or null on failure.</returns>
    public IDCardInteraction PlaceIDCard(GameObject idCardPrefab, NPCIdentity identity)
    {
        if (_placedIDCard != null)
        {
            Debug.LogWarning("[IDCardSlot] Slot already occupied! Removing existing card first.");
            RemoveIDCard();
        }

        if (idCardPrefab == null)
        {
            Debug.LogError("[IDCardSlot] Cannot place ID card: prefab is null!");
            return null;
        }

        // Networked: position in world space — NGO forbids parenting NetworkObjects to non-NetworkObjects.
        // Local fallback: parent to slot as before.
        NetworkObject prefabNetObj = idCardPrefab.GetComponent<NetworkObject>();
        if (prefabNetObj != null)
        {
            _placedIDCard = Instantiate(idCardPrefab);
            _placedIDCard.transform.position = transform.TransformPoint(positionOffset);
            _placedIDCard.transform.rotation = transform.rotation * Quaternion.Euler(rotationOffset);
        }
        else
        {
            _placedIDCard = Instantiate(idCardPrefab, transform);
            _placedIDCard.transform.localPosition = positionOffset;
            _placedIDCard.transform.localRotation = Quaternion.Euler(rotationOffset);
        }

        // Cache the interaction component reference
        _placedInteraction = _placedIDCard.GetComponent<IDCardInteraction>();
        if (_placedInteraction == null)
            _placedInteraction = _placedIDCard.GetComponentInChildren<IDCardInteraction>();

        NetworkObject cardNetObj = _placedIDCard.GetComponent<NetworkObject>();
        if (cardNetObj != null)
        {
            // Network-spawn: Initialize() will be called on all clients via ClientRpc from NPCInteractionController.
            cardNetObj.Spawn();
            Debug.Log($"[IDCardSlot] Network-spawned ID card for '{identity.fullName}' in slot '{gameObject.name}'");
        }
        else
        {
            // Local fallback: initialize directly.
            if (_placedInteraction != null)
            {
                _placedInteraction.Initialize(identity, focusCameraTarget);
                Debug.Log($"[IDCardSlot] Placed ID card (local) for '{identity.fullName}' in slot '{gameObject.name}'");
            }
            else
            {
                Debug.LogWarning("[IDCardSlot] ID card prefab has no IDCardInteraction component!");
            }
        }

        return _placedInteraction;
    }

    /// <summary>
    /// Removes and destroys the currently placed ID card.
    /// Called when the NPC exits or is destroyed.
    /// </summary>
    public void RemoveIDCard()
    {
        if (_placedIDCard != null)
        {
            Debug.Log($"[IDCardSlot] Removing ID card from slot '{gameObject.name}'");

            NetworkObject cardNetObj = _placedIDCard.GetComponent<NetworkObject>();
            if (cardNetObj != null && cardNetObj.IsSpawned)
                cardNetObj.Despawn(true);
            else
                Destroy(_placedIDCard);

            _placedIDCard = null;
            _placedInteraction = null;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw the ID card placement position
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.8f); // Gold
        Vector3 worldPos = transform.TransformPoint(positionOffset);
        Gizmos.DrawWireCube(worldPos, new Vector3(0.15f, 0.01f, 0.1f)); // Card-shaped outline

        // Draw focus camera target
        if (focusCameraTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(focusCameraTarget.position, 0.1f);
            Gizmos.DrawLine(worldPos, focusCameraTarget.position);
        }
    }
#endif
}
