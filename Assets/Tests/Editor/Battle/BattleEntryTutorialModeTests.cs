using NUnit.Framework;
using Axiom.Data;

namespace Axiom.Battle.Tests
{
    public class BattleEntryTutorialModeTests
    {
        [Test]
        public void Constructor_WithoutTutorialMode_DefaultsToNone()
        {
            var entry = new BattleEntry(CombatStartState.Surprised, enemyData: null);
            Assert.AreEqual(BattleTutorialMode.None, entry.TutorialMode);
        }

        [Test]
        public void Constructor_WithExplicitTutorialMode_StoresIt()
        {
            var entry = new BattleEntry(
                CombatStartState.Advantaged,
                enemyData: null,
                enemyId: "meltspawn_01",
                enemyCurrentHp: -1,
                environmentData: null,
                tutorialMode: BattleTutorialMode.SpellTutorial);

            Assert.AreEqual(BattleTutorialMode.SpellTutorial, entry.TutorialMode);
        }
    }
}
