using UnityEngine;

/// <summary>
/// Interface for objects that can receive items placed by the player.
/// Mirrors IInteractable but for placement targets.
/// </summary>
public interface IPlaceable
{
    /// <summary>
    /// Returns true if the placeable has room for the given item.
    /// </summary>
    bool CanPlaceItem(GameObject item);

    /// <summary>
    /// Attempts to place the item. Returns true if successful.
    /// </summary>
    bool TryPlaceItem(GameObject item);

    /// <summary>
    /// Returns the interaction prompt text (e.g., "Press E to Place").
    /// </summary>
    string GetPlacementPrompt();
}
