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
    [Tooltip("Fixed trigger collider that defines the dispensing window zone. " +
             "When assigned, this replaces the angle-based window check.")]
    [SerializeField] private Collider windowZone;
    [Tooltip("Child transform on the rotating disk at the spout tip. " +
             "If unassigned, the spout position is computed from hopperTransform + spoutRadius.")]
    [SerializeField] private Transform spoutTransform;
    [Tooltip("Fallback distance from hopper center to the spout tip (used when Spout Transform is unassigned).")]
    [SerializeField] private float spoutRadius = 0.5f;

    [Header("Alignment Window Fallback (used when no Window Zone collider is assigned)")]
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
    private bool _isLoaded;

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>Current spout angle in degrees (0–360).</summary>
    public float SpoutAngle => _currentAngle;

    /// <summary>Absolute rotation speed in degrees/second.</summary>
    public float CurrentSpeed => Mathf.Abs(_currentSpeed);

    /// <summary>Current rotation direction: +1 or -1.</summary>
    public float Direction => _direction;

    /// <summary>True when the hopper has been loaded (with or without explicit MedicationData).</summary>
    public bool IsLoaded => _isLoaded;

    /// <summary>The currently loaded medication data, or null if auto-loaded.</summary>
    public MedicationData LoadedMedication => _loadedMedication;

    /// <summary>Center of the alignment arc in degrees.</summary>
    public float AlignmentCenter => alignmentCenterAngle;

    /// <summary>Half-width of the alignment arc in degrees.</summary>
    public float AlignmentHalfWidthDeg => alignmentHalfWidth;

    /// <summary>The collider defining the dispensing window zone, or null if using angle fallback.</summary>
    public Collider WindowZone => windowZone;

    /// <summary>Load a specific medication into the hopper.</summary>
    public void LoadMedication(MedicationData data)
    {
        _loadedMedication = data;
        _isLoaded = true;
    }

    /// <summary>Mark the hopper as loaded without specifying medication data (auto-load from prescription).</summary>
    public void SetLoaded()
    {
        _isLoaded = true;
    }

    /// <summary>Clear the loaded state.</summary>
    public void ClearMedication()
    {
        _loadedMedication = null;
        _isLoaded = false;
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
    /// Uses the windowZone collider when assigned, otherwise falls back to angle math.
    /// </summary>
    /// <param name="normalizedPosition">0 = dead center, 1 = edge of window.</param>
    /// <returns>True if the spout is inside the window.</returns>
    public bool IsSpoutInWindow(out float normalizedPosition)
    {
        if (windowZone != null)
        {
            Vector3 spoutPos = GetSpoutWorldPosition();
            // ClosestPoint returns the point itself when spoutPos is inside the collider
            Vector3 closest = windowZone.ClosestPoint(spoutPos);
            if ((closest - spoutPos).sqrMagnitude < 0.0001f)
            {
                float maxDist = windowZone.bounds.extents.magnitude;
                float distToCenter = Vector3.Distance(spoutPos, windowZone.bounds.center);
                normalizedPosition = maxDist > 0f ? Mathf.Clamp01(distToCenter / maxDist) : 0f;
                return true;
            }
            normalizedPosition = 1f;
            return false;
        }

        // Fallback: angle-based check
        float delta = Mathf.Abs(Mathf.DeltaAngle(_currentAngle, alignmentCenterAngle));
        if (delta <= alignmentHalfWidth)
        {
            normalizedPosition = delta / alignmentHalfWidth;
            return true;
        }
        normalizedPosition = 1f;
        return false;
    }

    /// <summary>World-space position of the spout tip on the rotating disk.</summary>
    private Vector3 GetSpoutWorldPosition()
    {
        if (spoutTransform != null)
            return spoutTransform.position;

        // Spout sits at local +Z on the hopper disk; TransformPoint accounts for current rotation
        if (hopperTransform != null)
            return hopperTransform.TransformPoint(new Vector3(0f, 0f, spoutRadius));

        float rad = _currentAngle * Mathf.Deg2Rad;
        return transform.position + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * spoutRadius;
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

    // ── Gizmos ──────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (hopperTransform == null) return;

        Vector3 origin   = hopperTransform.position;
        Vector3 spoutPos = GetSpoutWorldPosition();
        Vector3 delta    = spoutPos - origin;
        float   len      = delta.magnitude;
        if (len < 0.001f) return;
        Vector3 dir = delta / len;

        // Green when spout is inside the window, white otherwise
        Gizmos.color = IsSpoutInWindow(out _) ? Color.green : Color.white;

        // Shaft
        Gizmos.DrawLine(origin, spoutPos);

        // V-shaped arrowhead at the tip
        Vector3 right = Vector3.Cross(dir, Vector3.up);
        if (right.sqrMagnitude < 0.01f)
            right = Vector3.Cross(dir, Vector3.forward);
        right = right.normalized;

        float head = len * 0.3f;
        Gizmos.DrawLine(spoutPos, spoutPos - dir * head + right * head * 0.5f);
        Gizmos.DrawLine(spoutPos, spoutPos - dir * head - right * head * 0.5f);

        // Draw the fallback window arc only when no collider is assigned
        // (the collider draws its own gizmo automatically)
        if (windowZone == null)
            DrawWindowArcGizmo(origin);
    }

    private void DrawWindowArcGizmo(Vector3 center)
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);

        Transform parent = hopperTransform != null ? hopperTransform.parent : null;
        int       segs   = 24;
        Vector3   prev   = Vector3.zero;

        for (int i = 0; i <= segs; i++)
        {
            float t      = (float)i / segs;
            float angleDeg = Mathf.Lerp(alignmentCenterAngle - alignmentHalfWidth,
                                         alignmentCenterAngle + alignmentHalfWidth, t);
            Vector3 pt = ArcPoint(center, parent, angleDeg, spoutRadius);
            if (i > 0) Gizmos.DrawLine(prev, pt);
            prev = pt;
        }

        // Short radial ticks at each boundary
        for (int e = 0; e < 2; e++)
        {
            float angleDeg = e == 0
                ? alignmentCenterAngle - alignmentHalfWidth
                : alignmentCenterAngle + alignmentHalfWidth;
            Gizmos.DrawLine(
                ArcPoint(center, parent, angleDeg, spoutRadius * 0.85f),
                ArcPoint(center, parent, angleDeg, spoutRadius * 1.15f));
        }
    }

    /// <summary>World-space point on a circle at the given angle and radius around center.</summary>
    private static Vector3 ArcPoint(Vector3 center, Transform parentTransform, float angleDeg, float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 local = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * radius;
        Vector3 world = parentTransform != null ? parentTransform.rotation * local : local;
        return center + world;
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
