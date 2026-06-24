using System.Reflection;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Battle.Tests
{
    /// <summary>
    /// Covers <see cref="BattleController.NotifyVoiceResultEmpty"/> staying armed in the
    /// voice spell phase. Without this, a first-call macOS mic-activation latency that
    /// captures no audio would silently kick the player back to the action menu while
    /// the spell-input prompt panel is still on screen — visible bug: the first "Spell"
    /// click never registers a spoken spell, only the second one does.
    /// </summary>
    public class BattleControllerSpellPhaseTests
    {
        private GameObject _go;
        private BattleController _controller;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestBattleController");
            _controller = _go.AddComponent<BattleController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void NotifyVoiceResultEmpty_OnPlayerTurn_KeepsSpellPhaseArmed()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Advantaged); // → PlayerTurn

            SetField(_controller, "_battleManager", bm);
            SetField(_controller, "_isAwaitingVoiceSpell", true);
            SetField(_controller, "_isProcessingAction", false);

            _controller.NotifyVoiceResultEmpty();

            Assert.IsTrue((bool)GetField(_controller, "_isAwaitingVoiceSpell"),
                "Empty Vosk result must NOT exit the spell phase. The player should be " +
                "able to retry PTT without re-clicking the Spell button.");
        }

        [Test]
        public void NotifyVoiceResultEmpty_DoesNotFireSpellChargeAborted()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Advantaged);

            SetField(_controller, "_battleManager", bm);
            SetField(_controller, "_isAwaitingVoiceSpell", true);

            int abortFired = 0;
            _controller.OnSpellChargeAborted += () => abortFired++;

            _controller.NotifyVoiceResultEmpty();

            Assert.AreEqual(0, abortFired,
                "Charge animation must not reset on empty result — the player is still " +
                "preparing the spell, so the charge pose should stay until they cast or " +
                "the recognizer returns a real word.");
        }

        [Test]
        public void NotifyVoiceResultEmpty_OutsideSpellPhase_IsNoOp()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Advantaged);

            SetField(_controller, "_battleManager", bm);
            SetField(_controller, "_isAwaitingVoiceSpell", false);

            Assert.DoesNotThrow(() => _controller.NotifyVoiceResultEmpty());
            Assert.IsFalse((bool)GetField(_controller, "_isAwaitingVoiceSpell"));
        }

        [Test]
        public void CancelSpellPhase_OnPlayerTurn_DuringSpellPhase_ResetsAwaitingFlag()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Advantaged); // → PlayerTurn

            SetField(_controller, "_battleManager", bm);
            SetField(_controller, "_isAwaitingVoiceSpell", true);
            SetField(_controller, "_isProcessingAction", false);

            _controller.CancelSpellPhase();

            Assert.IsFalse((bool)GetField(_controller, "_isAwaitingVoiceSpell"),
                "Cancel must exit the voice spell phase so the player can choose another action.");
            Assert.IsFalse((bool)GetField(_controller, "_isProcessingAction"),
                "Cancel must release the action lock so PlayerAttack/PlayerItem/PlayerFlee work after.");
        }

        [Test]
        public void CancelSpellPhase_FiresOnSpellPhaseCancelled()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Advantaged);

            SetField(_controller, "_battleManager", bm);
            SetField(_controller, "_isAwaitingVoiceSpell", true);

            int cancelledFired = 0;
            _controller.OnSpellPhaseCancelled += () => cancelledFired++;

            _controller.CancelSpellPhase();

            Assert.AreEqual(1, cancelledFired,
                "SpellInputUI listens for OnSpellPhaseCancelled to hide all panels — it must fire exactly once per cancel.");
        }

        [Test]
        public void CancelSpellPhase_FiresOnSpellChargeAborted()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Advantaged);

            SetField(_controller, "_battleManager", bm);
            SetField(_controller, "_isAwaitingVoiceSpell", true);

            int abortFired = 0;
            _controller.OnSpellChargeAborted += () => abortFired++;

            _controller.CancelSpellPhase();

            Assert.AreEqual(1, abortFired,
                "Cancel must reset the player animator from Charging → Idle. " +
                "Animator wiring is OnSpellChargeAborted → PlayerBattleAnimator.TriggerResetCharge.");
        }

        [Test]
        public void CancelSpellPhase_DoesNotFireOnSpellNotRecognized()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Advantaged);

            SetField(_controller, "_battleManager", bm);
            SetField(_controller, "_isAwaitingVoiceSpell", true);

            int notRecognizedFired = 0;
            _controller.OnSpellNotRecognized += () => notRecognizedFired++;

            _controller.CancelSpellPhase();

            Assert.AreEqual(0, notRecognizedFired,
                "Cancel is not an error path — the 'Not recognized. Try again.' feedback panel must NOT show.");
        }

        [Test]
        public void CancelSpellPhase_OutsideSpellPhase_IsNoOp()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Advantaged);

            SetField(_controller, "_battleManager", bm);
            SetField(_controller, "_isAwaitingVoiceSpell", false);

            int cancelledFired = 0;
            int abortFired = 0;
            _controller.OnSpellPhaseCancelled += () => cancelledFired++;
            _controller.OnSpellChargeAborted  += () => abortFired++;

            Assert.DoesNotThrow(() => _controller.CancelSpellPhase());

            Assert.AreEqual(0, cancelledFired,
                "Pressing Cancel from the action menu (panel hidden) must not raise the cancel event.");
            Assert.AreEqual(0, abortFired,
                "Pressing Cancel from the action menu must not retrigger the animator reset.");
        }

        [Test]
        public void CancelSpellPhase_OutsidePlayerTurn_IsNoOp()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Surprised); // → EnemyTurn

            SetField(_controller, "_battleManager", bm);
            SetField(_controller, "_isAwaitingVoiceSpell", true); // pathological state

            int cancelledFired = 0;
            _controller.OnSpellPhaseCancelled += () => cancelledFired++;

            _controller.CancelSpellPhase();

            Assert.IsTrue((bool)GetField(_controller, "_isAwaitingVoiceSpell"),
                "Cancel during EnemyTurn must not mutate spell-phase state.");
            Assert.AreEqual(0, cancelledFired,
                "Cancel during EnemyTurn must not raise the cancel event.");
        }

        [Test]
        public void CancelSpellPhase_DoesNotConsumeMP()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Advantaged);

            var playerStats = new CharacterStats { Name = "Test", MaxHP = 30, MaxMP = 20, ATK = 1, DEF = 1, SPD = 1 };
            playerStats.Initialize();

            SetField(_controller, "_battleManager", bm);
            SetField(_controller, "_playerStats", playerStats);
            SetField(_controller, "_isAwaitingVoiceSpell", true);

            int mpBefore = playerStats.CurrentMP;
            _controller.CancelSpellPhase();

            Assert.AreEqual(mpBefore, playerStats.CurrentMP,
                "Cancel returns to the action menu without consuming the turn or any MP (DEV-91 AC).");
        }

        [Test]
        public void OnSpellCast_InsufficientMP_FiresOnSpellChargeAborted()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Advantaged);

            var playerStats = new CharacterStats { Name = "Test", MaxHP = 30, MaxMP = 3, ATK = 1, DEF = 1, SPD = 1 };
            playerStats.Initialize();

            var spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = "flare";
            spell.mpCost = 5;

            try
            {
                SetField(_controller, "_battleManager", bm);
                SetField(_controller, "_playerStats", playerStats);
                SetField(_controller, "_isAwaitingVoiceSpell", true);
                SetField(_controller, "_isProcessingAction", true);

                int abortFired = 0;
                _controller.OnSpellChargeAborted += () => abortFired++;

                _controller.OnSpellCast(spell);

                Assert.AreEqual(1, abortFired,
                    "Insufficient MP exits the voice spell phase without casting, so the charging animation must reset.");
            }
            finally
            {
                Object.DestroyImmediate(spell);
            }
        }

        [Test]
        public void OnSpellCast_TutorialRestrictsToFreeze_RejectsOtherSpellWithoutSpendingTurn()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Advantaged); // → PlayerTurn

            SetField(_controller, "_battleManager", bm);
            SetField(_controller, "_isAwaitingVoiceSpell", true);
            SetField(_controller, "_isProcessingAction", true);

            _controller.SetTutorialSpellGate(
                new TutorialSpellGate(new[] { "freeze" }, "The tutorial needs Freeze — say 'Freeze' aloud."));

            string rejected = null;
            _controller.OnSpellCastRejected += msg => rejected = msg;

            var combust = ScriptableObject.CreateInstance<SpellData>();
            combust.spellName = "combust";
            combust.mpCost = 6;

            _controller.OnSpellCast(combust);

            Assert.AreEqual("The tutorial needs Freeze — say 'Freeze' aloud.", rejected,
                "Casting a non-Freeze spell during the restricted tutorial turn must be rejected with the coaching message.");
            Assert.IsFalse((bool)GetField(_controller, "_isAwaitingVoiceSpell"),
                "Rejection must exit the voice spell phase so the player returns to the action menu and can retry.");

            Object.DestroyImmediate(combust);
        }

        [Test]
        public void IsTutorialSpellRestricted_TracksTheActiveGate()
        {
            Assert.IsFalse(_controller.IsTutorialSpellRestricted,
                "No gate set — defaults to Unrestricted, so non-spell actions behave normally.");

            _controller.SetTutorialSpellGate(new TutorialSpellGate(new[] { "freeze" }, "msg"));
            Assert.IsTrue(_controller.IsTutorialSpellRestricted,
                "A restricting gate must report restricted so BattleHUD keeps non-spell actions locked.");

            _controller.SetTutorialSpellGate(TutorialSpellGate.Unrestricted);
            Assert.IsFalse(_controller.IsTutorialSpellRestricted,
                "Clearing the gate must restore normal non-spell action access.");

            _controller.SetTutorialSpellGate(new TutorialSpellGate(new[] { "freeze" }, "msg"));
            _controller.SetTutorialSpellGate(null);
            Assert.IsFalse(_controller.IsTutorialSpellRestricted, "Null clears to Unrestricted.");
        }

        // ── Reflection helpers ─────────────────────────────────────────────────────

        private static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"Field '{name}' not found on {target.GetType().Name}.");
            f.SetValue(target, value);
        }

        private static object GetField(object target, string name)
        {
            FieldInfo f = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"Field '{name}' not found on {target.GetType().Name}.");
            return f.GetValue(target);
        }
    }
}
