using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Axiom.Core;
using Axiom.Data;

namespace CoreTests
{
    public class ProgressionServiceTests
    {
        private static CharacterData MakeCharacterData(params int[] xpCurve)
        {
            CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
            data.baseMaxHP = 100;
            data.baseMaxMP = 30;
            data.baseATK   = 12;
            data.baseDEF   = 6;
            data.baseSPD   = 8;
            data.xpToNextLevelCurve = xpCurve;
            data.maxHpPerLevel = 10;
            data.maxMpPerLevel = 3;
            data.atkPerLevel   = 2;
            data.defPerLevel   = 1;
            data.spdPerLevel   = 1;
            return data;
        }

        private static PlayerState NewPlayerState() =>
            new PlayerState(maxHp: 100, maxMp: 30, attack: 12, defense: 6, speed: 8);

        // ── Construction ──────────────────────────────────────────────────

        [Test]
        public void Ctor_ThrowsOnNullState()
        {
            CharacterData data = MakeCharacterData(100);
            Assert.Throws<ArgumentNullException>(() => new ProgressionService(null, data));
        }

        [Test]
        public void Ctor_ThrowsOnNullCharacterData()
        {
            PlayerState state = NewPlayerState();
            Assert.Throws<ArgumentNullException>(() => new ProgressionService(state, null));
        }

        // ── AwardXp — basic ───────────────────────────────────────────────

        [Test]
        public void AwardXp_RejectsNegativeAmount()
        {
            var service = new ProgressionService(NewPlayerState(), MakeCharacterData(100));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.AwardXp(-1));
        }

        [Test]
        public void AwardXp_Zero_IsNoOp()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100));
            int events = 0;
            service.OnLevelUp += _ => events++;

            service.AwardXp(0);

            Assert.AreEqual(0, state.Xp);
            Assert.AreEqual(1, state.Level);
            Assert.AreEqual(0, events);
        }

        [Test]
        public void AwardXp_BelowThreshold_AccumulatesXpDoesNotLevelUp()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100));
            int events = 0;
            service.OnLevelUp += _ => events++;

            service.AwardXp(50);

            Assert.AreEqual(50, state.Xp);
            Assert.AreEqual(1,  state.Level);
            Assert.AreEqual(0,  events);
        }

        // ── AwardXp — single level-up ─────────────────────────────────────

        [Test]
        public void AwardXp_AtThreshold_LevelsUpOnce()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100, 200));
            var results = new List<LevelUpResult>();
            service.OnLevelUp += results.Add;

            service.AwardXp(100);

            Assert.AreEqual(2,   state.Level);
            Assert.AreEqual(0,   state.Xp);   // 100 - 100 = 0 carried over
            Assert.AreEqual(1,   results.Count);
            Assert.AreEqual(1,   results[0].PreviousLevel);
            Assert.AreEqual(2,   results[0].NewLevel);
            Assert.AreEqual(10,  results[0].DeltaMaxHp);
        }

        [Test]
        public void AwardXp_OverThreshold_CarriesRemainderToNextLevel()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100, 200));

            service.AwardXp(150);

            Assert.AreEqual(2,  state.Level);
            Assert.AreEqual(50, state.Xp);
        }

        [Test]
        public void AwardXp_AccumulatesAcrossMultipleCalls_LevelsUpOnCrossing()
        {
            // Real-gameplay path: XP drizzles in from multiple enemies.
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100, 200));
            var results = new List<LevelUpResult>();
            service.OnLevelUp += results.Add;

            service.AwardXp(50);
            Assert.AreEqual(50, state.Xp);
            Assert.AreEqual(1,  state.Level);
            Assert.AreEqual(0,  results.Count);

            service.AwardXp(50);
            Assert.AreEqual(0, state.Xp);
            Assert.AreEqual(2, state.Level);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(2, results[0].NewLevel);
        }

        [Test]
        public void AwardXp_LevelUp_GrowsStatsPerCharacterData()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100));

            service.AwardXp(100);

            Assert.AreEqual(110, state.MaxHp);
            Assert.AreEqual(33,  state.MaxMp);
            Assert.AreEqual(14,  state.Attack);
            Assert.AreEqual(7,   state.Defense);
            Assert.AreEqual(9,   state.Speed);
        }

        [Test]
        public void AwardXp_LevelUp_HealsCurrentVitalsToNewMax()
        {
            PlayerState state = NewPlayerState();
            state.SetCurrentHp(10);
            state.SetCurrentMp(2);
            var service = new ProgressionService(state, MakeCharacterData(100));

            service.AwardXp(100);

            Assert.AreEqual(110, state.CurrentHp);
            Assert.AreEqual(33,  state.CurrentMp);
        }

        // ── AwardXp — multi level-up ──────────────────────────────────────

        [Test]
        public void AwardXp_CrossingMultipleThresholds_FiresEventPerLevel()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100, 200, 400));
            var results = new List<LevelUpResult>();
            service.OnLevelUp += results.Add;

            service.AwardXp(350); // consumes 100 (→ L2) + 200 (→ L3) + 50 carry

            Assert.AreEqual(3,  state.Level);
            Assert.AreEqual(50, state.Xp);
            Assert.AreEqual(2,  results.Count);
            Assert.AreEqual(1,  results[0].PreviousLevel);
            Assert.AreEqual(2,  results[0].NewLevel);
            Assert.AreEqual(2,  results[1].PreviousLevel);
            Assert.AreEqual(3,  results[1].NewLevel);
        }

        [Test]
        public void AwardXp_MultipleLevelUps_StackStatGrowth()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100, 200));

            service.AwardXp(300); // L1 → L3 (consumes 100 + 200)

            Assert.AreEqual(3,   state.Level);
            Assert.AreEqual(120, state.MaxHp);   // 100 + 2×10
            Assert.AreEqual(36,  state.MaxMp);   // 30  + 2×3
            Assert.AreEqual(16,  state.Attack);  // 12  + 2×2
        }

        // ── Level cap ─────────────────────────────────────────────────────

        [Test]
        public void AwardXp_AtLevelCap_AccumulatesXpButDoesNotLevelUp()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100)); // cap = L2
            service.AwardXp(100);  // → L2

            int events = 0;
            service.OnLevelUp += _ => events++;
            service.AwardXp(9999);

            Assert.AreEqual(2,    state.Level);
            Assert.AreEqual(9999, state.Xp);
            Assert.AreEqual(0,    events);
        }

        [Test]
        public void AwardXp_EmptyCurve_NeverLevelsUp()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(/*empty*/));
            int events = 0;
            service.OnLevelUp += _ => events++;

            service.AwardXp(1_000_000);

            Assert.AreEqual(1,         state.Level);
            Assert.AreEqual(1_000_000, state.Xp);
            Assert.AreEqual(0,         events);
        }

        // ── XpForNextLevelUp helper ───────────────────────────────────────

        [Test]
        public void XpForNextLevelUp_ReturnsCurrentLevelThreshold()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100, 250));

            Assert.AreEqual(100, service.XpForNextLevelUp);

            service.AwardXp(100);

            Assert.AreEqual(250, service.XpForNextLevelUp);
        }

        [Test]
        public void XpForNextLevelUp_AtCap_ReturnsZero()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100));
            service.AwardXp(100);

            Assert.AreEqual(0, service.XpForNextLevelUp);
        }
    }
}
