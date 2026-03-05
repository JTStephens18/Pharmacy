using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles NPC animations based on movement and state changes from NPCInteractionController.
/// Attach this to the same GameObject as NPCInteractionController.
/// The Animator component should be on the FBX child object.
/// </summary>
public class NPCAnimationController : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Reference to the Animator component on the FBX child. If not set, will search in children.")]
    [SerializeField] private Animator animator;

    [Tooltip("Velocity threshold below which the NPC is considered idle.")]
    [SerializeField] private float walkThreshold = 0.1f;

    [Tooltip("The speed at which the walk animation was designed (matches NavMeshAgent speed for 1:1 sync).")]
    [SerializeField] private float animationReferenceSpeed = 3.5f;

    [Tooltip("Smoothing for animation transitions.")]
    [SerializeField] private float animationDampTime = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // Animator parameter names - must match your Animator Controller
    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int PickUpTrigger = Animator.StringToHash("PickUp");
    private static readonly int PlaceTrigger = Animator.StringToHash("Place");

    private NavMeshAgent _agent;
    private NPCInteractionController _npcController;
    private bool _wasWalking;
    // Used on non-server clients to derive NPC velocity from transform position delta,
    // because the NavMeshAgent is disabled on clients (server-authoritative NPC movement).
    private Vector3 _lastPosition;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _npcController = GetComponent<NPCInteractionController>();

        // Try to find Animator on children if not assigned
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError("[NPC Animation] No Animator found! Please assign an Animator component on the FBX child object.");
            enabled = false;
            return;
        }

        if (_agent == null)
        {
            Debug.LogError("[NPC Animation] No NavMeshAgent found!");
            enabled = false;
            return;
        }

        _lastPosition = transform.position;

        // Subscribe to C# events for non-networked (solo / editor) usage.
        // In networked play, NPCInteractionController fires ClientRpcs that call
        // TriggerPickup / TriggerPlace directly, so these events are only a fallback.
        if (_npcController != null)
        {
            _npcController.OnPickupStart += TriggerPickup;
            _npcController.OnPlaceStart += TriggerPlace;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (_npcController != null)
        {
            _npcController.OnPickupStart -= TriggerPickup;
            _npcController.OnPlaceStart -= TriggerPlace;
        }
    }

    private void Update()
    {
        UpdateLocomotionAnimation();
    }

    /// <summary>
    /// Updates walk/idle animation based on NavMeshAgent state (server) or position delta (clients).
    /// </summary>
    private void UpdateLocomotionAnimation()
    {
        if (animator == null || _agent == null) return;

        float speed;
        bool hasActiveDestination;

        if (_agent.enabled)
        {
            // Server (or non-networked): derive speed directly from the NavMeshAgent velocity.
            speed = new Vector3(_agent.velocity.x, 0, _agent.velocity.z).magnitude;
            hasActiveDestination = _agent.hasPath &&
                                   !_agent.pathPending &&
                                   _agent.remainingDistance > _agent.stoppingDistance;
        }
        else
        {
            // Non-server clients: the NavMeshAgent is disabled; derive speed from
            // how far the NPC moved this frame (NetworkTransform keeps position in sync).
            Vector3 delta = transform.position - _lastPosition;
            speed = new Vector3(delta.x, 0, delta.z).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            hasActiveDestination = speed > walkThreshold;
        }

        _lastPosition = transform.position;

        // Use destination-based walking detection OR velocity for edge cases
        bool isWalking = hasActiveDestination || speed > walkThreshold;

        // Set animator parameters
        animator.SetBool(IsWalking, isWalking);
        animator.SetFloat(Speed, speed, animationDampTime, Time.deltaTime);

        // Scale animation speed to match movement speed
        // Use a minimum speed to prevent animation from stopping during acceleration
        if (isWalking && animationReferenceSpeed > 0)
        {
            float animSpeed = Mathf.Max(speed, 0.5f) / animationReferenceSpeed;
            animator.speed = Mathf.Clamp(animSpeed, 0.5f, 2f); // Clamp to reasonable range
        }
        else
        {
            animator.speed = 1f;
        }

        // Debug logging on state change
        if (isWalking != _wasWalking)
        {
            Debug.Log($"[NPC Animation] {(isWalking ? "Started walking" : "Stopped walking")} - Speed: {speed:F2}, HasPath: {_agent.hasPath}, Remaining: {_agent.remainingDistance:F2}");
        }

        // Continuous debug logging when moving
        if (showDebugLogs && isWalking)
        {
            Debug.Log($"[NPC Animation] Speed: {speed:F2}, Anim Speed: {animator.speed:F2}");
        }

        _wasWalking = isWalking;
    }

    /// <summary>
    /// Triggers the pickup animation. Called by NPCInteractionController ClientRpc (networked)
    /// or via the OnPickupStart C# event (non-networked / solo testing).
    /// </summary>
    public void TriggerPickup()
    {
        if (animator == null) return;
        if (showDebugLogs) Debug.Log("[NPC Animation] Triggering PickUp animation");
        animator.SetTrigger(PickUpTrigger);
    }

    /// <summary>
    /// Triggers the place animation. Called by NPCInteractionController ClientRpc (networked)
    /// or via the OnPlaceStart C# event (non-networked / solo testing).
    /// </summary>
    public void TriggerPlace()
    {
        if (animator == null) return;
        if (showDebugLogs) Debug.Log("[NPC Animation] Triggering Place animation");
        animator.SetTrigger(PlaceTrigger);
    }
}
