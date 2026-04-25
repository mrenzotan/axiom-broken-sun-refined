using Axiom.Core;
using Axiom.Platformer.UI;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Place in Level_1-4 alongside the boss. On scene ready (after a returning
    /// post-battle transition), checks whether the boss's enemy id is present in
    /// GameManager.DefeatedEnemyIds. If so, shows the ChapterCompleteCardUI.
    /// </summary>
    public class BossVictoryTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Must match the EnemyController.EnemyId on the boss instance in this scene.")]
        private string _bossEnemyId = string.Empty;

        [SerializeField]
        [Tooltip("Card to show on confirmed boss victory.")]
        private ChapterCompleteCardUI _completeCard;

        private bool _shown;

        private void Start()
        {
            CheckAndShow();

            if (GameManager.Instance != null)
                GameManager.Instance.OnSceneReady += CheckAndShow;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnSceneReady -= CheckAndShow;
        }

        private void CheckAndShow()
        {
            if (_shown) return;
            if (GameManager.Instance == null) return;

            bool victorious = BossVictoryChecker.IsVictorious(
                defeatedEnemyIds: GameManager.Instance.DefeatedEnemyIds,
                bossEnemyId: _bossEnemyId);

            if (!victorious) return;

            _shown = true;
            if (_completeCard != null) _completeCard.Show();
        }
    }
}
