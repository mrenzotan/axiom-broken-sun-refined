using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace DataTests
{
    public class MenuAudioConfigTests
    {
        [Test]
        public void ValidateForRuntime_ReturnsFalse_WhenBgmMissing()
        {
            var cfg = ScriptableObject.CreateInstance<MenuAudioConfig>();
            cfg.SetClipsForTests(
                bgm: null,
                ambient: AudioClip.Create("a", 1, 1, 44100, false),
                ui: AudioClip.Create("u", 1, 1, 44100, false));

            Assert.IsFalse(cfg.ValidateForRuntime(out string _));
        }

        [Test]
        public void ValidateForRuntime_ReturnsTrue_WhenAllClipsAssigned()
        {
            var cfg = ScriptableObject.CreateInstance<MenuAudioConfig>();
            cfg.SetClipsForTests(
                bgm: AudioClip.Create("b", 1, 1, 44100, false),
                ambient: AudioClip.Create("a", 1, 1, 44100, false),
                ui: AudioClip.Create("u", 1, 1, 44100, false));

            Assert.IsTrue(cfg.ValidateForRuntime(out string message), message);
        }
    }
}
