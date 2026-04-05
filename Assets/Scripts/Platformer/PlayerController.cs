using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// MonoBehaviour — Unity lifecycle and input only.
/// All movement logic is delegated to PlayerMovement.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpForce = 16f;

    [Header("Jump Feel")]
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float fallGravityMultiplier = 2.5f;

    [Header("Ground Detection")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.1f;

    private Rigidbody2D _rb;
    private Animator _animator;
    private PlayerMovement _movement;
    private PlayerAnimator _playerAnimator;
    private InputSystem_Actions _input;
    private float _moveInput;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponentInChildren<Animator>(true);
        if (_animator == null)
        {
            _animator = FindAnyObjectByType<Animator>();
            if (_animator != null)
                Debug.LogWarning($"PlayerController: Animator not found on Player hierarchy. Using '{_animator.gameObject.name}' from scene search — assign it to the Player or a child in the Inspector.", this);
        }

        _movement = new PlayerMovement(
            _rb, groundCheck, groundLayer,
            moveSpeed, jumpForce,
            coyoteTime, jumpBufferTime,
            fallGravityMultiplier, groundCheckRadius);

        _playerAnimator = new PlayerAnimator(_animator, _movement);

        _input = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _input.Player.Enable();
        _input.Player.Jump.performed += OnJumpPerformed;
        _input.Player.Jump.canceled += OnJumpCanceled;
    }

    private void OnDisable()
    {
        _input.Player.Jump.performed -= OnJumpPerformed;
        _input.Player.Jump.canceled -= OnJumpCanceled;
        _input.Player.Disable();
    }

    private void Update()
    {
        _moveInput = _input.Player.Move.ReadValue<Vector2>().x;
        _movement.UpdateConfig(moveSpeed, jumpForce, coyoteTime, jumpBufferTime, fallGravityMultiplier, groundCheckRadius);
        _movement.Tick(Time.deltaTime);
        _movement.TryConsumeJumpBuffer();
        _playerAnimator.Tick(_moveInput);
    }

    private void FixedUpdate()
    {
        _movement.Move(_moveInput);
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        _movement.BufferJump();
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        _movement.CutJump();
    }
}
