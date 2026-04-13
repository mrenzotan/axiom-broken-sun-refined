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
    public class PlayerExplorationAttack : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Radius around the player's position to search for attackable enemies.")]
        private float _attackRange = 1.5f;

        [SerializeField]
        [Tooltip("Layer mask for exploration enemy GameObjects. Set to your enemy layer in the Inspector.")]
        private LayerMask _enemyLayer;

        private InputSystem_Actions _actions;
        private InputAction _attackAction;

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            _attackAction = _actions.Player.Attack;
        }

        private void OnEnable()   => _attackAction.Enable();
        private void OnDisable()  => _attackAction.Disable();
        private void OnDestroy()  => _actions.Dispose();

        private void Update()
        {
            if (!_attackAction.WasPerformedThisFrame()) return;

            Collider2D hit = Physics2D.OverlapCircle(transform.position, _attackRange, _enemyLayer);
            if (hit == null) return;

            var trigger = hit.GetComponent<ExplorationEnemyCombatTrigger>();
            if (trigger != null)
                GetComponent<PlayerController>().BeginAttack(trigger);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRange);
        }
    }
}
