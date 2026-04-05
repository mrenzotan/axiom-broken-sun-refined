using System.Collections.Generic;
using Axiom.Data;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Voice
{
    public class SpellResultMatcherTests
    {
        private readonly List<SpellData> _created = new List<SpellData>();

        // ── Helpers ────────────────────────────────────────────────────────────────

        private SpellData MakeSpell(string name)
        {
            var so = ScriptableObject.CreateInstance<SpellData>();
            so.spellName = name;
            _created.Add(so);
            return so;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (SpellData so in _created)
                UnityEngine.Object.DestroyImmediate(so);
            _created.Clear();
        }

        // ── ExtractTextField ───────────────────────────────────────────────────────

        [Test]
        public void ExtractTextField_NullJson_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => SpellResultMatcher.ExtractTextField(null));
        }

        [Test]
        public void ExtractTextField_FinalResultJson_ReturnsSpellText()
        {
            string result = SpellResultMatcher.ExtractTextField("{\"text\": \"hydrogen blast\"}");
            Assert.AreEqual("hydrogen blast", result);
        }

        [Test]
        public void ExtractTextField_PartialResultJson_ReturnsEmpty()
        {
            // Partial results have no "text" key — must not be treated as a match.
            string result = SpellResultMatcher.ExtractTextField("{\"partial\": \"hydrogen\"}");
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void ExtractTextField_EmptyTextField_ReturnsEmpty()
        {
            string result = SpellResultMatcher.ExtractTextField("{\"text\": \"\"}");
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void ExtractTextField_WhitespaceTextField_ReturnsEmpty()
        {
            // Vosk can produce "  " — trimmed result is empty.
            string result = SpellResultMatcher.ExtractTextField("{\"text\": \"   \"}");
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void ExtractTextField_EmptyJsonString_ReturnsEmpty()
        {
            string result = SpellResultMatcher.ExtractTextField("{}");
            Assert.AreEqual(string.Empty, result);
        }

        // ── Match ──────────────────────────────────────────────────────────────────

        [Test]
        public void Match_NullJson_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => SpellResultMatcher.Match(null, new List<SpellData>()));
        }

        [Test]
        public void Match_NullSpellList_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => SpellResultMatcher.Match("{\"text\": \"hydrogen blast\"}", null));
        }

        [Test]
        public void Match_EmptySpellList_ReturnsNull()
        {
            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"hydrogen blast\"}",
                new List<SpellData>());

            Assert.IsNull(result);
        }

        [Test]
        public void Match_KnownSpell_ExactCase_ReturnsSpellData()
        {
            var spell = MakeSpell("hydrogen blast");

            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"hydrogen blast\"}",
                new List<SpellData> { spell });

            Assert.AreSame(spell, result);
        }

        [Test]
        public void Match_KnownSpell_UppercaseRecognized_ReturnsSpellData()
        {
            // Vosk occasionally returns uppercase — matching must be case-insensitive.
            var spell = MakeSpell("hydrogen blast");

            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"HYDROGEN BLAST\"}",
                new List<SpellData> { spell });

            Assert.AreSame(spell, result);
        }

        [Test]
        public void Match_KnownSpell_MixedCaseRecognized_ReturnsSpellData()
        {
            var spell = MakeSpell("acid rain");

            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"Acid Rain\"}",
                new List<SpellData> { spell });

            Assert.AreSame(spell, result);
        }

        [Test]
        public void Match_UnknownWord_ReturnsNull()
        {
            var spell = MakeSpell("hydrogen blast");

            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"fire ball\"}",
                new List<SpellData> { spell });

            Assert.IsNull(result);
        }

        [Test]
        public void Match_EmptyTextField_ReturnsNull()
        {
            var spell = MakeSpell("hydrogen blast");

            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"\"}",
                new List<SpellData> { spell });

            Assert.IsNull(result);
        }

        [Test]
        public void Match_PartialResultJson_ReturnsNull()
        {
            // Partial results must never dispatch a spell.
            var spell = MakeSpell("hydrogen blast");

            SpellData result = SpellResultMatcher.Match(
                "{\"partial\": \"hydrogen blast\"}",
                new List<SpellData> { spell });

            Assert.IsNull(result);
        }

        [Test]
        public void Match_MultipleSpells_ReturnsCorrectOne()
        {
            var spellA = MakeSpell("hydrogen blast");
            var spellB = MakeSpell("acid rain");
            var spellC = MakeSpell("ember strike");

            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"acid rain\"}",
                new List<SpellData> { spellA, spellB, spellC });

            Assert.AreSame(spellB, result);
        }
    }
}
