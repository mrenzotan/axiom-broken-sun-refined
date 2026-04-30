namespace Axiom.Core
{
    /// <summary>
    /// Pure business logic for volume slider operations. No Unity dependencies beyond
    /// the <see cref="AudioManager"/> reference passed in at construction.
    /// </summary>
    public sealed class VolumeSettingsController
    {
        private readonly AudioManager _audioManager;

        public VolumeSettingsController(AudioManager audioManager)
        {
            _audioManager = audioManager;
        }

        public float GetMasterVolume() =>
            _audioManager != null ? _audioManager.GetMasterVolumeNormalized() : 1f;

        public float GetMusicVolume() =>
            _audioManager != null ? _audioManager.GetMusicVolumeNormalized() : 1f;

        public float GetSfxVolume() =>
            _audioManager != null ? _audioManager.GetSfxVolumeNormalized() : 1f;

        public void SetMasterVolume(float linear01)
        {
            if (_audioManager == null) return;
            _audioManager.SetMasterVolume(linear01);
        }

        public void SetMusicVolume(float linear01)
        {
            if (_audioManager == null) return;
            _audioManager.SetMusicVolume(linear01);
        }

        public void SetSfxVolume(float linear01)
        {
            if (_audioManager == null) return;
            _audioManager.SetSfxVolume(linear01);
        }
    }
}
