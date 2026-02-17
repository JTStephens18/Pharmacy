using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController controller;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 12f;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Jumping")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private LayerMask groundMask;

    // Internal state
    private Vector3 _velocity;
    private Vector3 _currentMoveVelocity;
    private float _currentSpeed;
    private bool _isGrounded;
    private float _lastGroundedTime = float.NegativeInfinity;
    private float _lastJumpPressedTime = float.NegativeInfinity;

    void Update()
    {
        HandleGroundCheck();
        HandleMovement();
        HandleJumping();
        ApplyGravity();
    }

    private void HandleGroundCheck()
    {
        // Use CharacterController's built-in ground detection for accuracy
        _isGrounded = controller.isGrounded;

        if (_isGrounded)
        {
            _lastGroundedTime = Time.time;
        }
    }

    private void HandleMovement()
    {
        // Get input
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");

        // Calculate move direction (normalized to prevent diagonal speed boost)
        Vector3 inputDirection = new Vector3(inputX, 0f, inputZ).normalized;
        Vector3 moveDirection = transform.right * inputDirection.x + transform.forward * inputDirection.z;

        // Determine target speed (sprint or walk)
        bool isSprinting = Input.GetKey(sprintKey) && inputZ > 0; // Only sprint when moving forward
        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;

        // Apply acceleration/deceleration for smoother movement
        if (inputDirection.magnitude > 0.1f)
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, deceleration * Time.deltaTime);
        }

        // Smooth the movement direction
        Vector3 targetVelocity = moveDirection * _currentSpeed;
        _currentMoveVelocity = Vector3.Lerp(_currentMoveVelocity, targetVelocity, Time.deltaTime * acceleration);

        // Apply movement
        controller.Move(_currentMoveVelocity * Time.deltaTime);
    }

    private void HandleJumping()
    {
        // Track jump input with buffering
        if (Input.GetButtonDown("Jump"))
        {
            _lastJumpPressedTime = Time.time;
        }

        // Check if we can jump (coyote time + jump buffer)
        bool canJump = (Time.time - _lastGroundedTime) <= coyoteTime;
        bool jumpPressed = (Time.time - _lastJumpPressedTime) <= jumpBufferTime;

        if (jumpPressed && canJump)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _lastJumpPressedTime = float.NegativeInfinity; // Consume the jump buffer
            _lastGroundedTime = float.NegativeInfinity;    // Consume coyote time
        }
    }

    private void ApplyGravity()
    {
        // Only apply gravity if not grounded, otherwise keep small downward force
        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }
        else
        {
            _velocity.y += gravity * Time.deltaTime;
        }

        controller.Move(_velocity * Time.deltaTime);
    }

    // Public getters for other scripts
    public bool IsGrounded() => _isGrounded;
    public bool IsSprinting() => Input.GetKey(sprintKey) && _currentSpeed > walkSpeed;
    public float GetCurrentSpeed() => _currentSpeed;

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        // Changes color based on whether the player is grounded
        Gizmos.color = _isGrounded ? Color.green : Color.red;

        // Draws the sphere at the groundCheck position with the groundDistance radius
        Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
    }
}