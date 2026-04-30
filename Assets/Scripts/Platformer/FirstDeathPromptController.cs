using System.Collections;
using Axiom.Core;
using Axiom.Platformer.UI;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// On scene load, consumes the GameManager._firstDeathPromptPending signal and
    /// shows a one-shot prompt. Sets PlayerState.HasSeenFirstDeath so subsequent
    /// deaths don't re-queue the prompt. Auto-hides after _displaySeconds.
    /// Place one instance in any level where the prompt should appear (Level_1-1).
    /// </summary>
    public class FirstDeathPromptController : MonoBehaviour
    {
        [SerializeField] private TutorialPromptPanelUI _panel;
        [SerializeField, TextArea]
        private string _message =
            "Defeated. You respawned at your last lit torch. Light new torches to save your progress — the first touch on each also fully restores HP and MP.";
        [SerializeField] private float _displaySeconds = 6f;

        private void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            // Check _panel BEFORE consuming the pending flag — ConsumeFirstDeathPromptPending
            // has a side effect (clears the flag), so a missing panel must not silently
            // discard the pending signal.
            if (_panel == null) return;
            if (!gm.ConsumeFirstDeathPromptPending()) return;
            gm.PlayerState.MarkFirstDeathSeen();
            gm.PersistToDisk();
            StartCoroutine(ShowAfterPhysics());
        }

        private IEnumerator ShowAfterPhysics()
        {
            // Defer Show() until after the player's Rigidbody has been positioned at the
            // respawn checkpoint AND the next physics step has fired any
            // TutorialPromptTrigger.OnTriggerEnter2D the spawn-overlap might queue.
            // Without this, a Tutorial_Movement collider sitting on top of the spawn
            // point overwrites our message a frame after Start() runs.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            if (_panel == null) yield break;
            _panel.Show(_message);
            Invoke(nameof(Hide), _displaySeconds);
        }

        private void Hide()
        {
            if (_panel != null) _panel.Hide();
        }
    }
}
