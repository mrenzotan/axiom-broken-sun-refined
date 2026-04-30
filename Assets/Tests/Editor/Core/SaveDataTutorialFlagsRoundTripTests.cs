using NUnit.Framework;
using UnityEngine;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Core.Tests
{
    public class SaveDataTutorialFlagsRoundTripTests
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
        public void BuildSaveData_WritesAllFourFlagsTrue()
        {
            _gm.PlayerState.MarkFirstDeathSeen();
            _gm.PlayerState.MarkFirstSpikeHitSeen();
            _gm.PlayerState.MarkFirstBattleTutorialCompleted();
            _gm.PlayerState.MarkSpellTutorialBattleCompleted();

            SaveData data = _gm.BuildSaveData();

            Assert.IsTrue(data.hasSeenFirstDeath);
            Assert.IsTrue(data.hasSeenFirstSpikeHit);
            Assert.IsTrue(data.hasCompletedFirstBattleTutorial);
            Assert.IsTrue(data.hasCompletedSpellTutorialBattle);
        }

        [Test]
        public void ApplySaveData_RestoresAllFourFlags()
        {
            var data = new SaveData
            {
                maxHp = 40,
                maxMp = 33,
                currentHp = 40,
                currentMp = 33,
                hasSeenFirstDeath = true,
                hasSeenFirstSpikeHit = true,
                hasCompletedFirstBattleTutorial = true,
                hasCompletedSpellTutorialBattle = true,
            };

            _gm.ApplySaveData(data);

            Assert.IsTrue(_gm.PlayerState.HasSeenFirstDeath);
            Assert.IsTrue(_gm.PlayerState.HasSeenFirstSpikeHit);
            Assert.IsTrue(_gm.PlayerState.HasCompletedFirstBattleTutorial);
            Assert.IsTrue(_gm.PlayerState.HasCompletedSpellTutorialBattle);
        }

        [Test]
        public void ApplySaveData_LegacyDataMissingFlags_DefaultsToFalse()
        {
            // Legacy save (pre-tutorial-flags) deserializes with default-false bools.
            var data = new SaveData { maxHp = 40, maxMp = 33, currentHp = 40, currentMp = 33 };

            _gm.ApplySaveData(data);

            Assert.IsFalse(_gm.PlayerState.HasSeenFirstDeath);
            Assert.IsFalse(_gm.PlayerState.HasSeenFirstSpikeHit);
            Assert.IsFalse(_gm.PlayerState.HasCompletedFirstBattleTutorial);
            Assert.IsFalse(_gm.PlayerState.HasCompletedSpellTutorialBattle);
        }
    }
}
