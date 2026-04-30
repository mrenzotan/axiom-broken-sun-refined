using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Axiom.Core;

/// <summary>
/// MonoBehaviour — Unity lifecycle and input only.
/// All movement logic is delegated to PlayerMovement.
/// </summary>
[DefaultExecutionOrder(-200)]
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

    [Header("Drop-Through")]
    [SerializeField] private LayerMask oneWayPlatformLayer;
    [SerializeField] private float dropThroughDuration = 0.2f;

    private Rigidbody2D _rb;
    private Animator _animator;
    private PlayerMovement _movement;
    private PlayerAnimator _playerAnimator;
    private InputSystem_Actions _input;
    private float _moveInput;
    private Axiom.Platformer.ExplorationEnemyCombatTrigger _pendingAttackTrigger;

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

        int playerLayerIndex = gameObject.layer;
        _movement = new PlayerMovement(
            _rb, groundCheck, groundLayer,
            oneWayPlatformLayer, playerLayerIndex,
            moveSpeed, jumpForce,
            coyoteTime, jumpBufferTime,
            fallGravityMultiplier, groundCheckRadius,
            dropThroughDuration);

        _playerAnimator = new PlayerAnimator(_animator, _movement);

        _input = new InputSystem_Actions();

        // Before first render / transition fade reveals the scene — not in Start/OnSceneReady
        // (that ran after the fade and caused a visible spawn-then-teleport).
        ApplyPersistedWorldPositionIfNeeded();
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
        _movement?.ResetDropThrough();
    }

    private void Start()
    {
        if (GameManager.Instance?.SceneTransition?.IsTransitioning == true)
        {
            // Disable input and lock movement until the transition reveal completes.
            // (OnEnable already ran and enabled input — we override that here.)
            _input.Player.Disable();
            _movement.SetMovementLocked(true);
            GameManager.Instance.OnSceneReady += InitializeFromTransition;
        }
        else
        {
            InitializeFromTransition();
        }
    }

    private void InitializeFromTransition()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnSceneReady -= InitializeFromTransition;

        _input.Player.Enable();
        _movement.SetMovementLocked(false);
    }

    /// <summary>
    /// After Continue, battle return, or any load, <see cref="PlayerState"/> holds the last saved
    /// world position — the scene prefab spawn is ignored in that case.
    /// </summary>
    private void ApplyPersistedWorldPositionIfNeeded()
    {
        PlayerState state = GameManager.Instance?.PlayerState;
        if (state == null || !state.HasPendingWorldPositionApply)
            return;

        // After a LevelExitTrigger transition the persisted position lives in the
        // previous scene's coordinate space. Skip the apply when scenes mismatch
        // so the new scene's player prefab spawn wins.
        string activeScene = SceneManager.GetActiveScene().name;
        if (!string.Equals(state.ActiveSceneName, activeScene, StringComparison.Ordinal))
        {
            state.ClearPendingWorldPositionApply();
            return;
        }

        float z = transform.position.z;
        var world = new Vector3(state.WorldPositionX, state.WorldPositionY, z);
        transform.SetPositionAndRotation(world, transform.rotation);
        _rb.position = world;
        _rb.linearVelocity = Vector2.zero;
        state.ClearPendingWorldPositionApply();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnSceneReady -= InitializeFromTransition;
        _movement?.ResetDropThrough();
    }

    private void Update()
    {
        _moveInput = _input.Player.Move.ReadValue<Vector2>().x;
        _movement.UpdateConfig(
            oneWayPlatformLayer,
            moveSpeed, jumpForce, coyoteTime, jumpBufferTime,
            fallGravityMultiplier, groundCheckRadius,
            dropThroughDuration);
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
        // Sample Move.y at the moment Jump is pressed. If the player is holding
        // Down (move.y < -0.5) and movement isn't locked, attempt drop-through
        // instead of buffering a jump. The lock check is required because the
        // attack-lock path leaves Jump enabled (only the tutorial-lock path
        // disables Jump outright).
        Vector2 move = _input.Player.Move.ReadValue<Vector2>();
        if (move.y < -0.5f && !_movement.IsMovementLocked)
        {
            if (_movement.TryDropThrough()) return;
        }
        _movement.BufferJump();
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        _movement.CutJump();
    }

    public bool IsFacingRight => _playerAnimator.IsFacingRight;

    /// <summary>
    /// Initiates the attack sequence: reserves the enemy trigger (blocking the Surprised
    /// path), locks movement, and starts the attack animation.
    /// Called by PlayerExplorationAttack. No-op if the player is airborne.
    /// </summary>
    public void BeginAttack(Axiom.Platformer.ExplorationEnemyCombatTrigger pending)
    {
        if (!_movement.IsGrounded) return;
        if (pending != null) pending.ReserveForAdvantagedBattle();
        _movement.SetMovementLocked(true);
        _playerAnimator.TriggerAttack();
        _pendingAttackTrigger = pending;
    }

    /// <summary>
    /// Called by PlayerExplorationAnimator when the attack animation clip ends.
    /// Unlocks movement and triggers the Advantaged battle scene transition.
    /// </summary>
    public void OnAttackAnimationEnd()
    {
        _movement.SetMovementLocked(false);
        if (_pendingAttackTrigger != null) _pendingAttackTrigger.TriggerAdvantagedBattle();
        _pendingAttackTrigger = null;
    }

    /// <summary>
    /// Locks/unlocks player movement AND jump input for tutorial purposes — leaves
    /// Attack input alive so the player can engage the locked-near-enemy battle trigger.
    /// Different from the attack-anim lock (which uses _movement.SetMovementLocked alone).
    /// Called by TutorialPromptTrigger when its _lockMovementWhileInside flag is true.
    /// </summary>
    public void SetTutorialMovementLocked(bool locked)
    {
        _movement.SetMovementLocked(locked);
        if (locked) _input.Player.Jump.Disable();
        else        _input.Player.Jump.Enable();
    }
}
