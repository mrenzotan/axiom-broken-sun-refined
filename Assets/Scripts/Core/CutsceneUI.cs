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

        [Header("Hold-to-Skip")]
        [SerializeField]
        [Tooltip("Seconds to hold Enter before skipping the entire cutscene.")]
        [Min(0.1f)]
        private float _holdToSkipDuration = 3f;

        [SerializeField]
        [Tooltip("Max hold duration (seconds) considered a tap. Releases below this threshold advance the slide.")]
        [Min(0.01f)]
        private float _tapThreshold = 0.2f;

        [SerializeField]
        [Tooltip("Image component for the radial progress ring (Image Type: Filled, Fill Method: Radial 360). Shown while holding Enter.")]
        private Image _skipRingImage;

        [SerializeField]
        [Tooltip("Fill portion of the radial ring. Must be a child of _skipRingImage or share same rect.")]
        private Image _skipRingFill;

        [SerializeField]
        [Tooltip("TMP_Text label shown alongside the ring (e.g. 'Hold Enter to skip').")]
        private TMP_Text _skipPromptText;

        private CutscenePlayer _player;
        private TypewriterEffect _typewriter;
        private CutsceneInputHandler _inputHandler;
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
            _inputHandler = new CutsceneInputHandler
            {
                HoldToSkipDuration = _holdToSkipDuration,
                TapThreshold = _tapThreshold
            };

            if (_cutsceneData == null)
            {
                Debug.LogWarning("[CutsceneUI] No CutsceneData assigned. Cutscene will complete immediately.", this);
            }

            if (_slideImage == null)
                Debug.LogError("[CutsceneUI] _slideImage (Image) is not assigned in the Inspector. Slide images will not display.", this);

            if (_textBox == null)
                Debug.LogError("[CutsceneUI] _textBox (TMP_Text) is not assigned in the Inspector. Text will not display.", this);

            EnsureSlideImageReady();
            HideSkipRingUI();

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
            UpdateSkipRingUI();

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

            Keyboard kb = Keyboard.current;

            if (kb != null)
            {
                var result = _inputHandler.ProcessEnterInput(
                    kb.enterKey.wasPressedThisFrame,
                    kb.enterKey.wasReleasedThisFrame,
                    Time.deltaTime
                );

                switch (result)
                {
                    case CutsceneInputResult.Skip:
                        Debug.Log("[CutsceneUI] Hold-to-skip triggered.");
                        _player.Skip();
                        return;
                    case CutsceneInputResult.Advance:
                        TryAdvanceOrFinishTypewriter();
                        return;
                }
            }
            else if (_inputHandler.IsHoldingEnter)
            {
                _inputHandler.Reset();
            }

            bool advancePressed =
                (kb != null && kb.spaceKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (Gamepad.current != null && Gamepad.current.aButton.wasPressedThisFrame)
                advancePressed = true;

            if (advancePressed)
                TryAdvanceOrFinishTypewriter();
        }

        private void TryAdvanceOrFinishTypewriter()
        {
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

        private void UpdateSkipRingUI()
        {
            if (_inputHandler == null) return;

            bool show = _inputHandler.IsHoldingEnter;

            if (_skipRingImage != null)
                _skipRingImage.gameObject.SetActive(show);

            if (_skipRingFill != null)
            {
                _skipRingFill.gameObject.SetActive(show);
                if (show)
                    _skipRingFill.fillAmount = _inputHandler.SkipProgress;
            }

            if (_skipPromptText != null)
                _skipPromptText.gameObject.SetActive(show);
        }

        private void HideSkipRingUI()
        {
            if (_skipRingImage != null)
                _skipRingImage.gameObject.SetActive(false);
            if (_skipRingFill != null)
                _skipRingFill.gameObject.SetActive(false);
            if (_skipPromptText != null)
                _skipPromptText.gameObject.SetActive(false);
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

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

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
