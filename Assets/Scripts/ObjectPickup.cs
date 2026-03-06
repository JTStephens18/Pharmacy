using Unity.Netcode;
using UnityEngine;

public class ObjectPickup : NetworkBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private float pickupSmoothSpeed = 10f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Throw Settings")]
    [SerializeField] private KeyCode throwKey = KeyCode.F;
    [SerializeField] private float throwForce = 15f;

    [Header("Place Settings")]
    [SerializeField] private KeyCode placeKey = KeyCode.G;

    [Header("Default Hold Position (bottom-right of view)")]
    [Tooltip("Default local position offset when held (used if object has no HoldableItem component).")]
    [SerializeField] private Vector3 defaultHoldOffset = new Vector3(0.3f, -0.3f, 0.6f);
    [Tooltip("Default local rotation (euler angles) when held.")]
    [SerializeField] private Vector3 defaultHoldRotation = new Vector3(10f, -15f, 0f);

    [Header("References")]
    [SerializeField] private LayerMask pickupLayerMask = ~0; // Default to all layers

    [Header("Debug")]
    [SerializeField] private bool showDebugRay = true;

    private Camera _playerCamera;
    private PlayerComponents _playerComponents;
    private GameObject _heldObject;
    private Rigidbody _heldRigidbody;
    private Collider _heldCollider;
    private IPlaceable _currentPlaceable;
    private bool _isHoldingInventoryBox = false;
    private DeliveryStation _currentDeliveryStation;
    private InteractableItem _currentCounterItem;
    private CounterSlot _currentCounterItemSlot;
    private Vector3 _heldObjectOriginalScale = Vector3.one;
    private PillCountingStation _currentSortingStation;
    private ComputerScreen _currentComputerScreen;
    private CashRegister _currentCashRegister;
    private IDCardInteraction _currentIDCard;

    // ── Networked hold state ────────────────────────────────────────
    // When the held object is a NetworkObject we can't SetParent to the camera
    // (camera is not a NetworkObject). Instead we store the hold offsets and
    // manually update world-space position every frame; ClientNetworkTransform
    // on the object then syncs that position to all other clients.
    private NetworkObject _heldNetworkObject;
    private Vector3 _networkHoldOffset;
    private Vector3 _networkHoldRotation;

    void Start()
    {
        _playerComponents = GetComponentInParent<PlayerComponents>();

        _playerCamera = GetComponent<Camera>();
        if (_playerCamera == null && _playerComponents != null)
            _playerCamera = _playerComponents.PlayerCamera;
    }

    void Update()
    {
        if (!IsOwner) return;
        // Always detect placeable targets for highlight (some slots show even without held item)
        DetectPlaceable();
        DetectDeliveryStation();
        DetectCounterItem();
        DetectSortingStation();
        DetectComputerScreen();
        DetectCashRegister();
        DetectIDCard();

        if (Input.GetKeyDown(interactKey))
        {
            if (_heldObject == null)
            {
                // Check for computer screen (interactable monitor)
                if (_currentComputerScreen != null && !_currentComputerScreen.IsActive)
                {
                    _currentComputerScreen.Activate();
                }
                // Check for sorting station (pill counting mini-game)
                else if (_currentSortingStation != null && !_currentSortingStation.IsActive)
                {
                    _currentSortingStation.Activate();
                }
                // Check for ID card on counter (focus + barcode scan)
                else if (_currentIDCard != null && !_currentIDCard.IsActive)
                {
                    _currentIDCard.Activate();
                }
                // Check for counter item (delete on E press)
                // Slot may be null for networked items (found server-side during deletion)
                else if (_currentCounterItem != null)
                {
                    Debug.Log($"[ObjectPickup] Bagging counter item: {_currentCounterItem.gameObject.name}");
                    DeleteCounterItem();
                }
                // Check for cash register (checkout)
                else if (_currentCashRegister != null)
                {
                    _currentCashRegister.ProcessCheckout();
                }
                // Check for delivery station
                else if (_currentDeliveryStation != null)
                {
                    _currentDeliveryStation.SpawnBox();
                }
                else
                {
                    // Debug: log what the raycast is hitting when nothing is detected
                    Ray debugRay = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
                    if (Physics.Raycast(debugRay, out RaycastHit debugHit, pickupRange, pickupLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        InteractableItem debugItem = debugHit.collider.GetComponent<InteractableItem>()
                                                  ?? debugHit.collider.GetComponentInParent<InteractableItem>();
                        if (debugItem != null)
                        {
                            NetworkObject debugNetObj = debugItem.GetComponent<NetworkObject>();
                            Debug.Log($"[ObjectPickup] E pressed but no counter item detected. Hit InteractableItem '{debugItem.gameObject.name}' " +
                                      $"(NetworkObject={debugNetObj != null}, IsDelivered={debugItem.IsDelivered}, " +
                                      $"active={debugItem.gameObject.activeSelf}, collider={debugHit.collider.enabled}, " +
                                      $"pos={debugItem.transform.position})");
                        }
                    }
                    TryPickup();
                }
            }
            // Check if we're in box-to-shelf placement mode
            else if (_playerComponents != null && _playerComponents.PlacementManager != null && _playerComponents.PlacementManager.IsPlacementReady())
            {
                // Place from inventory box onto shelf
                _playerComponents.PlacementManager.TryPlaceFromBox();
            }
            // Place on shelf instead of dropping
            // BUT: Don't place the inventory box itself on a shelf (that's accidental)
            else if (_currentPlaceable != null && _currentPlaceable.CanPlaceItem(_heldObject) && !_isHoldingInventoryBox)
            {
                PlaceOnShelf();
            }
            else if (_currentPlaceable != null && !_currentPlaceable.CanPlaceItem(_heldObject))
            {
                // Trying to place on a slot that rejects the item - shake feedback
                if (_playerComponents != null && _playerComponents.Look != null)
                {
                    _playerComponents.Look.Shake();
                }
                Debug.Log("[ObjectPickup] Cannot place item here - wrong category or slot full");
            }
            else
            {
                DropObject();
            }
        }

        // Throw held object
        if (Input.GetKeyDown(throwKey) && _heldObject != null)
        {
            ThrowObject();
        }

        // Place held object gently
        if (Input.GetKeyDown(placeKey) && _heldObject != null)
        {
            PlaceObject();
        }

        // Show debug ray in Scene view
        if (showDebugRay && _playerCamera != null)
        {
            Debug.DrawRay(_playerCamera.transform.position, _playerCamera.transform.forward * pickupRange, Color.yellow);
        }

        // Manually position networked held objects in world space each frame.
        // ClientNetworkTransform on the object picks up these world-space changes
        // and replicates them to all other clients automatically.
        if (_heldNetworkObject != null && _playerCamera != null)
        {
            _heldObject.transform.position = _playerCamera.transform.TransformPoint(_networkHoldOffset);
            _heldObject.transform.rotation = _playerCamera.transform.rotation * Quaternion.Euler(_networkHoldRotation);
        }
    }

    private void TryPickup()
    {
        Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayerMask))
        {
            // Check if the object has a Rigidbody (is pickupable)
            Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = hit.collider.GetComponentInParent<Rigidbody>();
            }

            if (rb != null && !rb.isKinematic)
            {
                // If the object is a NetworkObject, route through server so all clients agree
                NetworkObject netObj = hit.collider.GetComponentInParent<NetworkObject>()
                                   ?? hit.collider.GetComponent<NetworkObject>();
                if (netObj != null)
                    RequestPickupServerRpc(netObj.NetworkObjectId);
                else
                    PickupObject(hit.collider.gameObject, rb, hit.collider);
            }
        }
    }

    // ── Networked pickup ────────────────────────────────────────────

    /// <summary>
    /// Client asks server to give them ownership of a NetworkObject so they can pick it up.
    /// Server validates the object isn't already held (owned by another client), then
    /// transfers ownership and notifies all clients.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong networkObjectId, ServerRpcParams serverRpcParams = default)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            Debug.LogWarning($"[ObjectPickup] RequestPickupServerRpc: NetworkObject {networkObjectId} not found.");
            return;
        }

        // Only allow pickup if the server still owns it (not already held by another client)
        if (netObj.OwnerClientId != NetworkManager.ServerClientId)
        {
            Debug.Log($"[ObjectPickup] RequestPickupServerRpc: Object already held by client {netObj.OwnerClientId}.");
            return;
        }

        ulong senderClientId = serverRpcParams.Receive.SenderClientId;

        // If the item was sitting in a shelf slot, remove it and sync the count to all clients
        if (ShelfSlotNetwork.TryFindSlotContaining(netObj.gameObject, out ShelfSlotNetwork sectionNet, out int slotIdx))
        {
            sectionNet.GetSlotAt(slotIdx).RemoveSpecificItem(netObj.gameObject);
            sectionNet.RecordPickup(slotIdx, sectionNet.GetSlotAt(slotIdx).CurrentItemCount);
        }

        netObj.ChangeOwnership(senderClientId);
        ConfirmPickupClientRpc(networkObjectId, senderClientId);
    }

    /// <summary>
    /// Sent to all clients after server confirms pickup.
    /// Only the picker sets up local hold state; all other clients receive
    /// position updates automatically via ClientNetworkTransform.
    /// </summary>
    [ClientRpc]
    private void ConfirmPickupClientRpc(ulong networkObjectId, ulong holderClientId)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj)) return;

        // Only the picker does local hold setup
        if (holderClientId != NetworkManager.Singleton.LocalClientId) return;

        Rigidbody rb = netObj.GetComponent<Rigidbody>();
        Collider col = netObj.GetComponentInChildren<Collider>();
        if (rb == null) return;

        _heldNetworkObject = netObj;
        DoNetworkPickup(netObj.gameObject, rb, col);
    }

    /// <summary>
    /// Sets up local hold state for a NetworkObject without parenting it to the camera.
    /// Position is driven manually each frame in Update() instead.
    /// </summary>
    private void DoNetworkPickup(GameObject obj, Rigidbody rb, Collider col)
    {
        _heldObject = obj;
        _heldRigidbody = rb;
        _heldCollider = col;
        _heldObjectOriginalScale = obj.transform.lossyScale;
        _isHoldingInventoryBox = obj.GetComponent<InventoryBox>() != null;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;
        if (col != null) col.enabled = false;

        HoldableItem holdable = obj.GetComponent<HoldableItem>();
        _networkHoldOffset   = (holdable != null && holdable.useCustomHoldSettings) ? holdable.holdOffset   : defaultHoldOffset;
        _networkHoldRotation = (holdable != null && holdable.useCustomHoldSettings) ? holdable.holdRotation : defaultHoldRotation;
    }

    /// <summary>
    /// Releases a held NetworkObject: re-enables physics locally, then tells the server
    /// to take ownership back and apply the release velocity.
    /// </summary>
    private void ReleaseHeldNetworkObject(Vector3 velocity)
    {
        if (_heldCollider != null) _heldCollider.enabled = true;
        _heldRigidbody.isKinematic = false;
        _heldRigidbody.useGravity = true;
        _heldRigidbody.freezeRotation = false;
        _heldRigidbody.interpolation = RigidbodyInterpolation.None;

        ReleaseNetworkObjectServerRpc(_heldNetworkObject.NetworkObjectId, velocity);

        _heldNetworkObject = null;
        _heldObject = null;
        _heldRigidbody = null;
        _heldCollider = null;
        _isHoldingInventoryBox = false;
    }

    /// <summary>
    /// Server takes ownership back from the client and applies the release velocity
    /// so physics simulation is authoritative on the host.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void ReleaseNetworkObjectServerRpc(ulong networkObjectId, Vector3 velocity)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj)) return;
        netObj.ChangeOwnership(NetworkManager.ServerClientId);
        if (netObj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = velocity;
        }
    }

    // ── Local pickup (non-NetworkObject) ────────────────────────────

    private void PickupObject(GameObject obj, Rigidbody rb, Collider col)
    {
        // CRITICAL FIX: Check if this item belongs to a ShelfSlot and notify it
        ShelfSlot slot = obj.GetComponentInParent<ShelfSlot>();
        if (slot != null)
        {
            slot.RemoveSpecificItem(obj);
        }

        _heldObject = obj;
        _heldRigidbody = rb;
        _heldCollider = col;
        _heldObjectOriginalScale = obj.transform.lossyScale;

        // Track if this is an inventory box (used by ItemPlacementManager)
        _isHoldingInventoryBox = obj.GetComponent<InventoryBox>() != null;

        // --- Unified hold: all objects are parented to camera at a fixed position ---

        // Clear velocity first, then make kinematic
        _heldRigidbody.linearVelocity = Vector3.zero;
        _heldRigidbody.angularVelocity = Vector3.zero;
        _heldRigidbody.useGravity = false;
        _heldRigidbody.isKinematic = true;

        // Disable collider to prevent blocking view / physics issues
        if (_heldCollider != null)
            _heldCollider.enabled = false;

        // Determine hold offset & rotation (per-object override or defaults)
        Vector3 holdOffset = defaultHoldOffset;
        Vector3 holdRotation = defaultHoldRotation;

        HoldableItem holdable = obj.GetComponent<HoldableItem>();
        if (holdable != null && holdable.useCustomHoldSettings)
        {
            holdOffset = holdable.holdOffset;
            holdRotation = holdable.holdRotation;
        }

        // Parent to camera and set fixed position
        _heldObject.transform.SetParent(_playerCamera.transform);
        _heldObject.transform.localPosition = holdOffset;
        _heldObject.transform.localRotation = Quaternion.Euler(holdRotation);

        // Compensate for parent scale to keep object looking its original size
        Vector3 parentScale = _playerCamera.transform.lossyScale;
        _heldObject.transform.localScale = new Vector3(
            _heldObjectOriginalScale.x / parentScale.x,
            _heldObjectOriginalScale.y / parentScale.y,
            _heldObjectOriginalScale.z / parentScale.z
        );
    }

    private void ReleaseHeldObject()
    {
        // Shared release logic for drop/throw/place
        _heldObject.transform.SetParent(null);
        _heldObject.transform.localScale = _heldObjectOriginalScale;

        if (_heldCollider != null)
            _heldCollider.enabled = true;

        _heldRigidbody.isKinematic = false;
        _heldRigidbody.useGravity = true;
        _heldRigidbody.freezeRotation = false;
        _heldRigidbody.interpolation = RigidbodyInterpolation.None;
    }

    private void DropObject()
    {
        if (_heldNetworkObject != null) { ReleaseHeldNetworkObject(_playerCamera.transform.forward * 2f); return; }

        if (_heldRigidbody != null)
        {
            ReleaseHeldObject();

            // Give it a slight forward velocity when dropping
            _heldRigidbody.linearVelocity = _playerCamera.transform.forward * 2f;
        }

        _heldObject = null;
        _heldRigidbody = null;
        _heldCollider = null;
        _isHoldingInventoryBox = false;
    }

    private void ThrowObject()
    {
        if (_heldNetworkObject != null) { ReleaseHeldNetworkObject(_playerCamera.transform.forward * throwForce); return; }

        if (_heldRigidbody != null)
        {
            ReleaseHeldObject();

            // Apply throw force in camera direction
            _heldRigidbody.linearVelocity = _playerCamera.transform.forward * throwForce;
        }

        _heldObject = null;
        _heldRigidbody = null;
        _heldCollider = null;
        _isHoldingInventoryBox = false;
    }

    private void PlaceObject()
    {
        if (_heldNetworkObject != null) { ReleaseHeldNetworkObject(Vector3.zero); return; }

        if (_heldRigidbody != null)
        {
            ReleaseHeldObject();

            // Set velocity to zero for a gentle placement
            _heldRigidbody.linearVelocity = Vector3.zero;
            _heldRigidbody.angularVelocity = Vector3.zero;
        }

        _heldObject = null;
        _heldRigidbody = null;
        _heldCollider = null;
        _isHoldingInventoryBox = false;
    }

    private void DetectPlaceable()
    {
        Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);

        IPlaceable newPlaceable = null;

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayerMask))
        {
            newPlaceable = hit.collider.GetComponent<IPlaceable>();
            if (newPlaceable == null)
                newPlaceable = hit.collider.GetComponentInParent<IPlaceable>();
        }

        // Handle highlight changes
        if (newPlaceable != _currentPlaceable)
        {
            // Hide highlight on previous slot
            if (_currentPlaceable is ShelfSlot previousSlot)
            {
                previousSlot.HideHighlight();
            }

            // Show highlight on new slot (respecting RequireHeldItem setting)
            if (newPlaceable is ShelfSlot newSlot)
            {
                bool shouldShowHighlight = !newSlot.RequireHeldItem || _heldObject != null;
                if (shouldShowHighlight)
                {
                    newSlot.ShowHighlight();
                }
            }
        }

        _currentPlaceable = newPlaceable;
    }

    private void DetectDeliveryStation()
    {
        Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);

        DeliveryStation newStation = null;

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayerMask))
        {
            newStation = hit.collider.GetComponent<DeliveryStation>();
            if (newStation == null)
                newStation = hit.collider.GetComponentInParent<DeliveryStation>();
        }

        // Handle highlight changes
        if (newStation != _currentDeliveryStation)
        {
            // Hide highlight on previous station
            if (_currentDeliveryStation != null)
            {
                _currentDeliveryStation.HideHighlight();
            }

            // Show highlight on new station
            if (newStation != null)
            {
                newStation.ShowHighlight();
            }
        }

        _currentDeliveryStation = newStation;
    }

    // Debug throttle for DetectCounterItem logging
    private float _counterItemDebugTimer;

    private void DetectCounterItem()
    {
        // Only detect counter items when not holding anything
        if (_heldObject != null)
        {
            ClearCounterItemHighlight();
            return;
        }

        Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
        InteractableItem newItem = null;
        CounterSlot newSlot = null;

        bool shouldLog = false;
        _counterItemDebugTimer += Time.deltaTime;
        if (_counterItemDebugTimer >= 1f)
        {
            _counterItemDebugTimer = 0f;
            shouldLog = true;
        }

        // Ignore triggers (CounterSlot itself) so we can raycast through it to hit specific items
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayerMask, QueryTriggerInteraction.Ignore))
        {
            // Check if we hit an interactable item directly
            InteractableItem item = hit.collider.GetComponent<InteractableItem>();
            if (item == null)
                item = hit.collider.GetComponentInParent<InteractableItem>();

            if (item != null)
            {
                // Legacy path: item is parented to a CounterSlot (non-networked)
                CounterSlot parentSlot = item.transform.parent?.GetComponent<CounterSlot>();
                if (parentSlot != null)
                {
                    newItem = item;
                    newSlot = parentSlot;
                }
                else
                {
                    // Networked path: item is world-space (NGO forbids parenting to non-NetworkObjects).
                    // Accept any NetworkObject InteractableItem — the server validates via
                    // GetSlotContaining in DeleteCounterItemServerRpc before despawning.
                    NetworkObject netObj = item.GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        newItem = item;
                        newSlot = CounterSlot.FindContainingSlot(item.transform.position);

                        if (shouldLog)
                            Debug.Log($"[DetectCounterItem] Found networked counter item: '{item.gameObject.name}' netId={netObj.NetworkObjectId} pos={item.transform.position} rb.isKinematic={item.GetComponent<Rigidbody>()?.isKinematic}");
                    }
                    else if (shouldLog)
                    {
                        Debug.Log($"[DetectCounterItem] Hit InteractableItem '{item.gameObject.name}' but no NetworkObject — skipped");
                    }
                }
            }
            else if (shouldLog)
            {
                Debug.Log($"[DetectCounterItem] Raycast hit '{hit.collider.gameObject.name}' — no InteractableItem component");
            }
        }

        // Handle highlight changes
        if (newItem != _currentCounterItem)
        {
            ClearCounterItemHighlight();
        }

        _currentCounterItem = newItem;
        _currentCounterItemSlot = newSlot;
    }

    private void ClearCounterItemHighlight()
    {
        // Clear any visual highlight on current item if needed
        _currentCounterItem = null;
        _currentCounterItemSlot = null;
    }

    private void DeleteCounterItem()
    {
        if (_currentCounterItem == null) return;

        NetworkObject netObj = _currentCounterItem.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            Debug.Log($"[ObjectPickup] Client sending DeleteCounterItemServerRpc for netId={netObj.NetworkObjectId}");
            // Networked path: server finds the slot, notifies clients, and despawns the item
            DeleteCounterItemServerRpc(netObj.NetworkObjectId);
        }
        else if (_currentCounterItemSlot != null)
        {
            // Local fallback (non-networked items)
            Debug.Log($"[ObjectPickup] Deleting counter item: {_currentCounterItem.gameObject.name}");
            GameObject itemObj = _currentCounterItem.gameObject;
            _currentCounterItemSlot.RemoveItem(itemObj);
            Destroy(itemObj);
        }

        _currentCounterItem = null;
        _currentCounterItemSlot = null;
    }

    /// <summary>
    /// Server finds the counter slot that holds the item, broadcasts removal to clients,
    /// then despawns (destroys) the NetworkObject on all clients.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void DeleteCounterItemServerRpc(ulong itemNetworkObjectId)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkObjectId, out NetworkObject netObj))
            return;

        // Clear the slot reference on the server
        CounterSlot slot = CounterSlot.GetSlotContaining(netObj.gameObject);
        if (slot == null)
        {
            Debug.LogWarning($"[ObjectPickup] DeleteCounterItemServerRpc: item {itemNetworkObjectId} not found in any CounterSlot — skipping despawn.");
            return;
        }
        slot.RemoveItem(netObj.gameObject);

        // Tell all clients to unregister this item from the counter registry
        CounterSlotNetwork slotNetwork = slot.GetComponent<CounterSlotNetwork>();
        if (slotNetwork != null)
            slotNetwork.RecordRemoval(itemNetworkObjectId);

        // Despawn and destroy on all clients
        Debug.Log($"[ObjectPickup] Server despawning counter item {itemNetworkObjectId}.");
        netObj.Despawn(true);
    }


    private void DetectSortingStation()
    {
        Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);

        PillCountingStation newStation = null;

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayerMask))
        {
            newStation = hit.collider.GetComponent<PillCountingStation>();
            if (newStation == null)
                newStation = hit.collider.GetComponentInParent<PillCountingStation>();
        }

        _currentSortingStation = newStation;
    }

    private void DetectComputerScreen()
    {
        Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);

        ComputerScreen newScreen = null;

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayerMask))
        {
            newScreen = hit.collider.GetComponent<ComputerScreen>();
            if (newScreen == null)
                newScreen = hit.collider.GetComponentInParent<ComputerScreen>();
        }

        _currentComputerScreen = newScreen;
    }

    private void DetectCashRegister()
    {
        Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);

        CashRegister newRegister = null;

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayerMask))
        {
            newRegister = hit.collider.GetComponent<CashRegister>();
            if (newRegister == null)
                newRegister = hit.collider.GetComponentInParent<CashRegister>();
        }

        _currentCashRegister = newRegister;
    }

    private void DetectIDCard()
    {
        Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);

        IDCardInteraction newIDCard = null;

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayerMask))
        {
            newIDCard = hit.collider.GetComponent<IDCardInteraction>();
            if (newIDCard == null)
                newIDCard = hit.collider.GetComponentInParent<IDCardInteraction>();
        }

        _currentIDCard = newIDCard;
    }


    private void PlaceOnShelf()
    {
        if (_currentPlaceable == null || _heldObject == null) return;

        // Prepare the object for placement
        if (_heldRigidbody != null)
        {
            _heldRigidbody.linearVelocity = Vector3.zero;
            _heldRigidbody.angularVelocity = Vector3.zero;
        }

        // Unparent before placing so the shelf can re-parent
        _heldObject.transform.SetParent(null);
        _heldObject.transform.localScale = _heldObjectOriginalScale;

        if (_heldCollider != null)
            _heldCollider.enabled = true;

        _heldRigidbody.isKinematic = false;

        // Place the item
        if (_currentPlaceable.TryPlaceItem(_heldObject))
        {
            // Clear held references
            _heldObject = null;
            _heldRigidbody = null;
            _heldCollider = null;
            _isHoldingInventoryBox = false;
            _currentPlaceable = null;
        }
        else
        {
            // Placement failed — re-pickup the object
            PickupObject(_heldObject, _heldRigidbody, _heldCollider);
        }
    }

    // Public method to check if currently looking at a placeable
    public bool IsLookingAtPlaceable()
    {
        return _currentPlaceable != null;
    }

    // Public method to get the current placeable's prompt
    public string GetPlaceablePrompt()
    {
        return _currentPlaceable?.GetPlacementPrompt() ?? string.Empty;
    }

    // Public method to check if currently holding an object
    public bool IsHoldingObject()
    {
        return _heldObject != null;
    }

    // Public method to get the held object
    public GameObject GetHeldObject()
    {
        return _heldObject;
    }

    // Force drop (can be called externally if needed, e.g. by InventoryBox.ShrinkAndDestroy)
    public void ForceDropObject()
    {
        if (_heldNetworkObject != null) { ReleaseHeldNetworkObject(Vector3.zero); return; }
        if (_heldObject != null) DropObject();
    }

    // Check if currently holding an InventoryBox
    public bool IsHoldingInventoryBox()
    {
        return _heldObject != null && _heldObject.GetComponent<InventoryBox>() != null;
    }
}
