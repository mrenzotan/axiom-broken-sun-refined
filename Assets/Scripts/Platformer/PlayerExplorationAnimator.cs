using UnityEngine;

/// <summary>
/// MonoBehaviour bridge — must live on the same GameObject as the Player's Animator component.
/// Unity Animation Events fire on MonoBehaviours on the same GameObject as the Animator,
/// not on the root. This component delegates the event to PlayerController on the parent.
///
/// Mirrors PlayerBattleAnimator in the Battle scene: same placement (Animator child),
/// same AnimEvent_ naming convention for Animation Event methods.
/// </summary>
public class PlayerExplorationAnimator : MonoBehaviour
{
    private PlayerController _controller;

    private void Start()
    {
        _controller = GetComponentInParent<PlayerController>();
        Debug.Assert(_controller != null,
            "PlayerExplorationAnimator: no PlayerController found in parent hierarchy. " +
            "This component must be on the Animator child of the Player root.", this);
    }

    /// <summary>
    /// Called by Animation Event on the last frame of playerAttackLeft.anim and playerAttackRight.anim.
    /// Naming mirrors PlayerBattleAnimator.AnimEvent_OnHit — prefix signals Animation Event origin.
    /// </summary>
    public void AnimEvent_OnAttackEnd()
    {
        _controller?.OnAttackAnimationEnd();
    }
}
