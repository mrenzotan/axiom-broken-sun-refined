using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Axiom.Data;
using Axiom.Platformer;

namespace Axiom.Tests.Platformer
{
    public class CutsceneRunnerTests
    {
        private CutsceneRunner _runner;

        [SetUp]
        public void Setup()
        {
            _runner = new CutsceneRunner();
        }

        [Test]
        public void Start_WithValidSequence_ReturnsTrue()
        {
            var sequence = CreateTestSequence(1);
            bool result = _runner.Start(sequence);
            Assert.IsTrue(result);
        }

        [Test]
        public void Start_WithNullSequence_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() => _runner.Start(null));
        }

        [Test]
        public void AdvanceStep_WithEmptySequence_ReturnsFalse()
        {
            var sequence = CreateTestSequence(0);
            _runner.Start(sequence);
            bool result = _runner.AdvanceStep();
            Assert.IsFalse(result);
        }

        [Test]
        public void AdvanceStep_AdvancesStepIndex()
        {
            var sequence = CreateTestSequence(3);
            _runner.Start(sequence);

            Assert.IsTrue(_runner.IsRunning);
            _runner.AdvanceStep(); // Step 0
            Assert.IsTrue(_runner.IsRunning);

            _runner.AdvanceStep(); // Step 1
            Assert.IsTrue(_runner.IsRunning);

            _runner.AdvanceStep(); // Step 2
            Assert.IsTrue(_runner.IsRunning);

            bool result = _runner.AdvanceStep(); // Beyond end
            Assert.IsFalse(result);
            Assert.IsFalse(_runner.IsRunning);
        }

        [Test]
        public void OnSequenceEnd_FiresWhenSequenceCompletes()
        {
            var sequence = CreateTestSequence(1);
            bool eventFired = false;
            _runner.OnSequenceEnd += () => eventFired = true;

            _runner.Start(sequence);
            _runner.AdvanceStep();
            _runner.AdvanceStep(); // Advance beyond end

            Assert.IsTrue(eventFired);
        }

        [Test]
        public void RequestSkip_SetsSkipFlag()
        {
            var sequence = CreateTestSequence(1);
            _runner.Start(sequence);

            Assert.IsFalse(_runner.IsSkipRequested);
            _runner.RequestSkip();
            Assert.IsTrue(_runner.IsSkipRequested);
        }

        [Test]
        public void SetFastForward_SetsFastForwardFlag()
        {
            _runner.SetFastForward(true);
            Assert.IsTrue(_runner.IsFastForwarding);

            _runner.SetFastForward(false);
            Assert.IsFalse(_runner.IsFastForwarding);
        }

        [Test]
        public void OnSpellUnlockStep_FiresForSpellUnlockSteps()
        {
            var spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = "test_spell";

            var sequence = ScriptableObject.CreateInstance<CutsceneSequence>();
            sequence.steps = new List<CutsceneStep>
            {
                new CutsceneStep { stepType = CutsceneStepType.UnlockSpell, spellToUnlock = spell }
            };

            SpellData firedSpell = null;
            _runner.OnSpellUnlockStep += (s) => firedSpell = s;

            _runner.Start(sequence);
            _runner.AdvanceStep();

            Assert.AreSame(spell, firedSpell);
        }

        [Test]
        public void Abort_EndsRunningSequence()
        {
            var sequence = CreateTestSequence(3);
            bool eventFired = false;
            _runner.OnSequenceEnd += () => eventFired = true;

            _runner.Start(sequence);
            _runner.AdvanceStep();

            _runner.Abort();
            Assert.IsFalse(_runner.IsRunning);
            Assert.IsTrue(eventFired);
        }

        // Helper to create a test sequence with N dialogue steps.
        private CutsceneSequence CreateTestSequence(int stepCount)
        {
            var sequence = ScriptableObject.CreateInstance<CutsceneSequence>();
            sequence.steps = new List<CutsceneStep>();

            for (int i = 0; i < stepCount; i++)
            {
                var dialogue = ScriptableObject.CreateInstance<DialogueData>();
                dialogue.speakerName = $"Speaker{i}";
                dialogue.dialogueLines = new[] { $"Line {i}" };

                sequence.steps.Add(new CutsceneStep
                {
                    stepType = CutsceneStepType.Dialogue,
                    dialogueData = dialogue
                });
            }

            return sequence;
        }
    }
}
