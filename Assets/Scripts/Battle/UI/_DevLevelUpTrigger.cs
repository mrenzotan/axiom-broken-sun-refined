using Axiom.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Axiom.Battle.UI
{
    /// <summary>
    /// Temporary DEV-40 smoke-test helper. Delete before UVCS check-in.
    /// Press <c>L</c> to award 100 XP (forces a level-up with the default CharacterData curve).
    /// Press <c>P</c> to force-show the LevelUpPromptUI from any pending entries.
    /// </summary>
    public class _DevLevelUpTrigger : MonoBehaviour
    {
        private LevelUpPromptUI _prompt;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.lKey.wasPressedThisFrame && GameManager.Instance != null)
            {
                GameManager.Instance.AwardXp(100);
                var s = GameManager.Instance.PlayerState;
                Debug.Log($"[DEV-40] AwardXp(100) → Level={s.Level} Xp={s.Xp} MaxHp={s.MaxHp} MaxMp={s.MaxMp} ATK={s.Attack} DEF={s.Defense} SPD={s.Speed}");
            }
            if (kb.pKey.wasPressedThisFrame)
            {
                if (_prompt == null) _prompt = FindAnyObjectByType<LevelUpPromptUI>();
                if (_prompt != null) _prompt.ShowIfPending();
            }
        }
    }
}
