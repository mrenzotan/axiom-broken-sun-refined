using System;
using NUnit.Framework;
using UnityEngine;
using Axiom.Core;
using Axiom.Data;

namespace CoreTests
{
    public class GameManagerProgressionTests
    {
        private GameObject _go;
        private GameManager _manager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(GameManagerProgressionTests));
            _manager = _go.AddComponent<GameManager>();

            CharacterData character = ScriptableObject.CreateInstance<CharacterData>();
            character.baseMaxHP = 100;
            character.baseMaxMP = 30;
            character.baseATK   = 12;
            character.baseDEF   = 6;
            character.baseSPD   = 8;
            character.xpToNextLevelCurve = new[] { 100, 200 };
            character.maxHpPerLevel = 10;
            character.maxMpPerLevel = 3;
            character.atkPerLevel   = 2;
            character.defPerLevel   = 1;
            character.spdPerLevel   = 1;
            _manager.SetPlayerCharacterDataForTests(character);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void AwardXp_BelowThreshold_UpdatesPlayerStateXp()
        {
            _manager.AwardXp(50);

            Assert.AreEqual(50, _manager.PlayerState.Xp);
            Assert.AreEqual(1,  _manager.PlayerState.Level);
        }

        [Test]
        public void AwardXp_CrossesThreshold_LevelsUp()
        {
            _manager.AwardXp(100);

            Assert.AreEqual(2,   _manager.PlayerState.Level);
            Assert.AreEqual(110, _manager.PlayerState.MaxHp);
        }

        [Test]
        public void AwardXp_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _manager.AwardXp(-1));
        }

        [Test]
        public void ProgressionService_SameInstanceAcrossAccess()
        {
            ProgressionService first  = _manager.ProgressionService;
            ProgressionService second = _manager.ProgressionService;

            Assert.AreSame(first, second);
        }
    }
}
