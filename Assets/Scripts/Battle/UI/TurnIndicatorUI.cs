using System.Collections;
using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// Repositions a ▼ arrow RectTransform above the active character's slot.
    /// Runs a continuous bob coroutine while active.
    /// Designed to accept any Transform target — supports both Canvas RectTransforms and world-space sprites.
    /// </summary>
    public class TurnIndicatorUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The RectTransform of the ▼ arrow image.")]
        private RectTransform _arrowRect;

        [SerializeField]
        [Tooltip("Screen-pixel offset above the target's center position.")]
        private float _yOffset = 100f;

        [SerializeField]
        [Tooltip("Height of the bob animation in canvas units.")]
        private float _bobHeight = 6f;

        [SerializeField]
        [Tooltip("Speed of the bob animation.")]
        private float _bobSpeed = 3f;

        private Transform _currentTarget;
        private Coroutine _bobCoroutine;

        /// <summary>
        /// Moves the arrow above the given target and (re)starts the bob.
        /// Accepts both Canvas RectTransforms and world-space Transforms.
        /// Pass null to hide the indicator.
        /// </summary>
        public void SetActiveTarget(Transform target)
        {
            _currentTarget = target;

            if (_bobCoroutine != null)
                StopCoroutine(_bobCoroutine);

            if (target == null)
            {
                _arrowRect.gameObject.SetActive(false);
                return;
            }

            _arrowRect.gameObject.SetActive(true);
            _arrowRect.position = ScreenPositionOf(target) + Vector3.up * _yOffset;
            _bobCoroutine = StartCoroutine(Bob());
        }

        private IEnumerator Bob()
        {
            float elapsed = 0f;

            while (true)
            {
                elapsed += Time.deltaTime;
                Vector3 baseScreen = ScreenPositionOf(_currentTarget);
                _arrowRect.position = baseScreen + Vector3.up * (_yOffset + _bobHeight * Mathf.Sin(elapsed * _bobSpeed));
                yield return null;
            }
        }

        /// <summary>
        /// Returns the screen-space position of a transform.
        /// Canvas RectTransforms (Screen Space – Overlay) already use screen coordinates;
        /// world-space Transforms are converted via the main camera.
        /// </summary>
        private Vector3 ScreenPositionOf(Transform target)
        {
            if (target is RectTransform)
                return target.position;

            return Camera.main.WorldToScreenPoint(target.position);
        }
    }
}
