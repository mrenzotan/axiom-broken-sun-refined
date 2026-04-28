using Axiom.Data;
using NUnit.Framework;

namespace Axiom.Tests.Data
{
    public class CutsceneDataTests
    {
        [Test]
        public void DefaultNextSceneName_IsLevel1_1()
        {
            var data = CutsceneData.CreateInstance<CutsceneData>();
            Assert.AreEqual("Level_1-1", data.nextSceneName);
        }

        [Test]
        public void SlidesList_IsInitializedAndEmpty()
        {
            var data = CutsceneData.CreateInstance<CutsceneData>();
            Assert.IsNotNull(data.slides);
            Assert.AreEqual(0, data.slides.Count);
        }

        [Test]
        public void CutsceneMusic_DefaultsToNull()
        {
            var data = CutsceneData.CreateInstance<CutsceneData>();
            Assert.IsNull(data.cutsceneMusic);
        }

        [Test]
        public void HasCreateAssetMenuAttribute()
        {
            var attrs = typeof(CutsceneData).GetCustomAttributes(
                typeof(UnityEngine.CreateAssetMenuAttribute), false);
            Assert.AreEqual(1, attrs.Length);
        }
    }
}
