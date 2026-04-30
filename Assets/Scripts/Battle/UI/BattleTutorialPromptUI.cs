using TMPro;
using UnityEngine;

namespace Axiom.Battle.UI
{
    /// <summary>
    /// Battle-scene tutorial prompt panel. Mirrors the platformer's TutorialPromptPanelUI
    /// but lives in the Battle Canvas. BattleTutorialController calls Show/Hide as the
    /// state machine emits prompts.
    /// </summary>
    public class BattleTutorialPromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _bodyLabel;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void Show(string body)
        {
            if (_bodyLabel != null) _bodyLabel.text = body;
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
