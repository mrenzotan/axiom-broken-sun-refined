using NUnit.Framework;
using Axiom.Core;
using UnityEngine;

namespace Axiom.Tests.Editor.Core
{
    public class SceneTransitionServiceTests
    {
        [Test]
        public void IsTransitioning_IsFalseByDefault()
        {
            var service = new SceneTransitionService();
            Assert.IsFalse(service.IsTransitioning);
        }

        [Test]
        public void SetTransitioning_True_MakesIsTransitioningTrue()
        {
            var service = new SceneTransitionService();
            service.SetTransitioning(true);
            Assert.IsTrue(service.IsTransitioning);
        }

        [Test]
        public void SetTransitioning_False_MakesIsTransitioningFalse()
        {
            var service = new SceneTransitionService();
            service.SetTransitioning(true);
            service.SetTransitioning(false);
            Assert.IsFalse(service.IsTransitioning);
        }

        [Test]
        public void GetColor_WhiteFlash_ReturnsWhite()
        {
            var service = new SceneTransitionService();
            Assert.AreEqual(Color.white, service.GetColor(TransitionStyle.WhiteFlash));
        }

        [Test]
        public void GetColor_BlackFade_ReturnsBlack()
        {
            var service = new SceneTransitionService();
            Assert.AreEqual(Color.black, service.GetColor(TransitionStyle.BlackFade));
        }

        [Test]
        public void GetFadeOutDuration_WhiteFlash_Returns0Point2()
        {
            var service = new SceneTransitionService();
            Assert.AreEqual(0.2f, service.GetFadeOutDuration(TransitionStyle.WhiteFlash), delta: 0.001f);
        }

        [Test]
        public void GetFadeInDuration_WhiteFlash_Returns0Point8()
        {
            var service = new SceneTransitionService();
            Assert.AreEqual(0.8f, service.GetFadeInDuration(TransitionStyle.WhiteFlash), delta: 0.001f);
        }

        [Test]
        public void GetFadeOutDuration_BlackFade_Returns0Point5()
        {
            var service = new SceneTransitionService();
            Assert.AreEqual(0.5f, service.GetFadeOutDuration(TransitionStyle.BlackFade), delta: 0.001f);
        }

        [Test]
        public void GetFadeInDuration_BlackFade_Returns0Point5()
        {
            var service = new SceneTransitionService();
            Assert.AreEqual(0.5f, service.GetFadeInDuration(TransitionStyle.BlackFade), delta: 0.001f);
        }
    }
}
