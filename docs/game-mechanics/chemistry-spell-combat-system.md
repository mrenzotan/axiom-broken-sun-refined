# Chemistry Spell Combat System

**Status:** Live — implemented as of DEV-27  
**Attach when:** Implementing new spells, adding enemies, or extending combat interactions

---

## Core Concept

Every character in combat carries a list of active **chemical conditions**. Spells interact with these conditions to produce reactions. One resolver, one condition list, no separate type charts.

Conditions split into two categories:

**Material conditions** — what a character *is made of*
- Innate to each enemy, defined in `EnemyData.innateConditions`
- Never applied directly by spells
- Permanent unless a reaction temporarily phase-changes them
- Gate which attack types are valid (see Physical Immunity below)

**Status conditions** — what is *currently happening* to a character
- Applied mid-combat by spells or as reaction byproducts
- Produce ongoing turn effects (DoT, crowd control)
- Expire after a fixed number of turns

---

## Condition Catalogue

### Material Conditions

| Condition | Combat effect | Reaction target |
|---|---|---|
| `Liquid` | Immune to physical attacks | Freeze → `Solid` (phase change); Evaporate → `Vapor` (phase change) |
| `Solid` | Vulnerable to physical attacks | Melt → `Liquid` (phase change); Shatter (physical exploit) |
| `Vapor` | Immune to physical attacks | Combust → explosion; Condense → `Liquid` (phase change) |
| `Flammable` | No passive effect | Combust → `Burning` |
| `Pressurized` | No passive effect | Combust → explosion bonus + `Burning` |
| `AcidicFluid` | Passive acid damage to anything touching it | Base Surge → bonus damage (consumes condition) |
| `AlkalineBase` | No passive effect | Acid spell → bonus damage (consumes condition) |
| `MineralSaturated` | No passive effect | Crystallize → `Crystallized` |

### Status Conditions

| Condition | Turn effect | Duration | DoT base damage |
|---|---|---|---|
| `Frozen` | Target skips their action | 1 turn | — |
| `Evaporating` | Minor damage each turn | 2 turns | 3 |
| `Burning` | Fire damage each turn | 2 turns | 5 |
| `Corroded` | Escalating acid damage: ×1.0 / ×1.5 / ×2.0 per tick | 3 turns | 4 |
| `Crystallized` | Target's damage output (ATK) halved | 2 turns | — |

### `None`

Safe default for all `SpellData` condition fields. Has no gameplay effect.

---

## Physical Immunity

`CharacterStats.IsPhysicallyImmune` returns true when the character has `Liquid` or `Vapor`.

Physical attacks (basic attacks, Shatter-type spells) deal zero damage to physically immune enemies. Phase-change spells (Freeze, Condense) are the intended tools for opening a physical attack window.

---

## Phase-Change Reactions (Material Transformations)

Certain reactions temporarily replace a material condition with another:

| Trigger | Consumed | Temporary replacement | Default duration |
|---|---|---|---|
| Freeze (`reactsWith: Liquid`) | `Liquid` | `Solid` | Spell-defined (e.g. 2 turns) |
| Melt (`reactsWith: Solid`) | `Solid` | `Liquid` | Spell-defined |
| Evaporate (`reactsWith: Liquid`) | `Liquid` | `Vapor` | Spell-defined |
| Condense (`reactsWith: Vapor`) | `Vapor` | `Liquid` | Spell-defined |

When the transformation expires, `CharacterStats.ProcessConditionTurn()` removes the temporary condition and restores the enemy's original innate condition from `InnateConditions`. Enemy identity is preserved — a Meltspawn is always fundamentally `Liquid`.

---

## SpellData Fields Reference

| Field | Type | Description |
|---|---|---|
| `inflictsCondition` | `ChemicalCondition` | Status condition applied to the effect target after resolution. **Never a material condition.** `None` if no condition is inflicted. |
| `reactsWith` | `ChemicalCondition` | Condition (material or status) this spell reacts with if already present on the effect target. `None` if no reaction. |
| `reactionBonusDamage` | `int` | Flat bonus added to the spell's primary effect when a reaction fires. For Damage: bonus damage. For Heal: bonus HP. For Shield: bonus shield HP. |
| `transformsTo` | `ChemicalCondition` | Material condition temporarily applied when a phase-change reaction fires. `None` if no material transformation. |
| `transformationDuration` | `int` | Turns the transformed material condition lasts before the innate condition is restored. Only relevant when `transformsTo != None`. |

**Effect targeting rule** (also applies to reaction checks):
- `Damage` spells → effect target is the enemy
- `Heal` / `Shield` spells → effect target is the caster

A spell like Neutralize (`effectType: Heal`, `reactsWith: Corroded`) reacts with `Corroded` on the *caster*, not the enemy.

---

## Resolver Execution Order

`SpellEffectResolver.Resolve()` runs in this exact order:

1. **Null guards** — throws `ArgumentNullException` if spell, caster, or opposingTarget is null
2. **Reaction check** — if `spell.reactsWith != None` AND `effectTarget.HasCondition(spell.reactsWith)`:
   - Set `reactionTriggered = true`
   - Add `spell.reactionBonusDamage` to bonus
   - Call `effectTarget.ConsumeCondition(spell.reactsWith)`
   - If `spell.transformsTo != None`: call `effectTarget.ApplyMaterialTransformation(transformsTo, reactsWith, duration)`
3. **Primary effect** — apply `spell.power + bonusDamage`:
   - `Damage` → `opposingTarget.TakeDamage(magnitude)`
   - `Heal` → `caster.Heal(magnitude)`
   - `Shield` → `caster.ApplyShield(magnitude)`
4. **Inflict check** — if `spell.inflictsCondition != None` AND `!effectTarget.HasCondition(spell.inflictsCondition)`:
   - Call `effectTarget.ApplyStatusCondition(spell.inflictsCondition, DoTDamageFor(condition))`
5. **Return** `SpellResult` — includes `ReactionTriggered`, `MaterialTransformed`, `ConditionApplied`, `Amount`, `TargetDefeated`

---

## Condition Turn Processing

`CharacterStats.ProcessConditionTurn()` is called by `BattleController` at the **start of each character's turn**. It:

1. Iterates `ActiveStatusConditions` (reverse order for safe removal):
   - `Burning` / `Evaporating` → `TakeDamage(entry.BaseDamage)`
   - `Corroded` → `TakeDamage((int)(BaseDamage * (1.0f + 0.5f * TickCount)))` — escalates per tick
   - `Frozen` → sets `actionSkipped = true` in the result
   - Decrements `TurnsRemaining` and increments `TickCount`; removes expired entries
2. Iterates `_materialTransformations` (reverse order):
   - Decrements `TurnsRemaining`; when expired, removes the replacement from `ActiveMaterialConditions` and restores the suppressed innate condition

Returns `ConditionTurnResult { TotalDamageDealt, ActionSkipped }`.

---

## Data Architecture

| Class / File | Role |
|---|---|
| `ChemicalCondition.cs` (`Assets/Scripts/Data/`) | Enum listing all conditions — both material and status in one enum |
| `SpellData.cs` (`Assets/Scripts/Data/`) | ScriptableObject — holds the 5 chemistry fields per spell |
| `EnemyData.cs` (`Assets/Scripts/Data/`) | ScriptableObject — `innateConditions: List<ChemicalCondition>` |
| `CharacterStats.cs` (`Assets/Scripts/Battle/`) | Runtime condition state + mutation methods |
| `StatusConditionEntry.cs` (`Assets/Scripts/Battle/`) | Struct — `Condition`, `TurnsRemaining`, `TickCount`, `BaseDamage` |
| `SpellEffectResolver.cs` (`Assets/Scripts/Battle/`) | Pure C# — executes resolution order above |
| `SpellResult.cs` (`Assets/Scripts/Battle/`) | Struct returned by `Resolve()` — UI feedback payload |

---

## Invariants (Do Not Break)

- **Spells never directly apply material conditions.** `inflictsCondition` is status-only. Material conditions only change as a consequence of a reaction via `transformsTo`.
- **Duplicate status conditions are prevented at the resolver level**, not in `CharacterStats`. The resolver calls `HasCondition()` before `ApplyStatusCondition()`.
- **`InnateConditions` is never mutated during combat.** It is the restoration source for expired transformations.
- **Mutate `ActiveMaterialConditions` only via `ApplyMaterialTransformation()` and `ConsumeCondition()`** — never directly. Direct mutation breaks the `_materialTransformations` tracking invariant.
- **DoT base damage values live in `SpellEffectResolver`** as constants (`BurningDoTDamage = 5`, `EvaporatingDoTDamage = 3`, `CorrodedBaseDoTDamage = 4`). They are passed into `ApplyStatusCondition()` at inflict time.
- **Conditions are battle-scoped only.** All active conditions clear when a battle ends. No carry-over into platformer/exploration.

---

## Example: Adding a New Spell

A spell that Freezes a Liquid enemy and applies the Frozen status:

```
spellName:              "Freeze"
effectType:             Damage
power:                  8
mpCost:                 5
inflictsCondition:      Frozen
reactsWith:             Liquid
reactionBonusDamage:    4
transformsTo:           Solid
transformationDuration: 2
```

**What happens when cast on a Meltspawn (`innateConditions: [Liquid]`):**
1. Reaction fires (Liquid present) → 4 bonus damage added, `Liquid` consumed
2. `ApplyMaterialTransformation(Solid, Liquid, 2)` — `Solid` added to `ActiveMaterialConditions`
3. Primary effect: 8 + 4 = 12 damage dealt
4. `Frozen` applied (not already present)
5. Next turn start: `Frozen` expires (1 turn), `Solid` decrements to 1
6. Turn after: `Solid` expires, `Liquid` restored from `InnateConditions`

**A spell with no chemistry involvement** (e.g. a plain heal):

```
spellName:              "Mend"
effectType:             Heal
power:                  15
mpCost:                 4
inflictsCondition:      None
reactsWith:             None
reactionBonusDamage:    0
transformsTo:           None
transformationDuration: 0
```
