using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the box-to-shelf item placement workflow with multi-shelf detection
/// and a locked item queue for predictable restocking.
/// Attach to the player or camera object alongside ObjectPickup.
/// </summary>
public class ItemPlacementManager : MonoBehaviour
{
    public enum PlacementState { Idle, Ready, Disabled }

    [Header("References")]
    [SerializeField] private ObjectPickup objectPickup;

    [Header("Proximity Detection")]
    [Tooltip("Range to detect nearby shelf sections.")]
    [SerializeField] private float shelfDetectionRange = 3f;

    [Tooltip("Layer mask for shelf detection.")]
    [SerializeField] private LayerMask shelfLayerMask = ~0;

    [Header("Item Queue")]
    [Tooltip("Number of items at the front of the queue that are locked and won't change when shelves enter/exit range.")]
    [SerializeField] private int queueLockCount = 2;

    [Header("Ghost Preview")]
    [Tooltip("Whether the ghost item preview is currently enabled.")]
    [SerializeField] private bool showItemPreview = true;

    [Tooltip("Key to toggle the ghost item preview on/off.")]
    [SerializeField] private KeyCode togglePreviewKey = KeyCode.Tab;

    [Tooltip("Material to apply to ghost preview objects (should be semi-transparent).")]
    [SerializeField] private Material ghostMaterial;

    [Tooltip("Color for the ghost preview.")]
    [SerializeField] private Color ghostColor = new Color(0.5f, 1f, 0.5f, 0.5f);

    [Header("UI Prompts")]
    [SerializeField] private string equipBoxPrompt = "Equip box to restock";
    [SerializeField] private string emptyBoxPrompt = "Box is empty";
    [SerializeField] private string slotFullPrompt = "Slot full";
    [SerializeField] private string categoryMismatchPrompt = "Wrong item type";

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = false;

    // State
    public PlacementState State { get; private set; } = PlacementState.Idle;
    public ItemCategory CurrentlySelectedCategory { get; private set; }
    public string CurrentPrompt { get; private set; } = string.Empty;

    // Cached references
    private InventoryBox _activeBox;
    private BoxItemPreview _boxPreview;
    private List<ShelfSection> _nearbyShelves = new List<ShelfSection>();
    private ShelfSlot _targetSlot;
    private Camera _playerCamera;

    // Ghost preview pool: one ghost per available shelf slot
    private Dictionary<ShelfSlot, GameObject> _ghostPreviews = new Dictionary<ShelfSlot, GameObject>();

    // Item queue
    private List<ItemCategory> _itemQueue = new List<ItemCategory>();

    // Track shelf set for change detection
    private HashSet<ShelfSection> _previousShelfSet = new HashSet<ShelfSection>();
    private bool _wasNearShelves = false;

    // Singleton for easy access
    public static ItemPlacementManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        _playerCamera = Camera.main;

        if (objectPickup == null)
        {
            objectPickup = GetComponent<ObjectPickup>();
            if (objectPickup == null)
                objectPickup = FindFirstObjectByType<ObjectPickup>();
        }
    }

    private void Update()
    {
        // Toggle ghost preview
        if (Input.GetKeyDown(togglePreviewKey))
        {
            showItemPreview = !showItemPreview;

            if (!showItemPreview)
                ClearAllGhostPreviews();

            if (logStateChanges)
                Debug.Log($"[ItemPlacementManager] Item preview toggled: {(showItemPreview ? "ON" : "OFF")}");
        }

        UpdatePlacementState();
    }

    private void UpdatePlacementState()
    {
        // Check if player is holding an inventory box
        InventoryBox prevBox = _activeBox;
        _activeBox = GetHeldInventoryBox();

        // Handle box change (picked up a new one or dropped the old one)
        if (_activeBox != prevBox)
        {
            // Close the previous box if it was open (e.g., player dropped it near shelves)
            if (prevBox != null && prevBox.IsOpen)
            {
                BoxItemPreview prevPreview = prevBox.GetComponent<BoxItemPreview>();
                if (prevPreview != null)
                    prevPreview.ClearPreviews();
                prevBox.CloseBox();
            }

            _boxPreview = _activeBox != null ? _activeBox.GetComponent<BoxItemPreview>() : null;
        }

        if (_activeBox == null)
        {
            SetState(PlacementState.Idle, equipBoxPrompt);
            ClearProximityState();
            return;
        }

        // Check if box has any stock at all
        if (!_activeBox.HasAnyStock())
        {
            SetState(PlacementState.Disabled, emptyBoxPrompt);
            ClearAllGhostPreviews();
            return;
        }

        // Detect all nearby shelf sections
        List<ShelfSection> currentShelves = DetectNearbyShelfSections();
        bool isNearShelves = currentShelves.Count > 0;

        // Detect shelf proximity transitions for open/close
        if (isNearShelves && !_wasNearShelves)
        {
            // Just entered shelf proximity — open the box
            _activeBox.OpenBox();
        }
        else if (!isNearShelves && _wasNearShelves)
        {
            // Just left shelf proximity — clear previews and close
            if (_boxPreview != null)
                _boxPreview.ClearPreviews();
            _activeBox.CloseBox();
        }
        _wasNearShelves = isNearShelves;

        // Check if the set of nearby shelves has changed
        if (HasShelfSetChanged(currentShelves))
        {
            _nearbyShelves = currentShelves;
            UpdatePreviousShelfSet();
            RebuildItemQueue();
        }

        // Show ghost previews on ALL available slots (regardless of aim)
        if (showItemPreview && _nearbyShelves.Count > 0)
            UpdateAllGhostPreviews();
        else
            ClearAllGhostPreviews();

        // If no nearby shelves while holding box
        if (_nearbyShelves.Count == 0)
        {
            SetState(PlacementState.Idle, string.Empty);
            _targetSlot = null;
            return;
        }

        // Get the current item to place from the queue
        ItemCategory nextItem = _itemQueue.Count > 0 ? _itemQueue[0] : null;

        // If no items to place (shelves are fully stocked)
        if (nextItem == null)
        {
            SetState(PlacementState.Disabled, "Shelf is fully stocked");
            _targetSlot = null;
            return;
        }

        // Check if looking at a specific slot on any nearby shelf
        _targetSlot = GetTargetedShelfSlot();

        if (_targetSlot == null)
        {
            // Near shelf but not aiming at a slot
            SetState(PlacementState.Idle, $"Aim at slot to place: {nextItem.name}");
            return;
        }

        // Validate the targeted slot
        ItemCategory slotCategory = _targetSlot.AcceptedCategory;

        // Check if slot is full
        if (_targetSlot.IsOccupied)
        {
            SetState(PlacementState.Disabled, slotFullPrompt);
            return;
        }

        // Check if the next item matches this slot's category
        if (slotCategory != null && nextItem != slotCategory)
        {
            SetState(PlacementState.Disabled, $"{categoryMismatchPrompt} - needs {slotCategory.name}");
            return;
        }

        // Ready for placement
        CurrentlySelectedCategory = nextItem;

        int currentCount = _targetSlot.CurrentItemCount;
        int maxCount = _targetSlot.MaxItems;
        string prompt = $"Press E to Place {nextItem.name} ({currentCount + 1}/{maxCount})";

        SetState(PlacementState.Ready, prompt);
    }

    #region Item Queue

    /// <summary>
    /// Rebuilds the item queue based on all missing items from nearby shelves.
    /// Preserves the first N locked items (where N = min(queueLockCount, current queue size)).
    /// </summary>
    private void RebuildItemQueue()
    {
        if (_activeBox == null || !_activeBox.HasAnyStock())
        {
            _itemQueue.Clear();
            return;
        }

        // Aggregate missing items from ALL nearby shelves (weighted by empty slot count)
        List<ItemCategory> weightedPool = new List<ItemCategory>();
        foreach (ShelfSection shelf in _nearbyShelves)
        {
            weightedPool.AddRange(shelf.GetMissingItems());
        }

        // Determine how many items to lock at the front
        int lockCount = Mathf.Min(queueLockCount, _itemQueue.Count);
        int remaining = _activeBox.GetRemainingCount();

        // Preserve locked items
        List<ItemCategory> lockedItems = new List<ItemCategory>();
        for (int i = 0; i < lockCount; i++)
        {
            lockedItems.Add(_itemQueue[i]);
        }

        // Rebuild the queue
        _itemQueue.Clear();
        _itemQueue.AddRange(lockedItems);

        // Fill remaining slots using streak runs:
        // Categories with more missing slots get longer consecutive runs (2-3).
        List<ItemCategory> availablePool = new List<ItemCategory>(weightedPool);
        foreach (ItemCategory locked in lockedItems)
        {
            // Remove one instance of each locked category from pool
            availablePool.Remove(locked);
        }

        int maxToFill = remaining - _itemQueue.Count;
        while (availablePool.Count > 0 && _itemQueue.Count < remaining && maxToFill > 0)
        {
            // Pick a random category from the weighted pool
            int randomIndex = Random.Range(0, availablePool.Count);
            ItemCategory picked = availablePool[randomIndex];

            // Count how many of this category remain in the pool
            int availableCount = 0;
            foreach (ItemCategory cat in availablePool)
            {
                if (cat == picked) availableCount++;
            }

            // Determine run length based on availability:
            //   >= 3 available → run of 2-3
            //   == 2 available → run of 1-2
            //   == 1 available → run of 1
            int runLength;
            if (availableCount >= 3)
                runLength = Random.Range(2, 4); // 2 or 3
            else if (availableCount == 2)
                runLength = Random.Range(1, 3); // 1 or 2
            else
                runLength = 1;

            // Clamp to what's actually available and remaining capacity
            runLength = Mathf.Min(runLength, availableCount, remaining - _itemQueue.Count);

            // Add the run to the queue and remove from pool
            for (int j = 0; j < runLength; j++)
            {
                _itemQueue.Add(picked);
                availablePool.Remove(picked); // removes first occurrence
            }

            maxToFill -= runLength;
        }

        if (logStateChanges)
        {
            string queueStr = string.Join(", ", _itemQueue.ConvertAll(c => c != null ? c.name : "null"));
            Debug.Log($"[ItemPlacementManager] Queue rebuilt ({_itemQueue.Count} items, {lockCount} locked): [{queueStr}]");
        }

        // Update box item preview with new front two items
        if (_boxPreview != null && _activeBox != null && _activeBox.IsOpen)
        {
            ItemCategory frontItem = _itemQueue.Count > 0 ? _itemQueue[0] : null;
            ItemCategory secondItem = _itemQueue.Count > 1 ? _itemQueue[1] : null;
            _boxPreview.UpdatePreview(frontItem, secondItem);
        }
    }

    #endregion

    #region Placement

    /// <summary>
    /// Attempts to place an item from the box onto the targeted shelf slot.
    /// Called by ObjectPickup when interact key is pressed.
    /// </summary>
    public bool TryPlaceFromBox()
    {
        if (State != PlacementState.Ready) return false;
        if (_activeBox == null || _targetSlot == null) return false;

        ItemCategory category = CurrentlySelectedCategory;
        if (category == null)
        {
            Debug.LogWarning("[ItemPlacementManager] No category selected for placement");
            return false;
        }

        // Get prefab from the category itself
        GameObject prefab = category.prefab;
        if (prefab == null)
        {
            Debug.LogWarning($"[ItemPlacementManager] No prefab assigned on ItemCategory '{category.name}'");
            return false;
        }

        // Spawn the item
        GameObject itemInstance = Instantiate(prefab);

        // Try to place on shelf
        if (_targetSlot.TryPlaceItem(itemInstance))
        {
            // Decrement box inventory
            _activeBox.Decrement();

            // Trigger swap animation in box preview before rebuilding queue
            if (_boxPreview != null)
                _boxPreview.AnimateSlotSwap();

            // Refresh ghost previews after placement
            ClearAllGhostPreviews();

            if (logStateChanges)
                Debug.Log($"[ItemPlacementManager] Placed {category.name} on {_targetSlot.gameObject.name}. Box remaining: {_activeBox.GetRemainingCount()}");

            // Dequeue the placed item and rebuild queue
            if (_itemQueue.Count > 0)
                _itemQueue.RemoveAt(0);

            RebuildItemQueue();

            return true;
        }
        else
        {
            // Placement failed - destroy spawned item
            Destroy(itemInstance);

            // Trigger screen shake
            if (MouseLook.Instance != null)
                MouseLook.Instance.Shake();

            return false;
        }
    }

    #endregion

    #region Public Accessors

    /// <summary>
    /// Returns true if the player is currently holding an InventoryBox and looking at a valid slot.
    /// </summary>
    public bool IsPlacementReady()
    {
        return State == PlacementState.Ready;
    }

    /// <summary>
    /// Returns true if the player is holding an inventory box.
    /// </summary>
    public bool IsHoldingBox()
    {
        return _activeBox != null;
    }

    /// <summary>
    /// Returns the currently selected item category (next in queue).
    /// </summary>
    public ItemCategory GetSelectedCategory()
    {
        return _itemQueue.Count > 0 ? _itemQueue[0] : null;
    }

    /// <summary>
    /// Returns a read-only view of the upcoming item queue.
    /// </summary>
    public IReadOnlyList<ItemCategory> GetItemQueue()
    {
        return _itemQueue.AsReadOnly();
    }

    #endregion

    #region Detection

    private InventoryBox GetHeldInventoryBox()
    {
        if (objectPickup == null) return null;

        GameObject heldObject = objectPickup.GetHeldObject();
        if (heldObject == null) return null;

        return heldObject.GetComponent<InventoryBox>();
    }

    /// <summary>
    /// Detects ALL shelf sections within range and returns them sorted by distance.
    /// </summary>
    private List<ShelfSection> DetectNearbyShelfSections()
    {
        if (_playerCamera == null) return new List<ShelfSection>();

        Collider[] colliders = Physics.OverlapSphere(
            _playerCamera.transform.position,
            shelfDetectionRange,
            shelfLayerMask
        );

        // Use a set to avoid duplicates (multiple colliders on same shelf)
        HashSet<ShelfSection> shelfSet = new HashSet<ShelfSection>();
        List<ShelfSection> shelves = new List<ShelfSection>();

        foreach (Collider col in colliders)
        {
            ShelfSection section = col.GetComponent<ShelfSection>();
            if (section == null)
                section = col.GetComponentInParent<ShelfSection>();

            if (section != null && shelfSet.Add(section))
            {
                shelves.Add(section);
            }
        }

        // Sort by distance for consistent ordering
        Vector3 camPos = _playerCamera.transform.position;
        shelves.Sort((a, b) =>
            Vector3.Distance(camPos, a.transform.position)
                .CompareTo(Vector3.Distance(camPos, b.transform.position)));

        return shelves;
    }

    private bool HasShelfSetChanged(List<ShelfSection> currentShelves)
    {
        if (currentShelves.Count != _previousShelfSet.Count) return true;

        foreach (ShelfSection shelf in currentShelves)
        {
            if (!_previousShelfSet.Contains(shelf)) return true;
        }

        return false;
    }

    private void UpdatePreviousShelfSet()
    {
        _previousShelfSet.Clear();
        foreach (ShelfSection shelf in _nearbyShelves)
        {
            _previousShelfSet.Add(shelf);
        }
    }

    private ShelfSlot GetTargetedShelfSlot()
    {
        if (_playerCamera == null) return null;

        Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, shelfDetectionRange, shelfLayerMask))
        {
            ShelfSlot slot = hit.collider.GetComponent<ShelfSlot>();
            if (slot == null)
                slot = hit.collider.GetComponentInParent<ShelfSlot>();

            // Only return slots that belong to one of the nearby shelves
            if (slot != null && _nearbyShelves.Count > 0)
            {
                foreach (ShelfSection shelf in _nearbyShelves)
                {
                    if (shelf.GetSlots().Contains(slot))
                        return slot;
                }
            }

            return slot;
        }
        return null;
    }

    #endregion

    #region Ghost Preview

    /// <summary>
    /// Shows ghost previews only on slots that match the next item in the queue.
    /// </summary>
    private void UpdateAllGhostPreviews()
    {
        if (_activeBox == null) return;

        // Only show ghosts for the FIRST item in the queue
        ItemCategory nextCategory = _itemQueue.Count > 0 ? _itemQueue[0] : null;
        if (nextCategory == null || nextCategory.prefab == null)
        {
            ClearAllGhostPreviews();
            return;
        }

        // Track which slots we've processed this frame
        HashSet<ShelfSlot> activeSlots = new HashSet<ShelfSlot>();

        foreach (ShelfSection shelf in _nearbyShelves)
        {
            foreach (ShelfSlot slot in shelf.GetSlots())
            {
                // Skip full slots or slots that don't match the next queue item
                if (slot.IsOccupied || slot.AcceptedCategory == null) continue;
                if (slot.AcceptedCategory != nextCategory) continue;

                ItemCategory category = slot.AcceptedCategory;
                if (category.prefab == null) continue;

                // Get next placement position
                int nextIndex = slot.CurrentItemCount;
                if (nextIndex >= slot.ItemPlacements.Count) continue;

                activeSlots.Add(slot);

                ItemPlacement placement = slot.ItemPlacements[nextIndex];
                Vector3 worldPos = slot.transform.TransformPoint(placement.positionOffset);

                // Calculate rotation: slot offset * category offset
                Quaternion worldRot = slot.transform.rotation * Quaternion.Euler(placement.rotationOffset);
                worldRot *= Quaternion.Euler(category.shelfRotationOffset);

                // Create ghost if it doesn't exist for this slot
                if (!_ghostPreviews.TryGetValue(slot, out GameObject ghost) || ghost == null)
                {
                    ghost = Instantiate(category.prefab);
                    ghost.name = $"GhostPreview_{slot.gameObject.name}";

                    // Disable physics and colliders
                    Rigidbody rb = ghost.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;

                    Collider[] colliders = ghost.GetComponentsInChildren<Collider>();
                    foreach (Collider col in colliders)
                        col.enabled = false;

                    ApplyGhostMaterial(ghost);
                    _ghostPreviews[slot] = ghost;
                }

                // Update position and rotation
                ghost.transform.position = worldPos;
                ghost.transform.rotation = worldRot;
            }
        }

        // Clean up ghosts for slots that are no longer available
        List<ShelfSlot> toRemove = new List<ShelfSlot>();
        foreach (var kvp in _ghostPreviews)
        {
            if (!activeSlots.Contains(kvp.Key))
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (ShelfSlot key in toRemove)
        {
            _ghostPreviews.Remove(key);
        }
    }

    private void ApplyGhostMaterial(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            Material[] mats = new Material[renderer.materials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                if (ghostMaterial != null)
                {
                    mats[i] = new Material(ghostMaterial);
                }
                else
                {
                    // Create simple transparent material
                    mats[i] = new Material(Shader.Find("Sprites/Default"));
                }
                mats[i].color = ghostColor;
            }
            renderer.materials = mats;
        }
    }

    /// <summary>
    /// Destroys all ghost preview instances.
    /// </summary>
    private void ClearAllGhostPreviews()
    {
        foreach (var kvp in _ghostPreviews)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        _ghostPreviews.Clear();
    }

    #endregion

    #region State Management

    private void SetState(PlacementState newState, string prompt)
    {
        if (State != newState && logStateChanges)
        {
            Debug.Log($"[ItemPlacementManager] State: {State} → {newState}");
        }

        State = newState;
        CurrentPrompt = prompt;
    }

    private void ClearProximityState()
    {
        ClearAllGhostPreviews();

        if (_boxPreview != null)
            _boxPreview.ClearPreviews();

        _nearbyShelves.Clear();
        _previousShelfSet.Clear();
        _wasNearShelves = false;
        _targetSlot = null;
        _itemQueue.Clear();
        CurrentlySelectedCategory = null;
        _boxPreview = null;
    }

    private void OnDisable()
    {
        ClearProximityState();
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw shelf detection range
        if (_playerCamera != null)
        {
            Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireSphere(_playerCamera.transform.position, shelfDetectionRange);
        }
    }
#endif
}
