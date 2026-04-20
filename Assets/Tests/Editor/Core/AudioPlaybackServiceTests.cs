using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace CoreTests
{
    public class AudioPlaybackServiceTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(AudioSettingsStore.PlayerPrefsKeyMaster);
            PlayerPrefs.DeleteKey(AudioSettingsStore.PlayerPrefsKeyMusic);
            PlayerPrefs.DeleteKey(AudioSettingsStore.PlayerPrefsKeySfx);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(AudioSettingsStore.PlayerPrefsKeyMaster);
            PlayerPrefs.DeleteKey(AudioSettingsStore.PlayerPrefsKeyMusic);
            PlayerPrefs.DeleteKey(AudioSettingsStore.PlayerPrefsKeySfx);
            PlayerPrefs.Save();
        }

        [Test]
        public void AudioSettingsStore_RoundTripsMusicSfxAndMaster()
        {
            var store = new AudioSettingsStore();
            store.SetMasterVolume(0.8f);
            store.SetMusicVolume(0.35f);
            store.SetSfxVolume(0.62f);

            Assert.That(store.GetMasterVolumeNormalized(), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(store.GetMusicVolumeNormalized(), Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(store.GetSfxVolumeNormalized(), Is.EqualTo(0.62f).Within(0.0001f));
        }

        [Test]
        public void AudioSettingsStore_ClampsToUnitRange()
        {
            var store = new AudioSettingsStore();
            store.SetMasterVolume(2f);
            store.SetMusicVolume(2f);
            store.SetSfxVolume(-1f);

            Assert.That(store.GetMasterVolumeNormalized(), Is.EqualTo(1f));
            Assert.That(store.GetMusicVolumeNormalized(), Is.EqualTo(1f));
            Assert.That(store.GetSfxVolumeNormalized(), Is.EqualTo(0f));
        }

        [Test]
        public void LinearVolumeToDecibels_IsMutedNearZero()
        {
            Assert.That(AudioPlaybackService.LinearVolumeToDecibels(0f), Is.EqualTo(-80f).Within(0.01f));
        }

        [Test]
        public void SetMusicVolume_InvokesMixerForMusicOnly()
        {
            var cfg = ScriptableObject.CreateInstance<MenuAudioConfig>();
            cfg.SetClipsForTests(
                AudioClip.Create("b", 1, 1, 44100, false),
                AudioClip.Create("a", 1, 1, 44100, false),
                AudioClip.Create("u", 1, 1, 44100, false));

            var store = new AudioSettingsStore();
            var calls = new List<(string name, float db)>();

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
                    (n, d) => calls.Add((n, d)),
                    "Music",
                    "Sfx");

                svc.SetMusicVolume(0.5f);

                Assert.That(calls.Count, Is.EqualTo(1));
                Assert.That(calls[0].name, Is.EqualTo("Music"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SetSfxVolume_InvokesMixerForSfxOnly()
        {
            var cfg = ScriptableObject.CreateInstance<MenuAudioConfig>();
            cfg.SetClipsForTests(
                AudioClip.Create("b", 1, 1, 44100, false),
                AudioClip.Create("a", 1, 1, 44100, false),
                AudioClip.Create("u", 1, 1, 44100, false));

            var store = new AudioSettingsStore();
            var calls = new List<(string name, float db)>();

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
                    (n, d) => calls.Add((n, d)),
                    "Music",
                    "Sfx");

                svc.SetSfxVolume(0.25f);

                Assert.That(calls.Count, Is.EqualTo(1));
                Assert.That(calls[0].name, Is.EqualTo("Sfx"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OnSceneBecameActive_MainMenuTwice_DoesNotDoubleStartBgm()
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
                    "Sfx");

                svc.OnSceneBecameActive(AudioPlaybackService.MainMenuSceneName);
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
        public void PlayUiClick_DoesNotChangeBgmInvocationCount()
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
                    "Sfx");

                svc.OnSceneBecameActive(AudioPlaybackService.MainMenuSceneName);
                int afterMenu = svc.TestBgmPlayInvocationCount;

                svc.PlayUiClick();

                Assert.That(svc.TestBgmPlayInvocationCount, Is.EqualTo(afterMenu));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
