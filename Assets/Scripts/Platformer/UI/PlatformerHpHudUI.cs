using Axiom.Core;
using TMPro;
using UnityEngine;

namespace Axiom.Platformer.UI
{
    /// <summary>
    /// HUD display for the player's current HP in platformer scenes. Polls
    /// GameManager.Instance.PlayerState each frame and refreshes the TMP label
    /// when HP changes. PlayerState exposes no change event, so polling is the
    /// simplest viable approach.
    /// </summary>
    public class PlatformerHpHudUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("TextMeshProUGUI element that renders the HP line.")]
        private TMP_Text _hpLabel;

        private int _lastRenderedHp = -1;
        private int _lastRenderedMaxHp = -1;

        private void Update()
        {
            if (_hpLabel == null) return;
            if (GameManager.Instance == null) return;

            PlayerState state = GameManager.Instance.PlayerState;
            if (state == null) return;

            if (state.CurrentHp == _lastRenderedHp && state.MaxHp == _lastRenderedMaxHp)
                return;

            _hpLabel.text = PlatformerHpHudFormatter.Format(state.CurrentHp, state.MaxHp);
            _lastRenderedHp = state.CurrentHp;
            _lastRenderedMaxHp = state.MaxHp;
        }
    }
}
