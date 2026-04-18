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

        [SerializeField] private string _musicVolumeParameter = "MusicVol";
        [SerializeField] private string _ambientVolumeParameter = "AmbientVol";
        [SerializeField] private string _sfxVolumeParameter = "SfxVol";

        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _ambientSource;
        [SerializeField] private AudioSource _uiSource;

        private AudioSettingsStore _store;
        private AudioPlaybackService _service;

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
                _ambientVolumeParameter,
                _sfxVolumeParameter,
                amplifyUiOneShotWithStoredSfx: _mixer == null);

            _service.ApplyPersistedVolumesToMixer();
            _service.OnSceneBecameActive(SceneManager.GetActiveScene().name);

            if (GameManager.Instance != null)
                GameManager.Instance.OnSceneReady += HandleSceneReady;
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
        }

        public void SetMusicVolume(float linear01) => _service?.SetMusicVolume(linear01);

        public void SetAmbientVolume(float linear01) => _service?.SetAmbientVolume(linear01);

        public void SetSfxVolume(float linear01) => _service?.SetSfxVolume(linear01);

        public float GetMusicVolumeNormalized() => _service?.GetMusicVolumeNormalized() ?? 1f;

        public float GetAmbientVolumeNormalized() => _service?.GetAmbientVolumeNormalized() ?? 1f;

        public float GetSfxVolumeNormalized() => _service?.GetSfxVolumeNormalized() ?? 1f;

        public void PlayUiClick() => _service?.PlayUiClick();

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
            AssignSourceToMixerGroup(_ambientSource, _ambientVolumeParameter);
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
