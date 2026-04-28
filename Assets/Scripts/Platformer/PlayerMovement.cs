using UnityEngine;

/// <summary>
/// Plain C# class — all player movement logic lives here.
/// No MonoBehaviour, no Unity lifecycle. Injected into PlayerController.
/// </summary>
public class PlayerMovement
{
    private readonly Rigidbody2D _rb;
    private readonly Transform _groundCheck;
    private readonly LayerMask _groundLayer;

    private float _moveSpeed;
    private float _jumpForce;
    private float _coyoteTime;
    private float _jumpBufferTime;
    private float _fallGravityMultiplier;
    private float _groundCheckRadius;
    private readonly float _defaultGravityScale;

    private float _coyoteTimeCounter;
    private float _jumpBufferCounter;
    private bool _isGrounded;
    private bool _movementLocked;

    // Drop-through state — one-way platforms.
    private LayerMask _oneWayLayer;
    private int _oneWayLayerIndex;
    private readonly int _playerLayerIndex;
    private float _dropThroughDuration;
    private float _dropThroughTimer;
    private bool _dropThroughActive;

    public bool IsGrounded => _isGrounded;
    public float VelocityY => _rb.linearVelocity.y;
    public bool IsMovementLocked => _movementLocked;

    public PlayerMovement(
        Rigidbody2D rb,
        Transform groundCheck,
        LayerMask groundLayer,
        LayerMask oneWayLayer,
        int playerLayerIndex,
        float moveSpeed,
        float jumpForce,
        float coyoteTime,
        float jumpBufferTime,
        float fallGravityMultiplier,
        float groundCheckRadius,
        float dropThroughDuration)
    {
        _rb = rb;
        _groundCheck = groundCheck;
        _groundLayer = groundLayer;
        _oneWayLayer = oneWayLayer;
        _oneWayLayerIndex = LayerMaskToSingleIndex(oneWayLayer);
        _playerLayerIndex = playerLayerIndex;
        _moveSpeed = moveSpeed;
        _jumpForce = jumpForce;
        _coyoteTime = coyoteTime;
        _jumpBufferTime = jumpBufferTime;
        _fallGravityMultiplier = fallGravityMultiplier;
        _groundCheckRadius = groundCheckRadius;
        _dropThroughDuration = dropThroughDuration;
        _defaultGravityScale = rb.gravityScale;
    }

    /// <summary>
    /// Converts a single-layer LayerMask to its layer index. Returns -1 for an empty mask.
    /// Multi-layer masks return the lowest-bit layer; the OneWayPlatform mask is expected
    /// to contain exactly one layer in production wiring.
    /// </summary>
    private static int LayerMaskToSingleIndex(LayerMask mask)
    {
        int value = mask.value;
        if (value == 0) return -1;
        for (int i = 0; i < 32; i++)
        {
            if ((value & (1 << i)) != 0) return i;
        }
        return -1;
    }

    /// <summary>Syncs Inspector-tweakable values each frame. Called from PlayerController.Update().</summary>
    public void UpdateConfig(
        LayerMask oneWayLayer,
        float moveSpeed, float jumpForce,
        float coyoteTime, float jumpBufferTime,
        float fallGravityMultiplier, float groundCheckRadius,
        float dropThroughDuration)
    {
        _oneWayLayer = oneWayLayer;
        _oneWayLayerIndex = LayerMaskToSingleIndex(oneWayLayer);
        _moveSpeed = moveSpeed;
        _jumpForce = jumpForce;
        _coyoteTime = coyoteTime;
        _jumpBufferTime = jumpBufferTime;
        _fallGravityMultiplier = fallGravityMultiplier;
        _groundCheckRadius = groundCheckRadius;
        _dropThroughDuration = dropThroughDuration;
    }

    /// <summary>Called once per frame from PlayerController.Update().</summary>
    public void Tick(float deltaTime)
    {
        CheckGrounded();
        UpdateCoyoteTime(deltaTime);
        UpdateJumpBuffer(deltaTime);
        UpdateDropThrough(deltaTime);
        ApplyFallGravity();
    }

    /// <summary>Apply horizontal velocity. Called from FixedUpdate.</summary>
    public void Move(float horizontalInput)
    {
        float velocity = _movementLocked ? 0f : horizontalInput * _moveSpeed;
        _rb.linearVelocity = new Vector2(velocity, _rb.linearVelocity.y);
    }

    /// <summary>Store jump intent. Called when jump button is pressed.</summary>
    public void BufferJump()
    {
        _jumpBufferCounter = _jumpBufferTime;
    }

    /// <summary>Execute jump if conditions are met. Called each frame in Update.</summary>
    public void TryConsumeJumpBuffer()
    {
        if (_jumpBufferCounter > 0f && _coyoteTimeCounter > 0f)
        {
            ExecuteJump();
        }
    }

    /// <summary>
    /// Lock or unlock horizontal movement. When locked, Move() applies zero horizontal velocity.
    /// Called by PlayerController during attack animation playback.
    /// </summary>
    public void SetMovementLocked(bool locked)
    {
        _movementLocked = locked;
    }

    /// <summary>
    /// Attempts to drop through a one-way platform under the player's feet.
    /// Preconditions: grounded, not movement-locked, and the ground-check overlap
    /// finds a collider on the OneWayPlatform layer.
    /// On success: ignores Player↔OneWayPlatform collisions for _dropThroughDuration
    /// seconds, cancels coyote time so the player cannot immediately jump back up,
    /// and returns true. Otherwise returns false (caller may treat the input as a
    /// normal jump).
    /// </summary>
    public bool TryDropThrough()
    {
        if (!_isGrounded) return false;
        if (_movementLocked) return false;
        if (_oneWayLayerIndex < 0) return false;
        if (_playerLayerIndex < 0) return false;

        bool overOneWay = Physics2D.OverlapCircle(
            _groundCheck.position, _groundCheckRadius, _oneWayLayer);
        if (!overOneWay) return false;

        Physics2D.IgnoreLayerCollision(_playerLayerIndex, _oneWayLayerIndex, true);
        _dropThroughActive = true;
        _dropThroughTimer = _dropThroughDuration;
        _coyoteTimeCounter = 0f;
        return true;
    }

    /// <summary>
    /// Force-restores Player↔OneWayPlatform collision and clears drop-through state.
    /// Called from PlayerController.OnDisable and OnDestroy so the global
    /// IgnoreLayerCollision state never leaks across scene transitions, player
    /// destruction, or domain reload.
    /// </summary>
    public void ResetDropThrough()
    {
        if (_oneWayLayerIndex >= 0 && _playerLayerIndex >= 0)
        {
            Physics2D.IgnoreLayerCollision(_playerLayerIndex, _oneWayLayerIndex, false);
        }
        _dropThroughActive = false;
        _dropThroughTimer = 0f;
    }

    /// <summary>Cut jump height early. Called when jump button is released.</summary>
    public void CutJump()
    {
        if (_rb.linearVelocity.y > 0f)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.y * 0.5f);
        }
        _coyoteTimeCounter = 0f;
    }

    private void ExecuteJump()
    {
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _jumpForce);
        _jumpBufferCounter = 0f;
        _coyoteTimeCounter = 0f;
    }

    private void CheckGrounded()
    {
        _isGrounded = Physics2D.OverlapCircle(_groundCheck.position, _groundCheckRadius, _groundLayer);
    }

    private void UpdateCoyoteTime(float deltaTime)
    {
        // While drop-through is active we are still geometrically inside the
        // platform's collider AABB for the first few frames of the fall.
        // Physics2D.OverlapCircle (used by CheckGrounded) does NOT honor
        // IgnoreLayerCollision — it returns hits purely from the layer mask —
        // so _isGrounded would stay true and coyote would refill to its full
        // window, allowing the player to re-jump back onto the platform they
        // just dropped from. Suppress the refill until the drop-through window
        // closes. Spec smoke-test 6 (verification step) depends on this.
        if (_isGrounded && !_dropThroughActive)
            _coyoteTimeCounter = _coyoteTime;
        else
            _coyoteTimeCounter -= deltaTime;
    }

    private void UpdateJumpBuffer(float deltaTime)
    {
        if (_jumpBufferCounter > 0f)
            _jumpBufferCounter -= deltaTime;
    }

    private void UpdateDropThrough(float deltaTime)
    {
        if (!_dropThroughActive) return;
        _dropThroughTimer -= deltaTime;
        if (_dropThroughTimer <= 0f)
        {
            if (_oneWayLayerIndex >= 0 && _playerLayerIndex >= 0)
            {
                Physics2D.IgnoreLayerCollision(_playerLayerIndex, _oneWayLayerIndex, false);
            }
            _dropThroughActive = false;
            _dropThroughTimer = 0f;
        }
    }

    private void ApplyFallGravity()
    {
        _rb.gravityScale = _rb.linearVelocity.y < 0f
            ? _defaultGravityScale * _fallGravityMultiplier
            : _defaultGravityScale;
    }
}
