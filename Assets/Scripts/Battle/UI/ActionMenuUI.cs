using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Axiom.Battle
{
    /// <summary>
    /// Manages the 2×2 battle action menu (Attack, Spell, Item, Flee)
    /// plus a small Spell List info button beside the grid.
    ///
    /// BattleHUD wires OnXxx to BattleController methods.
    /// SetInteractable(false) is called on EnemyTurn, Victory, and Defeat.
    /// </summary>
    public class ActionMenuUI : MonoBehaviour
    {
        [SerializeField] private Button _attackButton;
        [SerializeField] private Button _spellButton;
        [SerializeField] private Button _itemButton;
        [SerializeField] private Button _fleeButton;
        [SerializeField] private Button _spellListButton;

        public Action OnAttack;
        public Action OnSpell;
        public Action OnItem;
        public Action OnFlee;
        public Action OnSpellList;

        private readonly bool[] _messageBlockSnapshot = new bool[5];
        private bool _isMessageBlocked;

        private void Start()
        {
            _attackButton.onClick.AddListener(() => OnAttack?.Invoke());
            _spellButton.onClick.AddListener(() => OnSpell?.Invoke());
            _itemButton.onClick.AddListener(() => OnItem?.Invoke());
            _fleeButton.onClick.AddListener(() => OnFlee?.Invoke());
            _spellListButton.onClick.AddListener(() => OnSpellList?.Invoke());
        }

        /// <summary>
        /// Enables or disables all four buttons plus the Spell List button.
        /// Call with false during EnemyTurn, Victory, and Defeat states.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            _attackButton.interactable = interactable;
            _spellButton.interactable  = interactable;
            _itemButton.interactable   = interactable;
            _fleeButton.interactable   = interactable;
            _spellListButton.interactable = interactable;

            if (interactable)
                FocusFirstInteractable();
            else if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                     EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void FocusFirstInteractable()
        {
            if (EventSystem.current == null) return;

            Button[] candidates = { _attackButton, _spellButton, _itemButton, _fleeButton, _spellListButton };
            foreach (Button candidate in candidates)
            {
                if (candidate == null || !candidate.IsActive() || !candidate.IsInteractable()) continue;
                EventSystem.current.SetSelectedGameObject(candidate.gameObject);
                return;
            }
        }

        public void SetMessageBlocked(bool blocked)
        {
            if (_isMessageBlocked == blocked)
                return;

            _isMessageBlocked = blocked;
            Button[] buttons = { _attackButton, _spellButton, _itemButton, _fleeButton, _spellListButton };

            if (blocked)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    _messageBlockSnapshot[i] = buttons[i].interactable;
                    buttons[i].interactable = false;
                }
                return;
            }

            for (int i = 0; i < buttons.Length; i++)
                buttons[i].interactable = _messageBlockSnapshot[i];

            FocusFirstInteractable();
        }

        public void SetSpellInteractable(bool interactable)
        {
            _spellButton.interactable = interactable;
        }

        /// <summary>Enables or disables only the Attack button. Used by BattleTutorialController.</summary>
        public void SetAttackInteractable(bool interactable)
        {
            _attackButton.interactable = interactable;
        }

        /// <summary>Enables or disables only the Item button. Used by BattleTutorialController.</summary>
        public void SetItemInteractable(bool interactable)
        {
            _itemButton.interactable = interactable;
        }

        /// <summary>Enables or disables only the Flee button. Used by BattleTutorialController.</summary>
        public void SetFleeInteractable(bool interactable)
        {
            _fleeButton.interactable = interactable;
        }

        /// <summary>
        /// Enables or disables Attack, Item, and Flee while leaving Spell and Spell List
        /// untouched. Called by BattleHUD when the voice spell phase starts/ends (DEV-92).
        /// </summary>
        public void SetNonSpellActionsInteractable(bool interactable)
        {
            _attackButton.interactable = interactable;
            _itemButton.interactable   = interactable;
            _fleeButton.interactable   = interactable;
        }

        private void OnDestroy()
        {
            _attackButton.onClick.RemoveAllListeners();
            _spellButton.onClick.RemoveAllListeners();
            _itemButton.onClick.RemoveAllListeners();
            _fleeButton.onClick.RemoveAllListeners();
            _spellListButton.onClick.RemoveAllListeners();
        }
    }
}
