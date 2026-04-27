using NUnit.Framework;
using UnityEngine;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Core.Tests
{
    public class GameManagerFirstDeathPendingTests
    {
        private GameObject _go;
        private GameManager _gm;
        private CharacterData _characterData;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GM");
            _gm = _go.AddComponent<GameManager>();
            _characterData = ScriptableObject.CreateInstance<CharacterData>();
            _characterData.baseMaxHP = 40;
            _characterData.baseMaxMP = 33;
            _characterData.baseATK = 5;
            _characterData.baseDEF = 3;
            _characterData.baseSPD = 4;
            _gm.SetPlayerCharacterDataForTests(_characterData);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_characterData);
        }

        [Test]
        public void Consume_WithoutPriorNotify_ReturnsFalse()
        {
            Assert.IsFalse(_gm.ConsumeFirstDeathPromptPending());
        }

        [Test]
        public void Notify_ThenConsume_ReturnsTrueOnce()
        {
            _gm.NotifyDiedAndRespawning();
            Assert.IsTrue(_gm.ConsumeFirstDeathPromptPending());
            Assert.IsFalse(_gm.ConsumeFirstDeathPromptPending(), "Second consume must return false.");
        }

        [Test]
        public void Notify_AfterFlagAlreadySet_DoesNotMarkPending()
        {
            _gm.PlayerState.MarkFirstDeathSeen();
            _gm.NotifyDiedAndRespawning();
            Assert.IsFalse(_gm.ConsumeFirstDeathPromptPending(),
                "If HasSeenFirstDeath is already true, no prompt should be queued.");
        }
    }
}
