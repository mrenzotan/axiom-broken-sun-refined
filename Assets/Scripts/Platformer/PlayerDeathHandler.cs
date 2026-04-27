using System.Collections;
using Axiom.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Platformer
{
    /// <summary>
    /// Polls PlayerState.CurrentHp each frame while on the platformer side.
    /// On HP reaching zero:
    ///   1. Resolves the death outcome via <see cref="PlayerDeathResolver"/>.
    ///   2. Disables the PlayerController so input stops immediately.
    ///   3. Triggers the player's Defeat animation.
    ///   4. Waits for <see cref="_deathAnimSeconds"/>.
    ///   5. Shows the defeat panel.
    ///   6. On <see cref="_continueButton"/> click: respawns at the last
    ///      checkpoint or loads <see cref="_gameOverSceneName"/>.
    /// </summary>
    public class PlayerDeathHandler : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Scene loaded when no checkpoint has been activated.")]
        private string _gameOverSceneName = "MainMenu";

        [SerializeField]
        [Tooltip("Seconds after HP hits zero before the defeat panel appears.")]
        private float _deathAnimSeconds = 1.2f;

        [SerializeField]
        [Tooltip("Root GameObject of the defeat panel. Hidden initially by the Editor, shown on death.")]
        private GameObject _defeatPanel;

        [SerializeField]
        [Tooltip("Button inside the defeat panel that triggers checkpoint respawn.")]
        private Button _continueButton;

        [SerializeField]
        [Tooltip("Scene transition style used for both checkpoint respawn and game-over scene load.")]
        private TransitionStyle _transitionStyle = TransitionStyle.BlackFade;

        private bool _dispatched;
        private bool _listening;
        private PlayerDeathOutcome _outcome;

        private void Update()
        {
            if (_dispatched) return;
            if (GameManager.Instance == null) return;

            PlayerState state = GameManager.Instance.PlayerState;
            if (state == null) return;

            _outcome = PlayerDeathResolver.Resolve(
                currentHp: state.CurrentHp,
                activatedCheckpointIds: state.ActivatedCheckpointIds);

            if (_outcome == PlayerDeathOutcome.None) return;

            _dispatched = true;

            if (_outcome == PlayerDeathOutcome.RespawnAtLastCheckpoint)
                GameManager.Instance.NotifyDiedAndRespawning();

            StartCoroutine(DeathSequence());
        }

        private IEnumerator DeathSequence()
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null)
            {
                controller.enabled = false;

                Animator animator = controller.GetComponentInChildren<Animator>();
                if (animator != null)
                    animator.SetTrigger("Defeat");
            }

            yield return new WaitForSeconds(_deathAnimSeconds);

            ShowDefeatPanel();
        }

        private void ShowDefeatPanel()
        {
            if (_defeatPanel == null) return;

            if (!_listening && _continueButton != null)
            {
                _continueButton.onClick.AddListener(HandleContinue);
                _listening = true;
            }

            _defeatPanel.SetActive(true);
        }

        private void HandleContinue()
        {
            if (_continueButton != null && _listening)
            {
                _continueButton.onClick.RemoveListener(HandleContinue);
                _listening = false;
            }

            if (_defeatPanel != null)
                _defeatPanel.SetActive(false);

            if (_outcome == PlayerDeathOutcome.RespawnAtLastCheckpoint &&
                GameManager.Instance.RespawnAtLastCheckpoint(_transitionStyle))
            {
                return;
            }

            SceneTransitionController transition = GameManager.Instance.SceneTransition;
            if (transition != null)
                transition.BeginTransition(_gameOverSceneName, _transitionStyle);
            else
                Debug.LogWarning("[PlayerDeathHandler] SceneTransition missing — game over skipped.", this);
        }
    }
}
