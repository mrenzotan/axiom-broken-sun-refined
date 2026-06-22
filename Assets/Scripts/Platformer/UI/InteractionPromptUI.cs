using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Platformer.UI
{
    /// <summary>
    /// Displays an interaction prompt (e.g., E key sprite) above the player or trigger zone
    /// when they can interact.
    ///
    /// Attach to a world-space Canvas child of the player or dialogue trigger zone.
    /// Assign the E key sprite in the Inspector.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Image component that displays the interaction prompt sprite.")]
        private Image _promptImage;

        [SerializeField]
        [Tooltip("E key sprite to display.")]
        private Sprite _eKeySprite;

        [SerializeField]
        [Tooltip("Optional offset above the player/trigger (in world units).")]
        private Vector3 _offset = new Vector3(0, 2, 0);

        private void Start()
        {
            if (_promptImage != null && _eKeySprite != null)
            {
                _promptImage.sprite = _eKeySprite;
            }

            Hide();
        }

        /// <summary>
        /// Shows the interaction prompt.
        /// </summary>
        public void Show()
        {
            Debug.Log($"[InteractionPromptUI] Show() called. Image: {_promptImage}, Sprite: {_eKeySprite}, Active: {gameObject.activeSelf}");
            
            if (_promptImage != null)
            {
                _promptImage.enabled = true;
                Debug.Log("[InteractionPromptUI] Image enabled");
            }
            else
            {
                Debug.LogError("[InteractionPromptUI] _promptImage is null!");
            }
            
            gameObject.SetActive(true);
            Debug.Log("[InteractionPromptUI] Prompt shown");
        }

        /// <summary>
        /// Hides the interaction prompt.
        /// </summary>
        public void Hide()
        {
            if (_promptImage != null)
            {
                _promptImage.enabled = false;
            }
            gameObject.SetActive(false);

            Debug.Log("[InteractionPromptUI] Prompt hidden");
        }
    }
}
