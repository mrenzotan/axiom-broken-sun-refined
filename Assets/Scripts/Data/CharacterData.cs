using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// Base stats for a playable character. Consumed by <c>PlayerState</c> /
    /// <c>ProgressionService</c>. Fields use the "base" prefix to signal Level-1 values;
    /// "perLevel" fields are the additive growth applied each level-up.
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

        [Header("Progression — DEV-40")]

        [Tooltip("XP required to advance from level (index+1) to (index+2). Index 0 = XP for level 1→2. Array length defines the level cap: once the player has completed every entry, no further level-ups fire.")]
        public int[] xpToNextLevelCurve = System.Array.Empty<int>();

        [Tooltip("Additive MaxHP gained per level-up.")]
        [Min(0)] public int maxHpPerLevel;

        [Tooltip("Additive MaxMP gained per level-up.")]
        [Min(0)] public int maxMpPerLevel;

        [Tooltip("Additive Attack gained per level-up.")]
        [Min(0)] public int atkPerLevel;

        [Tooltip("Additive Defense gained per level-up.")]
        [Min(0)] public int defPerLevel;

        [Tooltip("Additive Speed gained per level-up.")]
        [Min(0)] public int spdPerLevel;

        [Tooltip("Portrait sprite shown in the character status screen (Phase 6+). Leave null for now.")]
        public Sprite portraitSprite;
    }
}
