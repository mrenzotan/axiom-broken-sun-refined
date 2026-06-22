using System;
using System.Collections.Generic;
using Axiom.Data;

namespace Axiom.Battle
{
    public readonly struct ConditionDamageTick
    {
        public ChemicalCondition Condition { get; }
        public int Damage { get; }

        public ConditionDamageTick(ChemicalCondition condition, int damage)
        {
            if (condition == ChemicalCondition.None)
                throw new ArgumentException("A condition damage tick must name its condition.", nameof(condition));
            if (damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage), "Condition damage must be positive.");

            Condition = condition;
            Damage = damage;
        }
    }

    /// <summary>
    /// Return value of CharacterStats.ProcessConditionTurn().
    /// Tells BattleController what happened during the condition tick for this character's turn.
    /// </summary>
    public struct ConditionTurnResult
    {
        /// <summary>Sum of all DoT damage dealt to this character this tick.</summary>
        public int TotalDamageDealt;

        /// <summary>Individual DoT outcomes, preserving their condition identities.</summary>
        public IReadOnlyList<ConditionDamageTick> DamageTicks;

        /// <summary>True if the character has the Frozen status — their action should be skipped.</summary>
        public bool ActionSkipped;
    }
}
