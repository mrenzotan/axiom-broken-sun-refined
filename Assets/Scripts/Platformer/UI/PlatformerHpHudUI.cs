using Axiom.Battle;
using Axiom.Core;
using UnityEngine;

namespace Axiom.Platformer.UI
{
    /// <summary>
    /// Drives player HP/MP bars in the platformer HUD. Polls GameManager.PlayerState
    /// each frame and delegates to HealthBarUI for bar rendering and gradient color.
    /// </summary>
    public class PlatformerHpHudUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("HealthBarUI for the player's HP bar (with gradient fill).")]
        private HealthBarUI _hpBar;

        [SerializeField]
        [Tooltip("HealthBarUI for the player's MP bar.")]
        private HealthBarUI _mpBar;

        private int _lastHp = -1;
        private int _lastMaxHp = -1;
        private int _lastMp = -1;
        private int _lastMaxMp = -1;

        private void Update()
        {
            if (GameManager.Instance == null) return;

            PlayerState state = GameManager.Instance.PlayerState;
            if (state == null) return;

            if (_hpBar != null)
            {
                if (state.CurrentHp != _lastHp || state.MaxHp != _lastMaxHp)
                {
                    _hpBar.SetHP(state.CurrentHp, state.MaxHp);
                    _lastHp = state.CurrentHp;
                    _lastMaxHp = state.MaxHp;
                }
            }

            if (_mpBar != null)
            {
                if (state.CurrentMp != _lastMp || state.MaxMp != _lastMaxMp)
                {
                    _mpBar.SetMP(state.CurrentMp, state.MaxMp);
                    _lastMp = state.CurrentMp;
                    _lastMaxMp = state.MaxMp;
                }
            }
        }
    }
}
