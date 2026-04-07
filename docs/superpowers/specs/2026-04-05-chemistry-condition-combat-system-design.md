# Chemistry Condition Combat System — Design Spec

**Date:** 2026-04-05
**Status:** Approved for planning
**Replaces:** `SpellElement` enum concept from DEV-27

---

## Overview

This spec defines the unified condition system that powers elemental interactions in Axiom of the Broken Sun's turn-based combat. It replaces the abandoned `SpellElement` approach (which used the four chemistry concepts as spell "types," a direct analogue to Fire/Water/Earth in traditional JRPGs) with a system grounded in how chemistry actually works: substances react with other substances, producing new states.

The design is inspired by Genshin Impact's elemental reaction model but re-skinned entirely in chemistry vocabulary consistent with the game's lore. There is no separate type chart and no separate combo chart — one condition list and one resolver handles everything.

---

## Core Concept

Every character in combat (player and enemy) carries a list of active **chemical conditions**. Conditions are predefined, finite, and drawn from the four core chemistry concepts in the lore.

Conditions come in two categories:

**Material conditions** describe chemical composition — what something *is made of*. They are:
- Innate to each enemy, defined in `EnemyData` — never assigned by a spell directly
- Permanent unless a reaction *transforms* them (see Reaction Transformations below)
- Never produce ongoing turn effects by themselves — they are reaction targets and combat gatekeepers
- **Affect which attack types are valid** — e.g. `Liquid` and `Vapor` are immune to physical attacks; only `Solid` can be hit by basic attacks. This makes phase-change spells strategically essential, not just crowd control.

**Status conditions** describe an active chemical state — what is *currently happening* to something. They are:
- Applied mid-combat by spells or as reaction byproducts
- Can produce ongoing turn effects (damage over time, crowd control)
- Consumed or expired after a fixed number of turns

A spell interacts with the condition list in up to two ways:
1. **Inflicts** — adds a *status* condition to the target. Spells can never directly assign a material condition.
2. **Reacts with** — if a specific condition (material or status) is already present on the target, a reaction triggers: bonus effect is applied, the matched condition is consumed, and a byproduct condition may be applied.

**Reaction Transformations** — a special case of reactions where consuming a material condition produces a *new material condition* in its place, representing a phase change:
- `Liquid` + Freeze → suppresses `Liquid`, applies `Solid` temporarily
- `Solid` + Melt → suppresses `Solid`, applies `Liquid` temporarily
- `Liquid` + Boil/Evaporate → suppresses `Liquid`, applies `Vapor` temporarily
- `Vapor` + Condense → suppresses `Vapor`, applies `Liquid` temporarily

Transformed material conditions are **temporary**. They last for a spell-defined number of turns (e.g. 2), after which the enemy's original innate material condition is automatically restored. This keeps enemy identity intact — a Meltspawn is fundamentally a liquid creature; freezing it is a temporary tactical opening, not a permanent reclassification.

These are the only cases where a material condition changes — and only as a direct consequence of a chemical reaction, never as a spell's standalone inflict.

This is the complete system. One resolver, one condition list, no separate tables.

---

## Condition Catalogue

Derived from the four core chemistry concepts in `docs/LORE_AND_MECHANICS.md`.

### Material Conditions (innate composition descriptors, no turn effects)

| Condition | Chemistry Concept | Combat effect | Reaction target of |
|---|---|---|---|
| `Liquid` | Phase Change | Immune to physical attacks | Freeze → `Solid`; Evaporate → `Vapor` |
| `Solid` | Phase Change | Vulnerable to physical attacks; immune to Freeze | Melt → `Liquid`; Shatter (physical exploit) |
| `Vapor` | Phase Change | Immune to physical attacks; immune to Freeze | Combust → explosion; Condense → `Liquid` |
| `Flammable` | Combustion | No passive effect | Combust → `Burning` |
| `Pressurized` | Combustion | No passive effect | Combust → explosion bonus damage + `Burning` |
| `AcidicFluid` | Acid–Base | Deals passive acid damage to anything touching it | Base Surge (damage) → bonus damage, consumes condition |
| `AlkalineBase` | Acid–Base | No passive effect | Acid spell → bonus damage, consumes condition |
| `MineralSaturated` | Precipitation | No passive effect | Crystallize → `Crystallized` |

### Status Conditions (active states, can have turn effects)

| Condition | Chemistry Concept | Turn effect | Default duration |
|---|---|---|---|
| `Frozen` | Phase Change | Target skips their action | 1 turn |
| `Evaporating` | Phase Change | Target takes minor damage each turn | 2 turns |
| `Burning` | Combustion | Target takes fire damage each turn | 2 turns |
| `Corroded` | Acid–Base | Target takes escalating acid damage each turn — damage increases by 50% per tick as the acid strips away protective layers and reaches deeper material. Turn 1: base damage. Turn 2: base × 1.5. Turn 3: base × 2.0. Strategically, this pressures the afflicted character to either cleanse it early (via Neutralize) or commit to ending the battle before the damage peaks. | 3 turns |
| `Crystallized` | Precipitation | Target's damage output halved | 2 turns |

### Default / No Condition

`None` — the default value for all `ChemicalCondition` enum fields in `SpellData`. Means "not set." Has no gameplay effect. Exists so fields like `inflictsCondition` and `reactsWith` have a safe default that doesn't accidentally resolve to a real condition.

---

## Data Model

### `ChemicalCondition.cs` (replaces `SpellElement.cs`)

```
Assets/Scripts/Data/ChemicalCondition.cs
```

A single enum covering all conditions from the catalogue above. No subtyping in code — the material vs status distinction is a design-time concept, not enforced in the enum itself (keeping the resolver simple).

### `SpellData.cs` changes

The `element: SpellElement` field is replaced by four fields:

| Field | Type | Description |
|---|---|---|
| `inflictsCondition` | `ChemicalCondition` | *Status condition only.* Applied to the target after the spell resolves. `None` if the spell inflicts no status condition. Spells can never directly assign a material condition via this field. |
| `reactsWith` | `ChemicalCondition` | The condition (material or status) this spell reacts with if already present on the target. `None` if the spell has no reaction. |
| `reactionBonusDamage` | `int` | Flat bonus applied when a reaction triggers. For Damage spells: added to damage dealt. For Heal spells: added to HP restored. For Shield spells: added to shield HP granted. Zero if the reaction has no bonus magnitude. |
| `transformsTo` | `ChemicalCondition` | *Material condition only.* If the reaction fires, this material condition temporarily replaces the consumed one (phase change). `None` if the reaction does not transform the target's material state. |
| `transformationDuration` | `int` | How many turns the transformed material condition lasts before the innate condition is restored. Only meaningful when `transformsTo != None`. |

A heal spell with no elemental relevance leaves all four fields at their defaults.

### `EnemyData.cs` changes

One new field:

| Field | Type | Description |
|---|---|---|
| `innateConditions` | `List<ChemicalCondition>` | 1–2 material conditions the enemy starts every combat with. Populated into `CharacterStats.ActiveConditions` on battle init. |

### `CharacterStats.cs` changes

New fields and supporting methods:

| Addition | Description |
|---|---|
| `InnateConditions: List<ChemicalCondition>` | Read-only. Set once from `EnemyData.innateConditions` during `Initialize()`. Never modified during combat — this is the restore target when a transformation expires. |
| `ActiveMaterialConditions: List<ChemicalCondition>` | Current material conditions. Starts as a copy of `InnateConditions`. Temporarily replaced during phase-change reactions; restored from `InnateConditions` when the transformation expires. |
| `ActiveStatusConditions: List<StatusConditionEntry>` | Current status conditions. Each entry holds the condition, a `turnsRemaining` counter, and a `tickCount` (how many times it has already dealt damage this battle). `tickCount` is used by escalating conditions like `Corroded` to determine the current damage multiplier. |
| `HasCondition(ChemicalCondition)` | Returns true if the condition appears in either active list. |
| `ApplyMaterialTransformation(ChemicalCondition transformsTo, int duration)` | Replaces the current active material condition with `transformsTo` and records a `turnsRemaining` counter. |
| `ApplyStatusCondition(ChemicalCondition)` | Adds a status condition entry. No-op if already present (resolver guards against this). |
| `ConsumeCondition(ChemicalCondition)` | Removes one instance from whichever active list contains it. |
| `Initialize()` update | Sets `InnateConditions` from `EnemyData`, copies to `ActiveMaterialConditions`, clears `ActiveStatusConditions`. |

---

## Resolver Logic

`SpellEffectResolver.Resolve()` executes in this order:

1. **Null guards** — throw if spell, caster, or target is null (existing)
2. **Reaction check** — if `spell.reactsWith != None` AND `target.HasCondition(spell.reactsWith)`:
   - The reaction always checks the *spell's primary target* — the same character the spell's effect is applied to.
     - Damage spells target the enemy → reaction checks enemy conditions.
     - Heal/Shield spells target the caster → reaction checks caster conditions.
   - Set `reactionTriggered = true`
   - Apply `spell.reactionBonusDamage` to the spell's target
   - Call `target.ConsumeCondition(spell.reactsWith)`
   - **Transformation check** — if `spell.transformsTo != None`, call `target.ApplyMaterialCondition(spell.transformsTo)` (applies the new material state resulting from the phase change)
3. **Primary effect** — apply Damage, Heal, or Shield using existing logic (existing)
4. **Inflict check** — if `spell.inflictsCondition != None` AND `!target.HasCondition(spell.inflictsCondition)`:
   - Call `target.ApplyStatusCondition(spell.inflictsCondition)` — status conditions only; material conditions are never set here
5. **Return** `SpellResult` — extended with `ReactionTriggered: bool` and `MaterialTransformed: bool` for UI feedback

**Condition turn processing** runs at the end of each turn (in `BattleManager`, not the resolver):
- **Status conditions:** iterate `ActiveStatusConditions` — apply turn effects (DoT, crowd control), decrement `turnsRemaining`, remove expired entries
- **Material transformations:** iterate `ActiveMaterialConditions` for any temporary entries — decrement their `turnsRemaining`; when a transformation expires, remove it and restore the corresponding condition from `InnateConditions`
- Innate material conditions with no active transformation are skipped — no turn effect

---

## Example Encounters

### Gas Bloater — innate: `[Pressurized, Flammable]`

| Turn | Action | Result |
|---|---|---|
| 1 | Player casts Combust (`reactsWith: Pressurized`, `inflictsCondition: Burning`) | Reaction fires → bonus explosion damage. `Pressurized` consumed. `Burning` applied. |
| 2 | Turn end — status tick | Gas Bloater takes Burning DoT. |
| 2 | Player casts Combust again | `Flammable` still present → second reaction fires. `Flammable` consumed. Another `Burning` applied (resets duration). |

### Acid Slug — innate: `[AcidicFluid]`

Two distinct spells demonstrate the direction rule:

**Scenario A — damage spell targets the slug:**
| Turn | Action | Result |
|---|---|---|
| 1 | Player casts Base Surge (`effectType: Damage`, `reactsWith: AcidicFluid`) | Reaction fires against the *enemy's* `AcidicFluid` → bonus damage from the neutralization heat. `AcidicFluid` consumed. |
| 2 | Slug attacks | Standard damage — no further condition interactions. |

**Scenario B — Kaelen is Corroded, heal spell targets the caster:**
| Turn | Action | Result |
|---|---|---|
| 1 | Acid Slug attacks — applies `Corroded` to Kaelen | Kaelen takes acid DoT each turn. |
| 2 | Player casts Neutralize (`effectType: Heal`, `reactsWith: Corroded`) | Reaction fires against *Kaelen's own* `Corroded` — clears it AND heals for bonus HP. Chemically: the base neutralizes the acid eating through him. |

### Meltspawn — innate: `[Liquid]`

| Turn | Action | Result |
|---|---|---|
| 1 | Player casts Freeze (`reactsWith: Liquid`, `inflictsCondition: Frozen`, `transformsTo: Solid`, `transformationDuration: 2`) | Reaction fires → bonus damage. `Liquid` temporarily replaced by `Solid` (2 turns). `Frozen` status applied — Meltspawn skips next turn. Meltspawn is now vulnerable to physical attacks. |
| End of turn 1 | Turn processing | `Frozen` expires (1-turn status). `Solid` transformation decrements to 1 turn remaining. |
| 2 | Player uses basic Attack | `Solid` still active — physical attack lands. Meltspawn takes full damage. |
| End of turn 2 | Turn processing | `Solid` transformation expires. `Liquid` restored from `InnateConditions`. Meltspawn is physical-immune again. |
| 3 | Player uses basic Attack | `Liquid` restored — physical attack passes through harmlessly. Player must cast Freeze again to reopen the window. |

---

## What This Replaces

| Old (DEV-27 plan) | New (this spec) |
|---|---|
| `Assets/Scripts/Data/SpellElement.cs` — `enum SpellElement { Neutral, PhaseChange, Combustion, AcidBase, Precipitation }` | `Assets/Scripts/Data/ChemicalCondition.cs` — `enum ChemicalCondition { None, Liquid, Vapor, Flammable, ... }` |
| `SpellData.element: SpellElement` | `SpellData.inflictsCondition`, `reactsWith`, `reactionBonusDamage` |
| No condition tracking on `CharacterStats` | `CharacterStats.ActiveConditions: List<ChemicalCondition>` |
| No reaction logic in `SpellEffectResolver` | Reaction check + consume step added to `Resolve()` |
| No condition turn processing | Status condition tick added to `BattleManager` turn loop |

The DEV-27 plan's Task 1 (`SpellElement.cs` creation) is superseded by this spec. All other DEV-27 tasks remain valid.

---

## Scope Notes

- **Status condition durations** are defined as constants for now (no ScriptableObject per condition). Move to data-driven if balancing requires frequent tweaks.
- **Duplicate condition prevention** is enforced in the resolver, not in `CharacterStats`, to keep stats as a dumb data container.
- **Player conditions** — the player can also receive conditions from enemies (e.g. an Acid Slug's Corrode attack applies `Corroded` to Kaelen). The same system handles both directions.
- **Phase 6 expansion hooks** — the `reactionBonusDamage` field is a flat int for now. A multiplier field can be added later without breaking existing assets.
- **Implementation feasibility** — if the condition system proves too complex for Phase 2, `innateConditions` and the reaction step can be stubbed out and activated in Phase 5 without changing the data model.
- **Conditions are battle-scoped only** — all active conditions (both material transformations and status conditions) are cleared when a battle ends. They do not carry over into the platformer/exploration scene. This keeps the systems cleanly separated and avoids the need for field items or out-of-combat condition management in the current scope. Revisit in Phase 6 (World & Content) if the design calls for it.
