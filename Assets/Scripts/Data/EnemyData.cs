using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// ScriptableObject holding all data for one enemy type.
    /// innateConditions defines the enemy's material composition — what it is made of.
    /// These are copied into CharacterStats.InnateConditions on battle init.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "Axiom/Data/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Tooltip("Display name shown in the Battle UI.")]
        public string enemyName;

        public int maxHP;
        public int maxMP;
        public int atk;
        public int def;
        public int spd;

        [Tooltip("XP awarded to the player on defeat.")]
        [Min(0)] public int xpReward;

        [Tooltip("1–2 material conditions the enemy starts every combat with. Defines what the enemy is made of — determines physical immunity, reaction targets, and other combat interactions.")]
        public List<ChemicalCondition> innateConditions = new List<ChemicalCondition>();

        [Tooltip("Possible item drops. Each entry rolls independently against its dropChance.")]
        public List<LootEntry> loot = new List<LootEntry>();
    }
}
