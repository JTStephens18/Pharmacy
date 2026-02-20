using System;
using UnityEngine;

/// <summary>
/// Trigger zone at the edge of the tray that counts pills as they fall through.
/// Fires events for UI updates and completion detection.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class PillCountingChute : MonoBehaviour
{
    [Header("Layer")]
    [Tooltip("Layer index for Physics_Debris (pills).")]
    [SerializeField] private int debrisLayerIndex = 9;

    [Header("Pill Handling")]
    [Tooltip("If true, pills are destroyed when they enter the chute. If false, they are disabled.")]
    [SerializeField] private bool destroyOnCount = false;

    /// <summary>
    /// Fired whenever a pill is counted. Args: (currentCount, targetCount).
    /// </summary>
    public event Action<int, int> OnPillCounted;

    /// <summary>
    /// Fired when the target count is reached.
    /// </summary>
    public event Action OnTargetReached;

    private int _currentCount;
    private int _targetCount;
    private bool _completed;

    void Awake()
    {
        // Ensure the collider is a trigger
        BoxCollider col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    /// <summary>
    /// Initializes the chute for a new mini-game session.
    /// </summary>
    public void Initialize(int targetCount)
    {
        _currentCount = 0;
        _targetCount = targetCount;
        _completed = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_completed) return;

        // Only count objects on the pill layer
        if (other.gameObject.layer != debrisLayerIndex) return;

        _currentCount++;

        if (destroyOnCount)
        {
            Destroy(other.gameObject);
        }
        else
        {
            other.gameObject.SetActive(false);
        }

        OnPillCounted?.Invoke(_currentCount, _targetCount);

        Debug.Log($"[PillCountingChute] Pill counted: {_currentCount}/{_targetCount}");

        if (_currentCount >= _targetCount && !_completed)
        {
            _completed = true;
            OnTargetReached?.Invoke();
            Debug.Log("[PillCountingChute] Target reached!");
        }
    }

    public int GetCurrentCount() => _currentCount;
    public int GetTargetCount() => _targetCount;
    public bool IsCompleted() => _completed;

    /// <summary>
    /// Resets the chute state.
    /// </summary>
    public void ResetChute()
    {
        _currentCount = 0;
        _completed = false;
    }
}
