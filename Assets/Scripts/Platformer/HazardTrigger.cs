using Axiom.Core;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Attach to a trigger collider in a level scene. On player contact, applies damage
    /// or instant KO by mutating GameManager.Instance.PlayerState.CurrentHp.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HazardTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("InstantKO for pits; PercentMaxHpDamage for spikes.")]
        private HazardMode _mode = HazardMode.PercentMaxHpDamage;

        [SerializeField]
        [Tooltip("Percent of MaxHp to subtract. Ignored when mode is InstantKO. Valid range 1–100.")]
        [Range(1, 100)]
        private int _percentMaxHpDamage = 20;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[HazardTrigger] GameManager not found — hazard ignored.", this);
                return;
            }

            PlayerState state = GameManager.Instance.PlayerState;
            HazardDamageResult result = HazardDamageResolver.Resolve(
                currentHp: state.CurrentHp,
                maxHp: state.MaxHp,
                mode: _mode,
                percentMaxHpDamage: _percentMaxHpDamage);

            state.SetCurrentHp(result.NewHp);
        }
    }
}
