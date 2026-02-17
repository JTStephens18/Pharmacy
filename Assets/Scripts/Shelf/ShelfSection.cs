using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a section of shelf with multiple slots for item placement.
/// Implements IPlaceable to allow player interaction via ObjectPickup.
/// Note: Requires a Collider somewhere in the hierarchy (can be on children).
/// </summary>
public class ShelfSection : MonoBehaviour, IPlaceable
{
    [Header("Slot Configuration")]
    [Tooltip("Automatically find ShelfSlot children on Awake.")]
    [SerializeField] private bool autoFindSlots = true;

    [Tooltip("Manual slot references (used if autoFindSlots is false).")]
    [SerializeField] private List<ShelfSlot> slots = new List<ShelfSlot>();

    [Header("Settings")]
    [Tooltip("Optional: Only accept items with matching ItemCategory.")]
    [SerializeField] private ItemCategory acceptedCategory;

    [Tooltip("Prompt shown when player looks at shelf while holding an item.")]
    [SerializeField] private string placementPrompt = "Press E to Place";

    [Header("Audio (Optional)")]
    [SerializeField] private AudioClip placeSound;
    [SerializeField] private AudioClip fullSound;

    private AudioSource _audioSource;

    // Public accessors
    public int MaxCapacity => slots.Count;
    public int OccupiedSlots => GetOccupiedCount();
    public int AvailableSlots => MaxCapacity - OccupiedSlots;

    private void Awake()
    {
        if (autoFindSlots)
        {
            slots.Clear();
            slots.AddRange(GetComponentsInChildren<ShelfSlot>());
        }

        _audioSource = GetComponent<AudioSource>();

        if (slots.Count == 0)
        {
            Debug.LogWarning($"[ShelfSection] No slots found on {gameObject.name}. Add ShelfSlot children or disable autoFindSlots.");
        }
    }

    #region IPlaceable Implementation

    public bool CanPlaceItem(GameObject item)
    {
        // Check category filter if configured
        if (acceptedCategory != null)
        {
            InteractableItem interactable = item.GetComponent<InteractableItem>();
            if (interactable == null || interactable.ItemCategory != acceptedCategory)
            {
                return false;
            }
        }

        // Check if there's an available slot
        return GetFirstAvailableSlot() != null;
    }

    public bool TryPlaceItem(GameObject item)
    {
        if (!CanPlaceItem(item))
        {
            PlaySound(fullSound);
            return false;
        }

        ShelfSlot slot = GetFirstAvailableSlot();
        if (slot == null) return false;

        slot.PlaceItem(item);
        PlaySound(placeSound);

        Debug.Log($"[ShelfSection] Placed {item.name} on {gameObject.name}. Slots: {OccupiedSlots}/{MaxCapacity}");
        return true;
    }

    public string GetPlacementPrompt()
    {
        return AvailableSlots > 0 ? placementPrompt : "Shelf Full";
    }

    #endregion

    #region Slot Management

    private ShelfSlot GetFirstAvailableSlot()
    {
        foreach (ShelfSlot slot in slots)
        {
            if (!slot.IsOccupied)
                return slot;
        }
        return null;
    }

    private int GetOccupiedCount()
    {
        int count = 0;
        foreach (ShelfSlot slot in slots)
        {
            if (slot.HasItems) count++;
        }
        return count;
    }

    /// <summary>
    /// Returns all items currently on this shelf section.
    /// </summary>
    public List<GameObject> GetAllItems()
    {
        List<GameObject> items = new List<GameObject>();
        foreach (ShelfSlot slot in slots)
        {
            foreach (ItemPlacement placement in slot.ItemPlacements)
            {
                if (placement.placedItem != null)
                    items.Add(placement.placedItem);
            }
        }
        return items;
    }

    /// <summary>
    /// Removes and returns the first item from this shelf.
    /// Useful for NPC pickup integration.
    /// </summary>
    public GameObject RemoveFirstItem()
    {
        foreach (ShelfSlot slot in slots)
        {
            if (slot.IsOccupied)
                return slot.RemoveItem();
        }
        return null;
    }

    /// <summary>
    /// Returns a list of all missing items across all slots on this shelf.
    /// Each entry is a category that a slot needs, repeated for each empty position.
    /// </summary>
    public List<ItemCategory> GetMissingItems()
    {
        List<ItemCategory> missingItems = new List<ItemCategory>();

        foreach (ShelfSlot slot in slots)
        {
            // Skip slots without a category filter
            if (slot.AcceptedCategory == null) continue;

            // Calculate how many items this slot is missing
            int emptySpaces = slot.MaxItems - slot.CurrentItemCount;

            // Add the category once for each empty space
            for (int i = 0; i < emptySpaces; i++)
            {
                missingItems.Add(slot.AcceptedCategory);
            }
        }

        return missingItems;
    }

    /// <summary>
    /// Returns a list of all slots on this shelf section.
    /// </summary>
    public List<ShelfSlot> GetSlots()
    {
        return slots;
    }

    #endregion

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw slot positions for debugging
        ShelfSlot[] childSlots = GetComponentsInChildren<ShelfSlot>();
        foreach (ShelfSlot slot in childSlots)
        {
            Gizmos.color = slot.IsOccupied ? Color.red : Color.green;
            Gizmos.DrawWireCube(slot.Position, Vector3.one * 0.2f);
        }
    }
#endif
}
