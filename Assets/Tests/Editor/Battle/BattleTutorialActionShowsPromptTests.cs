using Axiom.Battle;
using NUnit.Framework;

namespace Axiom.Battle.Tests
{
    public class BattleTutorialActionShowsPromptTests
    {
        [Test]
        public void ShowsPrompt_FalseForNullPromptText()
        {
            var action = new BattleTutorialAction(promptText: null);
            Assert.IsFalse(action.ShowsPrompt);
        }

        [Test]
        public void ShowsPrompt_FalseForEmptyPromptText()
        {
            var action = new BattleTutorialAction(promptText: string.Empty);
            Assert.IsFalse(action.ShowsPrompt);
        }

        [Test]
        public void ShowsPrompt_TrueForNonEmptyPromptText()
        {
            var action = new BattleTutorialAction(promptText: "Press Attack to strike.");
            Assert.IsTrue(action.ShowsPrompt);
        }

        [Test]
        public void NoChange_DoesNotShowPrompt()
        {
            Assert.IsFalse(BattleTutorialAction.NoChange.ShowsPrompt);
        }
    }
}
