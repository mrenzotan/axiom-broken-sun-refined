using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace CoreTests
{
    public class AudioTransitionStabilityTests
    {
        [Test]
        public void RepeatedMainMenuSceneReady_DoesNotIncrementBgmStarts()
        {
            var cfg = ScriptableObject.CreateInstance<MenuAudioConfig>();
            cfg.SetClipsForTests(
                AudioClip.Create("b", 1, 1, 44100, false),
                AudioClip.Create("a", 1, 1, 44100, false),
                AudioClip.Create("u", 1, 1, 44100, false));

            var store = new AudioSettingsStore();

            var host = new GameObject("AudioHost");
            try
            {
                AudioSource bgm = host.AddComponent<AudioSource>();
                AudioSource amb = host.AddComponent<AudioSource>();
                AudioSource ui = host.AddComponent<AudioSource>();

                var svc = new AudioPlaybackService(
                    cfg,
                    store,
                    bgm,
                    amb,
                    ui,
                    null,
                    "Music",
                    "Ambient",
                    "Sfx");

                for (int i = 0; i < 50; i++)
                    svc.OnSceneBecameActive(AudioPlaybackService.MainMenuSceneName);

                Assert.That(svc.TestBgmPlayInvocationCount, Is.EqualTo(1));
                Assert.That(svc.TestAmbientPlayInvocationCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void LeavingMainMenuThenReturning_RestartsBgm()
        {
            var cfg = ScriptableObject.CreateInstance<MenuAudioConfig>();
            cfg.SetClipsForTests(
                AudioClip.Create("b", 1, 1, 44100, false),
                AudioClip.Create("a", 1, 1, 44100, false),
                AudioClip.Create("u", 1, 1, 44100, false));

            var store = new AudioSettingsStore();

            var host = new GameObject("AudioHost");
            try
            {
                AudioSource bgm = host.AddComponent<AudioSource>();
                AudioSource amb = host.AddComponent<AudioSource>();
                AudioSource ui = host.AddComponent<AudioSource>();

                var svc = new AudioPlaybackService(
                    cfg,
                    store,
                    bgm,
                    amb,
                    ui,
                    null,
                    "Music",
                    "Ambient",
                    "Sfx");

                svc.OnSceneBecameActive(AudioPlaybackService.MainMenuSceneName);
                svc.OnSceneBecameActive(AudioPlaybackService.PlatformerSceneName);
                svc.OnSceneBecameActive(AudioPlaybackService.MainMenuSceneName);

                Assert.That(svc.TestBgmPlayInvocationCount, Is.EqualTo(2));
                Assert.That(svc.TestAmbientPlayInvocationCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
