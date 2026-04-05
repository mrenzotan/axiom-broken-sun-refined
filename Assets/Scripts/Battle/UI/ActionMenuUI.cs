using System;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Battle
{
    /// <summary>
    /// Manages the 2×2 battle action menu (Attack, Spell, Item, Flee).
    ///
    /// BattleHUD wires OnAttack/OnSpell/OnItem/OnFlee to BattleController methods.
    /// SetInteractable(false) is called on EnemyTurn, Victory, and Defeat.
    /// </summary>
    public class ActionMenuUI : MonoBehaviour
    {
        [SerializeField] private Button _attackButton;
        [SerializeField] private Button _spellButton;
        [SerializeField] private Button _itemButton;
        [SerializeField] private Button _fleeButton;

        /// <summary>Wire these to BattleController.PlayerAttack / PlayerSpell / PlayerItem / PlayerFlee.</summary>
        public Action OnAttack;
        public Action OnSpell;
        public Action OnItem;
        public Action OnFlee;

        private void Start()
        {
            _attackButton.onClick.AddListener(() => OnAttack?.Invoke());
            _spellButton.onClick.AddListener(() => OnSpell?.Invoke());
            _itemButton.onClick.AddListener(() => OnItem?.Invoke());
            _fleeButton.onClick.AddListener(() => OnFlee?.Invoke());
        }

        /// <summary>
        /// Enables or disables all four buttons.
        /// Call with false during EnemyTurn, Victory, and Defeat states.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            _attackButton.interactable = interactable;
            _spellButton.interactable  = interactable;
            _itemButton.interactable   = interactable;
            _fleeButton.interactable   = interactable;
        }

        private void OnDestroy()
        {
            _attackButton.onClick.RemoveAllListeners();
            _spellButton.onClick.RemoveAllListeners();
            _itemButton.onClick.RemoveAllListeners();
            _fleeButton.onClick.RemoveAllListeners();
        }
    }
}
