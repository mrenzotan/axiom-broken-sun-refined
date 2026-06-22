using System;
using Axiom.Data;

namespace Axiom.Battle
{
    public static class BattleMessageFormatter
    {
        public static string ConditionApplied(string characterName, ChemicalCondition condition)
        {
            ValidateCharacterName(characterName);
            ValidateCondition(condition);

            return condition == ChemicalCondition.Frozen
                ? $"{characterName} was Frozen! It will skip its next action."
                : $"{characterName} was {condition}!";
        }

        public static string ConditionDamage(string characterName, ChemicalCondition condition, int damage)
        {
            ValidateCharacterName(characterName);
            ValidateCondition(condition);
            if (damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage), "Condition damage must be positive.");

            return $"{characterName} takes {damage} damage from {condition}.";
        }

        private static void ValidateCharacterName(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
                throw new ArgumentException("Character name cannot be empty.", nameof(characterName));
        }

        private static void ValidateCondition(ChemicalCondition condition)
        {
            if (condition == ChemicalCondition.None)
                throw new ArgumentException("A battle message must name a condition.", nameof(condition));
        }
    }
}
