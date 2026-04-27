using Axiom.Core;
using Axiom.Platformer.UI;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Subscribes to HazardTrigger.OnPlayerFirstHitFrame. On the first event after
    /// the controller is enabled, shows a one-shot prompt and sets
    /// PlayerState.HasSeenFirstSpikeHit. No collider needed — this is a passive listener.
    /// Place one instance in any level where the spike prompt should appear (Level_1-1).
    /// </summary>
    public class FirstSpikeHitPromptController : MonoBehaviour
    {
        [SerializeField] private TutorialPromptPanelUI _panel;
        [SerializeField, TextArea]
        private string _message =
            "Spikes hurt on touch and keep ticking while you stand on them. Move off quickly.";
        [SerializeField] private float _displaySeconds = 6f;

        private void OnEnable()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.PlayerState.HasSeenFirstSpikeHit) return;
            HazardTrigger.OnPlayerFirstHitFrame += HandleHit;
        }

        private void OnDisable()
        {
            HazardTrigger.OnPlayerFirstHitFrame -= HandleHit;
        }

        private void HandleHit()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.PlayerState.HasSeenFirstSpikeHit) return;
            if (_panel == null) return;
            _panel.Show(_message);
            gm.PlayerState.MarkFirstSpikeHitSeen();
            gm.PersistToDisk();
            Invoke(nameof(Hide), _displaySeconds);
            HazardTrigger.OnPlayerFirstHitFrame -= HandleHit;
        }

        private void Hide()
        {
            if (_panel != null) _panel.Hide();
        }
    }
}
