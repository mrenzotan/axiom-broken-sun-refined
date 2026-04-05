using System;
using System.Collections.Generic;
using Axiom.Data;

namespace Axiom.Voice
{
    /// <summary>
    /// Stateless utility for matching a Vosk JSON result string against the player's
    /// unlocked spell list. Contains no Unity types — fully testable in Edit Mode.
    ///
    /// Vosk final results: {"text": "hydrogen blast"}
    /// Vosk partial results: {"partial": "hydrogen"}  ← no "text" key → ExtractTextField returns ""
    /// </summary>
    public static class SpellResultMatcher
    {
        /// <summary>
        /// Parses the <c>"text"</c> field from a Vosk result JSON string.
        /// Returns <see cref="string.Empty"/> when no <c>"text"</c> key is present
        /// (e.g. partial results) or when the value is empty/whitespace.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="voskJson"/> is null.</exception>
        public static string ExtractTextField(string voskJson)
        {
            if (voskJson == null) throw new ArgumentNullException(nameof(voskJson));

            // Vosk final results always use {"text": "..."} — manual parsing avoids a JSON library dependency.
            const string key = "\"text\"";
            int keyIdx = voskJson.IndexOf(key, StringComparison.Ordinal);
            if (keyIdx < 0) return string.Empty;

            int colonIdx = voskJson.IndexOf(':', keyIdx + key.Length);
            if (colonIdx < 0) return string.Empty;

            int openQuote = voskJson.IndexOf('"', colonIdx + 1);
            if (openQuote < 0) return string.Empty;

            int closeQuote = voskJson.IndexOf('"', openQuote + 1);
            if (closeQuote < 0) return string.Empty;

            return voskJson.Substring(openQuote + 1, closeQuote - openQuote - 1).Trim();
        }

        /// <summary>
        /// Returns the first <see cref="SpellData"/> in <paramref name="unlockedSpells"/> whose
        /// <c>spellName</c> matches the <c>"text"</c> field in <paramref name="voskJson"/>
        /// (case-insensitive, trimmed). Returns <c>null</c> when:
        /// <list type="bullet">
        ///   <item>the spell list is empty</item>
        ///   <item>the JSON contains no <c>"text"</c> key (e.g. partial result)</item>
        ///   <item>the recognized text is empty or whitespace</item>
        ///   <item>no spell name matches</item>
        /// </list>
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="voskJson"/> or <paramref name="unlockedSpells"/> is null.
        /// </exception>
        public static SpellData Match(string voskJson, IReadOnlyList<SpellData> unlockedSpells)
        {
            if (voskJson == null)        throw new ArgumentNullException(nameof(voskJson));
            if (unlockedSpells == null)  throw new ArgumentNullException(nameof(unlockedSpells));
            if (unlockedSpells.Count == 0) return null;

            string recognized = ExtractTextField(voskJson);
            if (string.IsNullOrWhiteSpace(recognized)) return null;

            foreach (SpellData spell in unlockedSpells)
            {
                if (string.Equals(spell.spellName, recognized, StringComparison.OrdinalIgnoreCase))
                    return spell;
            }

            return null;
        }
    }
}
