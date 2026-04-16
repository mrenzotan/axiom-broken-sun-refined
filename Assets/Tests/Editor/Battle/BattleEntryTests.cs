using NUnit.Framework;
using Axiom.Data;

namespace Axiom.Tests.Editor.Battle
{
    public class BattleEntryTests
    {
        [Test]
        public void Constructor_StoresAdvantagedStartState()
        {
            var entry = new BattleEntry(CombatStartState.Advantaged, enemyData: null);

            Assert.AreEqual(CombatStartState.Advantaged, entry.StartState);
        }

        [Test]
        public void Constructor_StoresSurprisedStartState()
        {
            var entry = new BattleEntry(CombatStartState.Surprised, enemyData: null);

            Assert.AreEqual(CombatStartState.Surprised, entry.StartState);
        }

        [Test]
        public void Constructor_AllowsNullEnemyData()
        {
            var entry = new BattleEntry(CombatStartState.Advantaged, enemyData: null);

            Assert.IsNull(entry.EnemyData);
        }

        [Test]
        public void Constructor_StoresEnemyData_WhenProvided()
        {
            var data = UnityEngine.ScriptableObject.CreateInstance<EnemyData>();
            data.enemyName = "Test Enemy";

            var entry = new BattleEntry(CombatStartState.Surprised, data);

            Assert.AreSame(data, entry.EnemyData);

            UnityEngine.Object.DestroyImmediate(data);
        }

        [Test]
        public void Constructor_DefaultEnemyCurrentHp_IsNegativeOne()
        {
            var entry = new BattleEntry(CombatStartState.Advantaged, enemyData: null);

            Assert.AreEqual(-1, entry.EnemyCurrentHp);
        }

        [Test]
        public void Constructor_StoresEnemyCurrentHp_WhenProvided()
        {
            var entry = new BattleEntry(CombatStartState.Advantaged, enemyData: null,
                                        enemyId: "e1", enemyCurrentHp: 42);

            Assert.AreEqual(42, entry.EnemyCurrentHp);
        }

        [Test]
        public void Constructor_StoresEnemyId_WithEnemyCurrentHp()
        {
            var entry = new BattleEntry(CombatStartState.Surprised, enemyData: null,
                                        enemyId: "e2", enemyCurrentHp: 10);

            Assert.AreEqual("e2", entry.EnemyId);
            Assert.AreEqual(10, entry.EnemyCurrentHp);
        }
    }
}
