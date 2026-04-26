using Axiom.Core;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Polls PlayerState.CurrentHp each frame while on the platformer side.
    /// On HP reaching zero, delegates to <see cref="GameManager.RespawnAtLastCheckpoint"/>
    /// (heals + teleports to the last save point) or loads MainMenu when no checkpoint
    /// has been activated.
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

            if (outcome == PlayerDeathOutcome.RespawnAtLastCheckpoint &&
                GameManager.Instance.RespawnAtLastCheckpoint(_transitionStyle))
            {
                return;
            }

            transition.BeginTransition(_gameOverSceneName, _transitionStyle);
        }
    }
}
