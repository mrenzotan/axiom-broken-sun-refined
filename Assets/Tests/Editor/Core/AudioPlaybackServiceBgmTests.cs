using Axiom.Core;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Core
{
    public class AudioPlaybackServiceBgmTests
    {
        private class TestMixerHook
        {
            public string LastParam;
            public float LastValue;
            public void Set(string p, float v) { LastParam = p; LastValue = v; }
        }

        [Test]
        public void PlayBgm_StopsPreviousAndStartsNewClip()
        {
            var store = new AudioSettingsStore();
            var hook = new TestMixerHook();
            var go = new GameObject("TestBgmSource");
            var bgm = go.AddComponent<AudioSource>();
            bgm.playOnAwake = false;

            try
            {
                var service = new AudioPlaybackService(
                    config: null, store, bgmSource: bgm, ambientSource: null, uiSource: null,
                    setMixerParam: hook.Set, musicMixerParameterName: "MusicVol", sfxMixerParameterName: "SfxVol",
                    amplifyUiOneShotWithStoredSfx: false);

                var clip = AudioClip.Create("TestClip", 100, 1, 44100, false);
                service.PlayBgm(clip, 0.5f);

                Assert.IsTrue(bgm.isPlaying);
                Assert.AreEqual(clip, bgm.clip);
                Assert.AreEqual(0.5f, bgm.volume, 0.01f);
                Assert.IsTrue(bgm.loop);

                Object.DestroyImmediate(clip);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlayBgm_NullClip_StopsBgm()
        {
            var store = new AudioSettingsStore();
            var hook = new TestMixerHook();
            var go = new GameObject("TestBgmSource");
            var bgm = go.AddComponent<AudioSource>();
            bgm.playOnAwake = false;
            bgm.Play();

            try
            {
                var service = new AudioPlaybackService(
                    config: null, store, bgmSource: bgm, ambientSource: null, uiSource: null,
                    setMixerParam: hook.Set, musicMixerParameterName: "MusicVol", sfxMixerParameterName: "SfxVol",
                    amplifyUiOneShotWithStoredSfx: false);

                service.PlayBgm(null, 1f);

                Assert.IsFalse(bgm.isPlaying);
                Assert.IsNull(bgm.clip);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OnSceneBecameActive_CutsceneScene_DoesNotStopAudio()
        {
            var store = new AudioSettingsStore();
            var hook = new TestMixerHook();
            var go = new GameObject("TestBgmSource");
            var bgm = go.AddComponent<AudioSource>();
            bgm.playOnAwake = false;

            try
            {
                var clip = AudioClip.Create("TestBgm", 100, 1, 44100, false);
                var service = new AudioPlaybackService(
                    config: null, store, bgmSource: bgm, ambientSource: null, uiSource: null,
                    setMixerParam: hook.Set, musicMixerParameterName: "MusicVol", sfxMixerParameterName: "SfxVol",
                    amplifyUiOneShotWithStoredSfx: false);

                service.PlayBgm(clip, 1f);
                service.OnSceneBecameActive("Cutscene");

                Assert.IsTrue(bgm.isPlaying);
                Assert.AreEqual(clip, bgm.clip);

                Object.DestroyImmediate(clip);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OnSceneBecameActive_BattleScene_DoesNotStopAudio()
        {
            var store = new AudioSettingsStore();
            var hook = new TestMixerHook();
            var go = new GameObject("TestBgmSource");
            var bgm = go.AddComponent<AudioSource>();
            bgm.playOnAwake = false;

            try
            {
                var clip = AudioClip.Create("TestBgm", 100, 1, 44100, false);
                var service = new AudioPlaybackService(
                    config: null, store, bgmSource: bgm, ambientSource: null, uiSource: null,
                    setMixerParam: hook.Set, musicMixerParameterName: "MusicVol", sfxMixerParameterName: "SfxVol",
                    amplifyUiOneShotWithStoredSfx: false);

                service.PlayBgm(clip, 1f);
                service.OnSceneBecameActive("Battle");

                Assert.IsTrue(bgm.isPlaying);
                Assert.AreEqual(clip, bgm.clip);

                Object.DestroyImmediate(clip);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
