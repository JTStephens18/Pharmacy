using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines placement position and rotation for a single item in a slot.
/// </summary>
[System.Serializable]
public class ItemPlacement
{
    [Tooltip("Local position offset for this item.")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Local rotation for this item (euler angles).")]
    public Vector3 rotationOffset = Vector3.zero;

    [HideInInspector]
    public GameObject placedItem;
}

/// <summary>
/// Represents a single slot on a shelf where items can be placed.
/// Supports multiple items per slot with individual placement positions.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ShelfSlot : MonoBehaviour, IPlaceable
{
    [Header("Visualization")]
    [Tooltip("Size of the slot bounding box for visualization and collider.")]
    [SerializeField] private Vector3 slotSize = new Vector3(0.3f, 0.3f, 0.3f);

    [Header("Highlight")]
    [Tooltip("Color of the highlight outline when looking at this slot.")]
    [SerializeField] private Color highlightColor = new Color(0.5f, 1f, 0.5f, 0.8f);

    [Tooltip("Width of the highlight outline.")]
    [SerializeField] private float highlightWidth = 0.02f;

    [Tooltip("If true, only show highlight when player is holding an item. If false, always show when looking at slot.")]
    [SerializeField] private bool requireHeldItem = true;

    public bool RequireHeldItem => requireHeldItem;

    [Header("Item Placements")]
    [Tooltip("Define positions for each item that can be placed in this slot. Array size = max items.")]
    [SerializeField] private List<ItemPlacement> itemPlacements = new List<ItemPlacement>() { new ItemPlacement() };

    [Header("Item Filtering")]
    [Tooltip("If set, only items with this category can be placed. Leave empty to accept any item.")]
    [SerializeField] private ItemCategory acceptedCategory;

    public ItemCategory AcceptedCategory => acceptedCategory;

    [Header("State")]
    [SerializeField] private int currentItemCount = 0;

    private BoxCollider _collider;
    private LineRenderer _highlightRenderer;
    private bool _isHighlighted = false;

    public bool IsOccupied => currentItemCount >= itemPlacements.Count;
    public bool HasItems => currentItemCount > 0;
    public int CurrentItemCount => currentItemCount;
    public int MaxItems => itemPlacements.Count;
    public List<ItemPlacement> ItemPlacements => itemPlacements;
    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;
    public Vector3 SlotSize => slotSize;

    private void Awake()
    {
        // Setup collider to match slot size
        _collider = GetComponent<BoxCollider>();
        if (_collider != null)
        {
            _collider.size = slotSize;
            _collider.isTrigger = true; // Don't block physics, just detect raycasts
        }

        // Create highlight outline renderer
        CreateHighlightRenderer();
    }

    private void CreateHighlightRenderer()
    {
        // Create child object for the LineRenderer
        GameObject highlightObj = new GameObject("SlotHighlight");
        highlightObj.transform.SetParent(transform);
        highlightObj.transform.localPosition = Vector3.zero;
        highlightObj.transform.localRotation = Quaternion.identity;
        highlightObj.transform.localScale = Vector3.one; // Reset scale

        _highlightRenderer = highlightObj.AddComponent<LineRenderer>();
        _highlightRenderer.useWorldSpace = true; // Use world space for accurate positioning
        _highlightRenderer.loop = true;
        _highlightRenderer.startWidth = highlightWidth;
        _highlightRenderer.endWidth = highlightWidth;
        _highlightRenderer.positionCount = 16;

        // Setup material - use Unity's default unlit color
        _highlightRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _highlightRenderer.startColor = highlightColor;
        _highlightRenderer.endColor = highlightColor;

        // Start hidden
        _highlightRenderer.enabled = false;
    }

    private void UpdateHighlightPositions()
    {
        if (_highlightRenderer == null) return;

        Vector3 size = _collider != null ? _collider.size : slotSize;
        Vector3 half = size * 0.5f;

        // Draw box outline: 4 bottom corners, then 4 top corners
        _highlightRenderer.positionCount = 16;

        // Bottom face
        _highlightRenderer.SetPosition(0, new Vector3(-half.x, -half.y, -half.z));
        _highlightRenderer.SetPosition(1, new Vector3(half.x, -half.y, -half.z));
        _highlightRenderer.SetPosition(2, new Vector3(half.x, -half.y, half.z));
        _highlightRenderer.SetPosition(3, new Vector3(-half.x, -half.y, half.z));
        _highlightRenderer.SetPosition(4, new Vector3(-half.x, -half.y, -half.z));

        // Connect to top
        _highlightRenderer.SetPosition(5, new Vector3(-half.x, half.y, -half.z));

        // Top face
        _highlightRenderer.SetPosition(6, new Vector3(half.x, half.y, -half.z));
        _highlightRenderer.SetPosition(7, new Vector3(half.x, -half.y, -half.z));
        _highlightRenderer.SetPosition(8, new Vector3(half.x, half.y, -half.z));
        _highlightRenderer.SetPosition(9, new Vector3(half.x, half.y, half.z));
        _highlightRenderer.SetPosition(10, new Vector3(half.x, -half.y, half.z));
        _highlightRenderer.SetPosition(11, new Vector3(half.x, half.y, half.z));
        _highlightRenderer.SetPosition(12, new Vector3(-half.x, half.y, half.z));
        _highlightRenderer.SetPosition(13, new Vector3(-half.x, -half.y, half.z));
        _highlightRenderer.SetPosition(14, new Vector3(-half.x, half.y, half.z));
        _highlightRenderer.SetPosition(15, new Vector3(-half.x, half.y, -half.z));
    }

    /// <summary>
    /// Shows the highlight outline. Call when player looks at this slot.
    /// </summary>
    public void ShowHighlight()
    {
        if (_highlightRenderer != null && !_isHighlighted)
        {
            // Refresh positions from current BoxCollider in world space
            BoxCollider col = GetComponent<BoxCollider>();
            if (col != null)
            {
                Vector3 size = col.size;
                Vector3 center = col.center;
                Vector3 half = size * 0.5f;

                // Local corner positions
                Vector3[] localCorners = new Vector3[]
                {
                    center + new Vector3(-half.x, -half.y, -half.z),
                    center + new Vector3(half.x, -half.y, -half.z),
                    center + new Vector3(half.x, -half.y, half.z),
                    center + new Vector3(-half.x, -half.y, half.z),
                    center + new Vector3(-half.x, half.y, -half.z),
                    center + new Vector3(half.x, half.y, -half.z),
                    center + new Vector3(half.x, half.y, half.z),
                    center + new Vector3(-half.x, half.y, half.z)
                };

                // Transform to world space (same as how gizmos work)
                for (int i = 0; i < localCorners.Length; i++)
                {
                    localCorners[i] = transform.TransformPoint(localCorners[i]);
                }

                // Draw box outline in world space
                _highlightRenderer.positionCount = 16;

                // Bottom face
                _highlightRenderer.SetPosition(0, localCorners[0]);
                _highlightRenderer.SetPosition(1, localCorners[1]);
                _highlightRenderer.SetPosition(2, localCorners[2]);
                _highlightRenderer.SetPosition(3, localCorners[3]);
                _highlightRenderer.SetPosition(4, localCorners[0]);

                // Connect to top
                _highlightRenderer.SetPosition(5, localCorners[4]);

                // Top face edges
                _highlightRenderer.SetPosition(6, localCorners[5]);
                _highlightRenderer.SetPosition(7, localCorners[1]);
                _highlightRenderer.SetPosition(8, localCorners[5]);
                _highlightRenderer.SetPosition(9, localCorners[6]);
                _highlightRenderer.SetPosition(10, localCorners[2]);
                _highlightRenderer.SetPosition(11, localCorners[6]);
                _highlightRenderer.SetPosition(12, localCorners[7]);
                _highlightRenderer.SetPosition(13, localCorners[3]);
                _highlightRenderer.SetPosition(14, localCorners[7]);
                _highlightRenderer.SetPosition(15, localCorners[4]);
            }

            _highlightRenderer.enabled = true;
            _isHighlighted = true;
        }
    }

    /// <summary>
    /// Hides the highlight outline.
    /// </summary>
    public void HideHighlight()
    {
        if (_highlightRenderer != null && _isHighlighted)
        {
            _highlightRenderer.enabled = false;
            _isHighlighted = false;
        }
    }

    private void OnValidate()
    {
        // Update collider size when slot size changes in editor
        SyncCollider();
    }

    private void Reset()
    {
        // Called when component is first added or reset
        SyncCollider();
    }

    [ContextMenu("Sync Collider to Slot Size")]
    private void SyncCollider()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            col.size = slotSize;
            col.isTrigger = true;
        }
    }

    #region IPlaceable Implementation

    public bool CanPlaceItem(GameObject item)
    {
        if (IsOccupied) return false;

        // Check category filter if set
        if (acceptedCategory != null && item != null)
        {
            InteractableItem interactable = item.GetComponent<InteractableItem>();
            if (interactable == null || interactable.ItemCategory != acceptedCategory)
            {
                return false;
            }
        }

        return true;
    }

    public bool TryPlaceItem(GameObject item)
    {
        if (!CanPlaceItem(item)) return false;
        PlaceItem(item);
        return true;
    }

    public string GetPlacementPrompt()
    {
        if (IsOccupied) return "Slot Full";
        return acceptedCategory != null
            ? $"Press E to Place [{acceptedCategory.name}] ({currentItemCount}/{itemPlacements.Count})"
            : $"Press E to Place ({currentItemCount}/{itemPlacements.Count})";
    }

    #endregion

    /// <summary>
    /// Places an item in the next available position in this slot.
    /// </summary>
    public void PlaceItem(GameObject item)
    {
        if (currentItemCount >= itemPlacements.Count)
        {
            Debug.LogWarning($"[ShelfSlot] Cannot place item - slot '{gameObject.name}' is full ({currentItemCount}/{itemPlacements.Count})");
            return;
        }

        Debug.Log($"[ShelfSlot] PlaceItem: Adding '{item.name}' to slot '{gameObject.name}' at position {currentItemCount}");

        ItemPlacement placement = itemPlacements[currentItemCount];
        placement.placedItem = item;
        currentItemCount++;

        Debug.Log($"[ShelfSlot] PlaceItem: Slot '{gameObject.name}' now has {currentItemCount} items. placedItem at [{currentItemCount - 1}] = {placement.placedItem?.name ?? "null"}");

        // Parent first, then set local transforms for precise control
        item.transform.SetParent(transform);
        item.transform.localPosition = placement.positionOffset;

        // Calculate total rotation: slot offset * category offset
        Quaternion placementRot = Quaternion.Euler(placement.rotationOffset);
        Quaternion categoryRot = Quaternion.identity;

        InteractableItem interactable = item.GetComponent<InteractableItem>();
        if (interactable != null && interactable.ItemCategory != null)
        {
            categoryRot = Quaternion.Euler(interactable.ItemCategory.shelfRotationOffset);
        }

        item.transform.localRotation = placementRot * categoryRot;

        // Configure physics for completely static placement
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Clear velocity before making kinematic (can't set velocity on kinematic bodies)
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        Debug.Log($"[ShelfSlot] Placed '{item.name}' in slot '{gameObject.name}' position {currentItemCount}/{itemPlacements.Count}");
    }

    /// <summary>
    /// Removes and returns the most recently placed item from this slot.
    /// </summary>
    public GameObject RemoveItem()
    {
        Debug.Log($"[ShelfSlot] RemoveItem: Slot '{gameObject.name}' has {currentItemCount} items before removal");

        if (currentItemCount <= 0)
        {
            Debug.Log($"[ShelfSlot] RemoveItem: Slot '{gameObject.name}' is empty, returning null");
            return null;
        }

        currentItemCount--;
        ItemPlacement placement = itemPlacements[currentItemCount];
        GameObject item = placement.placedItem;
        placement.placedItem = null;

        Debug.Log($"[ShelfSlot] RemoveItem: Removed item '{item?.name ?? "null"}' from position {currentItemCount}. Slot now has {currentItemCount} items");

        if (item == null)
        {
            Debug.LogWarning($"[ShelfSlot] RemoveItem: Slot '{gameObject.name}' had count {currentItemCount + 1} but placedItem was null!");
            return null;
        }

        // Unparent the item
        item.transform.SetParent(null);

        // Re-enable physics
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        return item;
    }

    /// <summary>
    /// Removes a specific item from the slot (e.g. when picked up by player).
    /// Shifts remaining items to fill the gap.
    /// </summary>
    public void RemoveSpecificItem(GameObject item)
    {
        int index = -1;

        // Find the index of the item
        for (int i = 0; i < itemPlacements.Count; i++)
        {
            if (itemPlacements[i].placedItem == item)
            {
                index = i;
                break;
            }
        }

        if (index == -1)
        {
            Debug.LogWarning($"[ShelfSlot] RemoveSpecificItem: Item '{item.name}' not found in slot '{gameObject.name}'");
            return;
        }

        Debug.Log($"[ShelfSlot] Removing item '{item.name}' from index {index}");

        // Unparent the item
        item.transform.SetParent(null);

        // Re-enable physics
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // Shift items down to fill the gap
        // We need to move item at index+1 to index, index+2 to index+1, etc.
        // The last filled spot (currentItemCount-1) becomes null.

        for (int i = index; i < currentItemCount - 1; i++)
        {
            // Move item from i+1 to i
            GameObject nextItem = itemPlacements[i + 1].placedItem;
            itemPlacements[i].placedItem = nextItem;

            if (nextItem != null)
            {
                // Update transform to new position
                ItemPlacement placement = itemPlacements[i];
                nextItem.transform.localPosition = placement.positionOffset;

                // Recalculate rotation for new position
                Quaternion placementRot = Quaternion.Euler(placement.rotationOffset);
                Quaternion categoryRot = Quaternion.identity;

                InteractableItem interactable = nextItem.GetComponent<InteractableItem>();
                if (interactable != null && interactable.ItemCategory != null)
                {
                    categoryRot = Quaternion.Euler(interactable.ItemCategory.shelfRotationOffset);
                }

                nextItem.transform.localRotation = placementRot * categoryRot;
            }
        }

        // Clear the last occupied slot
        if (currentItemCount > 0)
        {
            itemPlacements[currentItemCount - 1].placedItem = null;
            currentItemCount--;
        }

        Debug.Log($"[ShelfSlot] RemoveSpecificItem complete. Slot count now {currentItemCount}");
    }

    /// <summary>
    /// Clears all items from the slot (e.g., if items were destroyed).
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < itemPlacements.Count; i++)
        {
            itemPlacements[i].placedItem = null;
        }
        currentItemCount = 0;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Use actual BoxCollider size for accurate visualization
        BoxCollider col = GetComponent<BoxCollider>();
        Vector3 size = col != null ? col.size : slotSize;
        Vector3 center = col != null ? col.center : Vector3.zero;

        // Always draw a subtle wireframe
        Gizmos.color = IsOccupied ? new Color(1f, 0.3f, 0.3f, 0.5f) : new Color(0.3f, 1f, 0.3f, 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.DrawWireCube(center, size);
    }

    private void OnDrawGizmosSelected()
    {
        // Use actual BoxCollider size for accurate visualization
        BoxCollider col = GetComponent<BoxCollider>();
        Vector3 size = col != null ? col.size : slotSize;
        Vector3 center = col != null ? col.center : Vector3.zero;

        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);

        // Draw solid box when selected
        Gizmos.color = IsOccupied ? new Color(1f, 0.3f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.3f, 0.3f);
        Gizmos.DrawCube(center, size);

        // Brighter wireframe when selected
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
