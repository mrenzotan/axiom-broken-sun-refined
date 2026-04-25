using Axiom.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Platformer.UI
{
    /// <summary>
    /// Placeholder chapter-complete card shown after a level boss is defeated.
    /// Disabled by default — BossVictoryTrigger activates it. Clicking the continue
    /// button transitions to a configured next scene (MainMenu for DEV-46 scope).
    /// Will be replaced by the DEV-81 cutscene system once built.
    /// </summary>
    public class ChapterCompleteCardUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private Button _continueButton;

        [SerializeField]
        [Tooltip("Scene to load when the continue button is pressed.")]
        private string _continueSceneName = "MainMenu";

        [SerializeField]
        [Tooltip("Default title to display.")]
        private string _chapterTitle = "Chapter 1 Complete";

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);
            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinueClicked);
        }

        private void OnDestroy()
        {
            if (_continueButton != null) _continueButton.onClick.RemoveListener(OnContinueClicked);
        }

        public void Show()
        {
            if (_titleLabel != null) _titleLabel.text = _chapterTitle;
            if (_root != null) _root.SetActive(true);
        }

        private void OnContinueClicked()
        {
            if (GameManager.Instance == null || GameManager.Instance.SceneTransition == null) return;
            GameManager.Instance.SceneTransition.BeginTransition(_continueSceneName, TransitionStyle.WhiteFlash);
        }
    }
}
