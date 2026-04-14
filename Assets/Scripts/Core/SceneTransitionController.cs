using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// MonoBehaviour — Unity lifecycle wrapper for scene transition animation.
    /// Add to the GameManager prefab. Owns the child Canvas + Image overlay.
    ///
    /// Public API:
    ///   void BeginTransition(string sceneName, TransitionStyle style)
    ///   bool IsTransitioning  — delegates to SceneTransitionService
    ///
    /// BeginTransition is a no-op when IsTransitioning is already true.
    /// </summary>
    public class SceneTransitionController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The full-screen Image on the TransitionOverlay Canvas child. Assign in Inspector.")]
        private Image _overlayImage;

        private SceneTransitionService _service;

        public bool IsTransitioning => _service.IsTransitioning;

        private void Awake()
        {
            _service = new SceneTransitionService();
        }

        /// <summary>
        /// Begins the three-phase transition: fade out → async load → fade in.
        /// No-op if a transition is already in progress.
        /// </summary>
        public void BeginTransition(string sceneName, TransitionStyle style)
        {
            if (_service.IsTransitioning) return;
            StartCoroutine(RunTransition(sceneName, style));
        }

        private IEnumerator RunTransition(string sceneName, TransitionStyle style)
        {
            _service.SetTransitioning(true);

            Color baseColor       = _service.GetColor(style);
            float fadeOutDuration = _service.GetFadeOutDuration(style);
            float fadeInDuration  = _service.GetFadeInDuration(style);

            // Phase 1: Fade out — alpha 0 → 1
            _overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeOutDuration);
                _overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
            _overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);

            // Phase 2: Async load — held until overlay is fully opaque, then activated
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                _service.SetTransitioning(false);
                yield break;
            }
            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
                yield return null;

            op.allowSceneActivation = true;
            yield return op;

            // Phase 3: Fade in — alpha 1 → 0
            elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - elapsed / fadeInDuration);
                _overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
            _overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

            // Fire OnSceneReady first (subscribers may check IsTransitioning),
            // then clear the flag.
            if (GameManager.Instance != null)
                GameManager.Instance.RaiseSceneReady();

            _service.SetTransitioning(false);
        }
    }
}
