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
            Assert.AreEqual(0, state.InventoryItemIds.Count);
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
    }
}
