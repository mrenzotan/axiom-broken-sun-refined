using UnityEngine;

/// <summary>
/// Plain C# — drives the exploration enemy Animator from patrol velocity each FixedUpdate.
/// No MonoBehaviour, no Unity lifecycle. Injected into EnemyController.
///
/// Contract: the Animator Controller assigned to this enemy must expose a bool parameter
/// named exactly "IsMoving". Each exploration enemy supplies its own controller; all must
/// honour this contract. See IceSlimeExploration.controller for the reference implementation.
/// </summary>
public class EnemyAnimator
{
    private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
    private const float MovingThreshold = 0.01f;

    private readonly Animator _animator;

    public EnemyAnimator(Animator animator)
    {
        Debug.Assert(animator != null,
            "EnemyAnimator: animator is null — Animator component not assigned on the enemy.");
        _animator = animator;
    }

    /// <summary>
    /// Call from EnemyController.FixedUpdate() after computing xVelocity.
    /// Sets IsMoving = true when the absolute horizontal velocity exceeds the threshold.
    /// </summary>
    public void Tick(float xVelocity)
    {
        _animator.SetBool(ParamIsMoving, Mathf.Abs(xVelocity) > MovingThreshold);
    }
}
