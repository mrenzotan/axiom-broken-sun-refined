using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "MenuAudioConfig", menuName = "Axiom/Audio/Menu Audio Config")]
    public sealed class MenuAudioConfig : ScriptableObject
    {
        [SerializeField] private AudioClip _bgm;
        [SerializeField] private AudioClip _ambientLoop;
        [SerializeField] private AudioClip _uiClick;
        [Range(0f, 1f)] [SerializeField] private float _bgmLinear = 1f;
        [Range(0f, 1f)] [SerializeField] private float _ambientLinear = 1f;
        [Range(0f, 1f)] [SerializeField] private float _uiLinear = 1f;

        public AudioClip Bgm => _bgm;
        public AudioClip AmbientLoop => _ambientLoop;
        public AudioClip UiClick => _uiClick;
        public float BgmLinear => _bgmLinear;
        public float AmbientLinear => _ambientLinear;
        public float UiLinear => _uiLinear;

#if UNITY_EDITOR
        /// <summary>Edit Mode tests only — <c>Axiom.Data</c> does not define <c>UNITY_INCLUDE_TESTS</c>.</summary>
        public void SetClipsForTests(AudioClip bgm, AudioClip ambient, AudioClip ui)
        {
            _bgm = bgm;
            _ambientLoop = ambient;
            _uiClick = ui;
        }
#endif

        /// <summary>False when required clips missing — check before starting menu audio.</summary>
        public bool ValidateForRuntime(out string error)
        {
            if (_bgm == null || _ambientLoop == null || _uiClick == null)
            {
                error = "MenuAudioConfig: assign BGM, Ambient, and UI clips.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
