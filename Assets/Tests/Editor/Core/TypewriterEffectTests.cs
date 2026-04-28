using Axiom.Core;
using NUnit.Framework;

namespace Axiom.Tests.Core
{
    public class TypewriterEffectTests
    {
        [Test]
        public void Start_SetsFullTextAndResetsProgress()
        {
            var effect = new TypewriterEffect();
            effect.Start("Hello World", charsPerSecond: 10f);

            Assert.AreEqual("Hello World", effect.FullText);
            Assert.AreEqual("", effect.VisibleText);
            Assert.IsFalse(effect.IsComplete);
        }

        [Test]
        public void Update_RevealsCharactersOverTime()
        {
            var effect = new TypewriterEffect();
            effect.Start("Hi", charsPerSecond: 10f);

            float progress = effect.Update(deltaTime: 0.1f);

            Assert.Greater(effect.VisibleText.Length, 0);
            Assert.LessOrEqual(effect.VisibleText.Length, 2);
            Assert.AreEqual(progress, (float)effect.VisibleText.Length / effect.FullText.Length, 0.001f);
        }

        [Test]
        public void Update_CompletesAfterEnoughTime()
        {
            var effect = new TypewriterEffect();
            effect.Start("Hi", charsPerSecond: 10f);

            float progress = effect.Update(deltaTime: 1.0f);

            Assert.AreEqual("Hi", effect.VisibleText);
            Assert.IsTrue(effect.IsComplete);
            Assert.AreEqual(1f, progress, 0.001f);
        }

        [Test]
        public void SkipToEnd_RevealsAllTextInstantly()
        {
            var effect = new TypewriterEffect();
            effect.Start("Hello World", charsPerSecond: 1f);

            effect.SkipToEnd();

            Assert.AreEqual("Hello World", effect.VisibleText);
            Assert.IsTrue(effect.IsComplete);
        }

        [Test]
        public void Update_AfterSkipToEnd_ReturnsFullText()
        {
            var effect = new TypewriterEffect();
            effect.Start("Hello", charsPerSecond: 1f);
            effect.SkipToEnd();

            float progress = effect.Update(deltaTime: 0.5f);

            Assert.AreEqual(1f, progress, 0.001f);
        }

        [Test]
        public void Start_EmptyText_IsCompleteImmediately()
        {
            var effect = new TypewriterEffect();
            effect.Start("", charsPerSecond: 10f);

            Assert.AreEqual("", effect.VisibleText);
            Assert.IsTrue(effect.IsComplete);
        }

        [Test]
        public void Start_NullText_IsCompleteWithEmptyVisible()
        {
            var effect = new TypewriterEffect();
            effect.Start(null, charsPerSecond: 10f);

            Assert.AreEqual("", effect.VisibleText);
            Assert.IsTrue(effect.IsComplete);
        }

        [Test]
        public void Progress_IncreasesLinearly()
        {
            var effect = new TypewriterEffect();
            effect.Start("ABCDEFGHIJ", charsPerSecond: 10f);

            float p1 = effect.Update(0.3f);
            float p2 = effect.Update(0.3f);
            float p3 = effect.Update(0.4f);

            Assert.AreEqual(1f, p3, 0.001f);
            Assert.AreEqual("ABCDEFGHIJ", effect.VisibleText);
        }

        [Test]
        public void Update_ZeroDeltaTime_ReturnsCurrentProgress()
        {
            var effect = new TypewriterEffect();
            effect.Start("Test", charsPerSecond: 10f);
            float initialProgress = effect.Update(0f);

            Assert.AreEqual(0f, initialProgress, 0.001f);
            Assert.AreEqual("", effect.VisibleText);
        }

        [Test]
        public void Reset_ClearsAllState()
        {
            var effect = new TypewriterEffect();
            effect.Start("Hello", charsPerSecond: 10f);
            effect.Update(1f);

            effect.Start("World", charsPerSecond: 5f);

            Assert.AreEqual("World", effect.FullText);
            Assert.AreEqual("", effect.VisibleText);
            Assert.IsFalse(effect.IsComplete);
        }
    }
}
