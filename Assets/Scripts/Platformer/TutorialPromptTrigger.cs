using System.Collections;
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

        [SerializeField]
        [Tooltip("When true, entering this zone pauses the game (Time.timeScale = 0) and shows a " +
                 "Continue button; the player must click Continue or press Enter to resume. " +
                 "Requires Player Controller and Player Attack refs assigned. During the pause ALL " +
                 "input is frozen; after Continue the Lock Movement / Lock Attack flags above apply " +
                 "(set both to keep the player locked after Continue, e.g. a Surprised first-battle " +
                 "encounter where the enemy must reach the player first).")]
        private bool _pauseOnPrompt;
        [SerializeField]
        [Tooltip("Only meaningful when Pause On Prompt is true. When true, only the FIRST entry " +
                 "pauses; later entries show the prompt without pausing. When false, every entry pauses.")]
        private bool _pauseOnlyOnce = true;

        private readonly TutorialPauseGate _pauseGate = new TutorialPauseGate();

        private bool _playerLockActive;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void Awake()
        {
            if (_pauseOnPrompt && (_playerController == null || _playerAttack == null))
                Debug.LogError($"{name}: Pause On Prompt is enabled but Player Controller and/or " +
                               "Player Attack refs are not assigned. Gameplay input will not be " +
                               "locked while paused, risking an attack firing on Continue.", this);

            if (_oneShotFlag == OneShotTutorialFlag.None) return;
            if (GameManager.Instance == null) return;
            if (TutorialOneShotFlagResolver.IsFlagSet(GameManager.Instance.PlayerState, _oneShotFlag))
                gameObject.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            if (_pauseOnPrompt && _panel != null && _pauseGate.ShouldPause(_pauseOnlyOnce))
            {
                SetPauseInputLock(true);
                _panel.ShowAndPause(_message, OnPauseContinue);
                return;
            }

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

        private void OnPauseContinue()
        {
            // The panel has already restored Time.timeScale and hidden the prompt. Defer the
            // transition by one frame so the Enter press that activated Continue does not leak
            // into PlayerExplorationAttack's same-frame WasPerformedThisFrame() read.
            if (isActiveAndEnabled) StartCoroutine(ApplyPostPauseLockNextFrame());
            else ApplyPostPauseLock();
        }

        private IEnumerator ApplyPostPauseLockNextFrame()
        {
            yield return null;
            ApplyPostPauseLock();
        }

        private void ApplyPostPauseLock()
        {
            // Drop the full pause-time input freeze, then fall through to the normal "while inside"
            // lock driven by _lockMovementWhileInside / _lockAttackWhileInside. This lets a trigger
            // keep the player frozen after Continue (e.g. the First Battle Surprised encounter,
            // where the enemy must reach the player first) while routing through SetPlayerLock so
            // OnTriggerExit2D / OnDisable still release it. With both flags off, the player is freed
            // (today's behavior). The two synchronous calls have no frame boundary between them, so
            // no attack input is read in the unlocked gap.
            SetPauseInputLock(false);
            SetPlayerLock(true);
        }

        private void SetPauseInputLock(bool locked)
        {
            if (_playerController != null) _playerController.SetTutorialMovementLocked(locked);
            if (_playerAttack != null) _playerAttack.SetInputLocked(locked);
        }
    }
}
