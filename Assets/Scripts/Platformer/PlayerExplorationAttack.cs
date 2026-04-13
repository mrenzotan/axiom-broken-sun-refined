using UnityEngine;
using UnityEngine.InputSystem;

namespace Axiom.Platformer
{
    /// <summary>
    /// Attach to the Player. Reads the "Player/Attack" input action.
    /// When Attack is pressed and an ExplorationEnemyCombatTrigger is within range,
    /// calls TriggerAdvantagedBattle() — the player acts first in the resulting battle.
    ///
    /// Requires:
    ///   - "Attack" action in the "Player" action map of the project's Input Actions asset.
    ///   - _enemyLayer set to the layer used by exploration enemy GameObjects.
    ///   - ExplorationEnemyCombatTrigger component present on enemy GameObjects.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerExplorationAttack : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Radius of the attack hit detection circle.")]
        private float _attackRange = 1.5f;

        [SerializeField]
        [Tooltip("How far in front of the player (in world units) to center the attack hit detection. " +
                 "Direction is derived from the player's current facing.")]
        private float _attackOffset = 0.75f;

        [SerializeField]
        [Tooltip("Layer mask for exploration enemy GameObjects. Set to your enemy layer in the Inspector.")]
        private LayerMask _enemyLayer;

        private InputSystem_Actions _actions;
        private InputAction _attackAction;
        private PlayerController _controller;

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            _attackAction = _actions.Player.Attack;
            _controller = GetComponent<PlayerController>();
        }

        private void OnEnable()   => _attackAction.Enable();
        private void OnDisable()  => _attackAction.Disable();
        private void OnDestroy()  => _actions.Dispose();

        private void Update()
        {
            if (!_attackAction.WasPerformedThisFrame()) return;

            Vector2 attackCenter = AttackCenter();
            Collider2D hit = Physics2D.OverlapCircle(attackCenter, _attackRange, _enemyLayer);
            ExplorationEnemyCombatTrigger trigger = hit != null
                ? hit.GetComponent<ExplorationEnemyCombatTrigger>()
                : null;
            _controller.BeginAttack(trigger);
        }

        private Vector2 AttackCenter()
        {
            float direction = _controller.IsFacingRight ? 1f : -1f;
            return (Vector2)transform.position + Vector2.right * (_attackOffset * direction);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            // In edit mode _controller isn't set — fall back to showing offset in +X.
            float direction = (_controller != null && !_controller.IsFacingRight) ? -1f : 1f;
            Vector2 center = (Vector2)transform.position + Vector2.right * (_attackOffset * direction);
            Gizmos.DrawWireSphere(center, _attackRange);
        }
    }
}
