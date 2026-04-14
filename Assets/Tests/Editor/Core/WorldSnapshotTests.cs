// Assets/Tests/Editor/Core/WorldSnapshotTests.cs
using NUnit.Framework;
using Axiom.Core;

namespace Axiom.Tests.Editor.Core
{
    public class WorldSnapshotTests
    {
        // ── EnemyWorldState ──────────────────────────────────────────────────

        [Test]
        public void EnemyWorldState_StoresPositionCorrectly()
        {
            var state = new EnemyWorldState(3f, -7.5f);

            Assert.AreEqual(3f,    state.PositionX, 0.001f);
            Assert.AreEqual(-7.5f, state.PositionY, 0.001f);
        }

        // ── WorldSnapshot — enemy capture ────────────────────────────────────

        [Test]
        public void CaptureEnemy_ThenTryGet_ReturnsCorrectPosition()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureEnemy("enemy_01", 5f, 2f);

            bool found = snapshot.TryGetEnemy("enemy_01", out EnemyWorldState state);

            Assert.IsTrue(found);
            Assert.AreEqual(5f, state.PositionX, 0.001f);
            Assert.AreEqual(2f, state.PositionY, 0.001f);
        }

        [Test]
        public void TryGetEnemy_ReturnsFalse_ForUnknownId()
        {
            var snapshot = new WorldSnapshot();

            bool found = snapshot.TryGetEnemy("ghost_id", out EnemyWorldState state);

            Assert.IsFalse(found);
            Assert.IsNull(state);
        }

        [Test]
        public void CaptureEnemy_IgnoresNullId()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureEnemy(null, 1f, 2f);

            bool found = snapshot.TryGetEnemy(null, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void CaptureEnemy_IgnoresWhitespaceId()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureEnemy("   ", 1f, 2f);

            bool found = snapshot.TryGetEnemy("   ", out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void CaptureEnemy_OverwritesPreviousEntryForSameId()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureEnemy("enemy_01", 1f, 2f);
            snapshot.CaptureEnemy("enemy_01", 9f, -4f);

            snapshot.TryGetEnemy("enemy_01", out EnemyWorldState state);

            Assert.AreEqual(9f,  state.PositionX, 0.001f);
            Assert.AreEqual(-4f, state.PositionY, 0.001f);
        }

        [Test]
        public void CaptureEnemy_MultipleEnemies_AreStoredIndependently()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureEnemy("enemy_01", 1f, 0f);
            snapshot.CaptureEnemy("enemy_02", 5f, 3f);

            snapshot.TryGetEnemy("enemy_01", out EnemyWorldState s1);
            snapshot.TryGetEnemy("enemy_02", out EnemyWorldState s2);

            Assert.AreEqual(1f, s1.PositionX, 0.001f);
            Assert.AreEqual(5f, s2.PositionX, 0.001f);
        }

        // ── WorldSnapshot — interactable capture ─────────────────────────────

        [Test]
        public void CaptureInteractable_ThenTryGet_ReturnsCorrectState()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureInteractable("door_01", isActive: true);

            bool found = snapshot.TryGetInteractable("door_01", out bool isActive);

            Assert.IsTrue(found);
            Assert.IsTrue(isActive);
        }

        [Test]
        public void CaptureInteractable_FalseState_RoundTrips()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureInteractable("chest_02", isActive: false);

            snapshot.TryGetInteractable("chest_02", out bool isActive);

            Assert.IsFalse(isActive);
        }

        [Test]
        public void TryGetInteractable_ReturnsFalse_ForNullId()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureInteractable("door_01", true);

            bool found = snapshot.TryGetInteractable(null, out bool isActive);

            Assert.IsFalse(found);
            Assert.IsFalse(isActive);
        }

        [Test]
        public void TryGetInteractable_ReturnsFalse_ForUnknownId()
        {
            var snapshot = new WorldSnapshot();

            bool found = snapshot.TryGetInteractable("nonexistent", out bool _);

            Assert.IsFalse(found);
        }
    }
}
