using System.Collections.Generic;
using Axiom.Data;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Voice
{
    public class SpellVocabularyManagerTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static SpellData MakeSpell(string name)
        {
            var so = ScriptableObject.CreateInstance<SpellData>();
            so.spellName = name;
            return so;
        }

        // ── BuildGrammarJson ───────────────────────────────────────────────────

        [Test]
        public void BuildGrammarJson_EmptyList_ReturnsNull()
        {
            string result = SpellVocabularyManager.BuildGrammarJson(
                new List<SpellData>());

            Assert.IsNull(result);
        }

        [Test]
        public void BuildGrammarJson_SingleSpell_ReturnsJsonArrayWithThatName()
        {
            var spell = MakeSpell("hydrogen blast");

            string result = SpellVocabularyManager.BuildGrammarJson(
                new List<SpellData> { spell });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.StartsWith("["), $"Grammar must start with '[', got: {result}");
            Assert.IsTrue(result.EndsWith("]"),   $"Grammar must end with ']', got: {result}");
            StringAssert.Contains("\"hydrogen blast\"", result);
        }

        [Test]
        public void BuildGrammarJson_MultipleSpells_ContainsAllNames()
        {
            var spells = new List<SpellData>
            {
                MakeSpell("hydrogen blast"),
                MakeSpell("acid rain"),
                MakeSpell("ember strike"),
            };

            string result = SpellVocabularyManager.BuildGrammarJson(spells);

            Assert.IsNotNull(result);
            StringAssert.Contains("\"hydrogen blast\"", result);
            StringAssert.Contains("\"acid rain\"",      result);
            StringAssert.Contains("\"ember strike\"",   result);
        }

        [Test]
        public void BuildGrammarJson_SpellNameWithEmbeddedQuote_EscapesCorrectly()
        {
            // Spell names with embedded quotes must be escaped so Vosk receives valid JSON.
            var spell = MakeSpell("alchemist\"s fire");

            string result = SpellVocabularyManager.BuildGrammarJson(
                new List<SpellData> { spell });

            Assert.IsNotNull(result);
            // The embedded " must appear as \" in the output
            StringAssert.Contains("\\\"", result,
                $"Embedded quote must be escaped in JSON output, got: {result}");
        }

        [Test]
        public void BuildGrammarJson_NullList_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => SpellVocabularyManager.BuildGrammarJson(null));
        }

        // ── RebuildRecognizerAsync ─────────────────────────────────────────────
        // Only the empty-list path is testable in Edit Mode — it exits before
        // touching the Vosk Model, so we can safely pass null for model here.

        [Test]
        public async System.Threading.Tasks.Task RebuildRecognizerAsync_EmptyList_ReturnsNull()
        {
            // model is irrelevant when the list is empty — the method returns before
            // ever touching it, so passing null here is intentional.
            Vosk.VoskRecognizer result = await SpellVocabularyManager.RebuildRecognizerAsync(
                model: null,
                sampleRate: 16000f,
                unlockedSpells: new List<SpellData>());

            Assert.IsNull(result);
        }
    }
}
