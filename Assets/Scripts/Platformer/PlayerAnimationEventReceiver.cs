using UnityEngine;

/// <summary>
/// MonoBehaviour bridge — must live on the same GameObject as the Player's Animator component.
/// Unity Animation Events fire on MonoBehaviours on the same GameObject as the Animator,
/// not on the root. This component delegates the event to PlayerController on the parent.
/// </summary>
public class PlayerAnimationEventReceiver : MonoBehaviour
{
    private PlayerController _controller;

    private void Start()
    {
        _controller = GetComponentInParent<PlayerController>();
        Debug.Assert(_controller != null,
            "PlayerAnimationEventReceiver: no PlayerController found in parent hierarchy. " +
            "This component must be on the Animator child of the Player root.", this);
    }

    /// <summary>
    /// Called by Animation Event on the last frame of playerAttackLeft.anim and playerAttackRight.anim.
    /// </summary>
    public void OnAttackEnd()
    {
        _controller?.OnAttackAnimationEnd();
    }
}
