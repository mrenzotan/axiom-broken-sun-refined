using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// A single dialogue sequence from an NPC. Holds speaker name, ordered dialogue lines,
    /// and an optional portrait sprite.
    ///
    /// Created as a ScriptableObject asset so dialogue content can be authored in the
    /// Inspector without changing code.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogueData", menuName = "Axiom/Data/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        [Tooltip("Name of the speaker (e.g., 'Sentinel', 'Phasekeeper').")]
        public string speakerName = "NPC";

        [Tooltip("Optional portrait sprite shown while this dialogue plays.")]
        public Sprite portraitSprite;

        [Tooltip("Ordered list of dialogue lines. Each entry is one line of text.")]
        public string[] dialogueLines = System.Array.Empty<string>();

        /// <summary>Read-only line count for validation.</summary>
        public int LineCount => dialogueLines?.Length ?? 0;
    }
}
