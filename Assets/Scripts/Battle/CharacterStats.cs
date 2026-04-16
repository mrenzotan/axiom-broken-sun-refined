using System;
using System.Collections.Generic;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Serializable plain C# class holding a character's base stats and all runtime combat state:
    /// HP, MP, Shield, and active chemical conditions.
    ///
    /// No MonoBehaviour — attach as a SerializeField on BattleController.
    /// Call Initialize() before each battle to reset all runtime state.
    ///
    /// Condition responsibilities:
    ///   InnateConditions            — read-only, set from EnemyData on Initialize(); never changes
    ///   ActiveMaterialConditions    — current material conditions; starts as copy of InnateConditions;
    ///                                 temporarily modified by phase-change reactions
    ///   ActiveStatusConditions      — status conditions applied mid-combat by spells or reactions;
    ///                                 expire after a fixed number of turns
    ///   _materialTransformations    — private tracking list for temporary material replacements;
    ///                                 drives restoration when a transformation expires
    /// </summary>
    [Serializable]
    public class CharacterStats
    {
        public string Name = string.Empty;
        public int MaxHP;
        public int MaxMP;
        public int ATK;
        public int DEF;
        public int SPD;

        // ── Runtime state ────────────────────────────────────────────────────

        public int  CurrentHP { get; private set; }
        public int  CurrentMP { get; private set; }
        public int  ShieldHP  { get; private set; }

        public bool IsDefeated        => CurrentHP <= 0;

        /// <summary>
        /// ATK halved while the character carries Crystallized status.
        /// Used by action handlers instead of the raw ATK field.
        /// </summary>
        public int  EffectiveATK      => HasCondition(ChemicalCondition.Crystallized) ? ATK / 2 : ATK;

        /// <summary>
        /// True while the character is Liquid or Vapor — physical attacks pass through harmlessly.
        /// </summary>
        public bool IsPhysicallyImmune =>
            HasCondition(ChemicalCondition.Liquid) || HasCondition(ChemicalCondition.Vapor);

        // ── Condition state ──────────────────────────────────────────────────

        /// <summary>
        /// Read-only. Set from EnemyData.innateConditions during Initialize().
        /// Never changes during combat — this is the restore source when a transformation expires.
        /// Empty for the player character.
        /// </summary>
        public IReadOnlyList<ChemicalCondition> InnateConditions => _innateConditions;
        private List<ChemicalCondition> _innateConditions = new List<ChemicalCondition>();

        /// <summary>
        /// Current material conditions. Starts as a copy of InnateConditions.
        /// Temporarily mutated during phase-change reactions; restored from InnateConditions
        /// when the transformation expires.
        /// Mutate only via ApplyMaterialTransformation() and ConsumeCondition() — never directly.
        /// Direct mutation bypasses the _materialTransformations tracking invariant.
        /// </summary>
        public List<ChemicalCondition> ActiveMaterialConditions { get; private set; } = new List<ChemicalCondition>();

        /// <summary>
        /// Active status conditions. Each entry holds the condition, remaining turns, tick count,
        /// and the base damage used for DoT calculations.
        /// Mutate only via ApplyStatusCondition() and ConsumeCondition() — never directly.
        /// </summary>
        public List<StatusConditionEntry> ActiveStatusConditions { get; private set; } = new List<StatusConditionEntry>();

        // Tracks active material transformations so we know what to restore when they expire.
        private readonly List<MaterialTransformEntry> _materialTransformations = new List<MaterialTransformEntry>();

        private struct MaterialTransformEntry
        {
            public ChemicalCondition ReplacementCondition;  // temporary condition added (e.g. Solid)
            public ChemicalCondition SuppressedCondition;   // innate condition it replaced (e.g. Liquid)
            public int TurnsRemaining;
            public int AppliedOrder;
        }

        private int _conditionAppliedCounter;

        // ── Initialization ───────────────────────────────────────────────────

        /// <summary>
        /// Resets all runtime state for a new battle.
        /// Pass innateConditions from EnemyData for enemies; omit (null) for the player.
        /// Optional startHp/startMp override persisted vitals; omit (null) to use max.
        /// </summary>
        public void Initialize(List<ChemicalCondition> innateConditions = null,
                               int? startHp = null, int? startMp = null)
        {
            CurrentHP = startHp.HasValue ? Math.Clamp(startHp.Value, 0, MaxHP) : MaxHP;
            CurrentMP = startMp.HasValue ? Math.Clamp(startMp.Value, 0, MaxMP) : MaxMP;
            ShieldHP  = 0;

            _innateConditions          = innateConditions ?? new List<ChemicalCondition>();
            ActiveMaterialConditions   = new List<ChemicalCondition>(_innateConditions);
            ActiveStatusConditions     = new List<StatusConditionEntry>();
            _materialTransformations.Clear();
            _conditionAppliedCounter   = 0;
        }

        // ── Stat mutation ────────────────────────────────────────────────────

        /// <summary>
        /// Reduces HP by amount. ShieldHP absorbs damage first; excess carries through to CurrentHP.
        /// </summary>
        public void TakeDamage(int amount)
        {
            if (ShieldHP > 0)
            {
                int absorbed = Math.Min(ShieldHP, amount);
                ShieldHP -= absorbed;
                amount   -= absorbed;
            }
            CurrentHP = Math.Max(0, CurrentHP - amount);
        }

        /// <summary>Restores CurrentHP by amount, clamped to MaxHP.</summary>
        public void Heal(int amount)
        {
            CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
        }

        /// <summary>Adds amount to ShieldHP. No maximum.</summary>
        public void ApplyShield(int amount)
        {
            ShieldHP += amount;
        }

        /// <summary>
        /// Attempts to spend amount MP.
        /// Returns true and deducts if sufficient; returns false and leaves MP unchanged if not.
        /// </summary>
        public bool SpendMP(int amount)
        {
            if (CurrentMP < amount) return false;
            CurrentMP -= amount;
            return true;
        }

        /// <summary>Restores CurrentMP by amount, clamped to MaxMP.</summary>
        public void RestoreMP(int amount)
        {
            CurrentMP = Math.Min(MaxMP, CurrentMP + amount);
        }

        // ── Condition queries ────────────────────────────────────────────────

        /// <summary>
        /// Returns true if condition appears in either ActiveMaterialConditions
        /// or ActiveStatusConditions.
        /// </summary>
        public bool HasCondition(ChemicalCondition condition)
        {
            if (ActiveMaterialConditions.Contains(condition)) return true;
            foreach (var entry in ActiveStatusConditions)
                if (entry.Condition == condition) return true;
            return false;
        }

        // ── Condition mutation ───────────────────────────────────────────────

        /// <summary>
        /// Adds a status condition entry. The resolver calls HasCondition() first, so
        /// duplicate prevention is the resolver's responsibility, not this method's.
        /// </summary>
        public void ApplyStatusCondition(ChemicalCondition condition, int baseDamage = 0, int duration = 0)
        {
            int effectiveDuration = duration > 0 ? duration : DefaultDurationFor(condition);
            ActiveStatusConditions.Add(new StatusConditionEntry
            {
                Condition      = condition,
                TurnsRemaining = effectiveDuration,
                TickCount      = 0,
                BaseDamage     = baseDamage,
                AppliedOrder   = _conditionAppliedCounter++
            });
        }

        /// <summary>
        /// Adds a temporary material condition that replaces suppressedCondition.
        /// suppressedCondition must already have been removed via ConsumeCondition().
        /// When the transformation expires, suppressedCondition is restored from InnateConditions.
        /// </summary>
        public void ApplyMaterialTransformation(
            ChemicalCondition transformsTo,
            ChemicalCondition suppressedCondition,
            int duration)
        {
            ActiveMaterialConditions.Add(transformsTo);
            _materialTransformations.Add(new MaterialTransformEntry
            {
                ReplacementCondition = transformsTo,
                SuppressedCondition  = suppressedCondition,
                TurnsRemaining       = duration,
                AppliedOrder         = _conditionAppliedCounter++
            });
        }

        /// <summary>
        /// Removes one instance of condition from whichever active list contains it.
        /// No-op if the condition is not present.
        /// </summary>
        public void ConsumeCondition(ChemicalCondition condition)
        {
            if (ActiveMaterialConditions.Remove(condition)) return;
            for (int i = 0; i < ActiveStatusConditions.Count; i++)
            {
                if (ActiveStatusConditions[i].Condition == condition)
                {
                    ActiveStatusConditions.RemoveAt(i);
                    return;
                }
            }
        }

        // ── Condition turn processing ────────────────────────────────────────

        /// <summary>
        /// Called by BattleController at the START of this character's turn.
        /// Applies DoT effects (Burning, Evaporating, Corroded), decrements all condition
        /// durations, removes expired status entries, and restores expired material
        /// transformations from InnateConditions.
        ///
        /// Returns a ConditionTurnResult so BattleController can skip the character's
        /// action if they are Frozen.
        /// </summary>
        public ConditionTurnResult ProcessConditionTurn()
        {
            int  totalDamage  = 0;
            bool actionSkipped = false;

            // ── Process status conditions ────────────────────────────────────
            for (int i = ActiveStatusConditions.Count - 1; i >= 0; i--)
            {
                StatusConditionEntry entry = ActiveStatusConditions[i];

                switch (entry.Condition)
                {
                    case ChemicalCondition.Burning:
                    case ChemicalCondition.Evaporating:
                    {
                        TakeDamage(entry.BaseDamage);
                        totalDamage += entry.BaseDamage;
                        break;
                    }
                    case ChemicalCondition.Corroded:
                    {
                        float multiplier = 1.0f + (0.5f * entry.TickCount);
                        int damage = (int)(entry.BaseDamage * multiplier);
                        TakeDamage(damage);
                        totalDamage += damage;
                        break;
                    }
                    case ChemicalCondition.Frozen:
                        actionSkipped = true;
                        break;
                }

                entry.TickCount++;
                entry.TurnsRemaining--;

                if (entry.TurnsRemaining <= 0)
                    ActiveStatusConditions.RemoveAt(i);
                else
                {
                    // struct copy — must write back after mutation (TurnsRemaining and TickCount changed above)
                    ActiveStatusConditions[i] = entry;
                }
            }

            // ── Process material transformations ─────────────────────────────
            for (int i = _materialTransformations.Count - 1; i >= 0; i--)
            {
                MaterialTransformEntry transform = _materialTransformations[i];
                transform.TurnsRemaining--;

                if (transform.TurnsRemaining <= 0)
                {
                    ActiveMaterialConditions.Remove(transform.ReplacementCondition);
                    if (_innateConditions.Contains(transform.SuppressedCondition))
                        ActiveMaterialConditions.Add(transform.SuppressedCondition);
                    _materialTransformations.RemoveAt(i);
                }
                else
                {
                    _materialTransformations[i] = transform;
                }
            }

            return new ConditionTurnResult
            {
                TotalDamageDealt = totalDamage,
                ActionSkipped    = actionSkipped
            };
        }

        // ── Condition queries (continued) ────────────────────────────────────────

        /// <summary>
        /// Returns the turns remaining for a condition that is active as a temporary
        /// material transformation (e.g. Liquid frozen into Solid for N turns).
        /// Returns 0 if the condition is not active as a transformation — i.e. it is
        /// either an innate permanent condition or not present at all.
        /// </summary>
        public int GetMaterialTransformTurns(ChemicalCondition condition)
        {
            foreach (var transform in _materialTransformations)
                if (transform.ReplacementCondition == condition)
                    return transform.TurnsRemaining;
            return 0;
        }

        /// <summary>
        /// Returns the applied order for a condition that is active as a temporary
        /// material transformation. Returns int.MaxValue if not found, so unsorted
        /// conditions sort to the end.
        /// </summary>
        public int GetMaterialTransformOrder(ChemicalCondition condition)
        {
            foreach (var transform in _materialTransformations)
                if (transform.ReplacementCondition == condition)
                    return transform.AppliedOrder;
            return int.MaxValue;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static int DefaultDurationFor(ChemicalCondition condition)
        {
            switch (condition)
            {
                case ChemicalCondition.Frozen:        return 1;
                case ChemicalCondition.Evaporating:   return 2;
                case ChemicalCondition.Burning:       return 2;
                case ChemicalCondition.Corroded:      return 3;
                case ChemicalCondition.Crystallized:  return 2;
                default:                              return 1;
            }
        }
    }
}
