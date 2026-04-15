using System;
using Axiom.Core;
using NUnit.Framework;

namespace Axiom.Tests.Editor.Core
{
    public class MainMenuControllerTests
    {
        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenHasSaveFileIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MainMenuController(hasSaveFile: null, startNewGame: () => { }, continueGame: () => { }));
        }

        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenStartNewGameIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MainMenuController(hasSaveFile: () => false, startNewGame: null, continueGame: () => { }));
        }

        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenContinueGameIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MainMenuController(hasSaveFile: () => false, startNewGame: () => { }, continueGame: null));
        }

        [Test]
        public void CanContinue_ReturnsFalse_WhenNoSaveFileExists()
        {
            var controller = new MainMenuController(() => false, () => { }, () => { });
            Assert.IsFalse(controller.CanContinue());
        }

        [Test]
        public void CanContinue_ReturnsTrue_WhenSaveFileExists()
        {
            var controller = new MainMenuController(() => true, () => { }, () => { });
            Assert.IsTrue(controller.CanContinue());
        }

        [Test]
        public void OnNewGameClicked_InvokesStartNewGame_WhenNoSaveExists()
        {
            bool called = false;
            var controller = new MainMenuController(
                hasSaveFile:  () => false,
                startNewGame: () => called = true,
                continueGame: () => { });

            controller.OnNewGameClicked();

            Assert.IsTrue(called);
        }

        [Test]
        public void OnNewGameClicked_InvokesStartNewGame_EvenWhenSaveFileExists()
        {
            bool called = false;
            var controller = new MainMenuController(
                hasSaveFile:  () => true,
                startNewGame: () => called = true,
                continueGame: () => { });

            controller.OnNewGameClicked();

            Assert.IsTrue(called);
        }

        [Test]
        public void OnContinueClicked_DoesNotInvokeContinueGame_WhenNoSaveExists()
        {
            bool called = false;
            var controller = new MainMenuController(
                hasSaveFile:  () => false,
                startNewGame: () => { },
                continueGame: () => called = true);

            controller.OnContinueClicked();

            Assert.IsFalse(called);
        }

        [Test]
        public void OnContinueClicked_InvokesContinueGame_WhenSaveExists()
        {
            bool called = false;
            var controller = new MainMenuController(
                hasSaveFile:  () => true,
                startNewGame: () => { },
                continueGame: () => called = true);

            controller.OnContinueClicked();

            Assert.IsTrue(called);
        }

        [Test]
        public void OnContinueClicked_DoesNotInvokeStartNewGame()
        {
            bool newGameCalled = false;
            var controller = new MainMenuController(
                hasSaveFile:  () => true,
                startNewGame: () => newGameCalled = true,
                continueGame: () => { });

            controller.OnContinueClicked();

            Assert.IsFalse(newGameCalled);
        }

        [Test]
        public void OnQuitClicked_DoesNothing_WhenQuitDelegateIsNull()
        {
            var controller = new MainMenuController(() => false, () => { }, () => { }, quit: null);
            Assert.DoesNotThrow(() => controller.OnQuitClicked());
        }

        [Test]
        public void OnQuitClicked_InvokesQuit_WhenDelegateProvided()
        {
            bool called = false;
            var controller = new MainMenuController(
                hasSaveFile:  () => false,
                startNewGame: () => { },
                continueGame: () => { },
                quit:         () => called = true);

            controller.OnQuitClicked();

            Assert.IsTrue(called);
        }

        // ── OnNewGameClicked confirmation branching tests (DEV-62) ───────────

        [Test]
        public void OnNewGameClicked_RequestsConfirmation_WhenSaveExistsAndDelegateProvided()
        {
            bool confirmationRequested = false;
            bool startNewGameCalled = false;

            var controller = new MainMenuController(
                hasSaveFile:  () => true,
                startNewGame: () => startNewGameCalled = true,
                continueGame: () => { },
                requestNewGameConfirmation: () => confirmationRequested = true);

            controller.OnNewGameClicked();

            Assert.IsTrue(confirmationRequested, "Confirmation delegate should be invoked when a save exists.");
            Assert.IsFalse(startNewGameCalled, "StartNewGame must not fire directly — wait for the UI to confirm.");
        }

        [Test]
        public void OnNewGameClicked_StartsImmediately_WhenNoSaveExists_EvenWithConfirmationDelegate()
        {
            bool confirmationRequested = false;
            bool startNewGameCalled = false;

            var controller = new MainMenuController(
                hasSaveFile:  () => false,
                startNewGame: () => startNewGameCalled = true,
                continueGame: () => { },
                requestNewGameConfirmation: () => confirmationRequested = true);

            controller.OnNewGameClicked();

            Assert.IsFalse(confirmationRequested, "No save means no confirmation needed.");
            Assert.IsTrue(startNewGameCalled, "StartNewGame should run immediately when no save exists.");
        }

        [Test]
        public void OnNewGameClicked_StartsImmediately_WhenSaveExistsButNoConfirmationDelegate()
        {
            bool startNewGameCalled = false;

            var controller = new MainMenuController(
                hasSaveFile:  () => true,
                startNewGame: () => startNewGameCalled = true,
                continueGame: () => { },
                requestNewGameConfirmation: null);

            controller.OnNewGameClicked();

            Assert.IsTrue(startNewGameCalled, "With no confirmation delegate, controller preserves legacy direct-start behaviour.");
        }

        [Test]
        public void OnNewGameClicked_EachCallReEvaluatesSaveExistence()
        {
            bool hasSave = true;
            int confirmationCount = 0;
            int startCount = 0;

            var controller = new MainMenuController(
                hasSaveFile:  () => hasSave,
                startNewGame: () => startCount++,
                continueGame: () => { },
                requestNewGameConfirmation: () => confirmationCount++);

            controller.OnNewGameClicked();    // save exists → confirmation
            hasSave = false;                  // save cleared externally
            controller.OnNewGameClicked();    // no save → direct start

            Assert.AreEqual(1, confirmationCount);
            Assert.AreEqual(1, startCount);
        }
    }
}
