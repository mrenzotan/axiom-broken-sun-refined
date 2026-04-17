using System;
using NUnit.Framework;
using Axiom.Core;

namespace CoreTests
{
    public class PlayerStateTests
    {
        // ── Constructor ──────────────────────────────────────────────────────

        [Test]
        public void Constructor_SetsCurrentHpEqualToMaxHp()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.AreEqual(100, state.CurrentHp);
        }

        [Test]
        public void Constructor_SetsCurrentMpEqualToMaxMp()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.AreEqual(50, state.CurrentMp);
        }

        [Test]
        public void Constructor_SetsStats()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.AreEqual(10, state.Attack);
            Assert.AreEqual(5,  state.Defense);
            Assert.AreEqual(8,  state.Speed);
        }

        [Test]
        public void Constructor_ActiveSceneNameIsEmpty()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.AreEqual(string.Empty, state.ActiveSceneName);
        }

        [Test]
        public void Constructor_InventoryIsEmpty()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.AreEqual(0, state.Inventory.GetAll().Count);
        }

        [Test]
        public void Constructor_ProgressionDefaultsInitialized()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.AreEqual(1, state.Level);
            Assert.AreEqual(0, state.Xp);
        }

        [Test]
        public void Constructor_UnlockedSpellIdsIsEmpty()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.AreEqual(0, state.UnlockedSpellIds.Count);
        }

        [Test]
        public void Constructor_ThrowsOnZeroMaxHp()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerState(maxHp: 0, maxMp: 50, attack: 10, defense: 5, speed: 8));
        }

        [Test]
        public void Constructor_ThrowsOnNegativeMaxHp()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerState(maxHp: -1, maxMp: 50, attack: 10, defense: 5, speed: 8));
        }

        [Test]
        public void Constructor_ThrowsOnNegativeMaxMp()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerState(maxHp: 100, maxMp: -1, attack: 10, defense: 5, speed: 8));
        }

        [Test]
        public void Constructor_ThrowsOnNegativeAttack()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerState(maxHp: 100, maxMp: 50, attack: -1, defense: 5, speed: 8));
        }

        [Test]
        public void Constructor_ThrowsOnNegativeDefense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: -1, speed: 8));
        }

        [Test]
        public void Constructor_ThrowsOnNegativeSpeed()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: -1));
        }

        // ── SetCurrentHp ─────────────────────────────────────────────────────

        [Test]
        public void SetCurrentHp_SetsExactValueWithinBounds()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetCurrentHp(42);
            Assert.AreEqual(42, state.CurrentHp);
        }

        [Test]
        public void SetCurrentHp_ClampsToMaxHp()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetCurrentHp(150);
            Assert.AreEqual(100, state.CurrentHp);
        }

        [Test]
        public void SetCurrentHp_ClampsToZero()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetCurrentHp(-10);
            Assert.AreEqual(0, state.CurrentHp);
        }

        // ── SetCurrentMp ─────────────────────────────────────────────────────

        [Test]
        public void SetCurrentMp_SetsExactValueWithinBounds()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetCurrentMp(25);
            Assert.AreEqual(25, state.CurrentMp);
        }

        [Test]
        public void SetCurrentMp_ClampsToMaxMp()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetCurrentMp(999);
            Assert.AreEqual(50, state.CurrentMp);
        }

        [Test]
        public void SetCurrentMp_ClampsToZero()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetCurrentMp(-1);
            Assert.AreEqual(0, state.CurrentMp);
        }

        // ── ApplyVitals ───────────────────────────────────────────────────────

        [Test]
        public void ApplyVitals_UpdatesMaxAndCurrentValues()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.ApplyVitals(maxHp: 140, maxMp: 80, currentHp: 90, currentMp: 20);

            Assert.AreEqual(140, state.MaxHp);
            Assert.AreEqual(80, state.MaxMp);
            Assert.AreEqual(90, state.CurrentHp);
            Assert.AreEqual(20, state.CurrentMp);
        }

        [Test]
        public void ApplyVitals_ThrowsOnInvalidMaxHp()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.ApplyVitals(maxHp: 0, maxMp: 50, currentHp: 1, currentMp: 1));
        }

        [Test]
        public void ApplyVitals_ThrowsOnNegativeMaxMp()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.ApplyVitals(maxHp: 100, maxMp: -1, currentHp: 1, currentMp: 1));
        }

        // ── Progression and world state ──────────────────────────────────────

        [Test]
        public void ApplyProgression_ClampsToMinimums()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.ApplyProgression(level: 0, xp: -5);

            Assert.AreEqual(1, state.Level);
            Assert.AreEqual(0, state.Xp);
        }

        [Test]
        public void SetWorldPosition_StoresCoordinates()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetWorldPosition(10.5f, -2.25f);

            Assert.AreEqual(10.5f, state.WorldPositionX);
            Assert.AreEqual(-2.25f, state.WorldPositionY);
            Assert.IsTrue(state.HasPendingWorldPositionApply);
            state.ClearPendingWorldPositionApply();
            Assert.IsFalse(state.HasPendingWorldPositionApply);
        }

        [Test]
        public void NewPlayerState_HasNoPendingWorldPositionApply()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.IsFalse(state.HasPendingWorldPositionApply);
        }

        [Test]
        public void SetUnlockedSpellIds_ReplacesList()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetUnlockedSpellIds(new[] { "spell_a", "", "spell_b" });

            Assert.AreEqual(2, state.UnlockedSpellIds.Count);
            Assert.AreEqual("spell_a", state.UnlockedSpellIds[0]);
            Assert.AreEqual("spell_b", state.UnlockedSpellIds[1]);
        }

        [Test]
        public void Inventory_AddAndGetQuantity_TracksItems()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.Inventory.Add("potion", 2);
            state.Inventory.Add("ether");
            Assert.AreEqual(2, state.Inventory.GetQuantity("potion"));
            Assert.AreEqual(1, state.Inventory.GetQuantity("ether"));
        }

        [Test]
        public void Inventory_Remove_DecrementsQuantity()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.Inventory.Add("potion", 3);
            bool removed = state.Inventory.Remove("potion");
            Assert.IsTrue(removed);
            Assert.AreEqual(2, state.Inventory.GetQuantity("potion"));
        }

        // ── SetActiveScene ────────────────────────────────────────────────────

        [Test]
        public void SetActiveScene_SetsName()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetActiveScene("Platformer");
            Assert.AreEqual("Platformer", state.ActiveSceneName);
        }

        [Test]
        public void SetActiveScene_NullTreatedAsEmpty()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetActiveScene(null);
            Assert.AreEqual(string.Empty, state.ActiveSceneName);
        }

        [Test]
        public void SetActiveScene_CanBeOverwritten()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetActiveScene("Platformer");
            state.SetActiveScene("World_01");
            Assert.AreEqual("World_01", state.ActiveSceneName);
        }

        // ── Checkpoint APIs ───────────────────────────────────────────────────

        [Test]
        public void MarkCheckpointActivated_ReturnsTrueOnFirstCall()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.IsTrue(state.MarkCheckpointActivated("cp_01"));
        }

        [Test]
        public void HasActivatedCheckpoint_ReturnsTrueAfterMark()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.MarkCheckpointActivated("cp_01");
            Assert.IsTrue(state.HasActivatedCheckpoint("cp_01"));
        }

        [Test]
        public void MarkCheckpointActivated_ReturnsFalseOnDuplicate()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.MarkCheckpointActivated("cp_01");
            Assert.IsFalse(state.MarkCheckpointActivated("cp_01"));
        }

        [Test]
        public void MarkCheckpointActivated_DoesNotAddDuplicate_CollectionUnchanged()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.MarkCheckpointActivated("cp_01");
            state.MarkCheckpointActivated("cp_01");
            Assert.AreEqual(1, state.ActivatedCheckpointIds.Count);
        }

        [Test]
        public void MarkCheckpointActivated_IgnoresNullId()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.IsFalse(state.MarkCheckpointActivated(null));
            Assert.AreEqual(0, state.ActivatedCheckpointIds.Count);
        }

        [Test]
        public void MarkCheckpointActivated_IgnoresEmptyId()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.IsFalse(state.MarkCheckpointActivated(string.Empty));
            Assert.AreEqual(0, state.ActivatedCheckpointIds.Count);
        }

        [Test]
        public void MarkCheckpointActivated_IgnoresWhitespaceId()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.IsFalse(state.MarkCheckpointActivated("   "));
            Assert.AreEqual(0, state.ActivatedCheckpointIds.Count);
        }

        [Test]
        public void SetActivatedCheckpointIds_ReplacesCollection()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.MarkCheckpointActivated("cp_old");
            state.SetActivatedCheckpointIds(new[] { "cp_a", "cp_b" });
            Assert.AreEqual(2, state.ActivatedCheckpointIds.Count);
            Assert.AreEqual("cp_a", state.ActivatedCheckpointIds[0]);
            Assert.AreEqual("cp_b", state.ActivatedCheckpointIds[1]);
        }

        [Test]
        public void SetActivatedCheckpointIds_NullClearsCollection()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.MarkCheckpointActivated("cp_01");
            state.SetActivatedCheckpointIds(null);
            Assert.AreEqual(0, state.ActivatedCheckpointIds.Count);
        }

        [Test]
        public void SetActivatedCheckpointIds_SkipsInvalidIds()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetActivatedCheckpointIds(new[] { "cp_valid", null, "", "   ", "cp_also_valid" });
            Assert.AreEqual(2, state.ActivatedCheckpointIds.Count);
            Assert.AreEqual("cp_valid", state.ActivatedCheckpointIds[0]);
            Assert.AreEqual("cp_also_valid", state.ActivatedCheckpointIds[1]);
        }
    }
}
