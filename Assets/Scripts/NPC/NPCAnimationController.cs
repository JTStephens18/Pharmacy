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

        // Subscribe to NPC events if available
        if (_npcController != null)
        {
            _npcController.OnPickupStart += HandlePickupAnimation;
            _npcController.OnPlaceStart += HandlePlaceAnimation;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (_npcController != null)
        {
            _npcController.OnPickupStart -= HandlePickupAnimation;
            _npcController.OnPlaceStart -= HandlePlaceAnimation;
        }
    }

    private void Update()
    {
        UpdateLocomotionAnimation();
    }

    /// <summary>
    /// Updates walk/idle animation based on NavMeshAgent state.
    /// </summary>
    private void UpdateLocomotionAnimation()
    {
        if (animator == null || _agent == null) return;

        // Get the horizontal speed (ignoring vertical movement)
        float speed = new Vector3(_agent.velocity.x, 0, _agent.velocity.z).magnitude;

        // Determine if NPC should be walking based on having an active path
        // This is more reliable than velocity because it doesn't depend on acceleration
        bool hasActiveDestination = _agent.hasPath &&
                                    !_agent.pathPending &&
                                    _agent.remainingDistance > _agent.stoppingDistance;

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
    /// Triggers the pickup animation.
    /// </summary>
    private void HandlePickupAnimation()
    {
        if (animator == null) return;

        if (showDebugLogs) Debug.Log("[NPC Animation] Triggering PickUp animation");
        animator.SetTrigger(PickUpTrigger);
    }

    /// <summary>
    /// Triggers the place animation.
    /// </summary>
    private void HandlePlaceAnimation()
    {
        if (animator == null) return;

        if (showDebugLogs) Debug.Log("[NPC Animation] Triggering Place animation");
        animator.SetTrigger(PlaceTrigger);
    }

    /// <summary>
    /// Manually trigger pickup animation (for testing or external calls).
    /// </summary>
    public void TriggerPickup()
    {
        HandlePickupAnimation();
    }

    /// <summary>
    /// Manually trigger place animation (for testing or external calls).
    /// </summary>
    public void TriggerPlace()
    {
        HandlePlaceAnimation();
    }
}
