using Axiom.Core;
using NUnit.Framework;

namespace Axiom.Tests.Core
{
    public class CutsceneInputHandlerTests
    {
        [Test]
        public void NoInput_ReturnsNone()
        {
            var handler = new CutsceneInputHandler();
            var result = handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 0.016f);
            Assert.AreEqual(CutsceneInputResult.None, result);
        }

        [Test]
        public void TapEnter_ReturnsAdvance()
        {
            var handler = new CutsceneInputHandler();
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0.0f);
            var result = handler.ProcessEnterInput(enterPressed: false, enterReleased: true, deltaTime: 0.1f);
            Assert.AreEqual(CutsceneInputResult.Advance, result);
        }

        [Test]
        public void SameFramePressAndRelease_ReturnsAdvance()
        {
            var handler = new CutsceneInputHandler();
            var result = handler.ProcessEnterInput(enterPressed: true, enterReleased: true, deltaTime: 0.016f);
            Assert.AreEqual(CutsceneInputResult.Advance, result);
        }

        [Test]
        public void HoldEnterForFullDuration_ReturnsSkip()
        {
            var handler = new CutsceneInputHandler { HoldToSkipDuration = 1f };
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            var result = handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 1.1f);
            Assert.AreEqual(CutsceneInputResult.Skip, result);
        }

        [Test]
        public void HoldEnter_ReleaseBeforeThreshold_ReturnsNone()
        {
            var handler = new CutsceneInputHandler();
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 1.0f);
            var result = handler.ProcessEnterInput(enterPressed: false, enterReleased: true, deltaTime: 0.016f);
            Assert.AreEqual(CutsceneInputResult.None, result);
        }

        [Test]
        public void HoldEnter_ReleaseWithinTapThreshold_ReturnsAdvance()
        {
            var handler = new CutsceneInputHandler { TapThreshold = 0.2f };
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            var result = handler.ProcessEnterInput(enterPressed: false, enterReleased: true, deltaTime: 0.15f);
            Assert.AreEqual(CutsceneInputResult.Advance, result);
        }

        [Test]
        public void SkipProgress_ReflectsHoldDuration()
        {
            var handler = new CutsceneInputHandler { HoldToSkipDuration = 3f };
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 1.5f);
            Assert.That(handler.SkipProgress, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void IsHoldingEnter_TrueWhilePressing_FalseAfterRelease()
        {
            var handler = new CutsceneInputHandler();
            Assert.IsFalse(handler.IsHoldingEnter);
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            Assert.IsTrue(handler.IsHoldingEnter);
            handler.ProcessEnterInput(enterPressed: false, enterReleased: true, deltaTime: 0.1f);
            Assert.IsFalse(handler.IsHoldingEnter);
        }

        [Test]
        public void IsHoldingEnter_FalseAfterSkip()
        {
            var handler = new CutsceneInputHandler { HoldToSkipDuration = 0.5f };
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 0.6f);
            Assert.IsFalse(handler.IsHoldingEnter);
        }

        [Test]
        public void Reset_ClearsHoldState()
        {
            var handler = new CutsceneInputHandler();
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 1.0f);
            handler.Reset();
            Assert.IsFalse(handler.IsHoldingEnter);
            Assert.AreEqual(0f, handler.SkipProgress);
        }

        [Test]
        public void MultipleSequentialTaps_EachReturnsAdvance()
        {
            var handler = new CutsceneInputHandler();
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            var result1 = handler.ProcessEnterInput(enterPressed: false, enterReleased: true, deltaTime: 0.1f);
            Assert.AreEqual(CutsceneInputResult.Advance, result1);
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            var result2 = handler.ProcessEnterInput(enterPressed: false, enterReleased: true, deltaTime: 0.1f);
            Assert.AreEqual(CutsceneInputResult.Advance, result2);
        }

        [Test]
        public void HoldToSkip_ReturnsNoneEachFrame_BeforeThreshold()
        {
            var handler = new CutsceneInputHandler { HoldToSkipDuration = 3f };
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            for (int i = 0; i < 10; i++)
            {
                var result = handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 0.2f);
                Assert.AreEqual(CutsceneInputResult.None, result);
            }
        }
    }
}
