using NUnit.Framework;
using Axiom.Battle;
using Axiom.Data;

namespace Axiom.Battle.Tests
{
    public class BattleTutorialFlowTests
    {
        // ── FirstBattle (IceSlime, Surprised) ──────────────────────────────────

        [Test]
        public void FirstBattle_OnInit_ShowsSurprisedPromptAndLocksToAttack()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.FirstBattle, CombatStartState.Surprised);
            BattleTutorialAction a = flow.OnInit();
            StringAssert.Contains("surprised", (a.PromptText ?? string.Empty).ToLowerInvariant());
            Assert.IsTrue(a.AttackInteractable);
            Assert.IsFalse(a.SpellInteractable);
            Assert.IsFalse(a.ItemInteractable);
            Assert.IsFalse(a.FleeInteractable);
            Assert.IsFalse(a.MarkComplete);
        }

        [Test]
        public void FirstBattle_OnPlayerTurnStarted_FirstCall_ShowsPressAttackPrompt()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.FirstBattle, CombatStartState.Surprised);
            flow.OnInit();
            BattleTutorialAction a = flow.OnPlayerTurnStarted();
            StringAssert.Contains("attack", (a.PromptText ?? string.Empty).ToLowerInvariant());
            Assert.IsTrue(a.AttackInteractable);
            Assert.IsFalse(a.SpellInteractable);
            Assert.IsFalse(a.ItemInteractable);
            Assert.IsFalse(a.FleeInteractable);
        }

        [Test]
        public void FirstBattle_OnPlayerTurnStarted_SecondCall_ReappliesButtonLockOnly()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.FirstBattle, CombatStartState.Surprised);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            BattleTutorialAction a = flow.OnPlayerTurnStarted();
            // No new prompt, but the button lock must still be re-applied because
            // BattleController re-enables all buttons after EnemyTurn.
            Assert.IsNull(a.PromptText, "Prompt should not change on re-entry to PlayerTurn.");
            Assert.IsTrue(a.AttackInteractable);
            Assert.IsFalse(a.SpellInteractable);
        }

        [Test]
        public void FirstBattle_OnPlayerAttackHit_HidesPrompt()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.FirstBattle, CombatStartState.Surprised);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            BattleTutorialAction a = flow.OnPlayerAttackHit();
            Assert.AreEqual(string.Empty, a.PromptText, "Empty string means hide.");
        }

        [Test]
        public void FirstBattle_OnBattleEnded_Victory_MarksComplete()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.FirstBattle, CombatStartState.Surprised);
            flow.OnInit();
            BattleTutorialAction a = flow.OnBattleEnded(victory: true);
            Assert.IsTrue(a.MarkComplete);
            Assert.AreEqual(string.Empty, a.PromptText);
        }

        [Test]
        public void FirstBattle_OnBattleEnded_Defeat_DoesNotMarkComplete()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.FirstBattle, CombatStartState.Surprised);
            flow.OnInit();
            BattleTutorialAction a = flow.OnBattleEnded(victory: false);
            Assert.IsFalse(a.MarkComplete);
        }

        // ── SpellTutorial (Meltspawn, Advantaged, Liquid innate) ───────────────

        [Test]
        public void SpellTutorial_OnInit_ShowsLiquidPromptAndAttackOnly()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            BattleTutorialAction a = flow.OnInit();
            StringAssert.Contains("liquid", (a.PromptText ?? string.Empty).ToLowerInvariant());
            Assert.IsTrue(a.AttackInteractable);
            Assert.IsFalse(a.SpellInteractable);
            Assert.IsFalse(a.ItemInteractable);
            Assert.IsFalse(a.FleeInteractable);
        }

        [Test]
        public void SpellTutorial_OnPlayerAttackImmune_ShowsLiquidBlocksPrompt()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            BattleTutorialAction a = flow.OnPlayerAttackImmune();
            StringAssert.Contains("liquid", (a.PromptText ?? string.Empty).ToLowerInvariant());
            StringAssert.Contains("spell", (a.PromptText ?? string.Empty).ToLowerInvariant());
        }

        [Test]
        public void SpellTutorial_PlayerTurn2_UnlocksSpellAndPromptsCast()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();              // turn 1
            flow.OnPlayerAttackImmune();             // attack bounced
            BattleTutorialAction a = flow.OnPlayerTurnStarted(); // turn 2
            StringAssert.Contains("freeze", (a.PromptText ?? string.Empty).ToLowerInvariant());
            Assert.IsTrue(a.AttackInteractable);
            Assert.IsTrue(a.SpellInteractable, "Spell button must unlock at turn 2.");
            Assert.IsFalse(a.ItemInteractable);
            Assert.IsFalse(a.FleeInteractable);
        }

        [Test]
        public void SpellTutorial_OnSpellCast_HidesPromptDuringResolve()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            flow.OnPlayerAttackImmune();
            flow.OnPlayerTurnStarted();
            BattleTutorialAction a = flow.OnSpellCast(spellName: "Freeze");
            Assert.AreEqual(string.Empty, a.PromptText);
        }

        [Test]
        public void SpellTutorial_OnConditionsChanged_AfterFreeze_ShowsFrozenSolidPrompt()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            flow.OnPlayerAttackImmune();
            flow.OnPlayerTurnStarted();
            flow.OnSpellCast(spellName: "Freeze");
            BattleTutorialAction a = flow.OnConditionsChanged();
            string lower = (a.PromptText ?? string.Empty).ToLowerInvariant();
            StringAssert.Contains("frozen", lower);
            StringAssert.Contains("solid", lower);
        }

        [Test]
        public void SpellTutorial_PlayerTurn3_PromptsToAttackWhileSolid()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            flow.OnPlayerAttackImmune();
            flow.OnPlayerTurnStarted();
            flow.OnSpellCast(spellName: "Freeze");
            flow.OnConditionsChanged();
            BattleTutorialAction a = flow.OnPlayerTurnStarted(); // turn 3
            StringAssert.Contains("solid", (a.PromptText ?? string.Empty).ToLowerInvariant());
            Assert.IsTrue(a.AttackInteractable);
            Assert.IsTrue(a.SpellInteractable);
        }

        [Test]
        public void SpellTutorial_OnPlayerAttackHit_AfterTurn3_ShowsClosingLine()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            flow.OnPlayerAttackImmune();
            flow.OnPlayerTurnStarted();
            flow.OnSpellCast(spellName: "Freeze");
            flow.OnConditionsChanged();
            flow.OnPlayerTurnStarted();
            BattleTutorialAction a = flow.OnPlayerAttackHit();
            StringAssert.Contains("spell", (a.PromptText ?? string.Empty).ToLowerInvariant());
        }

        [Test]
        public void SpellTutorial_AttackOnTurn2_RefiresLiquidBlocksPrompt()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            flow.OnPlayerAttackImmune();
            flow.OnPlayerTurnStarted();
            BattleTutorialAction a = flow.OnPlayerAttackImmune(); // player attacked again
            StringAssert.Contains("liquid", (a.PromptText ?? string.Empty).ToLowerInvariant());
        }

        [Test]
        public void SpellTutorial_OnBattleEnded_Victory_MarksComplete()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            BattleTutorialAction a = flow.OnBattleEnded(victory: true);
            Assert.IsTrue(a.MarkComplete);
        }

        // ── Mode = None ────────────────────────────────────────────────────────

        [Test]
        public void NoneMode_OnInit_ReturnsNoChange()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.None, CombatStartState.Surprised);
            BattleTutorialAction a = flow.OnInit();
            Assert.IsNull(a.PromptText);
            Assert.IsNull(a.AttackInteractable);
            Assert.IsNull(a.SpellInteractable);
            Assert.IsFalse(a.MarkComplete);
        }
    }
}
