using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines placement position and rotation for a single item in a counter slot.
/// Same as ItemPlacement in ShelfSlot but kept separate for clarity.
/// </summary>
[System.Serializable]
public class CounterItemPlacement
{
    [Tooltip("Local position offset for this item.")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Local rotation for this item (euler angles).")]
    public Vector3 rotationOffset = Vector3.zero;

    [HideInInspector]
    public GameObject placedItem;
}

/// <summary>
/// Represents a slot on the counter where NPCs place items for checkout.
/// Player can press E on individual items to delete them (bagging simulation).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class CounterSlot : MonoBehaviour, IPlaceable
{
    [Header("Visualization")]
    [Tooltip("Size of the slot bounding box for visualization and collider.")]
    [SerializeField] private Vector3 slotSize = new Vector3(0.4f, 0.3f, 0.4f);

    [Header("Highlight")]
    [Tooltip("Color of the highlight outline when looking at this slot.")]
    [SerializeField] private Color highlightColor = new Color(0.5f, 0.8f, 1f, 0.8f);

    [Tooltip("Width of the highlight outline.")]
    [SerializeField] private float highlightWidth = 0.02f;

    [Header("Item Placements")]
    [Tooltip("Define positions for each item that can be placed in this slot. Array size = max items.")]
    [SerializeField] private List<CounterItemPlacement> itemPlacements = new List<CounterItemPlacement>() { new CounterItemPlacement() };

    private BoxCollider _collider;
    private LineRenderer _highlightRenderer;
    private bool _isHighlighted = false;
    private int _currentItemCount = 0; // Legacy field, kept if serialized but we will calculate counts dynamically or track differently

    public bool IsOccupied => CurrentItemCount >= itemPlacements.Count;
    public bool HasItems => CurrentItemCount > 0;

    public int CurrentItemCount
    {
        get
        {
            int count = 0;
            if (itemPlacements != null)
            {
                foreach (var placement in itemPlacements)
                {
                    if (placement.placedItem != null) count++;
                }
            }
            return count;
        }
    }

    public int MaxItems => itemPlacements.Count;
    public List<CounterItemPlacement> ItemPlacements => itemPlacements;
    public Vector3 Position => transform.position;

    private void Awake()
    {
        // Setup collider to match slot size
        _collider = GetComponent<BoxCollider>();
        if (_collider != null)
        {
            _collider.size = slotSize;
            _collider.isTrigger = true;
        }

        // Create highlight outline renderer
        CreateHighlightRenderer();
    }

    private void CreateHighlightRenderer()
    {
        GameObject highlightObj = new GameObject("CounterSlotHighlight");
        highlightObj.transform.SetParent(transform);
        highlightObj.transform.localPosition = Vector3.zero;
        highlightObj.transform.localRotation = Quaternion.identity;
        highlightObj.transform.localScale = Vector3.one;

        _highlightRenderer = highlightObj.AddComponent<LineRenderer>();
        _highlightRenderer.useWorldSpace = true;
        _highlightRenderer.loop = true;
        _highlightRenderer.startWidth = highlightWidth;
        _highlightRenderer.endWidth = highlightWidth;
        _highlightRenderer.positionCount = 16;

        // Setup material
        _highlightRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _highlightRenderer.startColor = highlightColor;
        _highlightRenderer.endColor = highlightColor;

        // Start hidden
        _highlightRenderer.enabled = false;
    }

    #region IPlaceable Implementation

    public bool CanPlaceItem(GameObject item)
    {
        return !IsOccupied;
    }

    public bool TryPlaceItem(GameObject item)
    {
        if (!CanPlaceItem(item)) return false;
        PlaceItem(item);
        return true;
    }

    public string GetPlacementPrompt()
    {
        if (IsOccupied) return "Counter Full";
        return $"Counter ({CurrentItemCount}/{itemPlacements.Count})";
    }

    #endregion

    /// <summary>
    /// Places an item in the first available position in this slot.
    /// Called by NPC when placing items on counter.
    /// </summary>
    public void PlaceItem(GameObject item)
    {
        // Find first empty slot
        int emptyIndex = -1;
        for (int i = 0; i < itemPlacements.Count; i++)
        {
            if (itemPlacements[i].placedItem == null)
            {
                emptyIndex = i;
                break;
            }
        }

        if (emptyIndex == -1) return; // Should check CanPlaceItem before calling this

        CounterItemPlacement placement = itemPlacements[emptyIndex];
        placement.placedItem = item;

        // Parent and position
        item.transform.SetParent(transform);
        item.transform.localPosition = placement.positionOffset;

        // Calculate rotation including category offset
        Quaternion placementRot = Quaternion.Euler(placement.rotationOffset);
        Quaternion categoryRot = Quaternion.identity;

        InteractableItem interactable = item.GetComponent<InteractableItem>();
        if (interactable != null && interactable.ItemCategory != null)
        {
            categoryRot = Quaternion.Euler(interactable.ItemCategory.shelfRotationOffset);
        }

        item.transform.localRotation = placementRot * categoryRot;

        // Make item static (kinematic)
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        Debug.Log($"[CounterSlot] Placed '{item.name}' in counter slot '{gameObject.name}' position {emptyIndex}");
    }

    /// <summary>
    /// Removes a specific item from this slot by reference.
    /// Used when player interacts with (deletes) an item.
    /// </summary>
    public bool RemoveItem(GameObject item)
    {
        for (int i = 0; i < itemPlacements.Count; i++)
        {
            if (itemPlacements[i].placedItem == item)
            {
                // Just clear the slot, do NOT shift other items
                itemPlacements[i].placedItem = null;

                Debug.Log($"[CounterSlot] Removed '{item.name}' from counter slot '{gameObject.name}' at index {i}");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if a specific item is placed in this slot.
    /// </summary>
    public bool ContainsItem(GameObject item)
    {
        foreach (var placement in itemPlacements)
        {
            if (placement.placedItem == item)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the CounterSlot that contains a specific item.
    /// Static helper for ObjectPickup to find the slot.
    /// </summary>
    public static CounterSlot GetSlotContaining(GameObject item)
    {
        CounterSlot[] allSlots = FindObjectsByType<CounterSlot>(FindObjectsSortMode.None);
        foreach (var slot in allSlots)
        {
            if (slot.ContainsItem(item))
                return slot;
        }
        return null;
    }

    /// <summary>
    /// Clears all items from the slot.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < itemPlacements.Count; i++)
        {
            itemPlacements[i].placedItem = null;
        }
        // _currentItemCount = 0; // Removed
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        Vector3 size = col != null ? col.size : slotSize;
        Vector3 center = col != null ? col.center : Vector3.zero;

        Gizmos.color = IsOccupied ? new Color(1f, 0.5f, 0.3f, 0.5f) : new Color(0.3f, 0.7f, 1f, 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.DrawWireCube(center, size);
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        Vector3 size = col != null ? col.size : slotSize;
        Vector3 center = col != null ? col.center : Vector3.zero;

        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);

        Gizmos.color = IsOccupied ? new Color(1f, 0.5f, 0.3f, 0.3f) : new Color(0.3f, 0.7f, 1f, 0.3f);
        Gizmos.DrawCube(center, size);

        Gizmos.color = IsOccupied ? new Color(1f, 0.5f, 0f) : new Color(0.3f, 0.7f, 1f);
        Gizmos.DrawWireCube(center, size);

        // Draw item placement positions
        Gizmos.color = Color.yellow;
        foreach (var placement in itemPlacements)
        {
            Gizmos.DrawWireSphere(center + placement.positionOffset, 0.05f);
        }
    }
#endif
}
