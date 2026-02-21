using UnityEngine;

public class ObjectPickup : MonoBehaviour
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

    void Start()
    {
        _playerCamera = GetComponent<Camera>();
        if (_playerCamera == null)
        {
            _playerCamera = Camera.main;
        }
    }

    void Update()
    {
        // Always detect placeable targets for highlight (some slots show even without held item)
        DetectPlaceable();
        DetectDeliveryStation();
        DetectCounterItem();
        DetectSortingStation();
        DetectComputerScreen();

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
                // Check for counter item (delete on E press)
                else if (_currentCounterItem != null && _currentCounterItemSlot != null)
                {
                    DeleteCounterItem();
                }
                // Check for delivery station
                else if (_currentDeliveryStation != null)
                {
                    _currentDeliveryStation.SpawnBox();
                }
                else
                {
                    TryPickup();
                }
            }
            // Check if we're in box-to-shelf placement mode
            else if (ItemPlacementManager.Instance != null && ItemPlacementManager.Instance.IsPlacementReady())
            {
                // Place from inventory box onto shelf
                ItemPlacementManager.Instance.TryPlaceFromBox();
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
                if (MouseLook.Instance != null)
                {
                    MouseLook.Instance.Shake();
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
                PickupObject(hit.collider.gameObject, rb, hit.collider);
            }
        }
    }

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

        // Ignore triggers (CounterSlot itself) so we can raycast through it to hit specific items
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayerMask, QueryTriggerInteraction.Ignore))
        {
            // Check if we hit an interactable item directly
            InteractableItem item = hit.collider.GetComponent<InteractableItem>();
            if (item == null)
                item = hit.collider.GetComponentInParent<InteractableItem>();

            if (item != null)
            {
                // Check if this item is parented to a counter slot
                CounterSlot parentSlot = item.transform.parent?.GetComponent<CounterSlot>();
                if (parentSlot != null)
                {
                    newItem = item;
                    newSlot = parentSlot;
                }
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
        if (_currentCounterItem == null || _currentCounterItemSlot == null) return;

        Debug.Log($"[ObjectPickup] Deleting counter item: {_currentCounterItem.gameObject.name}");

        // Remove from slot
        GameObject itemObj = _currentCounterItem.gameObject;
        _currentCounterItemSlot.RemoveItem(itemObj);

        // Destroy the item
        Destroy(itemObj);

        // Clear references
        _currentCounterItem = null;
        _currentCounterItemSlot = null;
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

    // Force drop (can be called externally if needed)
    public void ForceDropObject()
    {
        if (_heldObject != null)
        {
            DropObject();
        }
    }

    // Check if currently holding an InventoryBox
    public bool IsHoldingInventoryBox()
    {
        return _heldObject != null && _heldObject.GetComponent<InventoryBox>() != null;
    }
}
