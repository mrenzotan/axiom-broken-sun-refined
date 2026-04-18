using System;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Core
{
    /// <summary>
    /// Plain C# audio policy: main-menu BGM on <see cref="MainMenuSceneName"/>, in-game ambient on
    /// <see cref="PlatformerSceneName"/>, UI clicks, mixer RTPC from persisted levels, idempotent scene hooks.
    /// </summary>
    public sealed class AudioPlaybackService
    {
        public const string MainMenuSceneName = "MainMenu";

        /// <summary>Gameplay scene that receives the ambient / exploration loop (matches <c>GameManager</c> default).</summary>
        public const string PlatformerSceneName = "Platformer";

        private readonly MenuAudioConfig _config;
        private readonly AudioSettingsStore _store;
        private readonly AudioSource _bgmSource;
        private readonly AudioSource _ambientSource;
        private readonly AudioSource _uiSource;
        private readonly System.Action<string, float> _setMixerParam;
        private readonly string _musicParam;
        private readonly string _ambientParam;
        private readonly string _sfxParam;

        /// <summary>
        /// When false, UI <see cref="AudioSource.PlayOneShot"/> scales by stored SFX level (legacy: no mixer bus).
        /// When true, assume the UI source feeds the SFX mixer group — stored SFX is applied only via RTPC, not here.
        /// </summary>
        private readonly bool _amplifyUiOneShotWithStoredSfx;

        /// <summary>CoreTests: number of times BGM <see cref="AudioSource.Play"/> was invoked after a (re)start.</summary>
        internal int TestBgmPlayInvocationCount { get; private set; }

        /// <summary>CoreTests: number of times ambient <see cref="AudioSource.Play"/> was invoked after a (re)start.</summary>
        internal int TestAmbientPlayInvocationCount { get; private set; }

        public AudioPlaybackService(
            MenuAudioConfig config,
            AudioSettingsStore store,
            AudioSource bgmSource,
            AudioSource ambientSource,
            AudioSource uiSource,
            System.Action<string, float> setMixerParam,
            string musicMixerParameterName,
            string ambientMixerParameterName,
            string sfxMixerParameterName,
            bool amplifyUiOneShotWithStoredSfx = true)
        {
            _config = config;
            _store = store;
            _bgmSource = bgmSource;
            _ambientSource = ambientSource;
            _uiSource = uiSource;
            _setMixerParam = setMixerParam;
            _musicParam = musicMixerParameterName;
            _ambientParam = ambientMixerParameterName;
            _sfxParam = sfxMixerParameterName;
            _amplifyUiOneShotWithStoredSfx = amplifyUiOneShotWithStoredSfx;
        }

        public void ApplyPersistedVolumesToMixer()
        {
            if (_store == null) return;

            float music = _store.GetMusicVolumeNormalized();
            float ambient = _store.GetAmbientVolumeNormalized();
            float sfx = _store.GetSfxVolumeNormalized();
            ApplyMusicMixerOnly(music);
            ApplyAmbientMixerOnly(ambient);
            ApplySfxMixerOnly(sfx);
        }

        public void OnSceneBecameActive(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            if (sceneName == MainMenuSceneName)
                ApplyMainMenuSceneAudio();
            else if (string.Equals(sceneName, PlatformerSceneName, StringComparison.Ordinal))
                ApplyPlatformerSceneAudio();
            else
                StopAllLoopingBuses();

            // Re-apply saved levels whenever the active scene changes so mixer RTPC matches prefs after menu → game.
            ApplyPersistedVolumesToMixer();
        }

        public void SetMusicVolume(float linear01)
        {
            linear01 = Mathf.Clamp01(linear01);
            if (_store != null)
                _store.SetMusicVolume(linear01);

            ApplyMusicMixerOnly(linear01);
            // When ambient has never been saved, store resolves ambient from music; otherwise ambient stays independent.
            float ambientLevel = _store != null ? _store.GetAmbientVolumeNormalized() : linear01;
            ApplyAmbientMixerOnly(ambientLevel);
        }

        public void SetAmbientVolume(float linear01)
        {
            linear01 = Mathf.Clamp01(linear01);
            if (_store != null)
                _store.SetAmbientVolume(linear01);

            ApplyAmbientMixerOnly(linear01);
        }

        public void SetSfxVolume(float linear01)
        {
            linear01 = Mathf.Clamp01(linear01);
            if (_store != null)
                _store.SetSfxVolume(linear01);

            ApplySfxMixerOnly(linear01);
        }

        public float GetMusicVolumeNormalized() =>
            _store != null ? _store.GetMusicVolumeNormalized() : 1f;

        public float GetAmbientVolumeNormalized() =>
            _store != null ? _store.GetAmbientVolumeNormalized() : 1f;

        public float GetSfxVolumeNormalized() =>
            _store != null ? _store.GetSfxVolumeNormalized() : 1f;

        public void PlayUiClick()
        {
            if (_config == null) return;

            AudioClip clip = _config.UiClick;
            if (clip == null || _uiSource == null) return;

            float sfxMul = _amplifyUiOneShotWithStoredSfx ? GetSfxVolumeNormalized() : 1f;
            float vol = Mathf.Clamp01(_config.UiLinear * sfxMul);
            _uiSource.PlayOneShot(clip, vol);
        }

        internal static float LinearVolumeToDecibels(float linear01)
        {
            linear01 = Mathf.Clamp01(linear01);
            if (linear01 <= 1e-4f)
                return -80f;
            return Mathf.Log10(linear01) * 20f;
        }

        private void ApplyMainMenuSceneAudio()
        {
            StopAmbientBus();
            if (_config == null) return;
            if (!_config.ValidateForRuntime(out _))
                return;

            EnsureBgmPlaying();
        }

        private void ApplyPlatformerSceneAudio()
        {
            StopBgmBus();
            if (_config == null) return;
            if (!_config.ValidateForRuntime(out _))
                return;

            EnsureAmbientPlaying();
        }

        private void StopAllLoopingBuses()
        {
            StopBgmBus();
            StopAmbientBus();
        }

        private void StopBgmBus()
        {
            if (_bgmSource == null) return;
            _bgmSource.Stop();
            _bgmSource.clip = null;
        }

        private void StopAmbientBus()
        {
            if (_ambientSource == null) return;
            _ambientSource.Stop();
            _ambientSource.clip = null;
        }

        private void EnsureBgmPlaying()
        {
            if (_bgmSource == null) return;

            AudioClip clip = _config.Bgm;
            if (clip == null) return;

            if (_bgmSource.isPlaying && _bgmSource.clip == clip)
                return;

            _bgmSource.Stop();
            _bgmSource.clip = clip;
            _bgmSource.loop = true;
            _bgmSource.volume = Mathf.Clamp01(_config.BgmLinear);
            _bgmSource.Play();
            TestBgmPlayInvocationCount++;
        }

        private void EnsureAmbientPlaying()
        {
            if (_ambientSource == null) return;

            AudioClip clip = _config.AmbientLoop;
            if (clip == null) return;

            if (_ambientSource.isPlaying && _ambientSource.clip == clip)
                return;

            _ambientSource.Stop();
            _ambientSource.clip = clip;
            _ambientSource.loop = true;
            _ambientSource.volume = Mathf.Clamp01(_config.AmbientLinear);
            _ambientSource.Play();
            TestAmbientPlayInvocationCount++;
        }

        private void ApplyMusicMixerOnly(float linear01)
        {
            TrySetMixer(_musicParam, linear01);
        }

        private void ApplyAmbientMixerOnly(float linear01)
        {
            TrySetMixer(_ambientParam, linear01);
        }

        private void ApplySfxMixerOnly(float linear01)
        {
            TrySetMixer(_sfxParam, linear01);
        }

        private void TrySetMixer(string paramName, float linear01)
        {
            if (_setMixerParam == null || string.IsNullOrEmpty(paramName)) return;

            float db = LinearVolumeToDecibels(linear01);
            _setMixerParam(paramName, db);
        }
    }
}
