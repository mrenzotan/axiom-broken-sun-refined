using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// Screen-space software cursor with Idle / Hover / Click / Disabled presentation,
    /// optional per-state sprite animation, and press-scale while the button is held (and briefly after tap).
    /// Hides the OS cursor while enabled. Assign sprites in the Inspector and place this on a
    /// Canvas that has a <see cref="GraphicRaycaster"/> (same as your menu UI).
    /// Optional <see cref="_persistAcrossScenes"/> keeps one instance alive across loads (see tooltips).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StateBasedCursorUI : MonoBehaviour
    {
        private static StateBasedCursorUI _instance;
        public enum PresentationState
        {
            Idle,
            Hover,
            Click,
            Disabled
        }

        [Serializable]
        public struct StateProfile
        {
            [Tooltip("Frames played in order while this state is active. Single sprite = static.")]
            public Sprite[] Sprites;

            [Tooltip("Animation speed for Sprites. 0 = hold the first sprite only.")]
            public float FramesPerSecond;
        }

        [Header("References")]
        [SerializeField]
        [Tooltip("The Image that draws the cursor. Raycast Target should be OFF.")]
        private RectTransform _cursorRect;

        [SerializeField]
        private Image _cursorImage;

        [Header("State visuals")]
        [SerializeField]
        private StateProfile _idle;

        [SerializeField]
        private StateProfile _hover;

        [SerializeField]
        private StateProfile _click;

        [SerializeField]
        private StateProfile _disabled;

        [Header("Layout")]
        [SerializeField]
        [Tooltip("Extra screen-space offset in pixels (e.g. nudge after changing pivot).")]
        private Vector2 _screenPixelOffset;

        [Header("Click feedback")]
        [SerializeField]
        [Tooltip("After a quick tap (release before this window ends), how long Click visuals / scale feedback last (unscaled seconds). While the button is held, pressed state stays until release.")]
        private float _clickStateSeconds = 0.12f;

        [SerializeField]
        [Tooltip("Local scale while the click state is active.")]
        private float _clickPressedScale = 0.92f;

        [SerializeField]
        [Tooltip("How fast the cursor scale returns to 1 after a click.")]
        private float _scaleRecoverSpeed = 18f;

        [Header("Scene persistence")]
        [SerializeField]
        [Tooltip("Keeps this cursor alive when you load another scene. Put the whole cursor UI under " +
                 "its own root (Canvas + Image), not under a menu that should unload — then enable this.")]
        private bool _persistAcrossScenes;

        [SerializeField]
        [Tooltip("Object passed to DontDestroyOnLoad. Leave empty to use transform.root. " +
                 "Must be an ancestor of this object (usually the prefab root).")]
        private Transform _persistRoot;

        private readonly List<RaycastResult> _raycastHits = new List<RaycastResult>(16);
        private PointerEventData _pointerEventData;

        private PresentationState _presentation;
        private int _frameIndex;
        private float _frameTimer;
        private float _clickUntilUnscaled;
        private float _displayScale = 1f;
        private bool _forceDisabled;
        private Vector3 _baseLocalScale = Vector3.one;

        /// <summary>When true, the Disabled profile is shown and hover is ignored.</summary>
        public bool ForceDisabled
        {
            get => _forceDisabled;
            set => _forceDisabled = value;
        }

        private void Awake()
        {
            if (_persistAcrossScenes)
            {
                if (_instance != null && _instance != this)
                {
                    GameObject discard = _persistRoot != null ? _persistRoot.gameObject : transform.root.gameObject;
                    Destroy(discard);
                    return;
                }

                _instance = this;
                GameObject persist = (_persistRoot != null ? _persistRoot : transform.root).gameObject;
                DontDestroyOnLoad(persist);
            }

            if (_cursorRect != null)
                _baseLocalScale = _cursorRect.localScale;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnEnable()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.None;
            _frameIndex = 0;
            _frameTimer = 0f;
            _clickUntilUnscaled = 0f;
            _displayScale = 1f;
            ApplyPresentation(ResolvePresentationState(), forceSpriteReset: true);
        }

        private void OnDisable()
        {
            Cursor.visible = true;
        }

        private void Update()
        {
            if (_cursorRect == null || _cursorImage == null)
                return;

            Vector2 screenPos = ReadPointerScreenPosition();
            _cursorRect.position = screenPos + _screenPixelOffset;

            if (WasPrimaryPressedThisFrame())
                _clickUntilUnscaled = Time.unscaledTime + Mathf.Max(0.01f, _clickStateSeconds);

            PresentationState next = ResolvePresentationState();
            bool stateChanged = next != _presentation;
            if (stateChanged)
                ApplyPresentation(next, forceSpriteReset: true);
            else
                StepAnimation(Time.unscaledDeltaTime);

            UpdateClickScale(Time.unscaledDeltaTime);
            _cursorRect.localScale = _baseLocalScale * _displayScale;
        }

        /// <summary>Call from gameplay systems to grey out or restore the cursor.</summary>
        public void SetForceDisabled(bool disabled) => _forceDisabled = disabled;

        private void ApplyPresentation(PresentationState state, bool forceSpriteReset)
        {
            _presentation = state;
            if (forceSpriteReset)
            {
                _frameIndex = 0;
                _frameTimer = 0f;
            }

            var profile = GetProfile(state);
            var sprites = profile.Sprites;
            if (sprites == null || sprites.Length == 0)
            {
                _cursorImage.enabled = false;
                return;
            }

            _cursorImage.enabled = true;
            _cursorImage.sprite = sprites[Mathf.Clamp(_frameIndex, 0, sprites.Length - 1)];
        }

        private void StepAnimation(float unscaledDeltaTime)
        {
            var profile = GetProfile(_presentation);
            var sprites = profile.Sprites;
            if (sprites == null || sprites.Length <= 1 || profile.FramesPerSecond <= 0f)
                return;

            _frameTimer += unscaledDeltaTime;
            float frameDuration = 1f / profile.FramesPerSecond;
            while (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _frameIndex = (_frameIndex + 1) % sprites.Length;
            }

            _cursorImage.sprite = sprites[_frameIndex];
        }

        private void UpdateClickScale(float unscaledDeltaTime)
        {
            bool pressed = IsPrimaryHeld() || Time.unscaledTime < _clickUntilUnscaled;
            float target = pressed ? _clickPressedScale : 1f;
            float t = 1f - Mathf.Exp(-_scaleRecoverSpeed * unscaledDeltaTime);
            _displayScale = Mathf.Lerp(_displayScale, target, t);
        }

        private PresentationState ResolvePresentationState()
        {
            if (_forceDisabled)
                return PresentationState.Disabled;

            if ((IsPrimaryHeld() || Time.unscaledTime < _clickUntilUnscaled) && HasSprites(_click))
                return PresentationState.Click;

            if (IsPointerOverInteractableUi())
                return PresentationState.Hover;

            return PresentationState.Idle;
        }

        private StateProfile GetProfile(PresentationState state) => state switch
        {
            PresentationState.Hover => _hover,
            PresentationState.Click => _click,
            PresentationState.Disabled => _disabled,
            _ => _idle
        };

        private static bool HasSprites(in StateProfile profile) =>
            profile.Sprites != null && profile.Sprites.Length > 0;

        private bool IsPointerOverInteractableUi()
        {
            if (EventSystem.current == null)
                return false;

            _pointerEventData ??= new PointerEventData(EventSystem.current);
            _pointerEventData.position = ReadPointerScreenPosition();

            _raycastHits.Clear();
            EventSystem.current.RaycastAll(_pointerEventData, _raycastHits);
            for (int i = 0; i < _raycastHits.Count; i++)
            {
                var selectable = _raycastHits[i].gameObject.GetComponentInParent<Selectable>();
                if (selectable != null && selectable.IsActive() && selectable.IsInteractable())
                    return true;
            }

            return false;
        }

        private static Vector2 ReadPointerScreenPosition()
        {
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            return Input.mousePosition;
        }

        private static bool WasPrimaryPressedThisFrame()
        {
            if (Mouse.current != null)
                return Mouse.current.leftButton.wasPressedThisFrame;
            return Input.GetMouseButtonDown(0);
        }

        private static bool IsPrimaryHeld()
        {
            if (Mouse.current != null)
                return Mouse.current.leftButton.isPressed;
            return Input.GetMouseButton(0);
        }
    }
}
