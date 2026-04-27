using System;
using UnityEngine;
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
