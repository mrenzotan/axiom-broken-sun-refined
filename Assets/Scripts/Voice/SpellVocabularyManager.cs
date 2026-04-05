using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Axiom.Data;
using Vosk;

namespace Axiom.Voice
{
    /// <summary>
    /// Stateless service that converts the player's unlocked spell list into a
    /// Vosk-compatible JSON grammar string and creates <see cref="VoskRecognizer"/>
    /// instances off the Unity main thread.
    ///
    /// No MonoBehaviour. No Unity lifecycle. All methods are thread-safe.
    ///
    /// Usage pattern on spell unlock:
    ///   1. Stop the current <see cref="VoskRecognizerService"/> and dispose its recognizer.
    ///   2. Await <see cref="RebuildRecognizerAsync"/> with the new unlocked spell list.
    ///   3. If the result is non-null, construct a new <see cref="VoskRecognizerService"/>
    ///      with it and call Start().
    ///   4. If the result is null (empty spell set), do not start recognition.
    /// </summary>
    public class SpellVocabularyManager
    {
        /// <summary>
        /// Converts the unlocked spell list into a Vosk-compatible JSON grammar string.
        /// Returns <c>null</c> when the list is empty — the caller must skip recognition
        /// in this case (do not pass null to <see cref="VoskRecognizer"/>).
        /// </summary>
        /// <param name="unlockedSpells">The player's currently unlocked spells.</param>
        /// <returns>
        /// A JSON array string, e.g. <c>["hydrogen blast","acid rain"]</c>,
        /// or <c>null</c> if the list is empty.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="unlockedSpells"/> is null.</exception>
        public static string BuildGrammarJson(IReadOnlyList<SpellData> unlockedSpells)
        {
            if (unlockedSpells == null) throw new ArgumentNullException(nameof(unlockedSpells));
            if (unlockedSpells.Count == 0) return null;

            IEnumerable<string> escaped = unlockedSpells.Select(s =>
                "\"" + s.spellName
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                + "\"");

            return "[" + string.Join(",", escaped) + "]";
        }

        /// <summary>
        /// Creates a new <see cref="VoskRecognizer"/> on a background thread using the
        /// Vosk grammar built from <paramref name="unlockedSpells"/>.
        /// Returns <c>null</c> when <paramref name="unlockedSpells"/> is empty —
        /// the caller must not start recognition in this case.
        ///
        /// The caller is responsible for stopping and disposing the previous
        /// <see cref="VoskRecognizerService"/> before using the returned recognizer.
        /// </summary>
        /// <param name="model">The already-loaded Vosk <see cref="Model"/>.</param>
        /// <param name="sampleRate">Microphone sample rate in Hz (typically 16000).</param>
        /// <param name="unlockedSpells">The player's currently unlocked spells.</param>
        /// <returns>
        /// A <see cref="Task{VoskRecognizer}"/> that completes on a background thread.
        /// The result is <c>null</c> when the spell list is empty.
        /// </returns>
        public static Task<VoskRecognizer> RebuildRecognizerAsync(
            Model model,
            float sampleRate,
            IReadOnlyList<SpellData> unlockedSpells)
        {
            if (unlockedSpells == null) throw new ArgumentNullException(nameof(unlockedSpells));

            // Check empty BEFORE checking model — an empty spell set returns null
            // regardless of whether a model is available.
            string grammarJson = BuildGrammarJson(unlockedSpells);
            if (grammarJson == null) return Task.FromResult<VoskRecognizer>(null);

            if (model == null) throw new ArgumentNullException(nameof(model));

            // VoskRecognizer construction applies the grammar to the model — potentially
            // slow. Task.Run keeps the Unity main thread unblocked.
            return Task.Run(() => new VoskRecognizer(model, sampleRate, grammarJson));
        }
    }
}
