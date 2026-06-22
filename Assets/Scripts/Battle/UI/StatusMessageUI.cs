using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Axiom.Battle
{
    /// <summary>
    /// Unity lifecycle and input wrapper for acknowledgment-gated battle messages.
    /// </summary>
    public class StatusMessageUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("TMP text component that displays battle narration.")]
        private TMP_Text _text;

        [SerializeField] private Button _continueButton;
        [SerializeField] private float _charactersPerSecond = 30f;

        private StatusMessageQueue _queue;

        public event Action<bool> BusyStateChanged;
        public bool IsBusy => _queue != null && _queue.IsBusy;

        private void Awake()
        {
            _queue = new StatusMessageQueue(_charactersPerSecond);
            _queue.BusyStateChanged += HandleBusyStateChanged;
            HandleBusyStateChanged(false);
        }

        private void OnEnable() => _continueButton.onClick.AddListener(Continue);

        private void OnDisable() => _continueButton.onClick.RemoveListener(Continue);

        private void Update()
        {
            _queue.Update(Time.deltaTime);
            _text.text = _queue.VisibleText;
        }

        private void OnDestroy() => _queue.BusyStateChanged -= HandleBusyStateChanged;

        public void Post(string message) => _queue.Post(message);

        public void Continue() => _queue.Continue();

        private void HandleBusyStateChanged(bool isBusy)
        {
            _continueButton.gameObject.SetActive(isBusy);
            _continueButton.interactable = isBusy;

            if (isBusy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);

            BusyStateChanged?.Invoke(isBusy);
        }
    }
}
