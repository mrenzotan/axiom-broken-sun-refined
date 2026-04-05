using UnityEngine;

/// <summary>
/// Plain C# — drives Animator parameters from movement state each frame.
/// No MonoBehaviour, no Unity lifecycle. Injected into PlayerController.
/// </summary>
public class PlayerAnimator
{
    private static readonly int ParamVelocityX = Animator.StringToHash("VelocityX");
    private static readonly int ParamVelocityY = Animator.StringToHash("VelocityY");
    private static readonly int ParamIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int ParamIsFacingRight = Animator.StringToHash("IsFacingRight");

    private readonly Animator _animator;
    private readonly PlayerMovement _movement;

    private bool _facingRight = true;

    public PlayerAnimator(Animator animator, PlayerMovement movement)
    {
        Debug.Assert(animator != null, "PlayerAnimator: animator is null — Animator component not found on Player or any child.");
        _animator = animator;
        _movement = movement;
    }

    /// <summary>
    /// Call each frame from PlayerController.Update().
    /// moveInput is the raw horizontal axis value from the Input System.
    /// </summary>
    public void Tick(float moveInput)
    {
        // Track facing from last intentional input — hold direction on idle.
        if (moveInput > 0f) _facingRight = true;
        else if (moveInput < 0f) _facingRight = false;

        // Normalize to -1 / 0 / +1.
        // Mathf.Sign(0f) returns 1 in Unity — handle zero explicitly.
        float normalizedX = moveInput > 0f ? 1f : moveInput < 0f ? -1f : 0f;

        _animator.SetFloat(ParamVelocityX, normalizedX);
        _animator.SetFloat(ParamVelocityY, _movement.VelocityY);
        _animator.SetBool(ParamIsGrounded, _movement.IsGrounded);
        _animator.SetBool(ParamIsFacingRight, _facingRight);
    }
}
