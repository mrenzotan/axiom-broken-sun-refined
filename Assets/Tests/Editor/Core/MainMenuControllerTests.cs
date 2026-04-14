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
    }
}
