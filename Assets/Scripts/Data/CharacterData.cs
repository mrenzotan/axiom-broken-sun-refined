using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// Base stats for a playable character. Consumed by <c>PlayerState</c> / character
    /// initialization instead of hardcoded constants. Fields use the "base" prefix to
    /// signal these are Level 1 starting values — a level-up system will scale from these.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "Axiom/Data/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Tooltip("Character name shown in UI and battle results.")]
        public string characterName;

        [Tooltip("Base maximum HP at Level 1.")]
        [Min(1)] public int baseMaxHP = 100;

        [Tooltip("Base maximum MP at Level 1.")]
        [Min(0)] public int baseMaxMP = 30;

        [Tooltip("Base Attack at Level 1.")]
        [Min(0)] public int baseATK = 12;

        [Tooltip("Base Defense at Level 1.")]
        [Min(0)] public int baseDEF = 6;

        [Tooltip("Base Speed at Level 1.")]
        [Min(0)] public int baseSPD = 8;

        [Tooltip("Portrait sprite shown in the character status screen (Phase 6+). Leave null for now.")]
        public Sprite portraitSprite;
    }
}
