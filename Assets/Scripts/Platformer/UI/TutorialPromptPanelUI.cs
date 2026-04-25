using TMPro;
using UnityEngine;

namespace Axiom.Platformer.UI
{
    /// <summary>
    /// Simple prompt panel anchored to the platformer HUD. Shown when the player
    /// enters a TutorialPromptTrigger zone; hidden when they leave it.
    /// </summary>
    public class TutorialPromptPanelUI : MonoBehaviour
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
