using UnityEngine;

namespace Axiom.Core
{
    /// <summary>
    /// Persists normalized music, ambient, and SFX levels (0..1) via <see cref="PlayerPrefs"/>.
    /// </summary>
    public sealed class AudioSettingsStore
    {
        public const string PlayerPrefsKeyMusic = "axiom.audio.music";
        public const string PlayerPrefsKeyAmbient = "axiom.audio.ambient";
        public const string PlayerPrefsKeySfx = "axiom.audio.sfx";

        public float GetMusicVolumeNormalized() =>
            Mathf.Clamp01(PlayerPrefs.GetFloat(PlayerPrefsKeyMusic, 1f));

        /// <summary>
        /// Until the player saves ambient explicitly, mirrors <see cref="GetMusicVolumeNormalized"/> so legacy installs stay in sync.
        /// </summary>
        public float GetAmbientVolumeNormalized() =>
            !PlayerPrefs.HasKey(PlayerPrefsKeyAmbient)
                ? GetMusicVolumeNormalized()
                : Mathf.Clamp01(PlayerPrefs.GetFloat(PlayerPrefsKeyAmbient));

        public float GetSfxVolumeNormalized() =>
            Mathf.Clamp01(PlayerPrefs.GetFloat(PlayerPrefsKeySfx, 1f));

        public void SetMusicVolume(float linear01)
        {
            PlayerPrefs.SetFloat(PlayerPrefsKeyMusic, Mathf.Clamp01(linear01));
            PlayerPrefs.Save();
        }

        public void SetAmbientVolume(float linear01)
        {
            PlayerPrefs.SetFloat(PlayerPrefsKeyAmbient, Mathf.Clamp01(linear01));
            PlayerPrefs.Save();
        }

        public void SetSfxVolume(float linear01)
        {
            PlayerPrefs.SetFloat(PlayerPrefsKeySfx, Mathf.Clamp01(linear01));
            PlayerPrefs.Save();
        }
    }
}
