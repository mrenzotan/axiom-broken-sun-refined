using UnityEngine;

namespace Axiom.Core
{
    /// <summary>
    /// Persists normalized master, music (menu BGM + exploration loop), and SFX levels (0..1) via <see cref="PlayerPrefs"/>.
    /// </summary>
    public sealed class AudioSettingsStore
    {
        public const string PlayerPrefsKeyMaster = "axiom.audio.master";
        public const string PlayerPrefsKeyMusic = "axiom.audio.music";
        public const string PlayerPrefsKeySfx = "axiom.audio.sfx";

        public float GetMasterVolumeNormalized() =>
            Mathf.Clamp01(PlayerPrefs.GetFloat(PlayerPrefsKeyMaster, 1f));

        public void SetMasterVolume(float linear01)
        {
            PlayerPrefs.SetFloat(PlayerPrefsKeyMaster, Mathf.Clamp01(linear01));
            PlayerPrefs.Save();
        }

        public float GetMusicVolumeNormalized() =>
            Mathf.Clamp01(PlayerPrefs.GetFloat(PlayerPrefsKeyMusic, 1f));

        public void SetMusicVolume(float linear01)
        {
            PlayerPrefs.SetFloat(PlayerPrefsKeyMusic, Mathf.Clamp01(linear01));
            PlayerPrefs.Save();
        }

        public float GetSfxVolumeNormalized() =>
            Mathf.Clamp01(PlayerPrefs.GetFloat(PlayerPrefsKeySfx, 1f));

        public void SetSfxVolume(float linear01)
        {
            PlayerPrefs.SetFloat(PlayerPrefsKeySfx, Mathf.Clamp01(linear01));
            PlayerPrefs.Save();
        }
    }
}
