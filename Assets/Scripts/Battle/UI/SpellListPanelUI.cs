using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Battle
{
    public class SpellListPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _contentParent;
        [SerializeField] private GameObject _spellRowPrefab;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _emptyMessageText;

        public event Action OnCloseClicked;

        private SpellListPanelLogic _logic;
        private readonly List<GameObject> _activeRows = new List<GameObject>();

        public bool IsVisible => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(HandleClose);
            if (_panel != null)
                _panel.SetActive(false);
        }

        public void Show(SpellListPanelLogic logic)
        {
            _logic = logic ?? throw new ArgumentNullException(nameof(logic));
            ClearRows();

            if (_logic.IsEmpty)
            {
                if (_emptyMessageText != null)
                {
                    _emptyMessageText.gameObject.SetActive(true);
                    _emptyMessageText.text = _logic.EmptyMessage;
                }
            }
            else
            {
                if (_emptyMessageText != null)
                    _emptyMessageText.gameObject.SetActive(false);

                IReadOnlyList<string> names = _logic.SpellNames;
                for (int i = 0; i < names.Count; i++)
                {
                    GameObject row = Instantiate(_spellRowPrefab, _contentParent);
                    TMP_Text label = row.GetComponentInChildren<TMP_Text>();
                    if (label != null)
                        label.text = names[i];
                    _activeRows.Add(row);
                }
            }

            if (_panel != null)
                _panel.SetActive(true);
        }

        public void Hide()
        {
            if (_panel != null)
                _panel.SetActive(false);
            ClearRows();
            _logic = null;
        }

        private void HandleClose()
        {
            OnCloseClicked?.Invoke();
        }

        private void ClearRows()
        {
            foreach (GameObject row in _activeRows)
                if (row != null) Destroy(row);
            _activeRows.Clear();
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(HandleClose);
            ClearRows();
        }
    }
}
