using NUnit.Framework;

namespace Axiom.Platformer.Tests
{
    public class AuraVisibilityStateTests
    {
        private sealed class FakePuzzle : ISpellPuzzle
        {
            public bool IsInteractable { get; set; } = true;
        }

        [Test]
        public void NewState_IsHidden()
        {
            var state = new AuraVisibilityState();
            Assert.IsFalse(state.IsVisible);
        }

        [Test]
        public void Enter_InteractablePuzzle_BecomesVisible()
        {
            var state = new AuraVisibilityState();
            state.Enter(new FakePuzzle());
            Assert.IsTrue(state.IsVisible);
        }

        [Test]
        public void Exit_LastPuzzle_Hides()
        {
            var state = new AuraVisibilityState();
            var puzzle = new FakePuzzle();
            state.Enter(puzzle);
            state.Exit(puzzle);
            Assert.IsFalse(state.IsVisible);
        }

        [Test]
        public void TwoPuzzles_ExitOne_StaysVisible()
        {
            var state = new AuraVisibilityState();
            var a = new FakePuzzle();
            var b = new FakePuzzle();
            state.Enter(a);
            state.Enter(b);
            state.Exit(a);
            Assert.IsTrue(state.IsVisible);
        }

        [Test]
        public void SolvedPuzzleInRange_IsNotVisible()
        {
            var state = new AuraVisibilityState();
            state.Enter(new FakePuzzle { IsInteractable = false });
            Assert.IsFalse(state.IsVisible, "A solved (non-interactable) puzzle must not show the cue.");
        }

        [Test]
        public void MixSolvedAndInteractable_IsVisible()
        {
            var state = new AuraVisibilityState();
            state.Enter(new FakePuzzle { IsInteractable = false });
            state.Enter(new FakePuzzle { IsInteractable = true });
            Assert.IsTrue(state.IsVisible);
        }

        [Test]
        public void Suppressed_HidesEvenWithInteractableInRange()
        {
            var state = new AuraVisibilityState();
            state.Enter(new FakePuzzle());
            state.SetSuppressed(true);
            Assert.IsFalse(state.IsVisible);
        }

        [Test]
        public void Unsuppress_RestoresVisibility()
        {
            var state = new AuraVisibilityState();
            state.Enter(new FakePuzzle());
            state.SetSuppressed(true);
            state.SetSuppressed(false);
            Assert.IsTrue(state.IsVisible);
        }

        [Test]
        public void EnterAndExitNull_AreIgnored()
        {
            var state = new AuraVisibilityState();
            state.Enter(null);
            Assert.IsFalse(state.IsVisible);
            state.Exit(null); // must not throw
        }

        [Test]
        public void EnterSamePuzzleTwice_ExitOnce_Hides()
        {
            var state = new AuraVisibilityState();
            var puzzle = new FakePuzzle();
            state.Enter(puzzle);
            state.Enter(puzzle);
            state.Exit(puzzle);
            Assert.IsFalse(state.IsVisible, "HashSet identity — a single Exit clears the entry.");
        }
    }
}
