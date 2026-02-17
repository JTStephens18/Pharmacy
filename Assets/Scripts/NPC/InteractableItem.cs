using UnityEngine;

/// <summary>
/// Component for shelf items that can be picked up by NPCs.
/// Requires a child GameObject named "GrabTarget" to define the hand placement point.
/// </summary>
[RequireComponent(typeof(Collider))]
public class InteractableItem : MonoBehaviour, IInteractable
{
    [Header("Grab Settings")]
    [Tooltip("The point where the NPC's hand will grab. If not assigned, searches for a child named 'GrabTarget'.")]
    [SerializeField] private Transform grabTarget;

    [Header("Category")]
    [Tooltip("The category of this item (e.g., Food, Drink, Cleaning). Used by NPC filtering.")]
    [SerializeField] private ItemCategory itemCategory;

    /// <summary>
    /// Returns the item's category for NPC filtering.
    /// </summary>
    public ItemCategory ItemCategory => itemCategory;

    /// <summary>
    /// Whether this item has been delivered to the counter and should not be picked up again.
    /// </summary>
    public bool IsDelivered { get; private set; } = false;

    /// <summary>
    /// Marks this item as delivered to the counter.
    /// </summary>
    public void MarkAsDelivered()
    {
        IsDelivered = true;
    }

    private Rigidbody _rigidbody;
    private Collider _collider;
    private Renderer[] _renderers;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _renderers = GetComponentsInChildren<Renderer>();

        // Auto-find GrabTarget if not assigned
        if (grabTarget == null)
        {
            Transform found = transform.Find("GrabTarget");
            if (found != null)
            {
                grabTarget = found;
            }
            else
            {
                Debug.LogWarning($"[InteractableItem] No 'GrabTarget' child found on {gameObject.name}. Using object center as grab point.");
            }
        }
    }

    /// <summary>
    /// Returns the world position of the grab target, or this object's position if none exists.
    /// </summary>
    public Vector3 GetInteractionPoint()
    {
        return grabTarget != null ? grabTarget.position : transform.position;
    }

    /// <summary>
    /// Called when the NPC picks up this item.
    /// Disables physics, hides the item, and parents it to the NPC's hand.
    /// </summary>
    public void OnPickedUp(Transform handTransform)
    {
        // Disable physics
        if (_rigidbody != null)
        {
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = true;
        }

        // Disable collider
        if (_collider != null)
        {
            _collider.enabled = false;
        }

        // Parent to hand and snap to position (do this before hiding)
        transform.SetParent(handTransform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Hide the item by deactivating it
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Places the item at the specified position and makes it visible again.
    /// </summary>
    public void PlaceAt(Vector3 position)
    {
        // Mark as delivered so NPC won't pick it up again
        IsDelivered = true;

        // Re-activate the item first
        gameObject.SetActive(true);

        // Unparent
        transform.SetParent(null);
        transform.position = position;

        // Re-enable collider
        if (_collider != null)
        {
            _collider.enabled = true;
        }

        // Keep physics disabled so item stays in place
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
        }
    }

    /// <summary>
    /// Releases the item from the NPC's hand, re-enabling physics.
    /// </summary>
    public void Release()
    {
        // Unparent
        transform.SetParent(null);

        // Show the item
        SetVisible(true);

        // Re-enable collider
        if (_collider != null)
        {
            _collider.enabled = true;
        }

        // Re-enable physics
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
        }
    }

    /// <summary>
    /// Shows or hides all renderers on this item.
    /// </summary>
    private void SetVisible(bool visible)
    {
        foreach (Renderer renderer in _renderers)
        {
            renderer.enabled = visible;
        }
    }
}
