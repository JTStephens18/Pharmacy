using System.Collections;
using UnityEngine;

/// <summary>
/// Manages spawning and animating preview item instances inside the inventory box.
/// Attach to the same GameObject as InventoryBox.
/// </summary>
public class BoxItemPreview : MonoBehaviour
{
    [Header("Slot Anchors")]
    [Tooltip("Anchor transform for the current (front-of-queue) item.")]
    [SerializeField] private Transform itemSlot1;

    [Tooltip("Anchor transform for the next (second-in-queue) item.")]
    [SerializeField] private Transform itemSlot2;

    [Header("Animation")]
    [Tooltip("Duration of the next→current slide animation.")]
    [SerializeField] private float swapAnimDuration = 0.25f;

    // Cached preview instances
    private GameObject _currentInstance;
    private GameObject _nextInstance;

    // Cached categories to avoid redundant spawns
    private ItemCategory _currentCategory;
    private ItemCategory _nextCategory;

    // Animation state
    private Coroutine _swapCoroutine;
    private bool _isSwapping = false;

    /// <summary>
    /// Updates the preview items to match the given queue front two categories.
    /// Only spawns/destroys when a category actually changes.
    /// </summary>
    public void UpdatePreview(ItemCategory current, ItemCategory next)
    {
        // Update slot 1 (current item) — skip if a swap is animating into this slot
        if (current != _currentCategory && !_isSwapping)
        {
            if (_currentInstance != null)
                Destroy(_currentInstance);

            _currentCategory = current;
            _currentInstance = (current != null) ? SpawnPreviewItem(current, itemSlot1) : null;
        }

        // Update slot 2 (next item) — skip if a swap is in progress
        // (the coroutine will spawn the new next item when it completes)
        if (!_isSwapping && next != _nextCategory)
        {
            if (_nextInstance != null)
                Destroy(_nextInstance);

            _nextCategory = next;
            _nextInstance = (next != null) ? SpawnPreviewItem(next, itemSlot2) : null;
        }
    }

    /// <summary>
    /// Triggers the swap animation: destroys current, slides next → slot 1,
    /// then signals for a new next item via UpdatePreview.
    /// Call this BEFORE rebuilding the queue so the animation uses the old next instance.
    /// </summary>
    public void AnimateSlotSwap()
    {
        // Cancel any in-progress swap
        if (_swapCoroutine != null)
        {
            StopCoroutine(_swapCoroutine);
            _swapCoroutine = null;
            _isSwapping = false;
        }

        // Destroy the current item (it was just placed on the shelf)
        if (_currentInstance != null)
        {
            Destroy(_currentInstance);
            _currentInstance = null;
            _currentCategory = null;
        }

        // If there's a next item, animate it into slot 1
        if (_nextInstance != null)
        {
            _isSwapping = true;
            _swapCoroutine = StartCoroutine(SlotSwapCoroutine());
        }
    }

    /// <summary>
    /// Destroys all preview instances and resets cached state.
    /// </summary>
    public void ClearPreviews()
    {
        if (_swapCoroutine != null)
        {
            StopCoroutine(_swapCoroutine);
            _swapCoroutine = null;
            _isSwapping = false;
        }

        if (_currentInstance != null)
        {
            Destroy(_currentInstance);
            _currentInstance = null;
        }

        if (_nextInstance != null)
        {
            Destroy(_nextInstance);
            _nextInstance = null;
        }

        _currentCategory = null;
        _nextCategory = null;
    }

    /// <summary>
    /// Instantiates a preview item at the given slot transform.
    /// Disables physics and colliders so it's purely visual.
    /// </summary>
    private GameObject SpawnPreviewItem(ItemCategory category, Transform slot)
    {
        if (category == null || category.prefab == null || slot == null)
            return null;

        GameObject instance = Instantiate(category.prefab, slot);

        // Reset local transform so the slot anchor controls positioning
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        // Disable physics
        Rigidbody rb = instance.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        // Disable all colliders
        Collider[] colliders = instance.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
            col.enabled = false;

        return instance;
    }

    /// <summary>
    /// Coroutine that lerps the next instance from slot 2 to slot 1.
    /// Once complete, the next instance becomes the current instance.
    /// </summary>
    private IEnumerator SlotSwapCoroutine()
    {
        if (_nextInstance == null || itemSlot1 == null)
        {
            _isSwapping = false;
            yield break;
        }

        Transform nextTransform = _nextInstance.transform;

        Vector3 startPos = nextTransform.position;
        Quaternion startRot = nextTransform.rotation;

        float elapsed = 0f;
        while (elapsed < swapAnimDuration)
        {
            // Safety check — instance may have been destroyed externally
            if (_nextInstance == null)
            {
                _isSwapping = false;
                _swapCoroutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / swapAnimDuration));

            nextTransform.position = Vector3.Lerp(startPos, itemSlot1.position, t);
            nextTransform.rotation = Quaternion.Slerp(startRot, itemSlot1.rotation, t);

            yield return null;
        }

        // Safety check before final snap
        if (_nextInstance == null)
        {
            _isSwapping = false;
            _swapCoroutine = null;
            yield break;
        }

        // Snap to slot 1 and re-parent
        nextTransform.SetParent(itemSlot1);
        nextTransform.localPosition = Vector3.zero;
        nextTransform.localRotation = Quaternion.identity;

        // Promote next → current
        _currentInstance = _nextInstance;
        _currentCategory = _nextCategory;
        _nextInstance = null;
        _nextCategory = null;

        _isSwapping = false;
        _swapCoroutine = null;
    }
}
