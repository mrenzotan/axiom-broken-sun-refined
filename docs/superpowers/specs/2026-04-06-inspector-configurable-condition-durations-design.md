# Inspector-Configurable Condition Durations

**Date:** 2026-04-06
**Status:** Approved

## Problem

Status condition durations (Frozen, Evaporating, Burning, Corroded, Crystallized) are hardcoded in `CharacterStats.DefaultDurationFor()`. Unlike material transformation durations (`transformationDuration` on `SpellData`), there is no way to tune them per spell in the Unity Inspector.

## Goal

Add an `inflictsConditionDuration` field to `SpellData` so designers can configure how long an inflicted status condition lasts. Existing spell assets default to `0`, which falls back to the hardcoded defaults — no migration required.

## Design

### Approach

Option A: fallback in `CharacterStats`. The spell supplies a duration; `ApplyStatusCondition` uses it if non-zero, otherwise falls back to `DefaultDurationFor`. The fallback logic stays co-located with the duration table.

### Files Changed

#### `Assets/Scripts/Data/SpellData.cs`

Add one field below `inflictsCondition`:

```csharp
[Tooltip("How many turns the inflicted condition lasts. 0 = use the default duration for that condition " +
         "(Frozen: 1, Evaporating: 2, Burning: 2, Corroded: 3, Crystallized: 2).")]
public int inflictsConditionDuration;
```

#### `Assets/Scripts/Battle/CharacterStats.cs`

Add an optional `duration` parameter to `ApplyStatusCondition`:

```csharp
public void ApplyStatusCondition(ChemicalCondition condition, int baseDamage = 0, int duration = 0)
{
    int effectiveDuration = duration > 0 ? duration : DefaultDurationFor(condition);
    ActiveStatusConditions.Add(new StatusConditionEntry
    {
        Condition      = condition,
        TurnsRemaining = effectiveDuration,
        TickCount      = 0,
        BaseDamage     = baseDamage
    });
}
```

`DefaultDurationFor` is unchanged — it remains the fallback for when `duration` is 0.

#### `Assets/Scripts/Battle/SpellEffectResolver.cs`

Pass `spell.inflictsConditionDuration` at the existing call site:

```csharp
effectTarget.ApplyStatusCondition(spell.inflictsCondition, baseDamage, spell.inflictsConditionDuration);
```

No other changes to the resolver.

## Constraints

- No new classes, no new files.
- Existing `.asset` files do not need updating — `inflictsConditionDuration` defaults to `0`, which triggers the fallback.
- `DefaultDurationFor` is kept as the authoritative fallback table; it is not removed.
