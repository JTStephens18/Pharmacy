using System;
using UnityEngine;

/// <summary>
/// Handles gate input, flow-rate calculation, and pill accumulation for the pill filling station.
///
/// The player holds left mouse to open the gate. Pills flow only while the gate is open
/// AND the hopper spout is within the alignment window. Flow rate follows a cosine curve:
/// dead center = maximum (~28 pills/sec), window edge = zero.
///
/// Pill count accumulates as a float; each integer crossing fires OnPillDispensed.
/// </summary>
public class DispensingController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RotatingHopper hopper;

    [Header("Flow Settings")]
    [Tooltip("Maximum pills per second when the spout is dead center in the alignment window.")]
    [SerializeField] private float maxFlowRate = 28f;

    [Header("Input")]
    [Tooltip("Hold this key to open the dispensing gate.")]
    [SerializeField] private KeyCode gateKey = KeyCode.Mouse0;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Looping sound while pills are flowing.")]
    [SerializeField] private AudioClip flowLoopClip;
    [Tooltip("One-shot sound per pill dispensed (optional, can be performance-heavy).")]
    [SerializeField] private AudioClip pillTickClip;

    // ── Events ──────────────────────────────────────────────────────

    /// <summary>Fired each time a pill is counted. Args: (currentCount, targetCount).</summary>
    public event Action<int, int> OnPillDispensed;

    /// <summary>Fired when currentCount reaches targetCount (if target > 0).</summary>
    public event Action OnTargetReached;

    /// <summary>Fired when the gate opens or closes. Arg: true = open.</summary>
    public event Action<bool> OnGateStateChanged;

    // ── State ────────────────────────────────────────────────────────

    private int _currentCount;
    private int _targetCount;
    private float _pillAccumulator;
    private bool _gateOpen;
    private bool _isActive;
    private bool _isFlowing;
    private float _debugLogTimer;

    public int CurrentCount => _currentCount;
    public int TargetCount => _targetCount;
    public bool IsGateOpen => _gateOpen;

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// Reset state and begin accepting input.
    /// </summary>
    /// <param name="targetCount">Prescription target (0 = no target, count-only mode).</param>
    public void Initialize(int targetCount)
    {
        _currentCount = 0;
        _targetCount = targetCount;
        _pillAccumulator = 0f;
        _gateOpen = false;
        _isFlowing = false;
        _isActive = true;
        _debugLogTimer = 0f;
        Debug.Log($"[DispensingController] Initialized. Target: {targetCount}, MaxFlowRate: {maxFlowRate}");
    }

    /// <summary>Stop accepting input and close the gate.</summary>
    public void Shutdown()
    {
        Debug.Log($"[DispensingController] Shutdown. Final count: {_currentCount}/{_targetCount}");
        _isActive = false;
        if (_gateOpen)
        {
            _gateOpen = false;
            OnGateStateChanged?.Invoke(false);
        }
        StopFlowAudio();
    }

    // ── Update ──────────────────────────────────────────────────────

    void Update()
    {
        if (!_isActive) return;

        // Gate input
        bool wantOpen = Input.GetKey(gateKey);
        if (wantOpen != _gateOpen)
        {
            _gateOpen = wantOpen;
            OnGateStateChanged?.Invoke(_gateOpen);
            Debug.Log($"[DispensingController] Gate {(_gateOpen ? "OPENED" : "CLOSED")} | Count: {_currentCount}/{_targetCount}");
        }

        // Flow calculation
        if (!_gateOpen || hopper == null || !hopper.IsLoaded)
        {
            if (_isFlowing) StopFlowAudio();
            return;
        }

        if (hopper.IsSpoutInWindow(out float normalizedPos))
        {
            // Cosine falloff: center (0) = full rate, edge (1) = zero
            float flowRate = maxFlowRate * Mathf.Cos(normalizedPos * Mathf.PI * 0.5f);
            _pillAccumulator += flowRate * Time.deltaTime;

            if (!_isFlowing)
            {
                StartFlowAudio();
                Debug.Log($"[DispensingController] Flow STARTED — spout in window (normPos: {normalizedPos:F2}, flowRate: {flowRate:F1} pills/s)");
            }

            // Periodic debug log while flowing (every 0.5s)
            _debugLogTimer += Time.deltaTime;
            if (_debugLogTimer >= 0.5f)
            {
                _debugLogTimer = 0f;
                Debug.Log($"[DispensingController] Flowing — angle: {hopper.SpoutAngle:F1}° | normPos: {normalizedPos:F2} | rate: {flowRate:F1}/s | count: {_currentCount}/{_targetCount} | accum: {_pillAccumulator:F2}");
            }

            while (_pillAccumulator >= 1f)
            {
                _pillAccumulator -= 1f;
                _currentCount++;

                Debug.Log($"[DispensingController] +1 pill dispensed! Count: {_currentCount}/{_targetCount}");

                if (pillTickClip != null && audioSource != null)
                    audioSource.PlayOneShot(pillTickClip, 0.3f);

                OnPillDispensed?.Invoke(_currentCount, _targetCount);

                if (_targetCount > 0 && _currentCount >= _targetCount)
                {
                    Debug.Log($"[DispensingController] TARGET REACHED! {_currentCount}/{_targetCount}");
                    OnTargetReached?.Invoke();
                    break;
                }
            }
        }
        else
        {
            if (_isFlowing)
            {
                StopFlowAudio();
                Debug.Log($"[DispensingController] Flow STOPPED — spout outside window (angle: {hopper.SpoutAngle:F1}°)");
            }
        }
    }

    // ── Audio Helpers ───────────────────────────────────────────────

    private void StartFlowAudio()
    {
        _isFlowing = true;
        if (audioSource != null && flowLoopClip != null && !audioSource.isPlaying)
        {
            audioSource.clip = flowLoopClip;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void StopFlowAudio()
    {
        _isFlowing = false;
        if (audioSource != null && audioSource.isPlaying && audioSource.clip == flowLoopClip)
            audioSource.Stop();
    }
}
