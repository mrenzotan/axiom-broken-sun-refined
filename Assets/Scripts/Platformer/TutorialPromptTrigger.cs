using Axiom.Core;
using Axiom.Platformer.UI;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Trigger zone that displays a tutorial prompt on the shared panel while the
    /// player is inside. Place in levels to teach movement, combat entry, or
    /// chemistry puzzle mechanics.
    ///
    /// Two optional behaviors layered on top:
    ///   _oneShotFlag: when set, the trigger self-disables on Awake if the matching
    ///                 PlayerState flag is already true. Use for tutorials that should
    ///                 not replay after completion (FirstBattle, SpellTutorialBattle).
    ///   _lockMovementWhileInside: when true, calls PlayerController.SetTutorialMovementLocked
    ///                 on enter/exit. Use for the Tutorial_Advantaged zone in front of
    ///                 the spell-tutorial Meltspawn.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TutorialPromptTrigger : MonoBehaviour
    {
        [SerializeField, TextArea(2, 6)] private string _message = string.Empty;
        [SerializeField] private TutorialPromptPanelUI _panel;
        [SerializeField]
        [Tooltip("When set, this trigger disables itself on Awake if the matching PlayerState flag is already true.")]
        private OneShotTutorialFlag _oneShotFlag = OneShotTutorialFlag.None;
        [SerializeField]
        [Tooltip("When true, locks player movement and jump while the player is inside this zone. " +
                 "Attack stays enabled so the player can engage a nearby battle trigger.")]
        private bool _lockMovementWhileInside = false;
        [SerializeField]
        [Tooltip("When true, also blocks exploration attacks while this tutorial lock is active.")]
        private bool _lockAttackWhileInside;
        [SerializeField]
        [Tooltip("Required when _lockMovementWhileInside is true. Reference to the player's PlayerController.")]
        private PlayerController _playerController;
        [SerializeField]
        [Tooltip("Required when _lockAttackWhileInside is true.")]
        private PlayerExplorationAttack _playerAttack;

        private bool _playerLockActive;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void Awake()
        {
            if (_oneShotFlag == OneShotTutorialFlag.None) return;
            if (GameManager.Instance == null) return;
            if (TutorialOneShotFlagResolver.IsFlagSet(GameManager.Instance.PlayerState, _oneShotFlag))
                gameObject.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_panel != null) _panel.Show(_message);
            SetPlayerLock(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_panel != null) _panel.Hide();
            SetPlayerLock(false);
        }

        private void OnDisable()
        {
            SetPlayerLock(false);
        }

        public void ReleasePlayerLock()
        {
            if (_panel != null) _panel.Hide();
            SetPlayerLock(false);
        }

        private void SetPlayerLock(bool locked)
        {
            if (_playerLockActive == locked) return;
            _playerLockActive = locked;

            if (_lockMovementWhileInside && _playerController != null)
                _playerController.SetTutorialMovementLocked(locked);
            if (_lockAttackWhileInside && _playerAttack != null)
                _playerAttack.SetInputLocked(locked);
        }
    }
}
