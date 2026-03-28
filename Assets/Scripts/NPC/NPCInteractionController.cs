using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls NPC behavior for detecting, navigating to, picking up, and delivering items.
/// Requires a NavMeshAgent component on the same GameObject.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NPCInteractionController : NetworkBehaviour
{
    /// <summary>
    /// Fired just before an NPC is destroyed after reaching the exit (or despawning immediately).
    /// </summary>
    public static event Action<NPCInteractionController> OnNPCExited;

    [Header("Detection Settings")]
    [Tooltip("Radius within which the NPC scans for interactable items.")]
    [SerializeField] private float detectionRadius = 10f;

    [Tooltip("Layer mask for interactable items. Set to 'Interactable' layer.")]
    [SerializeField] private LayerMask itemLayerMask;

    [Header("Interaction Settings")]
    [Tooltip("Distance at which the NPC can reach and pick up an item.")]
    [SerializeField] private float reachDistance = 0.75f;

    [Tooltip("Maximum distance to search for NavMesh when items are on shelves. Should be larger than tallest shelf height.")]
    [SerializeField] private float shelfReachDistance = 5f;

    [Tooltip("The NPC's hand transform where picked up items will be parented.")]
    [SerializeField] private Transform handBone;

    [Header("Counter Settings")]
    [Tooltip("Counter slots where items will be placed.")]
    [SerializeField] private List<CounterSlot> counterSlots = new List<CounterSlot>();

    [Header("Item Preferences")]
    [Tooltip("Categories of items this NPC will pick up. Leave empty to pick up any item.")]
    [SerializeField] private List<ItemCategory> wantedCategories = new List<ItemCategory>();

    [Header("Shelf Slot Source")]
    [Tooltip("If true, NPC takes items from specified shelf slots instead of scanning nearby items.")]
    [SerializeField] private bool useShelfSlots = false;

    [Tooltip("List of shelf slots the NPC is allowed to take items from.")]
    [SerializeField] private List<ShelfSlot> allowedShelfSlots = new List<ShelfSlot>();

    [Header("Behavior Settings")]
    [Tooltip("Automatically scan for items at regular intervals.")]
    [SerializeField] private bool autoScan = true;

    [Tooltip("Time between automatic scans in seconds.")]
    [SerializeField] private float scanInterval = 1f;

    [Tooltip("Time to pause at item before picking it up.")]
    [SerializeField] private float pickupPauseTime = 0.5f;

    [Header("Collection Settings")]
    [Tooltip("Number of items to collect before going to counter.")]
    [SerializeField] private int batchSize = 4;

    [Tooltip("Enable/disable item collection. Set to false to stop the NPC from collecting items.")]
    [SerializeField] private bool isCollecting = true;

    [Header("Exit Settings")]
    [Tooltip("The exit point where the NPC goes after checkout.")]
    [SerializeField] private Transform exitPoint;

    [Header("ID Card Settings")]
    [Tooltip("The identity data for this NPC (name, DOB, address, etc.).")]
    [SerializeField] private NPCIdentity npcIdentity;

    [Tooltip("The ID card prefab to spawn on the counter.")]
    [SerializeField] private GameObject idCardPrefab;

    [Tooltip("The counter slot where the ID card will be placed.")]
    [SerializeField] private IDCardSlot idCardSlot;

    [Header("Prescription & Doppelganger")]
    [Tooltip("Prescription data for this NPC. Displayed on the computer screen during verification.")]
    [SerializeField] private PrescriptionData prescriptionData;

    [Tooltip("Doppelganger profile. Null = real patient. Assigned at runtime by NPCSpawnManager.")]
    [SerializeField] private DoppelgangerProfile doppelgangerProfile;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showDebugLogs = false;

    private NavMeshAgent _agent;
    private IInteractable _currentTarget;
    private GameObject _currentTargetObject;
    private List<InteractableItem> _heldItems = new List<InteractableItem>();
    private List<GameObject> _placedCounterItems = new List<GameObject>();
    private Dictionary<GameObject, float> _unreachableItems = new Dictionary<GameObject, float>(); // Items we couldn't path to
    private float _scanTimer;
    private float _pauseTimer;
    private bool _hasStartedMoving;
    private bool _hasPlacedIDCard = false;
    private bool _hasCheckedOut = false; // Set true when player triggers checkout
    private const float UNREACHABLE_RETRY_TIME = 10f; // Seconds before retrying unreachable items

    // Animation events for NPCAnimationController
    public event System.Action OnPickupStart;
    public event System.Action OnPlaceStart;

    // States for the interaction flow
    public enum NPCState { Idle, MovingToItem, WaitingAtItem, PickingUp, MovingToCounter, PlacingItem, WaitingForCheckout, MovingToExit }

    // Networked state — synced to all clients so NPCAnimationController can read it.
    // Write-permission is Server so only the server's state machine updates it.
    private NetworkVariable<int> _networkState = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Networked NPC state integer (cast to NPCState). Readable on all clients.</summary>
    public NetworkVariable<int> NetworkNPCState => _networkState;

    // Private backing field. All assignments go through the property so _networkState stays in sync.
    private NPCState _currentStateInternal = NPCState.Idle;
    private NPCState _currentState
    {
        get => _currentStateInternal;
        set
        {
            _currentStateInternal = value;
            // Only the server writes to the NetworkVariable (guards against client-side reads).
            if (_networkState != null &&
                (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer))
            {
                _networkState.Value = (int)value;
            }
        }
    }

    /// <summary>The NPC's current state (read-only).</summary>
    public NPCState CurrentNPCState => _currentState;

    /// <summary>
    /// Helper method for conditional debug logging.
    /// </summary>
    private void DebugLog(string message)
    {
        if (showDebugLogs) Debug.Log(message);
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.stoppingDistance = reachDistance;
    }

    public override void OnNetworkSpawn()
    {
        // Clients don't run the NPC AI — disable their NavMeshAgent so it doesn't
        // fight with the NetworkTransform that syncs position from the server.
        if (!IsServer)
        {
            _agent.enabled = false;
        }
    }

    private void Update()
    {
        // NPC AI runs on the server only. Clients receive position/state via NetworkTransform
        // and the _networkState NetworkVariable.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
            return;

        switch (_currentState)
        {
            case NPCState.Idle:
                HandleIdleState();
                break;

            case NPCState.MovingToItem:
                HandleMovingToItemState();
                break;

            case NPCState.WaitingAtItem:
                HandleWaitingAtItemState();
                break;

            case NPCState.PickingUp:
                HandlePickupState();
                break;

            case NPCState.MovingToCounter:
                HandleMovingToCounterState();
                break;

            case NPCState.PlacingItem:
                HandlePlacingState();
                break;

            case NPCState.WaitingForCheckout:
                HandleWaitingForCheckoutState();
                break;

            case NPCState.MovingToExit:
                HandleMovingToExitState();
                break;
        }
    }

    /// <summary>
    /// Idle state: scan for items periodically if autoScan is enabled.
    /// If checkout is triggered, navigate to exit.
    /// If holding items, try to deliver them to counter.
    /// </summary>
    private void HandleIdleState()
    {
        // If checkout was triggered, head to exit
        if (_hasCheckedOut)
        {
            if (exitPoint != null)
            {
                DebugLog($"[NPC] Checkout complete! Heading to exit at {exitPoint.position}");
                CleanupIDCard();
                _agent.SetDestination(exitPoint.position);
                _currentState = NPCState.MovingToExit;
            }
            else
            {
                Debug.LogWarning("[NPC] Checkout triggered but no exit point assigned! Despawning immediately.");
                DespawnOrDestroy();
            }
            return;
        }

        if (!autoScan || !isCollecting) return;

        _scanTimer += Time.deltaTime;
        if (_scanTimer >= scanInterval)
        {
            _scanTimer = 0f;
            ScanForItems();
        }
    }

    /// <summary>
    /// Moving to exit state: check if NPC has reached the exit and despawn.
    /// </summary>
    private void HandleMovingToExitState()
    {
        if (_agent.pathPending) return;

        // Mark that we've started moving
        if (_agent.hasPath && _agent.remainingDistance > _agent.stoppingDistance)
        {
            _hasStartedMoving = true;
        }

        if (_hasStartedMoving && _agent.remainingDistance <= _agent.stoppingDistance && !_agent.pathPending)
        {
            DebugLog("[NPC] Reached exit. Goodbye!");
            _hasStartedMoving = false;
            DespawnOrDestroy();
        }
    }

    /// <summary>
    /// Moving to item state: check if NPC has reached the target item.
    /// </summary>
    private void HandleMovingToItemState()
    {
        if (_currentTargetObject == null || _currentTarget == null)
        {
            CancelCurrentAction();
            return;
        }

        // Wait for path to be calculated and agent to start moving
        if (_agent.pathPending) return;

        // Check if we're already at the destination (e.g., item placed in same slot we just picked from)
        // This can happen when remainingDistance starts at or below stoppingDistance
        if (!_hasStartedMoving)
        {
            if (_agent.remainingDistance > _agent.stoppingDistance)
            {
                // We need to travel, mark that we've started moving
                _hasStartedMoving = true;
            }
            else if (_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance)
            {
                // We're already at (or very close to) the destination - skip directly to waiting
                DebugLog($"[NPC] Already at item location: {_currentTargetObject.name} (distance: {_agent.remainingDistance:F2})");
                _hasStartedMoving = false;
                _pauseTimer = 0f;
                _currentState = NPCState.WaitingAtItem;
                return;
            }
        }

        // Check arrival after we've started moving
        if (_hasStartedMoving && _agent.remainingDistance <= _agent.stoppingDistance && !_agent.pathPending)
        {
            DebugLog($"[NPC] Arrived at item: {_currentTargetObject.name}");
            _hasStartedMoving = false;
            _pauseTimer = 0f;
            _currentState = NPCState.WaitingAtItem;
        }
    }

    /// <summary>
    /// Waiting at item state: pause before picking up.
    /// </summary>
    private void HandleWaitingAtItemState()
    {
        _pauseTimer += Time.deltaTime;

        if (_pauseTimer >= pickupPauseTime)
        {
            _currentState = NPCState.PickingUp;
        }
    }

    /// <summary>
    /// Pickup state: execute the pickup and decide whether to continue collecting or go to counter.
    /// </summary>
    private void HandlePickupState()
    {
        // Trigger pickup animation on all clients (including host) via ClientRpc.
        // Fall back to the C# event when NGO is not running (editor solo testing).
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            TriggerPickupAnimationClientRpc();
        else
            OnPickupStart?.Invoke();

        InteractableItem pickedItem = null;

        // Cache the held item - check both on object and in parents
        if (_currentTargetObject != null)
        {
            pickedItem = _currentTargetObject.GetComponent<InteractableItem>();
            if (pickedItem == null)
            {
                pickedItem = _currentTargetObject.GetComponentInParent<InteractableItem>();
            }
        }

        if (_currentTarget != null && handBone != null && pickedItem != null)
        {
            DebugLog($"[NPC] Picking up: {pickedItem.gameObject.name}");
            _currentTarget.OnPickedUp(handBone);
            _heldItems.Add(pickedItem);

            // Disable all renderers on the held item to hide it
            Renderer[] renderers = pickedItem.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                r.enabled = false;
                DebugLog($"[NPC] Disabled renderer: {r.gameObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"[NPC] Pickup failed - Target: {_currentTarget != null}, HandBone: {handBone != null}, PickedItem: {pickedItem != null}");
        }

        // Clear pickup target
        _currentTarget = null;
        _currentTargetObject = null;

        // Decide next action - go to queue when batch is full or if collecting is disabled
        bool shouldCheckout = _heldItems.Count >= batchSize || (!isCollecting && _heldItems.Count > 0);

        if (shouldCheckout)
        {
            if (counterSlots.Count > 0)
            {
                DebugLog($"[NPC] Batch full with {_heldItems.Count} items, heading to counter.");
                _agent.SetDestination(counterSlots[0].Position);
                _currentState = NPCState.MovingToCounter;
            }
            else
            {
                Debug.LogWarning("[NPC] No counter slots assigned!");
                _currentState = NPCState.Idle; // Or some fallback state
            }
        }
        else if (_heldItems.Count < batchSize && isCollecting)
        {
            // Continue collecting more items
            DebugLog($"[NPC] Collected {_heldItems.Count}/{batchSize} items, looking for more...");
            _currentState = NPCState.Idle;
        }
        else
        {
            _currentState = NPCState.Idle;
        }
    }

    /// <summary>
    /// Moving to counter state: check if NPC has reached the counter.
    /// </summary>
    private void HandleMovingToCounterState()
    {
        if (_agent.pathPending) return;

        // Check if we're already at the destination (e.g., waiting at counter for slot to become available)
        if (!_hasStartedMoving)
        {
            if (_agent.remainingDistance > _agent.stoppingDistance)
            {
                // We need to travel, mark that we've started moving
                _hasStartedMoving = true;
            }
            else if (_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance)
            {
                // We're already at (or very close to) the counter - skip directly to placing
                DebugLog($"[NPC] Already at counter (distance: {_agent.remainingDistance:F2})");
                _hasStartedMoving = false;
                _currentState = NPCState.PlacingItem;
                return;
            }
        }

        // Check arrival after we've started moving
        if (_hasStartedMoving && _agent.remainingDistance <= _agent.stoppingDistance && !_agent.pathPending)
        {
            DebugLog("[NPC] Arrived at counter");
            _hasStartedMoving = false;
            _currentState = NPCState.PlacingItem;
        }
    }

    /// <summary>
    /// Waiting for checkout state: NPC has placed items and waits for player to process checkout.
    /// </summary>
    private void HandleWaitingForCheckoutState()
    {
        // Just wait - TriggerCheckout will be called by the CashRegister when player checks out
        // If checkout was triggered, _hasCheckedOut will be set and we'll go to exit next update
        if (_hasCheckedOut)
        {
            // Head to exit
            if (exitPoint != null)
            {
                DebugLog($"[NPC] Checkout complete! Heading to exit at {exitPoint.position}");
                CleanupIDCard();
                _agent.SetDestination(exitPoint.position);
                _currentState = NPCState.MovingToExit;
            }
            else
            {
                Debug.LogWarning("[NPC] Checkout triggered but no exit point assigned! Despawning immediately.");
                DespawnOrDestroy();
            }
        }
    }

    /// <summary>
    /// Placing state: place all held items on the counter slots and return to idle.
    /// </summary>
    private void HandlePlacingState()
    {
        // Trigger place animation on all clients via ClientRpc (NGO) or C# event (solo).
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            TriggerPlaceAnimationClientRpc();
        else
            OnPlaceStart?.Invoke();

        DebugLog($"[NPC] HandlePlacingState: heldItems={_heldItems.Count}, counterSlots={counterSlots.Count}");

        // Check if we have counter slots assigned
        if (counterSlots.Count == 0)
        {
            Debug.LogWarning("[NPC] No counter slots assigned to NPC! Cannot place items.");
            _currentState = NPCState.WaitingForCheckout;
            return;
        }

        if (_heldItems.Count > 0)
        {
            // Track items that were successfully placed so we can remove them
            List<InteractableItem> placedItems = new List<InteractableItem>();

            for (int i = 0; i < _heldItems.Count; i++)
            {
                InteractableItem item = _heldItems[i];
                if (item != null)
                {
                    // Find an available counter slot
                    CounterSlot slot = GetAvailableCounterSlot();
                    if (slot != null)
                    {
                        DebugLog($"[NPC] Placing {item.gameObject.name} in counter slot '{slot.name}'");

                        // Re-activate and show the item
                        item.gameObject.SetActive(true);
                        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
                        foreach (Renderer r in renderers)
                        {
                            r.enabled = true;
                        }

                        // Re-enable collider so player can interact with it
                        Collider col = item.GetComponent<Collider>();
                        if (col != null)
                        {
                            col.enabled = true;
                        }

                        // Mark as delivered so NPC won't pick it up again
                        item.MarkAsDelivered();

                        // Place in slot (handles positioning and physics)
                        slot.PlaceItem(item.gameObject);
                        placedItems.Add(item);
                        _placedCounterItems.Add(item.gameObject);

                        // Broadcast to all clients that this NetworkObject is now a counter item,
                        // so non-host clients can detect it during player interaction
                        NetworkObject itemNetObj = item.GetComponent<NetworkObject>();
                        CounterSlotNetwork counterSlotNetwork = slot.GetComponent<CounterSlotNetwork>();
                        if (itemNetObj != null && counterSlotNetwork != null)
                            counterSlotNetwork.RecordPlacement(itemNetObj.NetworkObjectId);
                    }
                    else
                    {
                        // No more slots available, stop trying to place
                        DebugLog($"[NPC] Counter slots full, waiting to place remaining {_heldItems.Count - placedItems.Count} item(s)");
                        break;
                    }
                }
            }

            // Remove only the items that were successfully placed
            foreach (InteractableItem placed in placedItems)
            {
                _heldItems.Remove(placed);
            }

            DebugLog($"[NPC] Placed {placedItems.Count} item(s) at counter, {_heldItems.Count} remaining");

            // If we still have items, wait at counter for a slot to become available
            if (_heldItems.Count > 0)
            {
                DebugLog("[NPC] Waiting at counter for slot to become available...");
                // Stay in placing state, will retry next frame
                return;
            }
            else
            {
                // All items placed — also place ID card simultaneously
                PlaceIDCard();
                DebugLog("[NPC] All items placed, waiting for checkout");
                _currentState = NPCState.WaitingForCheckout;
            }
        }
        else
        {
            Debug.LogWarning("[NPC] Place failed - no items held");
            // Still place ID card even if no store items
            PlaceIDCard();
            _currentState = NPCState.WaitingForCheckout; // Still wait for checkout even if no items
        }
    }

    /// <summary>
    /// Gets the first available counter slot that has room for items.
    /// </summary>
    private CounterSlot GetAvailableCounterSlot()
    {
        foreach (CounterSlot slot in counterSlots)
        {
            if (slot != null && !slot.IsOccupied)
            {
                return slot;
            }
        }
        return null;
    }

    /// <summary>
    /// Scans for nearby interactable items and selects the nearest one.
    /// If useShelfSlots is enabled, takes items from allowedShelfSlots instead.
    /// </summary>
    public void ScanForItems()
    {
        DebugLog($"[NPC] ScanForItems called - isCollecting={isCollecting}, heldItems={_heldItems.Count}, batchSize={batchSize}, useShelfSlots={useShelfSlots}");

        // Don't scan if not collecting or already holding max items
        if (!isCollecting)
        {
            DebugLog("[NPC] ScanForItems: Skipped - not collecting");
            return;
        }
        if (_heldItems.Count >= batchSize)
        {
            DebugLog("[NPC] ScanForItems: Skipped - batch full");
            return;
        }

        // If using shelf slots, scan from allowed slots instead
        if (useShelfSlots)
        {
            ScanShelfSlots();
            return;
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, itemLayerMask);

        if (hitColliders.Length == 0)
        {
            return;
        }

        // Find the nearest item that implements IInteractable
        float nearestDistance = float.MaxValue;
        IInteractable nearestInteractable = null;
        GameObject nearestObject = null;

        foreach (Collider col in hitColliders)
        {
            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable == null)
            {
                interactable = col.GetComponentInParent<IInteractable>();
            }

            if (interactable != null)
            {
                // Get the InteractableItem to check its category
                InteractableItem item = col.GetComponent<InteractableItem>();
                if (item == null)
                {
                    item = col.GetComponentInParent<InteractableItem>();
                }

                // Skip items that have already been delivered
                if (item != null && item.IsDelivered)
                {
                    continue;
                }

                // Skip items we couldn't path to recently (clear expired entries)
                if (_unreachableItems.ContainsKey(col.gameObject))
                {
                    if (Time.time - _unreachableItems[col.gameObject] < UNREACHABLE_RETRY_TIME)
                    {
                        continue; // Still in timeout
                    }
                    else
                    {
                        _unreachableItems.Remove(col.gameObject); // Timeout expired, try again
                    }
                }

                // Check category filter (empty list = accept all)
                if (item != null && wantedCategories.Count > 0)
                {
                    if (item.ItemCategory == null || !wantedCategories.Contains(item.ItemCategory))
                    {
                        continue; // Skip items not in our wanted list
                    }
                }

                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestInteractable = interactable;
                    nearestObject = col.gameObject;
                }
            }
        }

        if (nearestInteractable != null)
        {
            SetTarget(nearestInteractable, nearestObject);
        }
    }

    /// <summary>
    /// Scans allowed shelf slots for items to pick up.
    /// Selects the nearest slot that has items.
    /// </summary>
    private void ScanShelfSlots()
    {
        if (allowedShelfSlots.Count == 0)
        {
            Debug.LogWarning("[NPC] useShelfSlots is enabled but no slots are assigned!");
            return;
        }

        DebugLog($"[NPC] ScanShelfSlots: Scanning {allowedShelfSlots.Count} slots...");

        float nearestDistance = float.MaxValue;
        ShelfSlot nearestSlot = null;
        InteractableItem nearestItem = null;

        foreach (ShelfSlot slot in allowedShelfSlots)
        {
            if (slot == null)
            {
                DebugLog("[NPC] ScanShelfSlots: Skipping null slot reference");
                continue;
            }

            DebugLog($"[NPC] ScanShelfSlots: Checking slot '{slot.name}' - HasItems={slot.HasItems}, CurrentCount={slot.CurrentItemCount}, MaxItems={slot.MaxItems}");

            if (!slot.HasItems)
            {
                DebugLog($"[NPC] ScanShelfSlots: Slot '{slot.name}' has no items, skipping");
                continue;
            }

            // Get the first available item from this slot's placements
            InteractableItem item = null;
            int placementIndex = 0;
            foreach (ItemPlacement placement in slot.ItemPlacements)
            {
                DebugLog($"[NPC] ScanShelfSlots: Slot '{slot.name}' placement[{placementIndex}] - placedItem={(placement.placedItem != null ? placement.placedItem.name : "null")}");

                if (placement.placedItem != null)
                {
                    item = placement.placedItem.GetComponent<InteractableItem>();
                    bool isDelivered = item != null && item.IsDelivered;
                    DebugLog($"[NPC] ScanShelfSlots: Found item at placement[{placementIndex}], InteractableItem={(item != null ? "found" : "null")}, IsDelivered={isDelivered}");

                    if (item != null && !item.IsDelivered)
                    {
                        DebugLog($"[NPC] ScanShelfSlots: Valid item found: {item.gameObject.name}");
                        break;
                    }
                    item = null;
                }
                placementIndex++;
            }

            if (item == null)
            {
                DebugLog($"[NPC] ScanShelfSlots: Slot '{slot.name}' had items but none valid for pickup");
                continue;
            }

            // Check category filter
            if (wantedCategories.Count > 0)
            {
                if (item.ItemCategory == null || !wantedCategories.Contains(item.ItemCategory))
                {
                    continue;
                }
            }

            float distance = Vector3.Distance(transform.position, slot.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestSlot = slot;
                nearestItem = item;
            }
        }

        if (nearestSlot != null && nearestItem != null)
        {
            // Find ShelfSlotNetwork BEFORE removing so we still have the slot index
            ShelfSlotNetwork shelfSlotNet = nearestSlot.GetComponentInParent<ShelfSlotNetwork>();
            int shelfSlotIndex = -1;
            if (shelfSlotNet != null)
            {
                for (int i = 0; i < shelfSlotNet.SlotCount; i++)
                    if (shelfSlotNet.GetSlotAt(i) == nearestSlot) { shelfSlotIndex = i; break; }
            }

            // Remove item from shelf slot (NetworkObjects stay kinematic — no physics re-enable)
            GameObject removedItem = nearestSlot.RemoveItem();

            // Sync slot count to all clients via NetworkList
            if (shelfSlotNet != null && shelfSlotIndex >= 0 && removedItem != null)
                shelfSlotNet.RecordPickup(shelfSlotIndex, nearestSlot.CurrentItemCount);

            if (removedItem != null)
            {
                IInteractable interactable = removedItem.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    DebugLog($"[NPC] Taking item from shelf slot: {nearestSlot.name}");
                    SetTarget(interactable, removedItem);
                }
            }
        }
    }

    /// <summary>
    /// Sets the current target item and begins navigation.
    /// </summary>
    public void SetTarget(IInteractable target, GameObject targetObject)
    {
        _currentTarget = target;
        _currentTargetObject = targetObject;

        Vector3 targetPosition = target.GetInteractionPoint();

        // Try to find a valid NavMesh position near the item
        // This allows items on shelves to be reachable by walking near them
        NavMeshHit navHit;
        Vector3 destinationPosition = targetPosition;

        // First check if the exact position is on NavMesh
        if (!NavMesh.SamplePosition(targetPosition, out navHit, 0.5f, NavMesh.AllAreas))
        {
            // Item isn't on NavMesh (likely on a shelf)
            // Try progressively larger search radii to find the floor beneath/near the item
            bool foundNavMesh = false;
            float[] searchRadii = { 2f, shelfReachDistance, shelfReachDistance * 2f };

            foreach (float radius in searchRadii)
            {
                if (NavMesh.SamplePosition(targetPosition, out navHit, radius, NavMesh.AllAreas))
                {
                    destinationPosition = navHit.position;
                    DebugLog($"[NPC] Item {targetObject.name} found NavMesh point at radius {radius}m");
                    foundNavMesh = true;
                    break;
                }
            }

            // Also try searching directly below the item (for tall shelves)
            if (!foundNavMesh)
            {
                Vector3 belowItem = new Vector3(targetPosition.x, 0f, targetPosition.z);
                if (NavMesh.SamplePosition(belowItem, out navHit, 3f, NavMesh.AllAreas))
                {
                    destinationPosition = navHit.position;
                    DebugLog($"[NPC] Item {targetObject.name} found NavMesh point directly below");
                    foundNavMesh = true;
                }
            }

            if (!foundNavMesh)
            {
                // No NavMesh point found nearby at all
                _unreachableItems[targetObject] = Time.time;
                Debug.LogWarning($"[NPC] Cannot find NavMesh near {targetObject.name} (tried up to {shelfReachDistance * 2f}m). Will retry in {UNREACHABLE_RETRY_TIME}s.");
                CancelCurrentAction();
                return;
            }
        }

        // Check if we can path to the destination
        NavMeshPath path = new NavMeshPath();
        bool pathValid = _agent.CalculatePath(destinationPosition, path);

        if (!pathValid || path.status != NavMeshPathStatus.PathComplete)
        {
            // Mark as unreachable so we don't spam this error
            _unreachableItems[targetObject] = Time.time;
            Debug.LogWarning($"[NPC] Cannot path to {targetObject.name} (NavMesh status: {path.status}). Will retry in {UNREACHABLE_RETRY_TIME}s.");
            CancelCurrentAction();
            return;
        }

        _agent.SetDestination(destinationPosition);
        _currentState = NPCState.MovingToItem;
    }

    /// <summary>
    /// Cancels the current action and returns to idle state.
    /// </summary>
    public void CancelCurrentAction()
    {
        _agent.ResetPath();
        _currentTarget = null;
        _currentTargetObject = null;
        _currentState = NPCState.Idle;
    }

    /// <summary>
    /// Returns true if the NPC currently has a target item.
    /// </summary>
    public bool HasTarget()
    {
        return _currentTarget != null;
    }

    /// <summary>
    /// Returns true if the NPC is currently holding an item.
    /// </summary>
    public bool IsHoldingItem()
    {
        return _heldItems.Count > 0;
    }

    /// <summary>
    /// Returns the number of items currently held.
    /// </summary>
    public int GetHeldItemCount()
    {
        return _heldItems.Count;
    }

    /// <summary>
    /// Sets whether the NPC should collect items.
    /// </summary>
    public void SetCollecting(bool collecting)
    {
        isCollecting = collecting;
        DebugLog($"[NPC] Collecting set to: {collecting}");

        // If we're stopping collection and have items, go deliver them
        if (!collecting && _heldItems.Count > 0 && _currentState == NPCState.Idle)
        {
            CounterSlot slot = GetAvailableCounterSlot();
            if (slot != null)
            {
                DebugLog($"[NPC] Stopping collection, delivering {_heldItems.Count} item(s) to counter");
                _agent.SetDestination(slot.Position);
                _currentState = NPCState.MovingToCounter;
            }
        }
    }

    /// <summary>
    /// Returns whether the NPC is currently collecting items.
    /// </summary>
    public bool IsCollecting()
    {
        return isCollecting;
    }

    /// <summary>
    /// Triggers checkout - NPC will stop collecting and head to exit after placing items.
    /// Call this when the player completes the transaction.
    /// </summary>
    public void TriggerCheckout()
    {
        _hasCheckedOut = true;
        isCollecting = false;
        DebugLog("[NPC] Checkout triggered! Will head to exit when ready.");

        // If idle with no items, immediately head to exit
        if (_currentState == NPCState.Idle && _heldItems.Count == 0)
        {
            if (exitPoint != null)
            {
                DebugLog($"[NPC] Heading to exit immediately at {exitPoint.position}");
                CleanupIDCard();
                _agent.SetDestination(exitPoint.position);
                _currentState = NPCState.MovingToExit;
            }
        }
        // If holding items, deliver them first (handled by existing logic)
        else if (_heldItems.Count > 0 && _currentState == NPCState.Idle)
        {
            CounterSlot slot = GetAvailableCounterSlot();
            if (slot != null)
            {
                DebugLog($"[NPC] Delivering remaining {_heldItems.Count} item(s) before checkout");
                _agent.SetDestination(slot.Position);
                _currentState = NPCState.MovingToCounter;
            }
        }
    }

    /// <summary>
    /// Immediately removes this NPC from the scene, as if they had exited normally.
    /// Server-only. Cleans up the ID card and fires OnNPCExited so the spawn queue advances.
    /// </summary>
    public void Kill()
    {
        if (!IsServer) return;
        CleanupIDCard();
        CleanupCounterItems();
        DespawnOrDestroy();
    }

    /// <summary>
    /// Destroys all items this NPC placed on the counter.
    /// Called on Kill() so items don't linger after the NPC is shot.
    /// Server-only.
    /// </summary>
    private void CleanupCounterItems()
    {
        foreach (GameObject item in _placedCounterItems)
        {
            if (item == null) continue;

            NetworkObject netObj = item.GetComponent<NetworkObject>();
            CounterSlot slot = CounterSlot.GetSlotContaining(item);

            if (slot != null)
                slot.RemoveItem(item);

            if (netObj != null && netObj.IsSpawned)
            {
                CounterSlotNetwork slotNetwork = slot != null ? slot.GetComponent<CounterSlotNetwork>() : null;
                if (slotNetwork != null)
                    slotNetwork.RecordRemoval(netObj.NetworkObjectId);
                netObj.Despawn(true);
            }
            else
            {
                Destroy(item);
            }
        }
        _placedCounterItems.Clear();
    }

    // ── ID Card ──────────────────────────────────────────────────────

    /// <summary>
    /// The NPC identity data assigned to this NPC (read-only).
    /// </summary>
    public NPCIdentity NpcIdentity => npcIdentity;

    /// <summary>Prescription data for this NPC (may be null for NPCs without prescriptions).</summary>
    public PrescriptionData Prescription => prescriptionData;

    /// <summary>True if this NPC is a doppelganger (has a DoppelgangerProfile assigned).</summary>
    public bool IsDoppelganger => doppelgangerProfile != null;

    /// <summary>The doppelganger profile, or null for real patients. Server-only ground truth.</summary>
    public DoppelgangerProfile DoppelgangerData => doppelgangerProfile;

    /// <summary>
    /// Assigns a doppelganger profile at runtime. Called by NPCSpawnManager (server-only).
    /// Pass null to leave as a real patient.
    /// </summary>
    public void AssignDoppelgangerProfile(DoppelgangerProfile profile)
    {
        doppelgangerProfile = profile;
    }

    /// <summary>
    /// Assigns shared scene references that can't be saved on prefabs.
    /// Called by NPCSpawnManager after instantiation.
    /// </summary>
    public void AssignSceneReferences(List<CounterSlot> counters, Transform exit, IDCardSlot cardSlot, List<ShelfSlot> shelfSlots)
    {
        counterSlots = counters;
        exitPoint = exit;
        idCardSlot = cardSlot;
        allowedShelfSlots = shelfSlots;
        if (shelfSlots != null && shelfSlots.Count > 0)
            useShelfSlots = true;
    }

    /// <summary>
    /// Places the NPC's ID card on the counter slot.
    /// Called when the NPC finishes placing items (server-only path).
    /// If the card is a NetworkObject, all clients are told to initialize it via ClientRpc.
    /// </summary>
    public void PlaceIDCard()
    {
        if (_hasPlacedIDCard) return;
        if (npcIdentity == null || idCardPrefab == null || idCardSlot == null)
        {
            DebugLog("[NPC] Cannot place ID card — missing identity, prefab, or slot reference.");
            return;
        }

        _hasPlacedIDCard = true;
        IDCardInteraction cardInteraction = idCardSlot.PlaceIDCard(idCardPrefab, npcIdentity);

        // If the card was network-spawned, tell all clients to initialize it.
        // npcIdentity is a ScriptableObject reference that already exists on every client's
        // copy of this NPC prefab, so we don't need to send it over the wire.
        if (cardInteraction != null)
        {
            NetworkObject cardNetObj = cardInteraction.GetComponent<NetworkObject>();
            if (cardNetObj != null && cardNetObj.IsSpawned)
                InitializeIDCardClientRpc(cardNetObj.NetworkObjectId);
        }

        DebugLog($"[NPC] Placed ID card for '{npcIdentity.fullName}' on counter.");
    }

    /// <summary>
    /// Called on all clients after the ID card NetworkObject is spawned.
    /// Reads the NPC's local npcIdentity reference (already present on every client)
    /// and initializes the card's interaction component.
    /// The focusCameraTarget is read from the IDCardSlot (a scene object that exists on every client).
    /// </summary>
    [ClientRpc]
    private void InitializeIDCardClientRpc(ulong cardNetworkObjectId)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(cardNetworkObjectId, out NetworkObject cardNetObj))
        {
            Debug.LogWarning($"[NPC] InitializeIDCardClientRpc: card {cardNetworkObjectId} not found on this client.");
            return;
        }

        IDCardInteraction interaction = cardNetObj.GetComponent<IDCardInteraction>();
        if (interaction == null) return;

        // npcIdentity is serialized on the prefab — available on all clients without network transfer.
        // idCardSlot is injected by NPCSpawnManager.AssignSceneReferences() which only runs on the
        // server. On clients, fall back to finding the scene IDCardSlot directly.
        IDCardSlot slot = idCardSlot;
        if (slot == null)
            slot = FindFirstObjectByType<IDCardSlot>();
        Transform focusTarget = slot != null ? slot.FocusCameraTarget : null;
        interaction.Initialize(npcIdentity, focusTarget);
        DebugLog($"[NPC] ID card initialized on client for '{npcIdentity?.fullName}'.");
    }

    /// <summary>
    /// Removes the ID card from the counter and clears the computer NPC info on all clients.
    /// Called when the NPC is destroyed (exits the store).
    /// </summary>
    public void CleanupIDCard()
    {
        if (!_hasPlacedIDCard) return;

        if (idCardSlot != null)
            idCardSlot.RemoveIDCard();

        // Clear NPCInfoDisplay on all clients (not just the server).
        ClearIDCardClientRpc();

        _hasPlacedIDCard = false;
        DebugLog("[NPC] ID card cleaned up.");
    }

    /// <summary>
    /// Clears the NPC info panel on every client's computer screen.
    /// </summary>
    [ClientRpc]
    private void ClearIDCardClientRpc()
    {
        if (NPCInfoDisplay.Instance != null)
            NPCInfoDisplay.Instance.ClearNPCInfo();
    }

    private void OnDestroy()
    {
        CleanupIDCard();
    }

    /// <summary>
    /// Returns whether checkout has been triggered.
    /// </summary>
    public bool HasCheckedOut()
    {
        return _hasCheckedOut;
    }

    /// <summary>
    /// Returns the current state of the NPC.
    /// On clients, reads from the synced NetworkVariable so NPCDialogueTrigger
    /// can check state correctly without the server-only state machine running locally.
    /// </summary>
    public string GetCurrentState()
    {
        if (IsSpawned && !IsServer)
            return ((NPCState)_networkState.Value).ToString();
        return _currentStateInternal.ToString();
    }

    // ── Network helpers ──────────────────────────────────────────────

    /// <summary>
    /// Fires OnNPCExited then despawns via NGO (all clients) or destroys locally (non-networked).
    /// Always call this instead of Destroy(gameObject) so clients stay in sync.
    /// </summary>
    private void DespawnOrDestroy()
    {
        OnNPCExited?.Invoke(this);
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            netObj.Despawn(true);   // Despawn + destroy on all clients
        else
            Destroy(gameObject);
    }

    /// <summary>Fires on all clients (including host) to trigger the pickup animation.</summary>
    [ClientRpc]
    private void TriggerPickupAnimationClientRpc()
    {
        GetComponent<NPCAnimationController>()?.TriggerPickup();
    }

    /// <summary>Fires on all clients (including host) to trigger the place animation.</summary>
    [ClientRpc]
    private void TriggerPlaceAnimationClientRpc()
    {
        GetComponent<NPCAnimationController>()?.TriggerPlace();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        // Draw detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Draw reach distance
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, reachDistance);

        // Draw line to current target
        if (_currentTargetObject != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, _currentTargetObject.transform.position);
        }

        // Draw lines to counter slots
        foreach (CounterSlot slot in counterSlots)
        {
            if (slot != null)
            {
                Gizmos.color = slot.IsOccupied ? Color.red : Color.cyan;
                Gizmos.DrawLine(transform.position, slot.Position);
                Gizmos.DrawWireCube(slot.Position, Vector3.one * 0.3f);
            }
        }

        // Draw line to exit
        if (exitPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, exitPoint.position);
            Gizmos.DrawWireSphere(exitPoint.position, 0.5f);
        }
    }
}
