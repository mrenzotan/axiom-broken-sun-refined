using System.Collections;
using Axiom.Battle.UI;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// Drives the in-Battle tutorial. Subscribes to BattleController events, forwards them
    /// to BattleTutorialFlow, applies returned BattleTutorialAction to ActionMenuUI and
    /// BattleTutorialPromptUI. On Victory, flips the matching persisted PlayerState flag.
    ///
    /// Setup() is called by BattleController.Start after pending battle context is read.
    /// If PlayerState already has the matching flag set, the controller self-disables —
    /// this handles the post-victory respawn-into-trigger case.
    /// </summary>
    public class BattleTutorialController : MonoBehaviour
    {
        [SerializeField] private BattleController _battleController;
        [SerializeField] private ActionMenuUI _actionMenu;
        [SerializeField] private BattleTutorialPromptUI _promptUI;

        private BattleTutorialFlow _flow;
        private bool _isActive;
        private BattleState _currentBattleState;

        public void Setup(BattleTutorialMode requestedMode)
        {
            BattleTutorialMode resolvedMode = ResolveMode(requestedMode);
            if (resolvedMode == BattleTutorialMode.None)
            {
                _isActive = false;
                return;
            }

            _isActive = true;
            _flow = new BattleTutorialFlow(resolvedMode, GuessStartState(resolvedMode));

            SubscribeToBattleEvents();
            Apply(_flow.OnInit());
        }

        private BattleTutorialMode ResolveMode(BattleTutorialMode requestedMode)
        {
            if (GameManager.Instance == null) return BattleTutorialMode.None;
            PlayerState ps = GameManager.Instance.PlayerState;
            return requestedMode switch
            {
                BattleTutorialMode.FirstBattle when !ps.HasCompletedFirstBattleTutorial =>
                    BattleTutorialMode.FirstBattle,
                BattleTutorialMode.SpellTutorial when !ps.HasCompletedSpellTutorialBattle =>
                    BattleTutorialMode.SpellTutorial,
                _ => BattleTutorialMode.None,
            };
        }

        private static CombatStartState GuessStartState(BattleTutorialMode mode) => mode switch
        {
            BattleTutorialMode.FirstBattle   => CombatStartState.Surprised,
            BattleTutorialMode.SpellTutorial => CombatStartState.Advantaged,
            _                                => CombatStartState.Surprised,
        };

        private void SubscribeToBattleEvents()
        {
            if (_battleController == null) return;
            _battleController.OnBattleStateChanged    += HandleStateChanged;
            _battleController.OnPhysicalAttackImmune  += HandlePhysicalAttackImmune;
            _battleController.OnDamageDealt           += HandleDamageDealt;
            _battleController.OnConditionsChanged     += HandleConditionsChanged;
            _battleController.OnSpellRecognized       += HandleSpellRecognized;
        }

        private void OnDestroy()
        {
            if (_battleController == null) return;
            _battleController.OnBattleStateChanged    -= HandleStateChanged;
            _battleController.OnPhysicalAttackImmune  -= HandlePhysicalAttackImmune;
            _battleController.OnDamageDealt           -= HandleDamageDealt;
            _battleController.OnConditionsChanged     -= HandleConditionsChanged;
            _battleController.OnSpellRecognized       -= HandleSpellRecognized;
        }

        private void HandleStateChanged(BattleState state)
        {
            // Track current state BEFORE the active gate so HandleDamageDealt and
            // HandlePhysicalAttackImmune can use it to disambiguate player vs enemy actions.
            _currentBattleState = state;
            if (!_isActive) return;
            switch (state)
            {
                case BattleState.PlayerTurn:
                    // BattleHUD subscribes to OnBattleStateChanged after this controller
                    // (in BattleController.Initialize → BattleHUD.Setup), so its bulk
                    // SetInteractable(true) on PlayerTurn fires AFTER our per-button
                    // locks, clobbering them. Defer the Apply by one frame so we are
                    // the last writer.
                    StartCoroutine(ApplyAfterFrame(_flow.OnPlayerTurnStarted()));
                    break;
                case BattleState.Victory:
                    Apply(_flow.OnBattleEnded(victory: true));
                    _isActive = false;
                    break;
                case BattleState.Defeat:
                    Apply(_flow.OnBattleEnded(victory: false));
                    _isActive = false;
                    break;
            }
        }

        private IEnumerator ApplyAfterFrame(BattleTutorialAction action)
        {
            yield return null;
            if (!_isActive) yield break;
            Apply(action);
        }

        private void HandlePhysicalAttackImmune(CharacterStats attacker, CharacterStats target)
        {
            if (!_isActive) return;
            // Only react during PlayerTurn — that's when the player's attack is what bounced.
            // (Enemy attacks against the player during EnemyTurn could also fire this event
            // if the player ever becomes Liquid, which the tutorial doesn't expect — but the
            // gate keeps the flow correct under any future content.)
            if (_currentBattleState != BattleState.PlayerTurn) return;
            Apply(_flow.OnPlayerAttackImmune());
        }

        private void HandleDamageDealt(CharacterStats target, int damage, bool isCrit)
        {
            if (!_isActive) return;
            // Filter zero-damage pings (BattleController fires these to refresh the MP bar).
            if (damage <= 0) return;
            // Only react to damage dealt during PlayerTurn — that's the player's attack
            // landing on the enemy. EnemyTurn damage is the enemy hitting the player
            // and must NOT fire OnPlayerAttackHit (which in SpellTutorial turn 3+ would
            // incorrectly show the closing line).
            if (_currentBattleState != BattleState.PlayerTurn) return;
            Apply(_flow.OnPlayerAttackHit());
        }

        private void HandleConditionsChanged(CharacterStats stats)
        {
            if (!_isActive) return;
            Apply(_flow.OnConditionsChanged());
        }

        private void HandleSpellRecognized(SpellData spell)
        {
            if (!_isActive) return;
            string name = spell != null ? spell.spellName : null;
            Apply(_flow.OnSpellCast(name));
        }

        private void Apply(BattleTutorialAction action)
        {
            if (_promptUI != null)
            {
                if (action.PromptText == string.Empty)      _promptUI.Hide();
                else if (action.PromptText != null)         _promptUI.Show(action.PromptText);
            }

            if (_actionMenu != null)
            {
                if (action.AttackInteractable.HasValue) _actionMenu.SetAttackInteractable(action.AttackInteractable.Value);
                if (action.SpellInteractable.HasValue)  _actionMenu.SetSpellInteractable(action.SpellInteractable.Value);
                if (action.ItemInteractable.HasValue)   _actionMenu.SetItemInteractable(action.ItemInteractable.Value);
                if (action.FleeInteractable.HasValue)   _actionMenu.SetFleeInteractable(action.FleeInteractable.Value);
            }

            if (action.MarkComplete && GameManager.Instance != null && _flow != null)
            {
                PlayerState ps = GameManager.Instance.PlayerState;
                switch (_flow.Mode)
                {
                    case BattleTutorialMode.FirstBattle:   ps.MarkFirstBattleTutorialCompleted(); break;
                    case BattleTutorialMode.SpellTutorial: ps.MarkSpellTutorialBattleCompleted(); break;
                }
                GameManager.Instance.PersistToDisk();
            }
        }
    }
}
