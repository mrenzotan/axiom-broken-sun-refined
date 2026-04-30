using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// MonoBehaviour shell for the pause menu's Settings panel volume sliders.
    /// Wires three sliders (Master, Music, SFX) to <see cref="AudioManager"/>.
    /// All logic lives in the plain C# <see cref="VolumeSettingsController"/>; this
    /// class handles Unity lifecycle only.
    /// </summary>
    public class VolumeSettingsUI : MonoBehaviour
    {
        [SerializeField] private Slider _masterSlider;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;

        private AudioManager _audioManager;
        private AudioManager _persistedAudioLevelsSubscription;
        private AudioManager _controllerOwner;
        private VolumeSettingsController _controller;

        private void OnEnable()
        {
            ResolveAudioManager();
            SubscribePersistedAudioLevelsChanged();
            EnsureController();
            SyncSlidersFromPersisted();
        }

        private void Start()
        {
            ResolveAudioManager();
            SubscribePersistedAudioLevelsChanged();
            EnsureController();

            if (_masterSlider != null)
            {
                _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
                _masterSlider.SetValueWithoutNotify(_audioManager != null ? _audioManager.GetMasterVolumeNormalized() : 1f);
                _masterSlider.onValueChanged.AddListener(OnMasterChanged);
            }

            if (_musicSlider != null)
            {
                _musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
                _musicSlider.SetValueWithoutNotify(_audioManager != null ? _audioManager.GetMusicVolumeNormalized() : 1f);
                _musicSlider.onValueChanged.AddListener(OnMusicChanged);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
                _sfxSlider.SetValueWithoutNotify(_audioManager != null ? _audioManager.GetSfxVolumeNormalized() : 1f);
                _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            }
        }

        private void OnDestroy()
        {
            if (_persistedAudioLevelsSubscription != null)
                _persistedAudioLevelsSubscription.PersistedAudioLevelsChanged -= SyncSlidersFromPersisted;
            _persistedAudioLevelsSubscription = null;

            if (_masterSlider != null) _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            if (_musicSlider != null) _musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            if (_sfxSlider != null) _sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        }

        private void ResolveAudioManager()
        {
            AudioManager fromGameManager = null;
            if (GameManager.Instance != null)
                fromGameManager = GameManager.Instance.GetComponentInChildren<AudioManager>(true);

            _audioManager = fromGameManager != null ? fromGameManager : FindAnyObjectByType<AudioManager>();
        }

        private void SubscribePersistedAudioLevelsChanged()
        {
            ResolveAudioManager();
            if (_persistedAudioLevelsSubscription == _audioManager)
                return;

            if (_persistedAudioLevelsSubscription != null)
                _persistedAudioLevelsSubscription.PersistedAudioLevelsChanged -= SyncSlidersFromPersisted;

            _persistedAudioLevelsSubscription = _audioManager;

            if (_persistedAudioLevelsSubscription != null)
                _persistedAudioLevelsSubscription.PersistedAudioLevelsChanged += SyncSlidersFromPersisted;
        }

        private void EnsureController()
        {
            ResolveAudioManager();
            if (_audioManager == _controllerOwner && _controller != null)
                return;

            _controllerOwner = _audioManager;
            _controller = _audioManager != null ? new VolumeSettingsController(_audioManager) : null;
        }

        private void SyncSlidersFromPersisted()
        {
            ResolveAudioManager();
            if (_audioManager == null) return;

            if (_masterSlider != null)
                _masterSlider.SetValueWithoutNotify(_audioManager.GetMasterVolumeNormalized());
            if (_musicSlider != null)
                _musicSlider.SetValueWithoutNotify(_audioManager.GetMusicVolumeNormalized());
            if (_sfxSlider != null)
                _sfxSlider.SetValueWithoutNotify(_audioManager.GetSfxVolumeNormalized());
        }

        private void OnMasterChanged(float value)
        {
            EnsureController();
            _controller?.SetMasterVolume(value);
        }

        private void OnMusicChanged(float value)
        {
            EnsureController();
            _controller?.SetMusicVolume(value);
        }

        private void OnSfxChanged(float value)
        {
            EnsureController();
            _controller?.SetSfxVolume(value);
        }
    }
}
