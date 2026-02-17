using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Header("Sensitivity")]
    [SerializeField] private float sensitivityX = 2.0f;
    [SerializeField] private float sensitivityY = 2.0f;
    [SerializeField] private bool invertY = false;

    [Header("Smoothing")]
    [SerializeField] private bool useRawInput = false;
    [SerializeField] private bool useSmoothDamp = true;
    [SerializeField] private float smoothTime = 0.03f;
    [Tooltip("Only used when useSmoothDamp is false")]
    [SerializeField] private float lerpSmoothing = 10f;

    [Header("Acceleration")]
    [SerializeField] private bool useAcceleration = false;
    [SerializeField] private float accelerationMultiplier = 1.5f;
    [SerializeField] private float accelerationThreshold = 3f;

    [Header("Constraints")]
    [SerializeField] private float minPitch = -90f;
    [SerializeField] private float maxPitch = 90f;

    [Header("Screen Shake")]
    [SerializeField] private float defaultShakeIntensity = 0.15f;
    [SerializeField] private float defaultShakeDuration = 0.2f;

    [Header("References")]
    [SerializeField] private Transform playerBody;

    // Internal state
    private float _xRotation = 0f;
    private Vector2 _currentVelocity;
    private Vector2 _smoothedInput;

    // Shake state
    private float _shakeTimeRemaining = 0f;
    private float _shakeIntensity = 0f;
    private Vector3 _shakeOffset = Vector3.zero;

    // Singleton for easy access
    public static MouseLook Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleLook();
        HandleShake();
    }

    private void HandleLook()
    {
        // 1. Get raw mouse input
        Vector2 rawInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

        // 2. Apply acceleration if enabled
        if (useAcceleration)
        {
            float magnitude = rawInput.magnitude;
            if (magnitude > accelerationThreshold)
            {
                float accelFactor = 1f + (magnitude - accelerationThreshold) * (accelerationMultiplier - 1f) / accelerationThreshold;
                rawInput *= accelFactor;
            }
        }

        // 3. Apply smoothing (or use raw input directly)
        Vector2 processedInput;

        if (useRawInput)
        {
            // No smoothing - direct 1:1 mouse movement
            processedInput = rawInput;
        }
        else if (useSmoothDamp)
        {
            // SmoothDamp for natural deceleration
            _smoothedInput = Vector2.SmoothDamp(_smoothedInput, rawInput, ref _currentVelocity, smoothTime);
            processedInput = _smoothedInput;
        }
        else
        {
            // Lerp-based smoothing (legacy)
            _smoothedInput = Vector2.Lerp(_smoothedInput, rawInput, Time.deltaTime * lerpSmoothing);
            processedInput = _smoothedInput;
        }

        // 4. Apply sensitivity
        float mouseX = processedInput.x * sensitivityX;
        float mouseY = processedInput.y * sensitivityY;

        // 5. Calculate vertical rotation (pitch)
        float verticalInput = invertY ? mouseY : -mouseY;
        _xRotation += verticalInput;
        _xRotation = Mathf.Clamp(_xRotation, minPitch, maxPitch);

        // 6. Apply rotations (with shake offset)
        transform.localRotation = Quaternion.Euler(_xRotation + _shakeOffset.x, _shakeOffset.y, _shakeOffset.z);
        playerBody.Rotate(Vector3.up * mouseX);
    }

    private void HandleShake()
    {
        if (_shakeTimeRemaining > 0)
        {
            _shakeTimeRemaining -= Time.deltaTime;

            // Decreasing intensity over time
            float progress = _shakeTimeRemaining / defaultShakeDuration;
            float currentIntensity = _shakeIntensity * progress;

            // Random shake offset
            _shakeOffset = new Vector3(
                Random.Range(-1f, 1f) * currentIntensity,
                Random.Range(-1f, 1f) * currentIntensity,
                Random.Range(-0.5f, 0.5f) * currentIntensity
            );
        }
        else
        {
            _shakeOffset = Vector3.zero;
        }
    }

    /// <summary>
    /// Triggers a screen shake effect.
    /// </summary>
    public void Shake(float intensity = -1f, float duration = -1f)
    {
        _shakeIntensity = intensity > 0 ? intensity : defaultShakeIntensity;
        _shakeTimeRemaining = duration > 0 ? duration : defaultShakeDuration;
    }

    // Public methods to adjust settings at runtime
    public void SetSensitivity(float x, float y)
    {
        sensitivityX = x;
        sensitivityY = y;
    }

    public void SetRawInput(bool enabled)
    {
        useRawInput = enabled;
        if (enabled)
        {
            _smoothedInput = Vector2.zero;
            _currentVelocity = Vector2.zero;
        }
    }

    public void SetSmoothTime(float time)
    {
        smoothTime = time;
    }

    public void SetAcceleration(bool enabled, float multiplier = 1.5f, float threshold = 3f)
    {
        useAcceleration = enabled;
        accelerationMultiplier = multiplier;
        accelerationThreshold = threshold;
    }
}