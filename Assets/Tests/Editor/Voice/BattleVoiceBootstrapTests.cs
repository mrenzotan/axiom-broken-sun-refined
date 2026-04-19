using System.Reflection;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Voice.Tests
{
    public class BattleVoiceBootstrapTests
    {
        private GameObject _bootstrapGo;
        private BattleVoiceBootstrap _bootstrap;
        private GameObject _micGo;
        private GameObject _spellGo;

        [SetUp]
        public void SetUp()
        {
            _bootstrapGo = new GameObject("TestBootstrap");
            _bootstrap = _bootstrapGo.AddComponent<BattleVoiceBootstrap>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_bootstrapGo != null) Object.DestroyImmediate(_bootstrapGo);
            if (_micGo != null) Object.DestroyImmediate(_micGo);
            if (_spellGo != null) Object.DestroyImmediate(_spellGo);
        }

        // ── ValidateRequiredReferences ──────────────────────────────────────────────

        [Test]
        public void ValidateRequiredReferences_AllNull_ReturnsFalseAndReportsMissingField()
        {
            bool result = _bootstrap.ValidateRequiredReferences(out string missingRefName);

            Assert.IsFalse(result, "Should return false when required refs are null.");
            Assert.AreEqual("_microphoneInputHandler", missingRefName,
                "Should report the first missing field in declaration order.");
        }

        [Test]
        public void ValidateRequiredReferences_MicrophoneInputHandlerMissing_ReturnsFalse()
        {
            _spellGo = new GameObject("SpellController");
            SetPrivateField(_bootstrap, "_spellCastController",
                _spellGo.AddComponent<SpellCastController>());

            bool result = _bootstrap.ValidateRequiredReferences(out string missingRefName);

            Assert.IsFalse(result);
            Assert.AreEqual("_microphoneInputHandler", missingRefName);
        }

        [Test]
        public void ValidateRequiredReferences_SpellCastControllerMissing_ReturnsFalse()
        {
            _micGo = new GameObject("MicHandler");
            SetPrivateField(_bootstrap, "_microphoneInputHandler",
                _micGo.AddComponent<MicrophoneInputHandler>());

            bool result = _bootstrap.ValidateRequiredReferences(out string missingRefName);

            Assert.IsFalse(result);
            Assert.AreEqual("_spellCastController", missingRefName);
        }

        [Test]
        public void ValidateRequiredReferences_AllPresent_ReturnsTrueAndNoMissingName()
        {
            _micGo = new GameObject("MicHandler");
            _spellGo = new GameObject("SpellController");
            SetPrivateField(_bootstrap, "_microphoneInputHandler",
                _micGo.AddComponent<MicrophoneInputHandler>());
            SetPrivateField(_bootstrap, "_spellCastController",
                _spellGo.AddComponent<SpellCastController>());

            bool result = _bootstrap.ValidateRequiredReferences(out string missingRefName);

            Assert.IsTrue(result, "Should return true when both required refs are assigned.");
            Assert.IsNull(missingRefName, "No missing ref name when all present.");
        }

        // ── Reflection helper ───────────────────────────────────────────────────────

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(field,
                $"Private field '{fieldName}' not found on {target.GetType().Name}. " +
                "Check field name and access modifier.");

            field.SetValue(target, value);
        }
    }
}
