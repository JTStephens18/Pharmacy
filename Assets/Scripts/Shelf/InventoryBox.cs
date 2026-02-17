using System.Collections;
using UnityEngine;

/// <summary>
/// Portable container that holds a fixed number of items for shelf restocking.
/// Items are category-agnostic — the ItemPlacementManager decides what to spawn.
/// Attach to a pickupable object (requires Rigidbody).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class InventoryBox : MonoBehaviour
{
    [Header("Inventory")]
    [Tooltip("Total number of items this box can dispense before being destroyed.")]
    [SerializeField] private int totalItems = 8;

    [Header("Box Visuals")]
    [Tooltip("Reference to the closed box child mesh.")]
    [SerializeField] private GameObject closedModel;

    [Tooltip("Reference to the open box child mesh.")]
    [SerializeField] private GameObject openModel;

    [Tooltip("Duration of the open/close scale animation.")]
    [SerializeField] private float openCloseDuration = 0.3f;

    [Header("Shrink Animation")]
    [Tooltip("Duration of the shrink animation when the box is emptied.")]
    [SerializeField] private float shrinkDuration = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool logOperations = false;

    private int _remainingItems;
    private bool _isDestroying = false;
    private bool _isOpen = false;
    private Coroutine _openCloseCoroutine;
    private Vector3 _closedModelOriginalScale;
    private Vector3 _openModelOriginalScale;

    /// <summary>
    /// Whether the box is currently in the open visual state.
    /// </summary>
    public bool IsOpen => _isOpen;

    private void Awake()
    {
        _remainingItems = totalItems;

        // Cache original scales before any animation modifies them
        if (closedModel != null) _closedModelOriginalScale = closedModel.transform.localScale;
        if (openModel != null) _openModelOriginalScale = openModel.transform.localScale;

        // Start with closed model visible, open model hidden
        if (closedModel != null) closedModel.SetActive(true);
        if (openModel != null) openModel.SetActive(false);
    }

    /// <summary>
    /// Transitions to the open visual state with a scale-up animation.
    /// </summary>
    public void OpenBox()
    {
        if (_isOpen || _isDestroying) return;
        _isOpen = true;

        StopAndResetOpenClose();
        _openCloseCoroutine = StartCoroutine(AnimateOpenClose(open: true));
    }

    /// <summary>
    /// Transitions to the closed visual state with a scale-up animation.
    /// </summary>
    public void CloseBox()
    {
        if (!_isOpen || _isDestroying) return;
        _isOpen = false;

        StopAndResetOpenClose();
        _openCloseCoroutine = StartCoroutine(AnimateOpenClose(open: false));
    }

    /// <summary>
    /// Stops any in-progress open/close animation and restores both models
    /// to their original scales so the next animation starts clean.
    /// </summary>
    private void StopAndResetOpenClose()
    {
        if (_openCloseCoroutine != null)
        {
            StopCoroutine(_openCloseCoroutine);
            _openCloseCoroutine = null;
        }

        // Reset scales so a half-finished animation doesn't corrupt future ones
        if (closedModel != null) closedModel.transform.localScale = _closedModelOriginalScale;
        if (openModel != null) openModel.transform.localScale = _openModelOriginalScale;
    }

    private IEnumerator AnimateOpenClose(bool open)
    {
        // The model being revealed scales from 0 → original
        // The model being hidden is instantly deactivated
        GameObject showModel = open ? openModel : closedModel;
        GameObject hideModel = open ? closedModel : openModel;
        Vector3 targetScale = open ? _openModelOriginalScale : _closedModelOriginalScale;

        if (hideModel != null)
            hideModel.SetActive(false);

        if (showModel != null)
        {
            showModel.SetActive(true);
            showModel.transform.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < openCloseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / openCloseDuration));
                showModel.transform.localScale = targetScale * t;
                yield return null;
            }

            showModel.transform.localScale = targetScale;
        }

        _openCloseCoroutine = null;
    }

    /// <summary>
    /// Returns true if the box still has items to dispense.
    /// </summary>
    public bool HasAnyStock()
    {
        return _remainingItems > 0 && !_isDestroying;
    }

    /// <summary>
    /// Gets the number of items remaining in the box.
    /// </summary>
    public int GetRemainingCount()
    {
        return _remainingItems;
    }

    /// <summary>
    /// Gets the total starting capacity of the box.
    /// </summary>
    public int GetTotalCapacity()
    {
        return totalItems;
    }

    /// <summary>
    /// Decrements the item count by one. If the box is now empty,
    /// triggers the shrink animation and destroys the box.
    /// </summary>
    public void Decrement()
    {
        if (_remainingItems <= 0 || _isDestroying) return;

        _remainingItems--;

        if (logOperations)
            Debug.Log($"[InventoryBox] Decremented. Remaining: {_remainingItems}/{totalItems}");

        if (_remainingItems <= 0)
        {
            StartCoroutine(ShrinkAndDestroy());
        }
    }

    /// <summary>
    /// Smoothly shrinks the box to zero scale, then destroys it.
    /// </summary>
    private IEnumerator ShrinkAndDestroy()
    {
        _isDestroying = true;

        if (logOperations)
            Debug.Log("[InventoryBox] Box is empty. Starting shrink animation...");

        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shrinkDuration);
            // SmoothStep gives a nice ease-in-out curve
            float smooth = Mathf.SmoothStep(1f, 0f, t);
            transform.localScale = startScale * smooth;
            yield return null;
        }

        transform.localScale = Vector3.zero;

        // Force drop if player is holding this box
        ObjectPickup pickup = FindFirstObjectByType<ObjectPickup>();
        if (pickup != null && pickup.GetHeldObject() == gameObject)
        {
            pickup.ForceDropObject();
        }

        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (totalItems < 1)
            totalItems = 1;
    }
#endif
}
