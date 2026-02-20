using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton that manages transitioning between FPS mode and a focused mini-game mode.
/// Uses a simple camera Lerp transition instead of Cinemachine.
/// When entering focus mode it disables FPS controls and smoothly moves the camera
/// to a target transform. On exit it lerps back to the original position.
/// </summary>
public class FocusStateManager : MonoBehaviour
{
    public static FocusStateManager Instance { get; private set; }

    /// <summary>
    /// Fired when focus state changes. True = entered focus, False = exited focus.
    /// </summary>
    public event Action<bool> OnFocusChanged;

    [Header("References (auto-found if left empty)")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private MouseLook mouseLook;
    [SerializeField] private ObjectPickup objectPickup;

    [Header("Transition Settings")]
    [Tooltip("How long the camera transition takes in seconds.")]
    [SerializeField] private float transitionDuration = 0.6f;
    [Tooltip("Easing curve for the camera transition.")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Exit Key")]
    [SerializeField] private KeyCode exitKey = KeyCode.Escape;

    private bool _isFocused;
    private bool _isTransitioning;
    private Camera _mainCamera;
    private Transform _cameraTransform;

    // Stored FPS camera state for returning
    private Vector3 _savedLocalPosition;
    private Quaternion _savedLocalRotation;
    private Transform _savedParent;

    private Action _onExitCallback;
    private Coroutine _transitionCoroutine;

    public bool IsFocused => _isFocused;
    public bool IsTransitioning => _isTransitioning;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // Auto-find references if not assigned in Inspector
        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (mouseLook == null)
            mouseLook = FindFirstObjectByType<MouseLook>();
        if (objectPickup == null)
            objectPickup = FindFirstObjectByType<ObjectPickup>();

        // Find the main camera with multiple fallbacks
        _mainCamera = Camera.main;

        // Fallback: use the camera on the same object as MouseLook
        if (_mainCamera == null && mouseLook != null)
            _mainCamera = mouseLook.GetComponent<Camera>();

        // Fallback: use the camera on the same object as ObjectPickup
        if (_mainCamera == null && objectPickup != null)
            _mainCamera = objectPickup.GetComponent<Camera>();

        // Fallback: find any camera in the scene
        if (_mainCamera == null)
            _mainCamera = FindFirstObjectByType<Camera>();

        if (_mainCamera != null)
        {
            _cameraTransform = _mainCamera.transform;
        }
        else
        {
            Debug.LogError("[FocusStateManager] Could not find any Camera in the scene! " +
                "Make sure your camera has the 'MainCamera' tag, or assign references manually.");
        }
    }

    void Update()
    {
        if (_isFocused && !_isTransitioning && Input.GetKeyDown(exitKey))
        {
            ExitFocus();
        }
    }

    /// <summary>
    /// Enters focus mode: disables FPS controls and smoothly moves the camera to the target.
    /// </summary>
    /// <param name="focusTarget">A Transform representing the desired camera position and rotation.</param>
    /// <param name="onExit">Optional callback invoked when the player exits focus.</param>
    public void EnterFocus(Transform focusTarget, Action onExit = null)
    {
        if (_isFocused || _isTransitioning || focusTarget == null) return;

        _onExitCallback = onExit;

        // Save current camera state so we can return later
        _savedParent = _cameraTransform.parent;
        _savedLocalPosition = _cameraTransform.localPosition;
        _savedLocalRotation = _cameraTransform.localRotation;

        // Disable FPS controls immediately
        if (playerMovement != null) playerMovement.enabled = false;
        if (mouseLook != null) mouseLook.enabled = false;
        if (objectPickup != null) objectPickup.enabled = false;

        // Unlock cursor for mini-game interaction
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;

        // Start the camera transition
        if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(TransitionCamera(focusTarget.position, focusTarget.rotation, true));
    }

    /// <summary>
    /// Exits focus mode: smoothly returns the camera and re-enables FPS controls.
    /// </summary>
    public void ExitFocus()
    {
        if (!_isFocused || _isTransitioning) return;

        // Calculate where the camera needs to return to (parent-relative → world)
        Vector3 returnWorldPos = _savedParent != null
            ? _savedParent.TransformPoint(_savedLocalPosition)
            : _savedLocalPosition;
        Quaternion returnWorldRot = _savedParent != null
            ? _savedParent.rotation * _savedLocalRotation
            : _savedLocalRotation;

        if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(TransitionCamera(returnWorldPos, returnWorldRot, false));
    }

    private IEnumerator TransitionCamera(Vector3 targetPos, Quaternion targetRot, bool entering)
    {
        _isTransitioning = true;

        // Unparent camera so we can move it freely in world space
        Vector3 startPos = _cameraTransform.position;
        Quaternion startRot = _cameraTransform.rotation;
        _cameraTransform.SetParent(null);

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(Mathf.Clamp01(elapsed / transitionDuration));

            _cameraTransform.position = Vector3.Lerp(startPos, targetPos, t);
            _cameraTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        // Snap to final position
        _cameraTransform.position = targetPos;
        _cameraTransform.rotation = targetRot;

        _isTransitioning = false;

        if (entering)
        {
            _isFocused = true;
            OnFocusChanged?.Invoke(true);
            Debug.Log("[FocusStateManager] Entered focus mode.");
        }
        else
        {
            // Re-parent camera back to its original parent
            _cameraTransform.SetParent(_savedParent);
            _cameraTransform.localPosition = _savedLocalPosition;
            _cameraTransform.localRotation = _savedLocalRotation;

            _isFocused = false;

            // Re-enable FPS controls
            if (playerMovement != null) playerMovement.enabled = true;
            if (mouseLook != null) mouseLook.enabled = true;
            if (objectPickup != null) objectPickup.enabled = true;

            // Re-lock cursor for FPS
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            OnFocusChanged?.Invoke(false);

            // Fire exit callback
            _onExitCallback?.Invoke();
            _onExitCallback = null;

            Debug.Log("[FocusStateManager] Exited focus mode.");
        }
    }
}
