using UnityEngine;

/// <summary>
/// Drives continuous rotation of the hopper disk with speed randomization.
/// Tracks the spout arm angle and provides alignment window queries for DispensingController.
///
/// The hopper cycles through speeds on a timer, pulling from a configurable range.
/// Direction occasionally reverses. Speed transitions are smoothed.
/// </summary>
public class RotatingHopper : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("The Transform that physically rotates (the hopper disk).")]
    [SerializeField] private Transform hopperTransform;
    [Tooltip("Local axis the hopper rotates around.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Header("Speed Range (degrees/second)")]
    [SerializeField] private float minSpeed = 50f;
    [SerializeField] private float maxSpeed = 400f;

    [Header("Speed Randomization")]
    [SerializeField] private float minChangeInterval = 3f;
    [SerializeField] private float maxChangeInterval = 8f;
    [Tooltip("How long it takes to ramp to the new speed.")]
    [SerializeField] private float speedTransitionDuration = 1f;
    [Range(0f, 1f)]
    [Tooltip("Probability of reversing direction on each speed change.")]
    [SerializeField] private float reverseChance = 0.2f;

    [Header("Alignment Window")]
    [Tooltip("Center of the valid dispensing arc (degrees, 0 = +Z forward).")]
    [SerializeField] private float alignmentCenterAngle = 180f;
    [Tooltip("Half-width of the alignment arc in degrees.")]
    [SerializeField] private float alignmentHalfWidth = 15f;

    // ── Runtime State ───────────────────────────────────────────────
    private float _currentAngle;
    private float _currentSpeed;
    private float _targetSpeed;
    private float _direction = 1f;
    private float _speedChangeTimer;
    private float _nextChangeInterval;
    private bool _isActive;

    // ── Medication ──────────────────────────────────────────────────
    private MedicationData _loadedMedication;

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>Current spout angle in degrees (0–360).</summary>
    public float SpoutAngle => _currentAngle;

    /// <summary>Absolute rotation speed in degrees/second.</summary>
    public float CurrentSpeed => Mathf.Abs(_currentSpeed);

    /// <summary>True when a medication has been loaded into the hopper.</summary>
    public bool IsLoaded => _loadedMedication != null;

    /// <summary>The currently loaded medication data, or null.</summary>
    public MedicationData LoadedMedication => _loadedMedication;

    /// <summary>Center of the alignment arc in degrees.</summary>
    public float AlignmentCenter => alignmentCenterAngle;

    /// <summary>Half-width of the alignment arc in degrees.</summary>
    public float AlignmentHalfWidthDeg => alignmentHalfWidth;

    /// <summary>Load a medication into the hopper, replacing any current load.</summary>
    public void LoadMedication(MedicationData data)
    {
        _loadedMedication = data;
    }

    /// <summary>Clear the loaded medication without unloading visuals.</summary>
    public void ClearMedication()
    {
        _loadedMedication = null;
    }

    /// <summary>Start the hopper spinning (called when the station is activated).</summary>
    public void Activate()
    {
        _isActive = true;
        _currentAngle = 0f;
        PickNewSpeed(instant: true);
        ResetChangeTimer();
    }

    /// <summary>Stop the hopper (called when the station is deactivated).</summary>
    public void Deactivate()
    {
        _isActive = false;
    }

    /// <summary>
    /// Check whether the spout is currently within the alignment window.
    /// </summary>
    /// <param name="normalizedPosition">0 = dead center, 1 = edge of window.</param>
    /// <returns>True if the spout is inside the window.</returns>
    public bool IsSpoutInWindow(out float normalizedPosition)
    {
        float delta = Mathf.Abs(Mathf.DeltaAngle(_currentAngle, alignmentCenterAngle));
        if (delta <= alignmentHalfWidth)
        {
            normalizedPosition = delta / alignmentHalfWidth;
            return true;
        }
        normalizedPosition = 1f;
        return false;
    }

    // ── Update ──────────────────────────────────────────────────────

    void Update()
    {
        if (!_isActive || !IsLoaded) return;

        // Smooth speed transition
        float rampRate = (maxSpeed - minSpeed) / Mathf.Max(speedTransitionDuration, 0.01f);
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, _targetSpeed, rampRate * Time.deltaTime);

        // Rotate
        _currentAngle += _currentSpeed * _direction * Time.deltaTime;
        _currentAngle = ((_currentAngle % 360f) + 360f) % 360f;

        if (hopperTransform != null)
            hopperTransform.localRotation = Quaternion.AngleAxis(_currentAngle, rotationAxis);

        // Speed change timer
        _speedChangeTimer += Time.deltaTime;
        if (_speedChangeTimer >= _nextChangeInterval)
        {
            PickNewSpeed(instant: false);
            ResetChangeTimer();
        }
    }

    // ── Internals ───────────────────────────────────────────────────

    private void PickNewSpeed(bool instant)
    {
        _targetSpeed = Random.Range(minSpeed, maxSpeed);

        if (Random.value < reverseChance)
            _direction = -_direction;

        if (instant)
            _currentSpeed = _targetSpeed;
    }

    private void ResetChangeTimer()
    {
        _speedChangeTimer = 0f;
        _nextChangeInterval = Random.Range(minChangeInterval, maxChangeInterval);
    }
}
