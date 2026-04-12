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
    }
}
