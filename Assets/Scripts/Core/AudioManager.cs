using System;
using Axiom.Data;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Axiom.Core
{
    /// <summary>
    /// MonoBehaviour shell: owns bus <see cref="AudioSource"/> instances, builds
    /// <see cref="AudioPlaybackService"/>, routes mixer RTPC, and reacts to <see cref="GameManager.OnSceneReady"/>.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Clips and default linear gains for main menu BGM, ambient, and UI.")]
        private MenuAudioConfig _menuAudioConfig;

        [SerializeField]
        [Tooltip("Optional. When null, volume prefs still persist but mixer RTPC is skipped.")]
        private AudioMixer _mixer;

        [SerializeField]
        [Tooltip("Exposed mixer parameter for both menu BGM and exploration/ambient loop (same group name for both AudioSources).")]
        private string _musicVolumeParameter = "MusicVol";

        [SerializeField] private string _sfxVolumeParameter = "SfxVol";

        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _ambientSource;
        [SerializeField] private AudioSource _uiSource;

        private AudioSettingsStore _store;
        private AudioPlaybackService _service;

        /// <summary>
        /// Raised after master / music / SFX levels are written to prefs (or listener volume for master).
        /// UI sliders should refresh with <see cref="SetValueWithoutNotify"/> so main menu and pause menu stay in sync.
        /// </summary>
        public event Action PersistedAudioLevelsChanged;

        private void Awake()
        {
            EnsureChildAudioSource(ref _bgmSource, "AxiomAudio_BGM");
            EnsureChildAudioSource(ref _ambientSource, "AxiomAudio_Ambient");
            EnsureChildAudioSource(ref _uiSource, "AxiomAudio_UI");

            if (_bgmSource != null) _bgmSource.playOnAwake = false;
            if (_ambientSource != null) _ambientSource.playOnAwake = false;
            if (_uiSource != null) _uiSource.playOnAwake = false;
        }

        private void Start()
        {
            AssignOutputMixerGroups();

            _store = new AudioSettingsStore();
            _service = new AudioPlaybackService(
                _menuAudioConfig,
                _store,
                _bgmSource,
                _ambientSource,
                _uiSource,
                SetMixerFloatIfConfigured,
                _musicVolumeParameter,
                _sfxVolumeParameter,
                amplifyUiOneShotWithStoredSfx: _mixer == null);

            _service.ApplyPersistedVolumesToMixer();
            AudioListener.volume = _store != null ? _store.GetMasterVolumeNormalized() : 1f;
            _service.OnSceneBecameActive(SceneManager.GetActiveScene().name);

            if (GameManager.Instance != null)
                GameManager.Instance.OnSceneReady += HandleSceneReady;

            NotifyPersistedAudioLevelsChanged();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnSceneReady -= HandleSceneReady;
        }

        private void HandleSceneReady()
        {
            string name = SceneManager.GetActiveScene().name;
            _service?.OnSceneBecameActive(name);
            NotifyPersistedAudioLevelsChanged();
        }

        public void SetMusicVolume(float linear01)
        {
            _service?.SetMusicVolume(linear01);
            NotifyPersistedAudioLevelsChanged();
        }

        public void SetSfxVolume(float linear01)
        {
            _service?.SetSfxVolume(linear01);
            NotifyPersistedAudioLevelsChanged();
        }

        public void SetMasterVolume(float linear01)
        {
            AudioListener.volume = Mathf.Clamp01(linear01);
            if (_store != null)
                _store.SetMasterVolume(Mathf.Clamp01(linear01));
            NotifyPersistedAudioLevelsChanged();
        }

        private void NotifyPersistedAudioLevelsChanged() =>
            PersistedAudioLevelsChanged?.Invoke();

        public float GetMasterVolumeNormalized() =>
            _store != null ? _store.GetMasterVolumeNormalized() : 1f;

        public float GetMusicVolumeNormalized() => _service?.GetMusicVolumeNormalized() ?? 1f;

        public float GetSfxVolumeNormalized() => _service?.GetSfxVolumeNormalized() ?? 1f;

        public void PlayUiClick() => _service?.PlayUiClick();

        /// <summary>
        /// Play a looping <see cref="AudioClip"/> on the BGM bus (routed through MusicVol mixer group).
        /// Null clip stops the bus. Used by CutsceneUI and BattleController for dynamic music.
        /// </summary>
        public void PlayBgm(AudioClip clip, float volume) => _service?.PlayBgm(clip, volume);

        /// <summary>
        /// Sends gameplay or battle <see cref="AudioSource"/> output through the configured SFX mixer group
        /// so the SFX volume slider (<see cref="_sfxVolumeParameter"/>) affects it.
        /// </summary>
        public void RouteSourceThroughSfxBus(AudioSource source) =>
            AssignSourceToMixerGroup(source, _sfxVolumeParameter);

        private void SetMixerFloatIfConfigured(string parameterName, float valueDb)
        {
            if (_mixer == null || string.IsNullOrEmpty(parameterName)) return;
            _mixer.SetFloat(parameterName, valueDb);
        }

        /// <summary>
        /// Routes each bus through the mixer group whose name matches the exposed volume parameter (e.g. MusicVol).
        /// Without this, <see cref="AudioSource"/> output bypasses the mixer and RTPC sliders have no audible effect.
        /// </summary>
        private void AssignOutputMixerGroups()
        {
            if (_mixer == null) return;

            AssignSourceToMixerGroup(_bgmSource, _musicVolumeParameter);
            AssignSourceToMixerGroup(_ambientSource, _musicVolumeParameter);
            AssignSourceToMixerGroup(_uiSource, _sfxVolumeParameter);
        }

        private void AssignSourceToMixerGroup(AudioSource source, string groupName)
        {
            if (source == null || string.IsNullOrEmpty(groupName)) return;

            AudioMixerGroup[] matches = _mixer.FindMatchingGroups(groupName);
            if (matches == null || matches.Length == 0)
                matches = _mixer.FindMatchingGroups("Master/" + groupName);

            if (matches == null || matches.Length == 0)
            {
                Debug.LogWarning(
                    $"[AudioManager] No mixer group matching '{groupName}' or 'Master/{groupName}' on '{_mixer.name}'. " +
                    $"Volume sliders will not affect this AudioSource until groups and parameter names align.",
                    this);
                return;
            }

            source.outputAudioMixerGroup = matches[0];
        }

        private void EnsureChildAudioSource(ref AudioSource field, string childName)
        {
            if (field != null) return;

            Transform existing = transform.Find(childName);
            if (existing != null)
            {
                field = existing.GetComponent<AudioSource>();
                if (field != null) return;
            }

            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            field = go.AddComponent<AudioSource>();
        }
    }
}
