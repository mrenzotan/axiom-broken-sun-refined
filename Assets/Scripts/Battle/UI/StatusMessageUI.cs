using TMPro;
using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour wrapper for StatusMessageQueue.
    /// Attach to the MessageLog GameObject in the Battle Canvas.
    /// Call Post() to display battle narration lines.
    /// </summary>
    public class StatusMessageUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("TMP text component that displays the message log.")]
        private TMP_Text _text;

        private readonly StatusMessageQueue _queue = new StatusMessageQueue();

        /// <summary>Posts a message to the 2-line rolling log.</summary>
        public void Post(string message)
        {
            _queue.Post(message);
            _text.text = _queue.GetDisplay();
        }
    }
}
