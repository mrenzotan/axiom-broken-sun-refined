using Axiom.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Axiom.Core
{
    public class CutsceneUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Full-screen Image that displays each slide's sprite.")]
        private Image _slideImage;

        [SerializeField]
        [Tooltip("TextMeshPro text box for the typewriter effect.")]
        private TMP_Text _textBox;

        [SerializeField]
        [Tooltip("Optional: assign directly to override GameManager-driven data.")]
        private CutsceneData _cutsceneData;

        [SerializeField]
        [Tooltip("Characters revealed per second. Default: 40.")]
        [Min(1f)]
        private float _charsPerSecond = 40f;

        [SerializeField]
        [Tooltip("Transition style used when loading the next scene.")]
        private TransitionStyle _exitTransitionStyle = TransitionStyle.BlackFade;

        private CutscenePlayer _player;
        private TypewriterEffect _typewriter;
        private float _autoAdvanceTimer;

        public bool IsPlaying => _player != null && !_player.IsComplete;

        private void Awake()
        {
            if (Camera.main == null && Camera.allCamerasCount == 0)
            {
                var camGo = new GameObject("CutsceneCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                Debug.Log("[CutsceneUI] No camera found in scene — created fallback MainCamera.", this);
            }
        }

        private void Start()
        {
            _player = new CutscenePlayer();
            _typewriter = new TypewriterEffect();

            if (_cutsceneData == null)
            {
                Debug.LogWarning("[CutsceneUI] No CutsceneData assigned. Cutscene will complete immediately.", this);
            }

            if (_slideImage == null)
                Debug.LogError("[CutsceneUI] _slideImage (Image) is not assigned in the Inspector. Slide images will not display.", this);

            if (_textBox == null)
                Debug.LogError("[CutsceneUI] _textBox (TMP_Text) is not assigned in the Inspector. Text will not display.", this);

            EnsureSlideImageReady();

            _player.Start(_cutsceneData);

            if (!_player.IsComplete)
            {
                PlayCutsceneMusic();
                RenderCurrentSlide();
            }
        }

        private void Update()
        {
            if (_player == null || _player.IsComplete)
            {
                HandleCompletion();
                return;
            }

            HandleInput();

            if (_typewriter != null && !_typewriter.IsComplete)
            {
                _typewriter.Update(Time.deltaTime);
                if (_textBox != null)
                    _textBox.text = _typewriter.VisibleText;

                if (_typewriter.IsComplete)
                {
                    float delay = _player.CurrentSlide?.autoAdvanceDelay ?? 3f;
                    _autoAdvanceTimer = delay >= 0f ? delay : 0f;
                }
            }

            if (_typewriter != null && _typewriter.IsComplete && _autoAdvanceTimer > 0f)
            {
                _autoAdvanceTimer -= Time.deltaTime;
                if (_autoAdvanceTimer <= 0f)
                    AdvanceSlide();
            }
        }

        private void HandleInput()
        {
            if (_player == null || _player.IsComplete) return;

            // Enter key skips the entire cutscene immediately
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            {
                _player.Skip();
                return;
            }

            // Space, mouse click, or gamepad A advances the current slide (or finishes typewriter)
            bool advancePressed =
                (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (Gamepad.current != null && Gamepad.current.aButton.wasPressedThisFrame)
                advancePressed = true;

            if (!advancePressed) return;

            if (_typewriter != null && !_typewriter.IsComplete)
            {
                _typewriter.SkipToEnd();
                if (_textBox != null)
                    _textBox.text = _typewriter.VisibleText;
                _autoAdvanceTimer = _player.CurrentSlide?.autoAdvanceDelay ?? 3f;
            }
            else
            {
                AdvanceSlide();
            }
        }

        private void AdvanceSlide()
        {
            if (_player == null || _player.IsComplete) return;

            _player.Advance();

            if (!_player.IsComplete)
                RenderCurrentSlide();
            else
                HandleCompletion();
        }

        private void RenderCurrentSlide()
        {
            CutsceneSlide slide = _player.CurrentSlide;

            if (_slideImage != null)
            {
                if (slide?.image != null)
                {
                    _slideImage.sprite = slide.image;
                    _slideImage.enabled = true;
                    if (_slideImage.color.a < 0.01f)
                        _slideImage.color = Color.white;
                }
                else
                {
                    _slideImage.enabled = false;
                    Debug.Log($"[CutsceneUI] Slide {_player.CurrentSlideIndex + 1} has no image assigned.", this);
                }
            }

            if (_textBox != null && _typewriter != null && slide != null)
            {
                _typewriter.Start(slide.text ?? "", _charsPerSecond);
                _textBox.text = _typewriter.VisibleText;
                _autoAdvanceTimer = 0f;
            }
        }

        private void EnsureSlideImageReady()
        {
            if (_slideImage == null) return;

            RectTransform rt = _slideImage.rectTransform;

            // Ensure the image fills the canvas (stretch to all four edges)
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            // Ensure fully opaque white so the sprite renders at full opacity
            _slideImage.color = Color.white;
            _slideImage.enabled = true;
            _slideImage.raycastTarget = false;
        }

        private void PlayCutsceneMusic()
        {
            AudioClip clip = _player?.CutsceneMusic;
            if (clip == null)
            {
                Debug.Log("[CutsceneUI] No cutsceneMusic assigned on CutsceneData — no music will play.", this);
                return;
            }

            AudioManager audioManager = GetAudioManager();
            if (audioManager == null)
            {
                Debug.LogError("[CutsceneUI] Cannot play cutscene music — GameManager.Instance or AudioManager not found.", this);
                return;
            }

            Debug.Log($"[CutsceneUI] Playing cutscene music: {clip.name}", this);
            audioManager.PlayBgm(clip, 1f);
        }

        private void HandleCompletion()
        {
            if (_player == null || !_player.IsComplete) return;

            string nextScene = _player.NextSceneName;
            if (string.IsNullOrEmpty(nextScene))
            {
                Debug.LogWarning("[CutsceneUI] Cutscene complete but no nextSceneName set.", this);
                return;
            }

            SceneTransitionController transition = GetSceneTransition();
            if (transition != null && !transition.IsTransitioning)
            {
                transition.BeginTransition(nextScene, _exitTransitionStyle);
            }
            else if (transition == null)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
            }

            _player = null;
        }

        private SceneTransitionController GetSceneTransition()
        {
            GameManager gm = GameManager.Instance;
            return gm?.SceneTransition;
        }

        private AudioManager GetAudioManager()
        {
            GameManager gm = GameManager.Instance;
            return gm?.AudioManager;
        }
    }
}
