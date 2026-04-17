using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Core;

namespace Axiom.Battle.UI
{
    /// <summary>
    /// Battle-scene prompt shown after level-up. Subscribes to
    /// <see cref="ProgressionService.OnLevelUp"/> and queues one entry per level gained,
    /// attributing newly unlocked spells to the level that granted them via a
    /// delta of <see cref="SpellUnlockService.UnlockedSpellNames"/>.
    ///
    /// The widget stays hidden until DEV-36 calls <see cref="ShowIfPending"/>.
    /// After the player confirms, <see cref="OnDismissed"/> fires once the queue is
    /// drained — letting DEV-36's post-battle flow proceed.
    /// </summary>
    public class LevelUpPromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _statsText;
        [SerializeField] private TextMeshProUGUI _spellsText;
        [SerializeField] private Button _confirmButton;

        private readonly LevelUpPromptController _controller = new LevelUpPromptController();

        private ProgressionService _progression;
        private SpellUnlockService _spellUnlocks;
        private int _lastSeenUnlockCount;

        public bool IsShowing => _panel != null && _panel.activeSelf;

        /// <summary>Fires when the player dismisses the final pending prompt.</summary>
        public event Action OnDismissed;

        private void OnEnable()
        {
            HidePanel();

            GameManager manager = GameManager.Instance;
            if (manager == null) return;

            _progression  = manager.ProgressionService;
            _spellUnlocks = manager.SpellUnlockService;
            _lastSeenUnlockCount = _spellUnlocks?.UnlockedSpellNames.Count ?? 0;

            if (_progression != null)
                _progression.OnLevelUp += HandleLevelUp;

            _controller.OnDismissed += HandleQueueDrained;

            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        private void OnDisable()
        {
            if (_progression != null)
                _progression.OnLevelUp -= HandleLevelUp;

            _controller.OnDismissed -= HandleQueueDrained;

            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }

        /// <summary>
        /// DEV-36 calls this after the victory screen is dismissed.
        /// Reveals the panel if any level-ups are queued; otherwise fires
        /// <see cref="OnDismissed"/> immediately so DEV-36 can continue.
        /// </summary>
        public void ShowIfPending()
        {
            if (!_controller.IsPending)
            {
                OnDismissed?.Invoke();
                return;
            }
            RenderCurrent();
            ShowPanel();
        }

        private void HandleLevelUp(LevelUpResult result)
        {
            // GameManager.HandleLevelUp is the first OnLevelUp subscriber (wired in
            // EnsureProgressionService during GameManager init, which runs before
            // Battle scene OnEnable). It calls SpellUnlockService.NotifyPlayerLevel
            // synchronously — so by the time this handler runs, any spells unlocked
            // at result.NewLevel have already been appended to UnlockedSpellNames.
            // Take the new suffix as "spells granted by this level-up".
            string[] newSpellNames = System.Array.Empty<string>();
            if (_spellUnlocks != null)
            {
                var allUnlocked = _spellUnlocks.UnlockedSpellNames;
                int delta = allUnlocked.Count - _lastSeenUnlockCount;
                if (delta > 0)
                {
                    newSpellNames = new string[delta];
                    for (int i = 0; i < delta; i++)
                        newSpellNames[i] = allUnlocked[_lastSeenUnlockCount + i];
                }
                _lastSeenUnlockCount = allUnlocked.Count;
            }

            _controller.Enqueue(result, newSpellNames);
        }

        private void OnConfirmClicked()
        {
            _controller.Dismiss();
            if (_controller.IsPending)
                RenderCurrent();
            else
                HidePanel();
        }

        private void HandleQueueDrained()
        {
            HidePanel();
            OnDismissed?.Invoke();
        }

        private void RenderCurrent()
        {
            LevelUpPromptController.Entry entry = _controller.Current;
            if (_titleText != null)
                _titleText.text = $"LEVEL UP!   Lv. {entry.Result.PreviousLevel} → Lv. {entry.Result.NewLevel}";

            if (_statsText != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"HP  +{entry.Result.DeltaMaxHp}");
                sb.AppendLine($"MP  +{entry.Result.DeltaMaxMp}");
                sb.AppendLine($"ATK +{entry.Result.DeltaAttack}");
                sb.AppendLine($"DEF +{entry.Result.DeltaDefense}");
                sb.Append   ($"SPD +{entry.Result.DeltaSpeed}");
                _statsText.text = sb.ToString();
            }

            if (_spellsText != null)
            {
                if (entry.NewSpellNames == null || entry.NewSpellNames.Count == 0)
                {
                    _spellsText.text = string.Empty;
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("New spells:");
                    foreach (string name in entry.NewSpellNames) sb.AppendLine(name);
                    _spellsText.text = sb.ToString().TrimEnd();
                }
            }
        }

        private void ShowPanel()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        private void HidePanel()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
