using UnityEngine;

/// <summary>
/// Interface for objects that can be interacted with by NPCs.
/// Implement this on any item that the NPC should be able to pick up.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Returns the exact world position where the NPC's hand should reach.
    /// Typically this is a child "GrabTarget" transform's position.
    /// </summary>
    Vector3 GetInteractionPoint();

    /// <summary>
    /// Called when the NPC picks up this item.
    /// The item should parent itself to the hand and disable physics.
    /// </summary>
    /// <param name="handTransform">The NPC's hand transform to parent to.</param>
    void OnPickedUp(Transform handTransform);
}
