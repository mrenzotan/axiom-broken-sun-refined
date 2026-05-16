using System.Collections.Generic;
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
        [System.Serializable]
        public class ParsedLine
        {
            public string speakerName = "";
            public string lineText = "";
        }

        [System.Serializable]
        public class DialogueLine
        {
            [Tooltip("Line text shown in the main dialogue area.")]
            public string lineText = "";

            [Tooltip("Optional per-line speaker override. Leave empty to use DialogueData.speakerName.")]
            public string speakerNameOverride = "";
        }

        [System.Serializable]
        public class SpeakerPortrait
        {
            [Tooltip("Speaker name (e.g., 'Lois', 'Kaelen', 'Sentinel').")]
            public string speakerName = "";

            [Tooltip("Portrait sprite for this speaker.")]
            public Sprite portraitSprite;
        }

        [HideInInspector]
        [Tooltip("Name of the speaker (e.g., 'Sentinel', 'Phasekeeper').")]
        public string speakerName = "NPC";

        [Tooltip("Map of speaker names to their portrait sprites. Used in turn-based dialogue.")]
        public List<SpeakerPortrait> speakerPortraits = new List<SpeakerPortrait>();

        [Header("Raw Dialogue (Option 3)")]
        [Tooltip("Paste dialogue here using format: Speaker: \"Line text\". One line per entry.")]
        [TextArea(5, 20)]
        public string rawDialogueText = "";

        [Tooltip("Optional text asset containing dialogue in Speaker: \"Line text\" format.")]
        public TextAsset rawDialogueAsset;

        [HideInInspector]
        [Tooltip("Structured dialogue lines with optional player responses.")]
        public List<DialogueLine> lines = new List<DialogueLine>();

        [HideInInspector]
        [Tooltip("Legacy ordered list of dialogue lines. Each entry is one line of text.")]
        public string[] dialogueLines = System.Array.Empty<string>();

        /// <summary>Read-only line count for validation.</summary>
        public int LineCount
        {
            get
            {
                List<ParsedLine> parsed = GetParsedLines();
                return parsed.Count;
            }
        }

        public bool HasStructuredLines => lines != null && lines.Count > 0;

        public List<ParsedLine> GetParsedLines()
        {
            List<ParsedLine> parsed = new List<ParsedLine>();

            string sourceText = rawDialogueText;
            if (rawDialogueAsset != null && !string.IsNullOrWhiteSpace(rawDialogueAsset.text))
                sourceText = rawDialogueAsset.text;

            if (!string.IsNullOrWhiteSpace(sourceText))
            {
                ParseRawDialogue(sourceText, parsed);
                return parsed;
            }

            // Fallback: structured lines without response text.
            if (lines != null && lines.Count > 0)
            {
                foreach (DialogueLine line in lines)
                {
                    ParsedLine entry = new ParsedLine
                    {
                        speakerName = string.IsNullOrWhiteSpace(line.speakerNameOverride)
                            ? speakerName
                            : line.speakerNameOverride,
                        lineText = line.lineText
                    };

                    parsed.Add(entry);
                }

                return parsed;
            }

            // Legacy fallback: simple list of lines (single speaker).
            if (dialogueLines != null)
            {
                foreach (string line in dialogueLines)
                {
                    parsed.Add(new ParsedLine
                    {
                        speakerName = speakerName,
                        lineText = line
                    });
                }
            }

            return parsed;
        }

        private void ParseRawDialogue(string rawText, List<ParsedLine> output)
        {
            string[] linesRaw = rawText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            foreach (string rawLine in linesRaw)
            {
                string trimmed = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                if (trimmed.StartsWith("*"))
                    continue;

                int colonIndex = trimmed.IndexOf(':');
                if (colonIndex <= 0)
                    continue;

                string speaker = trimmed.Substring(0, colonIndex).Trim();
                string text = trimmed.Substring(colonIndex + 1).Trim();

                // Remove surrounding quotes if present.
                if (text.StartsWith("\"") && text.EndsWith("\""))
                    text = text.Substring(1, text.Length - 2);

                output.Add(new ParsedLine
                {
                    speakerName = speaker,
                    lineText = text
                });
            }
        }

        /// <summary>
        /// Looks up a speaker's portrait sprite by name. Returns null if not found.
        /// </summary>
        public Sprite GetPortraitForSpeaker(string speakerName)
        {
            if (speakerPortraits == null || speakerPortraits.Count == 0)
                return null;

            foreach (SpeakerPortrait sp in speakerPortraits)
            {
                if (sp.speakerName.Equals(speakerName, System.StringComparison.OrdinalIgnoreCase))
                    return sp.portraitSprite;
            }

            return null;
        }
    }
}
