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

    public bool IsGrounded => _isGrounded;
    public float VelocityY => _rb.linearVelocity.y;

    public PlayerMovement(
        Rigidbody2D rb,
        Transform groundCheck,
        LayerMask groundLayer,
        float moveSpeed,
        float jumpForce,
        float coyoteTime,
        float jumpBufferTime,
        float fallGravityMultiplier,
        float groundCheckRadius)
    {
        _rb = rb;
        _groundCheck = groundCheck;
        _groundLayer = groundLayer;
        _moveSpeed = moveSpeed;
        _jumpForce = jumpForce;
        _coyoteTime = coyoteTime;
        _jumpBufferTime = jumpBufferTime;
        _fallGravityMultiplier = fallGravityMultiplier;
        _groundCheckRadius = groundCheckRadius;
        _defaultGravityScale = rb.gravityScale;
    }

    /// <summary>Syncs Inspector-tweakable values each frame. Called from PlayerController.Update().</summary>
    public void UpdateConfig(
        float moveSpeed, float jumpForce,
        float coyoteTime, float jumpBufferTime,
        float fallGravityMultiplier, float groundCheckRadius)
    {
        _moveSpeed = moveSpeed;
        _jumpForce = jumpForce;
        _coyoteTime = coyoteTime;
        _jumpBufferTime = jumpBufferTime;
        _fallGravityMultiplier = fallGravityMultiplier;
        _groundCheckRadius = groundCheckRadius;
    }

    /// <summary>Called once per frame from PlayerController.Update().</summary>
    public void Tick(float deltaTime)
    {
        CheckGrounded();
        UpdateCoyoteTime(deltaTime);
        UpdateJumpBuffer(deltaTime);
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
        if (_isGrounded)
            _coyoteTimeCounter = _coyoteTime;
        else
            _coyoteTimeCounter -= deltaTime;
    }

    private void UpdateJumpBuffer(float deltaTime)
    {
        if (_jumpBufferCounter > 0f)
            _jumpBufferCounter -= deltaTime;
    }

    private void ApplyFallGravity()
    {
        _rb.gravityScale = _rb.linearVelocity.y < 0f
            ? _defaultGravityScale * _fallGravityMultiplier
            : _defaultGravityScale;
    }
}
