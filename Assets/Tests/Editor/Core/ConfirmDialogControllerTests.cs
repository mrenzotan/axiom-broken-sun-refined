using System;
using Axiom.Core;
using NUnit.Framework;

namespace Axiom.Tests.Editor.Core
{
    public class ConfirmDialogControllerTests
    {
        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenOnConfirmIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ConfirmDialogController(onConfirm: null, onCancel: () => { }));
        }

        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenOnCancelIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ConfirmDialogController(onConfirm: () => { }, onCancel: null));
        }

        [Test]
        public void OnYesClicked_InvokesOnConfirm_NotOnCancel()
        {
            bool confirmCalled = false;
            bool cancelCalled = false;

            var controller = new ConfirmDialogController(
                onConfirm: () => confirmCalled = true,
                onCancel:  () => cancelCalled = true);

            controller.OnYesClicked();

            Assert.IsTrue(confirmCalled);
            Assert.IsFalse(cancelCalled);
        }

        [Test]
        public void OnNoClicked_InvokesOnCancel_NotOnConfirm()
        {
            bool confirmCalled = false;
            bool cancelCalled = false;

            var controller = new ConfirmDialogController(
                onConfirm: () => confirmCalled = true,
                onCancel:  () => cancelCalled = true);

            controller.OnNoClicked();

            Assert.IsTrue(cancelCalled);
            Assert.IsFalse(confirmCalled);
        }
    }
}
