using System;
using System.Collections.Generic;
using Axiom.Battle;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Axiom.Platformer.UI
{
    public class ExplorationMenuController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _spellbookButton;
        [SerializeField] private Button _itemsButton;

        [Header("Panels (reused from Battle assembly)")]
        [SerializeField] private SpellListPanelUI _spellListPanel;
        [SerializeField] private ItemMenuUI _itemMenuPanel;

        [Header("Data")]
        [SerializeField] private ItemCatalog _itemCatalog;

        private bool _areEnabled;

        private void Awake()
        {
            Debug.Log("[ExplorationMenu] Awake — buttons start hidden.", this);

            if (_spellbookButton != null)
            {
                _spellbookButton.onClick.AddListener(ToggleSpellbook);
                _spellbookButton.gameObject.SetActive(false);
            }
            else
                Debug.LogError("[ExplorationMenu] _spellbookButton is not assigned.", this);

            if (_itemsButton != null)
            {
                _itemsButton.onClick.AddListener(OpenItems);
                _itemsButton.gameObject.SetActive(false);
            }
            else
                Debug.LogError("[ExplorationMenu] _itemsButton is not assigned.", this);

            var gm = GameManager.Instance;
            if (gm != null && gm.PlayerState.ExplorationMenusUnlocked)
                EnableButtons();
        }

        public void EnableButtons()
        {
            if (_areEnabled) return;
            _areEnabled = true;
            var gm = GameManager.Instance;
            if (gm != null)
                gm.PlayerState.ExplorationMenusUnlocked = true;
            if (_spellbookButton != null)
            {
                _spellbookButton.gameObject.SetActive(true);
                Debug.Log("[ExplorationMenu] Spellbook button enabled.", this);
            }
            if (_itemsButton != null)
            {
                _itemsButton.gameObject.SetActive(true);
                Debug.Log("[ExplorationMenu] Items button enabled.", this);
            }
        }

        private void Update()
        {
            if (!_areEnabled) return;
            if (Keyboard.current == null) return;

            if (Keyboard.current.bKey.wasPressedThisFrame)
                ToggleSpellbook();
            if (Keyboard.current.iKey.wasPressedThisFrame)
                OpenItems();
        }

        private void ToggleSpellbook()
        {
            if (_spellListPanel == null) return;

            if (_spellListPanel.IsVisible)
            {
                HideSpellbook();
                return;
            }

            HideItems();

            var gm = GameManager.Instance;
            if (gm == null) return;

            SpellListPanelLogic logic = SpellListPanelLogic.BuildFromSpellUnlockService(gm.SpellUnlockService)
                ?? new SpellListPanelLogic(null);

            _spellListPanel.OnCloseClicked -= HandleSpellbookClosed;
            _spellListPanel.OnCloseClicked += HandleSpellbookClosed;
            _spellListPanel.Show(logic);
        }

        private void OpenItems()
        {
            if (_itemMenuPanel == null)
            {
                Debug.LogError("[ExplorationMenu] _itemMenuPanel is not assigned in the Inspector.", this);
                return;
            }
            if (_itemCatalog == null)
            {
                Debug.LogError("[ExplorationMenu] _itemCatalog is not assigned in the Inspector.", this);
                return;
            }

            HideSpellbook();

            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[ExplorationMenu] GameManager.Instance is null.", this);
                return;
            }

            var availableItems = new List<(ItemData item, int quantity)>();
            foreach (var kvp in gm.PlayerState.Inventory.GetAll())
            {
                if (kvp.Value <= 0) continue;
                if (!_itemCatalog.TryGetItem(kvp.Key, out ItemData itemData)) continue;
                availableItems.Add((itemData, kvp.Value));
            }

            _itemMenuPanel.OnItemSelected -= HandleItemUsed;
            _itemMenuPanel.OnItemSelected += HandleItemUsed;
            _itemMenuPanel.OnCancelled -= HandleItemsCancelled;
            _itemMenuPanel.OnCancelled += HandleItemsCancelled;
            _itemMenuPanel.Show(availableItems);
        }

        private void HandleItemUsed(ItemData item)
        {
            _itemMenuPanel.OnItemSelected -= HandleItemUsed;
            _itemMenuPanel.OnCancelled -= HandleItemsCancelled;
            _itemMenuPanel.Hide();

            var gm = GameManager.Instance;
            if (gm == null || item == null) return;

            PlayerState state = gm.PlayerState;
            state.Inventory.Remove(item.itemId);

            switch (item.effectType)
            {
                case ItemEffectType.RestoreHP:
                {
                    int before = state.CurrentHp;
                    state.SetCurrentHp(state.CurrentHp + item.effectPower);
                    int healed = state.CurrentHp - before;
                    Debug.Log($"[Exploration] Used {item.displayName}: HP +{healed}");
                    break;
                }
                case ItemEffectType.RestoreMP:
                {
                    int before = state.CurrentMp;
                    state.SetCurrentMp(state.CurrentMp + item.effectPower);
                    int restored = state.CurrentMp - before;
                    Debug.Log($"[Exploration] Used {item.displayName}: MP +{restored}");
                    break;
                }
            }

            gm.PersistToDisk();
        }

        private void HandleItemsCancelled()
        {
            _itemMenuPanel.OnItemSelected -= HandleItemUsed;
            _itemMenuPanel.OnCancelled -= HandleItemsCancelled;
            _itemMenuPanel.Hide();
        }

        private void HandleSpellbookClosed()
        {
            HideSpellbook();
        }

        private void HideSpellbook()
        {
            if (_spellListPanel == null) return;
            _spellListPanel.OnCloseClicked -= HandleSpellbookClosed;
            _spellListPanel.Hide();
        }

        private void HideItems()
        {
            if (_itemMenuPanel == null) return;
            _itemMenuPanel.OnItemSelected -= HandleItemUsed;
            _itemMenuPanel.OnCancelled -= HandleItemsCancelled;
            _itemMenuPanel.Hide();
        }

        private void OnDestroy()
        {
            if (_spellbookButton != null)
                _spellbookButton.onClick.RemoveListener(ToggleSpellbook);
            if (_itemsButton != null)
                _itemsButton.onClick.RemoveListener(OpenItems);
            HideSpellbook();
            HideItems();
        }
    }
}
