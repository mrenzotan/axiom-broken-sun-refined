# DEV-27: Spell Combat Mechanics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement spell combat effects (Damage, Heal, Shield) driven by `SpellData` ScriptableObjects, and the full Chemistry Condition system — innate material conditions on enemies, status conditions applied by spells, and chemical reactions that consume conditions and apply bonus effects or temporary phase-change transformations.

**Architecture:** A new `SpellEffectResolver` plain C# class reads `SpellData` and applies effects to `CharacterStats`. `CharacterStats` is extended with `ShieldHP` (absorbs physical damage), condition lists (`InnateConditions`, `ActiveMaterialConditions`, `ActiveStatusConditions`), and a `ProcessConditionTurn()` method that applies DoT and decrements condition durations each turn. `BattleController.OnSpellCast` deducts MP first (rejecting if insufficient), then calls the resolver and fires typed UI events. Condition turn processing fires at the start of each character's turn, enabling Frozen-skip and DoT display. The `SpellElement` enum concept is abandoned entirely — all elemental interactions are expressed through `ChemicalCondition`.

**Tech Stack:** Unity 6 LTS · URP 2D · C# · ScriptableObjects · `Axiom.Battle` / `Axiom.Data` asmdefs · NUnit (Edit Mode) via Unity Test Framework

**Spec:** `docs/superpowers/specs/2026-04-05-chemistry-condition-combat-system-design.md`

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Data/SpellEffectType.cs` | Enum: Damage, Heal, Shield |
| Create | `Assets/Scripts/Data/ChemicalCondition.cs` | Enum: all material + status conditions (replaces SpellElement) |
| Replace | `Assets/Scripts/Data/SpellData.cs` | Add effectType, power, mpCost, inflictsCondition, reactsWith, reactionBonusDamage, transformsTo, transformationDuration |
| Create | `Assets/Scripts/Data/EnemyData.cs` | ScriptableObject: enemy stats + innateConditions |
| Create | `Assets/Scripts/Battle/StatusConditionEntry.cs` | Struct: Condition, TurnsRemaining, TickCount, BaseDamage |
| Create | `Assets/Scripts/Battle/ConditionTurnResult.cs` | Struct: TotalDamageDealt, ActionSkipped |
| Create | `Assets/Scripts/Battle/SpellResult.cs` | Struct: EffectType, Amount, TargetDefeated, ReactionTriggered, MaterialTransformed, ConditionApplied |
| Modify | `Assets/Scripts/Battle/AttackResult.cs` | Add IsImmune field |
| Replace | `Assets/Scripts/Battle/CharacterStats.cs` | Add ShieldHP, condition lists, condition methods, ProcessConditionTurn, EffectiveATK, IsPhysicallyImmune |
| Create | `Assets/Scripts/Battle/SpellEffectResolver.cs` | Resolve(spell, caster, opposingTarget) → SpellResult |
| Modify | `Assets/Scripts/Battle/PlayerActionHandler.cs` | Use EffectiveATK; check IsPhysicallyImmune before attack |
| Modify | `Assets/Scripts/Battle/EnemyActionHandler.cs` | Use EffectiveATK; check IsPhysicallyImmune before attack |
| Modify | `Assets/Scripts/Battle/BattleController.cs` | SpellEffectResolver field; EnemyData SerializeField; 5 new events; rewrite OnSpellCast; condition tick at turn start |
| Modify | `Assets/Scripts/Battle/UI/SpellInputUILogic.cs` | Add Rejected state + ShowRejection() |
| Modify | `Assets/Scripts/Battle/UI/FloatingNumberSpawner.cs` | Add NumberType.Shield (blue color) |
| Modify | `Assets/Scripts/Battle/UI/BattleHUD.cs` | Subscribe to 4 new events; update HP bars on heal/DoT |
| Modify | `Assets/Scripts/Battle/UI/SpellInputUI.cs` | Subscribe to OnSpellCastRejected; show rejection message |
| Modify (tests) | `Assets/Tests/Editor/Battle/CharacterStatsTests.cs` | Add shield + condition + ProcessConditionTurn tests |
| Create (tests) | `Assets/Tests/Editor/Battle/SpellEffectResolverTests.cs` | All resolver paths: damage, heal, shield, reaction, transform, inflict, immunity, null guards |
| Modify (tests) | `Assets/Tests/Editor/Battle/SpellInputUILogicTests.cs` | Add Rejected state tests |

No new `.asmdef` files are needed — all new code lands in `Axiom.Data` or `Axiom.Battle`, both of which already exist and are `autoReferenced: true`.

---

## Task 1: Core data types — SpellEffectType, ChemicalCondition, SpellData, EnemyData

Pure data — no logic, no tests required.

**Files:**
- Create: `Assets/Scripts/Data/SpellEffectType.cs`
- Create: `Assets/Scripts/Data/ChemicalCondition.cs`
- Replace: `Assets/Scripts/Data/SpellData.cs`
- Create: `Assets/Scripts/Data/EnemyData.cs`

- [ ] **Create `Assets/Scripts/Data/SpellEffectType.cs`**

```csharp
namespace Axiom.Data
{
    public enum SpellEffectType
    {
        Damage,
        Heal,
        Shield
    }
}
```

- [ ] **Create `Assets/Scripts/Data/ChemicalCondition.cs`**

```csharp
namespace Axiom.Data
{
    /// <summary>
    /// All chemical conditions a character can carry during combat.
    ///
    /// Material conditions describe chemical composition — what an enemy IS MADE OF.
    /// They are innate (set on EnemyData), never applied directly by spells, and are
    /// permanent unless temporarily suppressed by a phase-change reaction.
    /// They gate which attack types are valid (Liquid/Vapor are immune to physical attacks).
    ///
    /// Status conditions describe an active chemical state — what is CURRENTLY HAPPENING
    /// to a character. They are applied mid-combat by spells or as reaction byproducts,
    /// produce ongoing turn effects (DoT, crowd control), and expire after a fixed number of turns.
    ///
    /// None is the safe default for all SpellData condition fields — has no gameplay effect.
    /// </summary>
    public enum ChemicalCondition
    {
        None,

        // ── Material Conditions (innate composition; no turn effects) ──────────
        Liquid,           // Phase Change — immune to physical attacks; reaction target: Freeze → Solid, Evaporate → Vapor
        Solid,            // Phase Change — vulnerable to physical attacks; reaction target: Melt → Liquid, Shatter (physical exploit)
        Vapor,            // Phase Change — immune to physical attacks; reaction target: Combust → explosion, Condense → Liquid
        Flammable,        // Combustion  — reaction target: Combust → Burning
        Pressurized,      // Combustion  — reaction target: Combust → explosion bonus + Burning
        AcidicFluid,      // Acid–Base   — passive acid damage; reaction target: Base Surge → bonus damage
        AlkalineBase,     // Acid–Base   — reaction target: Acid spell → bonus damage
        MineralSaturated, // Precipitation — reaction target: Crystallize → Crystallized

        // ── Status Conditions (active states; can have turn effects) ──────────
        Frozen,           // Phase Change  — target skips their action (1 turn)
        Evaporating,      // Phase Change  — minor DoT each turn (2 turns)
        Burning,          // Combustion    — fire DoT each turn (2 turns)
        Corroded,         // Acid–Base     — escalating acid DoT: ×1.0 / ×1.5 / ×2.0 per tick (3 turns)
        Crystallized      // Precipitation — target's damage output halved (2 turns)
    }
}
```

- [ ] **Replace the contents of `Assets/Scripts/Data/SpellData.cs`**

```csharp
using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewSpellData", menuName = "Axiom/Data/Spell Data")]
    public class SpellData : ScriptableObject
    {
        [Tooltip("The spoken trigger word or phrase the player says to cast this spell.")]
        public string spellName;

        [Tooltip("The type of effect this spell applies: Damage (targets enemy), Heal (targets caster), or Shield (targets caster).")]
        public SpellEffectType effectType;

        [Tooltip("Base magnitude: damage dealt, HP restored, or shield HP added. Stat-based modifiers are not applied in Phase 2.")]
        public int power;

        [Tooltip("MP cost to cast this spell.")]
        public int mpCost;

        [Header("Chemistry Condition System")]

        [Tooltip("Status condition applied to the spell's primary target after it resolves. None if no condition is inflicted. Spells can never directly apply a material condition via this field.")]
        public ChemicalCondition inflictsCondition;

        [Tooltip("The condition (material or status) this spell reacts with if already present on the target. None if the spell has no reaction.")]
        public ChemicalCondition reactsWith;

        [Tooltip("Flat bonus added to the spell's primary effect when a reaction triggers. For Damage: bonus damage. For Heal: bonus HP restored. For Shield: bonus shield HP.")]
        public int reactionBonusDamage;

        [Tooltip("Material condition temporarily applied to the target when a phase-change reaction fires. None if this reaction causes no material transformation.")]
        public ChemicalCondition transformsTo;

        [Tooltip("How many turns the transformed material condition lasts before the innate condition is restored. Only meaningful when transformsTo != None.")]
        public int transformationDuration;
    }
}
```

- [ ] **Create `Assets/Scripts/Data/EnemyData.cs`**

```csharp
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

        [Tooltip("1–2 material conditions the enemy starts every combat with. Defines what the enemy is made of — determines physical immunity, reaction targets, and other combat interactions.")]
        public List<ChemicalCondition> innateConditions = new List<ChemicalCondition>();
    }
}
```

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-27): add SpellEffectType, ChemicalCondition enums; expand SpellData; add EnemyData`
  - `Assets/Scripts/Data/SpellEffectType.cs`
  - `Assets/Scripts/Data/SpellEffectType.cs.meta`
  - `Assets/Scripts/Data/ChemicalCondition.cs`
  - `Assets/Scripts/Data/ChemicalCondition.cs.meta`
  - `Assets/Scripts/Data/SpellData.cs`
  - `Assets/Scripts/Data/EnemyData.cs`
  - `Assets/Scripts/Data/EnemyData.cs.meta`

---

## Task 2: Battle value types — StatusConditionEntry, ConditionTurnResult, SpellResult, AttackResult

Pure data structs — no logic, no tests required.

**Files:**
- Create: `Assets/Scripts/Battle/StatusConditionEntry.cs`
- Create: `Assets/Scripts/Battle/ConditionTurnResult.cs`
- Create: `Assets/Scripts/Battle/SpellResult.cs`
- Modify: `Assets/Scripts/Battle/AttackResult.cs`

- [ ] **Create `Assets/Scripts/Battle/StatusConditionEntry.cs`**

```csharp
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Tracks one active status condition on a character during combat.
    /// TickCount records how many DoT ticks have already fired — used by
    /// escalating conditions like Corroded to compute the current damage multiplier.
    /// BaseDamage is set by SpellEffectResolver when the condition is applied and
    /// used by ProcessConditionTurn for DoT calculations.
    /// </summary>
    public struct StatusConditionEntry
    {
        public ChemicalCondition Condition;
        public int TurnsRemaining;
        public int TickCount;
        public int BaseDamage;
    }
}
```

- [ ] **Create `Assets/Scripts/Battle/ConditionTurnResult.cs`**

```csharp
namespace Axiom.Battle
{
    /// <summary>
    /// Return value of CharacterStats.ProcessConditionTurn().
    /// Tells BattleController what happened during the condition tick for this character's turn.
    /// </summary>
    public struct ConditionTurnResult
    {
        /// <summary>Sum of all DoT damage dealt to this character this tick.</summary>
        public int TotalDamageDealt;

        /// <summary>True if the character has the Frozen status — their action should be skipped.</summary>
        public bool ActionSkipped;
    }
}
```

- [ ] **Create `Assets/Scripts/Battle/SpellResult.cs`**

```csharp
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Return value of SpellEffectResolver.Resolve().
    /// Carries all spell outcome data so BattleController can fire the correct UI events.
    /// </summary>
    public struct SpellResult
    {
        /// <summary>The category of effect that was applied.</summary>
        public SpellEffectType EffectType;

        /// <summary>
        /// Total effect magnitude after reaction bonus is applied.
        /// Damage dealt, HP healed, or shield HP added — depending on EffectType.
        /// </summary>
        public int Amount;

        /// <summary>True if the spell's primary target (enemy for Damage) was defeated.</summary>
        public bool TargetDefeated;

        /// <summary>True if a chemical reaction triggered during resolution.</summary>
        public bool ReactionTriggered;

        /// <summary>True if a material condition transformation was applied to the target.</summary>
        public bool MaterialTransformed;

        /// <summary>The status condition inflicted during this spell, or None if none was applied.</summary>
        public ChemicalCondition ConditionApplied;
    }
}
```

- [ ] **Replace the contents of `Assets/Scripts/Battle/AttackResult.cs`**

```csharp
namespace Axiom.Battle
{
    /// <summary>
    /// Return value of ExecuteAttack() on action handlers.
    /// Carries damage amount, crit flag, defeat flag, and immunity flag so BattleController
    /// can fire the correct UI events without querying stats again.
    /// IsImmune is true when the target's active material conditions (Liquid or Vapor) make
    /// them immune to physical attacks. In that case Damage is 0.
    /// </summary>
    public struct AttackResult
    {
        public int Damage;
        public bool IsCrit;
        public bool TargetDefeated;
        public bool IsImmune;
    }
}
```

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-27): add StatusConditionEntry, ConditionTurnResult, SpellResult structs; extend AttackResult`
  - `Assets/Scripts/Battle/StatusConditionEntry.cs`
  - `Assets/Scripts/Battle/StatusConditionEntry.cs.meta`
  - `Assets/Scripts/Battle/ConditionTurnResult.cs`
  - `Assets/Scripts/Battle/ConditionTurnResult.cs.meta`
  - `Assets/Scripts/Battle/SpellResult.cs`
  - `Assets/Scripts/Battle/SpellResult.cs.meta`
  - `Assets/Scripts/Battle/AttackResult.cs`

---

## Task 3: CharacterStats — Shield + Condition support (TDD)

`CharacterStats` is a plain C# class and fully testable in Edit Mode. TDD is used for all new logic.

**Files:**
- Modify (tests): `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
- Replace: `Assets/Scripts/Battle/CharacterStats.cs`

### Phase A — Shield

- [ ] **Append these four failing tests to `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`**

```csharp
// ---- ApplyShield ----

[Test]
public void ApplyShield_SetsShieldHP()
{
    var stats = MakeStats();
    stats.Initialize();
    stats.ApplyShield(30);
    Assert.AreEqual(30, stats.ShieldHP);
}

[Test]
public void TakeDamage_WithShield_AbsorbsDamageBeforeHP()
{
    var stats = MakeStats(maxHp: 100);
    stats.Initialize();
    stats.ApplyShield(20);
    stats.TakeDamage(15);
    Assert.AreEqual(5,   stats.ShieldHP);   // 20 - 15 = 5
    Assert.AreEqual(100, stats.CurrentHP);  // HP untouched
}

[Test]
public void TakeDamage_ExceedingShield_ReducesHPByRemainder()
{
    var stats = MakeStats(maxHp: 100);
    stats.Initialize();
    stats.ApplyShield(10);
    stats.TakeDamage(25);   // 10 absorbed by shield, 15 carries through
    Assert.AreEqual(0,  stats.ShieldHP);
    Assert.AreEqual(85, stats.CurrentHP);   // 100 - 15
}

[Test]
public void Initialize_ResetsShieldHPToZero()
{
    var stats = MakeStats();
    stats.Initialize();
    stats.ApplyShield(50);
    stats.Initialize();     // second init must clear shield
    Assert.AreEqual(0, stats.ShieldHP);
}
```

- [ ] **Run the four new tests in Unity Test Runner — confirm they FAIL**

  Unity Editor → Window → General → Test Runner → Edit Mode → run the four new tests.
  Expected: compile errors — `ShieldHP` and `ApplyShield` do not yet exist.

### Phase B — Condition fields and methods

- [ ] **Append these condition tests to `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`**

```csharp
// ---- Condition helpers ----

[Test]
public void HasCondition_ReturnsFalse_WhenConditionAbsent()
{
    var stats = MakeStats();
    stats.Initialize();
    Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Burning));
}

[Test]
public void ApplyStatusCondition_AddsConditionToActiveList()
{
    var stats = MakeStats();
    stats.Initialize();
    stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Burning, baseDamage: 5);
    Assert.IsTrue(stats.HasCondition(Axiom.Data.ChemicalCondition.Burning));
}

[Test]
public void HasCondition_ReturnsTrue_WhenInActiveMaterialConditions()
{
    var stats = MakeStats();
    stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
        { Axiom.Data.ChemicalCondition.Liquid });
    Assert.IsTrue(stats.HasCondition(Axiom.Data.ChemicalCondition.Liquid));
}

[Test]
public void ConsumeCondition_RemovesStatusCondition()
{
    var stats = MakeStats();
    stats.Initialize();
    stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Burning, baseDamage: 5);
    stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Burning);
    Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Burning));
}

[Test]
public void ConsumeCondition_RemovesMaterialCondition()
{
    var stats = MakeStats();
    stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
        { Axiom.Data.ChemicalCondition.Flammable });
    stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Flammable);
    Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Flammable));
}

[Test]
public void ApplyMaterialTransformation_ReplacesActiveConditionWithTemporaryOne()
{
    var stats = MakeStats();
    stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
        { Axiom.Data.ChemicalCondition.Liquid });
    stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Liquid);
    stats.ApplyMaterialTransformation(
        Axiom.Data.ChemicalCondition.Solid,
        Axiom.Data.ChemicalCondition.Liquid,
        duration: 2);
    Assert.IsTrue (stats.HasCondition(Axiom.Data.ChemicalCondition.Solid));
    Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Liquid));
}

[Test]
public void Initialize_ClearsAllConditionsAndRestoresInnate()
{
    var stats = MakeStats();
    stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
        { Axiom.Data.ChemicalCondition.Liquid });
    stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Burning, baseDamage: 5);
    stats.Initialize(); // re-init with no args — clears everything
    Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Burning));
    Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Liquid));
    Assert.AreEqual(0, stats.ShieldHP);
}
```

- [ ] **Run all new tests — confirm they FAIL**

  Expected: compile errors — `HasCondition`, `ApplyStatusCondition`, `ConsumeCondition`, `ApplyMaterialTransformation` do not yet exist; `Initialize` overload does not yet exist.

- [ ] **Replace the entire contents of `Assets/Scripts/Battle/CharacterStats.cs`**

```csharp
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
        /// </summary>
        public List<ChemicalCondition> ActiveMaterialConditions { get; private set; } = new List<ChemicalCondition>();

        /// <summary>
        /// Active status conditions. Each entry holds the condition, remaining turns, tick count,
        /// and the base damage used for DoT calculations.
        /// </summary>
        public List<StatusConditionEntry> ActiveStatusConditions { get; private set; } = new List<StatusConditionEntry>();

        // Tracks active material transformations so we know what to restore when they expire.
        private readonly List<MaterialTransformEntry> _materialTransformations = new List<MaterialTransformEntry>();

        private struct MaterialTransformEntry
        {
            public ChemicalCondition ReplacementCondition;  // temporary condition added (e.g. Solid)
            public ChemicalCondition SuppressedCondition;   // innate condition it replaced (e.g. Liquid)
            public int TurnsRemaining;
        }

        // ── Initialization ───────────────────────────────────────────────────

        /// <summary>
        /// Resets all runtime state for a new battle.
        /// Pass innateConditions from EnemyData for enemies; omit (null) for the player.
        /// </summary>
        public void Initialize(List<ChemicalCondition> innateConditions = null)
        {
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
            ShieldHP  = 0;

            _innateConditions          = innateConditions ?? new List<ChemicalCondition>();
            ActiveMaterialConditions   = new List<ChemicalCondition>(_innateConditions);
            ActiveStatusConditions     = new List<StatusConditionEntry>();
            _materialTransformations.Clear();
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
        public void ApplyStatusCondition(ChemicalCondition condition, int baseDamage = 0)
        {
            int duration = DefaultDurationFor(condition);
            ActiveStatusConditions.Add(new StatusConditionEntry
            {
                Condition      = condition,
                TurnsRemaining = duration,
                TickCount      = 0,
                BaseDamage     = baseDamage
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
                TurnsRemaining       = duration
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
                    ActiveStatusConditions[i] = entry;
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
```

- [ ] **Run all CharacterStats tests in Unity Test Runner — confirm they all PASS**

  Unity Editor → Window → General → Test Runner → Edit Mode → select `CharacterStatsTests` → Run Selected.
  Expected: all tests green, including the existing 17 tests plus the 11 new ones.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-27): extend CharacterStats with shield absorption, condition tracking, and ProcessConditionTurn`
  - `Assets/Scripts/Battle/CharacterStats.cs`
  - `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`

---

## Task 4: SpellEffectResolver (TDD)

The resolver is a plain C# class — fully testable in Edit Mode. Write tests first.

**Files:**
- Create (tests): `Assets/Tests/Editor/Battle/SpellEffectResolverTests.cs`
- Create: `Assets/Scripts/Battle/SpellEffectResolver.cs`

- [ ] **Create `Assets/Tests/Editor/Battle/SpellEffectResolverTests.cs`**

```csharp
using NUnit.Framework;
using Axiom.Battle;
using Axiom.Data;
using System.Collections.Generic;
using UnityEngine;

public class SpellEffectResolverTests
{
    private SpellEffectResolver _resolver;
    private CharacterStats _caster;
    private CharacterStats _target;

    [SetUp]
    public void SetUp()
    {
        _resolver = new SpellEffectResolver();
        _caster   = new CharacterStats { Name = "Kael",      MaxHP = 100, MaxMP = 50, ATK = 10, DEF = 5,  SPD = 8 };
        _target   = new CharacterStats { Name = "VoidWraith", MaxHP = 60,  MaxMP = 0,  ATK = 8,  DEF = 3,  SPD = 5 };
        _caster.Initialize();
        _target.Initialize();
    }

    private static SpellData MakeSpell(
        SpellEffectType effect = SpellEffectType.Damage,
        int power = 10,
        int mpCost = 0,
        ChemicalCondition inflicts = ChemicalCondition.None,
        ChemicalCondition reactsWith = ChemicalCondition.None,
        int reactionBonus = 0,
        ChemicalCondition transformsTo = ChemicalCondition.None,
        int transformDuration = 0)
    {
        var spell = ScriptableObject.CreateInstance<SpellData>();
        spell.effectType          = effect;
        spell.power               = power;
        spell.mpCost              = mpCost;
        spell.inflictsCondition   = inflicts;
        spell.reactsWith          = reactsWith;
        spell.reactionBonusDamage = reactionBonus;
        spell.transformsTo        = transformsTo;
        spell.transformationDuration = transformDuration;
        return spell;
    }

    // ── Null guards ───────────────────────────────────────────────────────────

    [Test]
    public void Resolve_NullSpell_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(null, _caster, _target));
    }

    [Test]
    public void Resolve_NullCaster_ThrowsArgumentNullException()
    {
        var spell = MakeSpell();
        Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(spell, null, _target));
    }

    [Test]
    public void Resolve_NullTarget_ThrowsArgumentNullException()
    {
        var spell = MakeSpell();
        Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(spell, _caster, null));
    }

    // ── Primary effect: Damage ────────────────────────────────────────────────

    [Test]
    public void Resolve_DamageSpell_DealsDamagePowerToTarget()
    {
        var spell = MakeSpell(effect: SpellEffectType.Damage, power: 15);
        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(SpellEffectType.Damage, result.EffectType);
        Assert.AreEqual(15,  result.Amount);
        Assert.AreEqual(45,  _target.CurrentHP);  // 60 - 15
        Assert.IsFalse(result.TargetDefeated);
    }

    [Test]
    public void Resolve_DamageSpell_SetsTargetDefeated_WhenHPReachesZero()
    {
        var spell = MakeSpell(effect: SpellEffectType.Damage, power: 60);
        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.IsTrue(result.TargetDefeated);
        Assert.IsTrue(_target.IsDefeated);
    }

    // ── Primary effect: Heal ──────────────────────────────────────────────────

    [Test]
    public void Resolve_HealSpell_RestoresCasterHP()
    {
        _caster.TakeDamage(30);    // CurrentHP = 70
        var spell = MakeSpell(effect: SpellEffectType.Heal, power: 20);
        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(SpellEffectType.Heal, result.EffectType);
        Assert.AreEqual(20,  result.Amount);
        Assert.AreEqual(90,  _caster.CurrentHP);  // 70 + 20
    }

    [Test]
    public void Resolve_HealSpell_DoesNotAffectTarget()
    {
        _caster.TakeDamage(30);
        var spell = MakeSpell(effect: SpellEffectType.Heal, power: 20);
        _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(60, _target.CurrentHP);  // unchanged
    }

    // ── Primary effect: Shield ────────────────────────────────────────────────

    [Test]
    public void Resolve_ShieldSpell_AppliesShieldToCaster()
    {
        var spell = MakeSpell(effect: SpellEffectType.Shield, power: 25);
        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(SpellEffectType.Shield, result.EffectType);
        Assert.AreEqual(25, result.Amount);
        Assert.AreEqual(25, _caster.ShieldHP);
    }

    [Test]
    public void Resolve_ShieldSpell_DoesNotAffectTarget()
    {
        var spell = MakeSpell(effect: SpellEffectType.Shield, power: 25);
        _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(0, _target.ShieldHP);
    }

    // ── Reaction system ───────────────────────────────────────────────────────

    [Test]
    public void Resolve_WithReactsWith_WhenConditionPresent_TriggersReactionAndBonusDamage()
    {
        _target.Initialize(new List<ChemicalCondition> { ChemicalCondition.Flammable });
        var spell = MakeSpell(
            effect: SpellEffectType.Damage,
            power: 10,
            reactsWith: ChemicalCondition.Flammable,
            reactionBonus: 5);

        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.IsTrue(result.ReactionTriggered);
        Assert.AreEqual(15, result.Amount);          // 10 base + 5 bonus
        Assert.AreEqual(45, _target.CurrentHP);      // 60 - 15
        Assert.IsFalse(_target.HasCondition(ChemicalCondition.Flammable)); // consumed
    }

    [Test]
    public void Resolve_WithReactsWith_WhenConditionAbsent_NoReactionFires()
    {
        var spell = MakeSpell(
            effect: SpellEffectType.Damage,
            power: 10,
            reactsWith: ChemicalCondition.Flammable,
            reactionBonus: 5);

        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.IsFalse(result.ReactionTriggered);
        Assert.AreEqual(10, result.Amount);
    }

    [Test]
    public void Resolve_HealSpell_ReactionChecksAndAppliesToCaster()
    {
        // Neutralize: heal spell that reacts with Corroded on the caster
        _caster.Initialize();
        _caster.ApplyStatusCondition(ChemicalCondition.Corroded, baseDamage: 4);
        var spell = MakeSpell(
            effect: SpellEffectType.Heal,
            power: 10,
            reactsWith: ChemicalCondition.Corroded,
            reactionBonus: 5);
        _caster.TakeDamage(20);  // CurrentHP = 80

        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.IsTrue(result.ReactionTriggered);
        Assert.AreEqual(15, result.Amount);          // 10 + 5 bonus
        Assert.AreEqual(95, _caster.CurrentHP);      // 80 + 15
        Assert.IsFalse(_caster.HasCondition(ChemicalCondition.Corroded)); // consumed
    }

    // ── Phase-change transformation ───────────────────────────────────────────

    [Test]
    public void Resolve_WithTransformsTo_AppliesMaterialTransformation()
    {
        _target.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });
        var spell = MakeSpell(
            effect: SpellEffectType.Damage,
            power: 10,
            reactsWith: ChemicalCondition.Liquid,
            transformsTo: ChemicalCondition.Solid,
            transformDuration: 2);

        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.IsTrue(result.MaterialTransformed);
        Assert.IsTrue (_target.HasCondition(ChemicalCondition.Solid));
        Assert.IsFalse(_target.HasCondition(ChemicalCondition.Liquid));
    }

    // ── Inflict condition ─────────────────────────────────────────────────────

    [Test]
    public void Resolve_WithInflictsCondition_WhenNotPresent_AppliesCondition()
    {
        var spell = MakeSpell(
            effect: SpellEffectType.Damage,
            power: 10,
            inflicts: ChemicalCondition.Burning);

        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(ChemicalCondition.Burning, result.ConditionApplied);
        Assert.IsTrue(_target.HasCondition(ChemicalCondition.Burning));
    }

    [Test]
    public void Resolve_WithInflictsCondition_WhenAlreadyPresent_DoesNotDuplicate()
    {
        _target.ApplyStatusCondition(ChemicalCondition.Burning, baseDamage: 5);
        var spell = MakeSpell(
            effect: SpellEffectType.Damage,
            power: 10,
            inflicts: ChemicalCondition.Burning);

        _resolver.Resolve(spell, _caster, _target);

        int count = 0;
        foreach (var e in _target.ActiveStatusConditions)
            if (e.Condition == ChemicalCondition.Burning) count++;
        Assert.AreEqual(1, count); // still only one
    }

    // ── Physical immunity ─────────────────────────────────────────────────────

    [Test]
    public void Resolve_DamageSpell_TargetLiquid_StillDealsSpellDamage()
    {
        // Spells bypass physical immunity — only basic attacks are blocked
        _target.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });
        var spell = MakeSpell(effect: SpellEffectType.Damage, power: 15);

        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(15, result.Amount);
        Assert.AreEqual(45, _target.CurrentHP);
    }
}
```

- [ ] **Run all SpellEffectResolverTests — confirm they FAIL**

  Unity Editor → Window → General → Test Runner → Edit Mode → run `SpellEffectResolverTests`.
  Expected: compile errors — `SpellEffectResolver` does not yet exist.

- [ ] **Create `Assets/Scripts/Battle/SpellEffectResolver.cs`**

```csharp
using System;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Pure C# resolver that applies a spell's effects to combat stats.
    /// No MonoBehaviour — created and held by BattleController.
    ///
    /// Resolution order (per chemistry-condition-combat-system-design spec):
    ///   1. Null guards
    ///   2. Reaction check — if reactsWith condition present on effect target, reaction fires
    ///   3. Primary effect — Damage to opposing target, or Heal/Shield to caster
    ///   4. Inflict check — status condition applied to effect target if not already present
    ///   5. Return SpellResult
    ///
    /// DoT base damage values are defined as constants here and passed to
    /// CharacterStats.ApplyStatusCondition(). They are intentionally flat for Phase 2.
    /// </summary>
    public class SpellEffectResolver
    {
        // ── DoT damage constants ─────────────────────────────────────────────
        private const int BurningDoTDamage      = 5;
        private const int EvaporatingDoTDamage  = 3;
        private const int CorrodedBaseDoTDamage = 4;

        /// <summary>
        /// Resolves a spell cast by caster against an opposing target.
        ///
        /// Effect targeting:
        ///   Damage → applied to opposingTarget (the enemy)
        ///   Heal / Shield → applied to caster (self-targeted effects)
        ///
        /// Reactions always check the primary effect target (opposingTarget for Damage,
        /// caster for Heal/Shield). MP has already been deducted by BattleController.
        /// </summary>
        public SpellResult Resolve(SpellData spell, CharacterStats caster, CharacterStats opposingTarget)
        {
            if (spell           == null) throw new ArgumentNullException(nameof(spell));
            if (caster          == null) throw new ArgumentNullException(nameof(caster));
            if (opposingTarget  == null) throw new ArgumentNullException(nameof(opposingTarget));

            // The character the spell's effect applies to
            CharacterStats effectTarget = spell.effectType == SpellEffectType.Damage
                ? opposingTarget
                : caster;

            bool reactionTriggered   = false;
            bool materialTransformed = false;
            int  bonusDamage         = 0;

            // ── 2. Reaction check ────────────────────────────────────────────
            if (spell.reactsWith != ChemicalCondition.None
                && effectTarget.HasCondition(spell.reactsWith))
            {
                reactionTriggered = true;
                bonusDamage       = spell.reactionBonusDamage;

                effectTarget.ConsumeCondition(spell.reactsWith);

                if (spell.transformsTo != ChemicalCondition.None)
                {
                    effectTarget.ApplyMaterialTransformation(
                        spell.transformsTo,
                        spell.reactsWith,
                        spell.transformationDuration);
                    materialTransformed = true;
                }
            }

            // ── 3. Primary effect ────────────────────────────────────────────
            int     magnitude      = spell.power + bonusDamage;
            bool    targetDefeated = false;

            switch (spell.effectType)
            {
                case SpellEffectType.Damage:
                    opposingTarget.TakeDamage(magnitude);
                    targetDefeated = opposingTarget.IsDefeated;
                    break;
                case SpellEffectType.Heal:
                    caster.Heal(magnitude);
                    break;
                case SpellEffectType.Shield:
                    caster.ApplyShield(magnitude);
                    break;
            }

            // ── 4. Inflict check ─────────────────────────────────────────────
            ChemicalCondition conditionApplied = ChemicalCondition.None;
            if (spell.inflictsCondition != ChemicalCondition.None
                && !effectTarget.HasCondition(spell.inflictsCondition))
            {
                int baseDamage = DoTDamageFor(spell.inflictsCondition);
                effectTarget.ApplyStatusCondition(spell.inflictsCondition, baseDamage);
                conditionApplied = spell.inflictsCondition;
            }

            return new SpellResult
            {
                EffectType          = spell.effectType,
                Amount              = magnitude,
                TargetDefeated      = targetDefeated,
                ReactionTriggered   = reactionTriggered,
                MaterialTransformed = materialTransformed,
                ConditionApplied    = conditionApplied
            };
        }

        private static int DoTDamageFor(ChemicalCondition condition)
        {
            switch (condition)
            {
                case ChemicalCondition.Burning:     return BurningDoTDamage;
                case ChemicalCondition.Evaporating: return EvaporatingDoTDamage;
                case ChemicalCondition.Corroded:    return CorrodedBaseDoTDamage;
                default:                            return 0;
            }
        }
    }
}
```

- [ ] **Run all SpellEffectResolverTests — confirm they all PASS**

  Unity Editor → Window → General → Test Runner → Edit Mode → run `SpellEffectResolverTests`.
  Expected: all tests green.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-27): add SpellEffectResolver with full reaction, transform, and condition logic`
  - `Assets/Scripts/Battle/SpellEffectResolver.cs`
  - `Assets/Scripts/Battle/SpellEffectResolver.cs.meta`
  - `Assets/Tests/Editor/Battle/SpellEffectResolverTests.cs`
  - `Assets/Tests/Editor/Battle/SpellEffectResolverTests.cs.meta`

---

## Task 5: ProcessConditionTurn tests

The implementation was written in Task 3, but `ProcessConditionTurn` branches need their own dedicated tests. Append these now that the resolver and structs exist.

**Files:**
- Modify (tests): `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`

- [ ] **Append these tests to `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`**

```csharp
// ---- ProcessConditionTurn ----

[Test]
public void ProcessConditionTurn_BurningDealsDoTDamage()
{
    var stats = MakeStats(maxHp: 100);
    stats.Initialize();
    stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Burning, baseDamage: 5);

    ConditionTurnResult result = stats.ProcessConditionTurn();

    Assert.AreEqual(5,  result.TotalDamageDealt);
    Assert.AreEqual(95, stats.CurrentHP);
}

[Test]
public void ProcessConditionTurn_FrozenSetsActionSkipped()
{
    var stats = MakeStats();
    stats.Initialize();
    stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Frozen, baseDamage: 0);

    ConditionTurnResult result = stats.ProcessConditionTurn();

    Assert.IsTrue(result.ActionSkipped);
}

[Test]
public void ProcessConditionTurn_CorrodedEscalatesDamageOnSecondTick()
{
    var stats = MakeStats(maxHp: 100);
    stats.Initialize();
    stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Corroded, baseDamage: 4);

    // Tick 1: ×1.0 = 4 damage
    stats.ProcessConditionTurn();
    Assert.AreEqual(96, stats.CurrentHP);

    // Tick 2: ×1.5 = 6 damage
    stats.ProcessConditionTurn();
    Assert.AreEqual(90, stats.CurrentHP);
}

[Test]
public void ProcessConditionTurn_ExpiredStatusCondition_IsRemoved()
{
    var stats = MakeStats();
    stats.Initialize();
    stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Frozen, baseDamage: 0); // 1-turn duration

    stats.ProcessConditionTurn(); // tick and expire

    Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Frozen));
}

[Test]
public void ProcessConditionTurn_ExpiredMaterialTransformation_RestoresInnateCondition()
{
    var stats = MakeStats();
    stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
        { Axiom.Data.ChemicalCondition.Liquid });

    // Simulate a Freeze reaction: consume Liquid, apply Solid for 1 turn
    stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Liquid);
    stats.ApplyMaterialTransformation(
        Axiom.Data.ChemicalCondition.Solid,
        Axiom.Data.ChemicalCondition.Liquid,
        duration: 1);

    stats.ProcessConditionTurn(); // Solid expires

    Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Solid));
    Assert.IsTrue (stats.HasCondition(Axiom.Data.ChemicalCondition.Liquid)); // restored
}

[Test]
public void ProcessConditionTurn_NoConditions_ReturnsZeroDamageAndNoSkip()
{
    var stats = MakeStats();
    stats.Initialize();

    ConditionTurnResult result = stats.ProcessConditionTurn();

    Assert.AreEqual(0,     result.TotalDamageDealt);
    Assert.IsFalse(result.ActionSkipped);
}
```

- [ ] **Run all CharacterStats tests — confirm they all PASS**

  Unity Editor → Window → General → Test Runner → Edit Mode → run `CharacterStatsTests`.
  Expected: all tests green.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `test(DEV-27): add ProcessConditionTurn tests for CharacterStats`
  - `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`

---

## Task 6: ActionHandlers — physical immunity and Crystallized ATK halving

`PlayerActionHandler` and `EnemyActionHandler` must check `IsPhysicallyImmune` before calculating damage and use `EffectiveATK` to respect Crystallized halving.

**Files:**
- Modify: `Assets/Scripts/Battle/PlayerActionHandler.cs`
- Modify: `Assets/Scripts/Battle/EnemyActionHandler.cs`

- [ ] **Replace the `ExecuteAttack` method in `Assets/Scripts/Battle/PlayerActionHandler.cs`**

```csharp
/// <summary>
/// Deals damage to the enemy. Returns AttackResult with damage, crit flag,
/// defeat flag, and immunity flag.
/// If the enemy is Liquid or Vapor (IsPhysicallyImmune), the attack deals 0 damage.
/// Uses EffectiveATK to respect the Crystallized halving condition on the player.
/// </summary>
public AttackResult ExecuteAttack()
{
    if (_enemyStats.IsPhysicallyImmune)
        return new AttackResult { Damage = 0, IsCrit = false, TargetDefeated = false, IsImmune = true };

    int baseDamage = Math.Max(1, _playerStats.EffectiveATK - _enemyStats.DEF);
    bool isCrit    = _randomSource() < CritChance;
    int damage     = isCrit ? (int)(baseDamage * CritMultiplier) : baseDamage;
    _enemyStats.TakeDamage(damage);
    return new AttackResult
    {
        Damage         = damage,
        IsCrit         = isCrit,
        TargetDefeated = _enemyStats.IsDefeated,
        IsImmune       = false
    };
}
```

- [ ] **Replace the `ExecuteAttack` method in `Assets/Scripts/Battle/EnemyActionHandler.cs`**

```csharp
/// <summary>
/// Deals damage to the player. Returns AttackResult with damage, crit flag,
/// defeat flag, and immunity flag.
/// If the player is Liquid or Vapor (IsPhysicallyImmune), the attack deals 0 damage.
/// Uses EffectiveATK to respect Crystallized halving on the enemy.
/// </summary>
public AttackResult ExecuteAttack()
{
    if (_playerStats.IsPhysicallyImmune)
        return new AttackResult { Damage = 0, IsCrit = false, TargetDefeated = false, IsImmune = true };

    int baseDamage = Math.Max(1, _enemyStats.EffectiveATK - _playerStats.DEF);
    bool isCrit    = _randomSource() < CritChance;
    int damage     = isCrit ? (int)(baseDamage * CritMultiplier) : baseDamage;
    _playerStats.TakeDamage(damage);
    return new AttackResult
    {
        Damage         = damage,
        IsCrit         = isCrit,
        TargetDefeated = _playerStats.IsDefeated,
        IsImmune       = false
    };
}
```

- [ ] **Check compile errors in Unity Editor — confirm no errors**

  Unity Editor → bottom status bar should show no errors. If the compiler complains about `EffectiveATK` or `IsPhysicallyImmune`, verify Task 3's `CharacterStats.cs` was saved correctly.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-27): action handlers respect physical immunity and Crystallized ATK halving`
  - `Assets/Scripts/Battle/PlayerActionHandler.cs`
  - `Assets/Scripts/Battle/EnemyActionHandler.cs`

---

## Task 7: BattleController — full wiring

Wire the resolver, condition turn processing, MP rejection, and new UI events into `BattleController`.

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

- [ ] **Add these fields and events to `BattleController`** (insert after the existing `_isAwaitingVoiceSpell` field and after the `OnSpellNotRecognized` event respectively)

New private field (after `_isAwaitingVoiceSpell`):
```csharp
private SpellEffectResolver _resolver;
```

New SerializeField (after the existing `_enemyAnimator` SerializeField):
```csharp
[SerializeField]
[Tooltip("EnemyData ScriptableObject for the enemy in this battle. Provides innateConditions for the enemy's CharacterStats. Optional — leave unassigned for standalone testing without conditions.")]
private Axiom.Data.EnemyData _enemyData;
```

New events (after the existing `OnSpellNotRecognized` event):
```csharp
/// <summary>
/// Fires when a spell cast is rejected because the caster has insufficient MP.
/// Parameter: the rejection reason message string.
/// SpellInputUI subscribes to show the rejection message.
/// </summary>
public event Action<string> OnSpellCastRejected;

/// <summary>
/// Fires when a Heal spell resolves. Parameters: target CharacterStats, HP amount healed.
/// BattleHUD subscribes to update the HP bar.
/// </summary>
public event Action<CharacterStats, int> OnSpellHealed;

/// <summary>
/// Fires when a Shield spell resolves. Parameters: target CharacterStats, shield HP amount.
/// BattleHUD subscribes to display a blue shield number.
/// </summary>
public event Action<CharacterStats, int> OnShieldApplied;

/// <summary>
/// Fires when a condition DoT ticks at the start of a character's turn.
/// Parameters: afflicted CharacterStats, damage dealt, the condition that dealt it.
/// BattleHUD subscribes to show a floating DoT number.
/// </summary>
public event Action<CharacterStats, int, Axiom.Data.ChemicalCondition> OnConditionDamageTick;
```

- [ ] **Add `_resolver = new SpellEffectResolver();` to the `Initialize` method**

  In `BattleController.Initialize`, after the line `_actionHandler = new PlayerActionHandler(...)`, add:

```csharp
_resolver = new SpellEffectResolver();
```

- [ ] **Also update the `_playerStats.Initialize()` and `_enemyStats.Initialize()` calls inside `Initialize`**

  Replace the two bare `Initialize()` calls:

```csharp
_playerStats.Initialize();
_enemyStats.Initialize(_enemyData != null ? _enemyData.innateConditions : null);
```

- [ ] **Replace the `HandleStateChanged` private method in `BattleController`**

```csharp
private void HandleStateChanged(BattleState state)
{
    Debug.Log($"[Battle] → {state}");
    OnBattleStateChanged?.Invoke(state);

    if (state == BattleState.PlayerTurn)
        ProcessPlayerTurnStart();
    else if (state == BattleState.EnemyTurn)
        ProcessEnemyTurnStart();
    else if (state == BattleState.Fled)
        SceneManager.LoadScene("Platformer");
}

private void ProcessPlayerTurnStart()
{
    ConditionTurnResult result = _playerStats.ProcessConditionTurn();
    if (result.TotalDamageDealt > 0)
        OnConditionDamageTick?.Invoke(_playerStats, result.TotalDamageDealt, Axiom.Data.ChemicalCondition.None);

    if (result.ActionSkipped)
    {
        Debug.Log("[Battle] Player is Frozen — turn skipped.");
        _isProcessingAction = true;
        _playerDamageVisualsFired = true;
        StartCoroutine(CompletePlayerAction(targetDefeated: false));
    }
}

private void ProcessEnemyTurnStart()
{
    ConditionTurnResult result = _enemyStats.ProcessConditionTurn();
    if (result.TotalDamageDealt > 0)
        OnConditionDamageTick?.Invoke(_enemyStats, result.TotalDamageDealt, Axiom.Data.ChemicalCondition.None);

    if (result.ActionSkipped)
    {
        Debug.Log("[Battle] Enemy is Frozen — turn skipped.");
        _battleManager.OnEnemyActionComplete(false);
        return;
    }

    ExecuteEnemyTurn();
}
```

- [ ] **Replace the `OnSpellCast` method in `BattleController`**

```csharp
/// <summary>
/// Called by Axiom.Voice.SpellCastController when a recognized spell name
/// matches an unlocked spell during the voice spell phase.
/// Guards against calls outside the voice spell phase or outside PlayerTurn.
/// Deducts MP first — if insufficient, fires OnSpellCastRejected and lets
/// the player choose again without advancing the turn.
/// </summary>
public void OnSpellCast(SpellData spell)
{
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
    if (!_isAwaitingVoiceSpell) return;

    if (!_playerStats.SpendMP(spell.mpCost))
    {
        _isAwaitingVoiceSpell = false;
        _isProcessingAction   = false;
        OnSpellCastRejected?.Invoke($"Not enough MP to cast {spell.spellName}.");
        Debug.Log($"[Battle] Spell rejected — insufficient MP for {spell.spellName}.");
        return;
    }

    _isAwaitingVoiceSpell     = false;
    _playerDamageVisualsFired = true;
    OnSpellRecognized?.Invoke(spell);

    SpellResult result = _resolver.Resolve(spell, _playerStats, _enemyStats);

    switch (result.EffectType)
    {
        case SpellEffectType.Damage:
            OnDamageDealt?.Invoke(_enemyStats, result.Amount, false);
            if (result.TargetDefeated)
                OnCharacterDefeated?.Invoke(_enemyStats);
            break;
        case SpellEffectType.Heal:
            OnSpellHealed?.Invoke(_playerStats, result.Amount);
            break;
        case SpellEffectType.Shield:
            OnShieldApplied?.Invoke(_playerStats, result.Amount);
            break;
    }

    // Update MP bar after spend
    OnDamageDealt?.Invoke(_playerStats, 0, false); // zero-damage ping so BattleHUD refreshes MP bar

    Debug.Log($"[Battle] Spell cast: {spell.spellName} → {result.EffectType} {result.Amount}" +
              $"{(result.ReactionTriggered ? " [REACTION]" : string.Empty)}");

    StartCoroutine(CompletePlayerAction(result.TargetDefeated));
}
```

> **Note:** The zero-damage `OnDamageDealt` ping is a targeted workaround to trigger BattleHUD's MP bar refresh without adding a dedicated MP-changed event. A dedicated `OnMPChanged` event would be cleaner but is out of scope for Phase 2.

- [ ] **Check compile errors in Unity Editor — confirm no errors**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-27): wire SpellEffectResolver, condition turn processing, and MP rejection into BattleController`
  - `Assets/Scripts/Battle/BattleController.cs`

---

## Task 8: SpellInputUILogic — Rejected state (TDD)

The MP rejection case needs a `Rejected` state so the UI can show the player why the cast failed.

**Files:**
- Modify (tests): `Assets/Tests/Editor/Battle/SpellInputUILogicTests.cs`
- Modify: `Assets/Scripts/Battle/UI/SpellInputUILogic.cs`

- [ ] **Append two failing tests to `Assets/Tests/Editor/Battle/SpellInputUILogicTests.cs`**

```csharp
// ── ShowRejection ─────────────────────────────────────────────────────────

[Test]
public void ShowRejection_SetsRejectedState()
{
    _logic.ShowRejection("Not enough MP.");
    Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.Rejected));
}

[Test]
public void ShowRejection_StoresRejectionMessage()
{
    _logic.ShowRejection("Not enough MP to cast Freeze.");
    Assert.That(_logic.RejectionMessage, Is.EqualTo("Not enough MP to cast Freeze."));
}

[Test]
public void ShowPrompt_ClearsRejectionMessage()
{
    _logic.ShowRejection("Not enough MP.");
    _logic.ShowPrompt();
    Assert.That(_logic.RejectionMessage, Is.Null);
}
```

- [ ] **Run the new tests — confirm they FAIL**

  Expected: compile errors — `State.Rejected`, `ShowRejection`, and `RejectionMessage` do not yet exist.

- [ ] **Replace the entire contents of `Assets/Scripts/Battle/UI/SpellInputUILogic.cs`**

```csharp
namespace Axiom.Battle
{
    /// <summary>
    /// Stateless (stateful but Unity-free) state machine for the spell input UI panel.
    /// Tracks which panel should be visible and what text to display.
    /// Contains no Unity types — fully testable in Edit Mode.
    ///
    /// Owned and driven by SpellInputUI.
    /// </summary>
    public class SpellInputUILogic
    {
        public enum State
        {
            Idle,
            PromptVisible,
            Listening,
            SpellRecognized,
            NotRecognized,
            Rejected         // Cast attempted but rejected (e.g. insufficient MP)
        }

        /// <summary>The current display state of the spell input UI.</summary>
        public State  CurrentState       { get; private set; } = State.Idle;

        /// <summary>
        /// The name of the recognized spell, populated by ShowResult.
        /// Null in all other states.
        /// </summary>
        public string RecognizedSpellName { get; private set; }

        /// <summary>
        /// The rejection reason message, populated by ShowRejection.
        /// Null in all other states.
        /// </summary>
        public string RejectionMessage   { get; private set; }

        /// <summary>Transition to PromptVisible. Clears stored spell name and rejection message.</summary>
        public void ShowPrompt()
        {
            CurrentState       = State.PromptVisible;
            RecognizedSpellName = null;
            RejectionMessage   = null;
        }

        /// <summary>Transition to Listening. Clears stored spell name and rejection message.</summary>
        public void StartListening()
        {
            CurrentState       = State.Listening;
            RecognizedSpellName = null;
            RejectionMessage   = null;
        }

        /// <summary>Transition to SpellRecognized and store the spell name for display.</summary>
        public void ShowResult(string spellName)
        {
            CurrentState       = State.SpellRecognized;
            RecognizedSpellName = spellName;
            RejectionMessage   = null;
        }

        /// <summary>Transition to NotRecognized. Clears stored text.</summary>
        public void ShowError()
        {
            CurrentState       = State.NotRecognized;
            RecognizedSpellName = null;
            RejectionMessage   = null;
        }

        /// <summary>
        /// Transition to Rejected and store the rejection reason.
        /// Called when a spell is recognized but cast fails (e.g. insufficient MP).
        /// </summary>
        public void ShowRejection(string message)
        {
            CurrentState       = State.Rejected;
            RecognizedSpellName = null;
            RejectionMessage   = message;
        }

        /// <summary>Return to Idle. Clears all stored text.</summary>
        public void Hide()
        {
            CurrentState       = State.Idle;
            RecognizedSpellName = null;
            RejectionMessage   = null;
        }
    }
}
```

- [ ] **Run all SpellInputUILogicTests — confirm they all PASS**

  Unity Editor → Window → General → Test Runner → Edit Mode → run `SpellInputUILogicTests`.
  Expected: all tests green, including the 3 new ones.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-27): add Rejected state to SpellInputUILogic for MP rejection feedback`
  - `Assets/Scripts/Battle/UI/SpellInputUILogic.cs`
  - `Assets/Tests/Editor/Battle/SpellInputUILogicTests.cs`

---

## Task 9: UI — FloatingNumberSpawner Shield; BattleHUD new subscriptions; SpellInputUI rejection

**Files:**
- Modify: `Assets/Scripts/Battle/UI/FloatingNumberSpawner.cs`
- Modify: `Assets/Scripts/Battle/UI/BattleHUD.cs`
- Modify: `Assets/Scripts/Battle/UI/SpellInputUI.cs`

### FloatingNumberSpawner — Shield color

- [ ] **In `Assets/Scripts/Battle/UI/FloatingNumberSpawner.cs`, make these three edits:**

  1. Add `Shield` to the `NumberType` enum:

```csharp
public enum NumberType { Damage, Heal, Crit, Shield }
```

  2. Add the Shield color constant (after the existing `CritColor` line):

```csharp
private static readonly Color ShieldColor = new Color(0.28f, 0.62f, 0.95f); // blue
```

  3. Add the `Shield` case to the `switch` statement in `Spawn`, before the `default` case:

```csharp
case NumberType.Shield:
    color = ShieldColor;
    scale = 1f;
    label = $"+{amount} shield";
    break;
```

### BattleHUD — subscribe to new events

- [ ] **In `Assets/Scripts/Battle/UI/BattleHUD.cs`, add these four subscriptions inside `Setup`, after the existing `_battleController.OnCharacterDefeated += HandleCharacterDefeated;` line:**

```csharp
_battleController.OnSpellHealed       += HandleSpellHealed;
_battleController.OnShieldApplied     += HandleShieldApplied;
_battleController.OnConditionDamageTick += HandleConditionDamageTick;
_battleController.OnSpellCastRejected += HandleSpellCastRejected;
```

- [ ] **Add these four private methods to `BattleHUD` (before the `Unsubscribe` method):**

```csharp
private void HandleSpellHealed(CharacterStats target, int amount)
{
    if (target == _playerStats)
    {
        _partyHealthBar.SetHP(target.CurrentHP, target.MaxHP);
        _partyHealthBar.SetMP(target.CurrentMP, target.MaxMP);
    }
    else if (target == _enemyStats)
    {
        _enemyHealthBar.SetHP(target.CurrentHP, target.MaxHP);
    }

    if (_statToRect.TryGetValue(target, out RectTransform rect))
        _floatingNumberSpawner.Spawn(rect, amount, FloatingNumberSpawner.NumberType.Heal);

    _statusMessageUI.Post($"{target.Name} recovers {amount} HP.");
}

private void HandleShieldApplied(CharacterStats target, int amount)
{
    if (_statToRect.TryGetValue(target, out RectTransform rect))
        _floatingNumberSpawner.Spawn(rect, amount, FloatingNumberSpawner.NumberType.Shield);

    _statusMessageUI.Post($"{target.Name} is shielded for {amount} HP.");
}

private void HandleConditionDamageTick(CharacterStats target, int damage, Axiom.Data.ChemicalCondition condition)
{
    if (target == _playerStats)
    {
        _partyHealthBar.SetHP(target.CurrentHP, target.MaxHP);
        _partyHealthBar.SetMP(target.CurrentMP, target.MaxMP);
    }
    else if (target == _enemyStats)
    {
        _enemyHealthBar.SetHP(target.CurrentHP, target.MaxHP);
    }

    if (_statToRect.TryGetValue(target, out RectTransform rect) && damage > 0)
        _floatingNumberSpawner.Spawn(rect, damage, FloatingNumberSpawner.NumberType.Damage);

    if (damage > 0)
        _statusMessageUI.Post($"{target.Name} takes {damage} damage from a condition.");

    if (target.IsDefeated)
        HandleCharacterDefeated(target);
}

private void HandleSpellCastRejected(string reason)
{
    _statusMessageUI.Post(reason);
}
```

- [ ] **Add the four new unsubscribe lines to the `Unsubscribe` method in `BattleHUD`:**

```csharp
_battleController.OnSpellHealed         -= HandleSpellHealed;
_battleController.OnShieldApplied       -= HandleShieldApplied;
_battleController.OnConditionDamageTick -= HandleConditionDamageTick;
_battleController.OnSpellCastRejected   -= HandleSpellCastRejected;
```

### SpellInputUI — rejection display

- [ ] **In `Assets/Scripts/Battle/UI/SpellInputUI.cs`, add a subscription for `OnSpellCastRejected`**

  In `Setup`, after the existing `_battleController.OnSpellNotRecognized += HandleSpellNotRecognized;`:

```csharp
_battleController.OnSpellCastRejected += HandleSpellCastRejected;
```

- [ ] **Add the handler method to `SpellInputUI` (before `Unsubscribe`):**

```csharp
private void HandleSpellCastRejected(string reason)
{
    CancelAutoHide();
    _logic.ShowRejection(reason);
    Refresh();
    // Return to action menu automatically — player must re-select Spell or another action.
    _autoHide = StartCoroutine(AutoHideAfterDelay(returnToPrompt: false));
}
```

- [ ] **Update `Refresh` in `SpellInputUI` to show the feedback panel for `Rejected` state and display the rejection message**

  Replace the existing `Refresh` method:

```csharp
private void Refresh()
{
    SpellInputUILogic.State state = _logic.CurrentState;

    SetActive(_promptPanel,    state == SpellInputUILogic.State.PromptVisible);
    SetActive(_listeningPanel, state == SpellInputUILogic.State.Listening);
    SetActive(_feedbackPanel,  state == SpellInputUILogic.State.SpellRecognized
                            || state == SpellInputUILogic.State.NotRecognized
                            || state == SpellInputUILogic.State.Rejected);

    if (_feedbackText != null)
    {
        _feedbackText.text = state switch
        {
            SpellInputUILogic.State.SpellRecognized => _logic.RecognizedSpellName,
            SpellInputUILogic.State.NotRecognized   => "Not recognized. Try again.",
            SpellInputUILogic.State.Rejected        => _logic.RejectionMessage,
            _                                       => string.Empty
        };
    }
}
```

- [ ] **Add the unsubscribe line to `Unsubscribe` in `SpellInputUI`:**

```csharp
_battleController.OnSpellCastRejected -= HandleSpellCastRejected;
```

- [ ] **Check compile errors in Unity Editor — confirm no errors**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-27): UI — shield floating numbers, condition tick display, spell rejection feedback`
  - `Assets/Scripts/Battle/UI/FloatingNumberSpawner.cs`
  - `Assets/Scripts/Battle/UI/BattleHUD.cs`
  - `Assets/Scripts/Battle/UI/SpellInputUI.cs`

---

## Task 10: Unity Editor tasks — SpellData assets and EnemyData assets

> **Unity Editor task (user):** All steps in this task are performed in the Unity Editor. No code is written.

The AC requires at least 3 `SpellData` assets covering at least 2 different effect types. Create them via the asset menu.

- [ ] **Unity Editor task (user): Create a `Data/Spells/` folder under `Assets/Data/`**

  Project window → right-click `Assets/Data/` → Create → Folder → name it `Spells`.

- [ ] **Unity Editor task (user): Create `Assets/Data/Spells/SD_Freeze.asset`**

  Right-click `Assets/Data/Spells/` → Create → Axiom → Data → Spell Data. Name it `SD_Freeze`. Set values in the Inspector:
  - spellName: `Freeze`
  - effectType: `Damage`
  - power: `12`
  - mpCost: `8`
  - inflictsCondition: `Frozen`
  - reactsWith: `Liquid`
  - reactionBonusDamage: `5`
  - transformsTo: `Solid`
  - transformationDuration: `2`

- [ ] **Unity Editor task (user): Create `Assets/Data/Spells/SD_Combust.asset`**

  Right-click → Create → Axiom → Data → Spell Data. Name it `SD_Combust`. Set values:
  - spellName: `Combust`
  - effectType: `Damage`
  - power: `15`
  - mpCost: `10`
  - inflictsCondition: `Burning`
  - reactsWith: `Flammable`
  - reactionBonusDamage: `8`
  - transformsTo: `None`
  - transformationDuration: `0`

- [ ] **Unity Editor task (user): Create `Assets/Data/Spells/SD_Neutralize.asset`**

  Right-click → Create → Axiom → Data → Spell Data. Name it `SD_Neutralize`. Set values:
  - spellName: `Neutralize`
  - effectType: `Heal`
  - power: `20`
  - mpCost: `6`
  - inflictsCondition: `None`
  - reactsWith: `Corroded`
  - reactionBonusDamage: `10`
  - transformsTo: `None`
  - transformationDuration: `0`

- [ ] **Unity Editor task (user): Create a `Data/Enemies/` folder under `Assets/Data/`**

  Project window → right-click `Assets/Data/` → Create → Folder → name it `Enemies`.

- [ ] **Unity Editor task (user): Create `Assets/Data/Enemies/ED_MeltspawnTest.asset`**

  Right-click `Assets/Data/Enemies/` → Create → Axiom → Data → Enemy Data. Name it `ED_MeltspawnTest`. Set values:
  - enemyName: `Meltspawn`
  - maxHP: `60`
  - maxMP: `0`
  - atk: `8`
  - def: `4`
  - spd: `5`
  - innateConditions: size 1, element 0 = `Liquid`

- [ ] **Unity Editor task (user): Assign ED_MeltspawnTest to the BattleController in the Battle scene**

  Battle scene hierarchy → select the BattleController GameObject → Inspector → `_enemyData` field → assign `ED_MeltspawnTest`.

- [ ] **Unity Editor task (user): Enter Play Mode in the Battle scene and verify end-to-end**

  1. Press Play. The battle starts.
  2. The enemy (Meltspawn) should have `Liquid` as an innate condition.
  3. Press Attack — confirm the attack misses (immune, 0 damage; status bar should indicate immunity — no floating number should appear or it shows 0).
  4. Choose Spell → speak "Freeze" → confirm Freeze resolves: the status area shows damage + reaction bonus; the enemy becomes Solid (no longer immune to physical).
  5. Press Attack on the next turn — confirm the physical attack now lands.
  6. After 2 turns, confirm the enemy reverts to Liquid and physical attacks pass through again.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-27): add SpellData and EnemyData assets for Freeze, Combust, Neutralize, Meltspawn`
  - `Assets/Data/Spells/SD_Freeze.asset`
  - `Assets/Data/Spells/SD_Freeze.asset.meta`
  - `Assets/Data/Spells/SD_Combust.asset`
  - `Assets/Data/Spells/SD_Combust.asset.meta`
  - `Assets/Data/Spells/SD_Neutralize.asset`
  - `Assets/Data/Spells/SD_Neutralize.asset.meta`
  - `Assets/Data/Enemies/ED_MeltspawnTest.asset`
  - `Assets/Data/Enemies/ED_MeltspawnTest.asset.meta`
  - `Assets/Data/Spells/` (folder meta)
  - `Assets/Data/Enemies/` (folder meta)
  - `Assets/Scenes/Battle.unity`

---

## Self-Review Checklist

### 1. Spec coverage

| Spec requirement | Covered in |
|---|---|
| `ChemicalCondition` enum with all 13 values | Task 1 |
| `SpellData` fields: inflictsCondition, reactsWith, reactionBonusDamage, transformsTo, transformationDuration | Task 1 |
| `EnemyData.innateConditions` | Task 1 |
| `CharacterStats.InnateConditions`, `ActiveMaterialConditions`, `ActiveStatusConditions` | Task 3 |
| `CharacterStats.HasCondition`, `ApplyStatusCondition`, `ConsumeCondition`, `ApplyMaterialTransformation` | Task 3 |
| `CharacterStats.Initialize()` populates from EnemyData | Task 7 |
| `SpellEffectResolver.Resolve()` — reaction check, primary effect, inflict step | Task 4 |
| `SpellResult.ReactionTriggered`, `MaterialTransformed` | Task 2 |
| Condition turn processing (DoT, CC, transformation expiry) | Task 3 + Task 5 |
| Phase-change transformation restoration from InnateConditions | Task 3 + Task 5 |
| Liquid/Vapor physical immunity | Task 3 (EffectiveATK, IsPhysicallyImmune) + Task 6 |
| Crystallized ATK halving | Task 3 (EffectiveATK) + Task 6 |
| MP deduction + rejection | Task 7 |
| Reaction checks heal/shield spells against caster | Task 4 (test: Neutralize + Corroded) |
| Shield absorption before HP | Task 3 |
| At least 3 SpellData assets, 2+ effect types | Task 10 |
| Damage numbers + HP bar updates for spells and DoT | Task 9 |

### 2. Type consistency

- `StatusConditionEntry.BaseDamage` — set in `CharacterStats.ApplyStatusCondition`, read in `CharacterStats.ProcessConditionTurn`. ✓
- `MaterialTransformEntry` — private struct in `CharacterStats`; created in `ApplyMaterialTransformation`, decremented in `ProcessConditionTurn`. ✓
- `SpellEffectResolver.Resolve(SpellData, CharacterStats caster, CharacterStats opposingTarget)` — called in `BattleController.OnSpellCast` with `(_playerStats, _enemyStats)`. ✓
- `BattleController.OnSpellCastRejected: Action<string>` — fired in `OnSpellCast`, subscribed in `SpellInputUI.Setup` and `BattleHUD.Setup`. ✓
- `BattleController.OnConditionDamageTick: Action<CharacterStats, int, ChemicalCondition>` — fired in `ProcessPlayerTurnStart` / `ProcessEnemyTurnStart`, subscribed in `BattleHUD.Setup`. ✓
- `SpellInputUILogic.State.Rejected` — set by `ShowRejection`, read in `SpellInputUI.Refresh`. ✓
- `FloatingNumberSpawner.NumberType.Shield` — added in Task 9, used in `BattleHUD.HandleShieldApplied`. ✓

### 3. UVCS staged file audit

Every `.cs` file has a corresponding `.cs.meta` listed in its check-in step. New folders list their `.meta` in Task 10. ✓

### 4. Unity Editor task isolation

All Unity Editor steps in Task 10 are in explicit `> **Unity Editor task (user):**` callouts and are not mixed with code steps. ✓
