using Axiom.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Axiom.Platformer
{
    /// <summary>
    /// Polls PlayerState.CurrentHp each frame while on the platformer side.
    /// On HP reaching zero, delegates to PlayerDeathResolver and either reloads
    /// the current scene (respawn) or loads MainMenu (game over).
    /// Heals to full on respawn so the player has a recoverable slate.
    /// </summary>
    public class PlayerDeathHandler : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Scene loaded when no checkpoint has been activated — game over path.")]
        private string _gameOverSceneName = "MainMenu";

        [SerializeField]
        [Tooltip("Visual style for respawn/game-over transitions.")]
        private TransitionStyle _transitionStyle = TransitionStyle.WhiteFlash;

        private bool _dispatched;

        private void Update()
        {
            if (_dispatched) return;
            if (GameManager.Instance == null) return;

            PlayerState state = GameManager.Instance.PlayerState;
            if (state == null) return;

            PlayerDeathOutcome outcome = PlayerDeathResolver.Resolve(
                currentHp: state.CurrentHp,
                activatedCheckpointIds: state.ActivatedCheckpointIds);

            if (outcome == PlayerDeathOutcome.None) return;

            SceneTransitionController transition = GameManager.Instance.SceneTransition;
            if (transition == null)
            {
                Debug.LogWarning("[PlayerDeathHandler] SceneTransition missing — death dispatch skipped.", this);
                return;
            }

            _dispatched = true;

            if (outcome == PlayerDeathOutcome.RespawnAtLastCheckpoint)
            {
                state.SetCurrentHp(state.MaxHp);
                string sceneName = SceneManager.GetActiveScene().name;
                transition.BeginTransition(sceneName, _transitionStyle);
                return;
            }

            transition.BeginTransition(_gameOverSceneName, _transitionStyle);
        }
    }
}
