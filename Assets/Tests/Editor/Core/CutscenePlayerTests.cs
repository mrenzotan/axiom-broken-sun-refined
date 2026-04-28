using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;

namespace Axiom.Tests.Core
{
    public class CutscenePlayerTests
    {
        private CutsceneData MakeData(List<CutsceneSlide> slides, string nextScene = "Level_1-1")
        {
            var data = CutsceneData.CreateInstance<CutsceneData>();
            data.nextSceneName = nextScene;
            data.slides = slides;
            return data;
        }

        private CutsceneSlide MakeSlide(string text = "Slide text", UnityEngine.Sprite image = null)
        {
            return new CutsceneSlide { text = text, image = image, autoAdvanceDelay = 0f };
        }

        [Test]
        public void Start_WithSlides_SetsCurrentSlideToFirst()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide>
            {
                MakeSlide("First"), MakeSlide("Second")
            });
            player.Start(data);

            Assert.AreEqual(0, player.CurrentSlideIndex);
            Assert.AreEqual("First", player.CurrentSlide.text);
            Assert.IsFalse(player.IsComplete);
        }

        [Test]
        public void Advance_MovesToNextSlide()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide>
            {
                MakeSlide("First"), MakeSlide("Second"), MakeSlide("Third")
            });
            player.Start(data);
            player.Advance();

            Assert.AreEqual(1, player.CurrentSlideIndex);
            Assert.AreEqual("Second", player.CurrentSlide.text);
            Assert.IsFalse(player.IsComplete);
        }

        [Test]
        public void Advance_PastLastSlide_MarksComplete()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide>
            {
                MakeSlide("Only slide")
            });
            player.Start(data);
            player.Advance();

            Assert.IsTrue(player.IsComplete);
        }

        [Test]
        public void Advance_WhenComplete_StaysComplete()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide>
            {
                MakeSlide("Only slide")
            });
            player.Start(data);
            player.Advance();
            player.Advance();

            Assert.IsTrue(player.IsComplete);
            Assert.GreaterOrEqual(player.CurrentSlideIndex, 0);
        }

        [Test]
        public void Skip_MarksCompleteImmediately()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide>
            {
                MakeSlide("First"), MakeSlide("Second"), MakeSlide("Third")
            });
            player.Start(data);
            player.Skip();

            Assert.IsTrue(player.IsComplete);
        }

        [Test]
        public void Start_EmptySlides_IsCompleteImmediately()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide>());
            player.Start(data);

            Assert.IsTrue(player.IsComplete);
        }

        [Test]
        public void Start_NullData_IsCompleteWithNullSlide()
        {
            var player = new CutscenePlayer();
            player.Start(null);

            Assert.IsTrue(player.IsComplete);
            Assert.IsNull(player.CurrentSlide);
        }

        [Test]
        public void CurrentSlide_WhenComplete_IsNull()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide> { MakeSlide("Only") });
            player.Start(data);
            player.Advance();

            Assert.IsNull(player.CurrentSlide);
        }

        [Test]
        public void NextSceneName_ReturnsFromData()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide> { MakeSlide() }, nextScene: "TestScene");
            player.Start(data);

            Assert.AreEqual("TestScene", player.NextSceneName);
        }

        [Test]
        public void NextSceneName_WithNullData_ReturnsEmptyString()
        {
            var player = new CutscenePlayer();
            player.Start(null);

            Assert.AreEqual("", player.NextSceneName);
        }

        [Test]
        public void CutsceneMusic_ReturnsFromData()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide> { MakeSlide() });
            data.cutsceneMusic = null;
            player.Start(data);

            Assert.IsNull(player.CutsceneMusic);
        }
    }
}
