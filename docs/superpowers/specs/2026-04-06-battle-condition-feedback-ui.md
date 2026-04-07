# Spec: Battle Condition Feedback UI

**Date:** 2026-04-06
**Status:** Approved

## Summary

Two UI improvements to the Battle scene that give the player clearer feedback about the chemistry condition system:

1. **Physical immunity message** — when a physical attack is blocked by Liquid or Vapor, a specific message appears in the log explaining why instead of a silent "0 damage" line.
2. **Condition turn badges** — a row of colored badges below each character's HP bar shows active time-limited conditions and how many turns remain.

---

## Feature 1: Physical Immunity Message Log

### Problem

`PlayerActionHandler.ExecuteAttack()` already returns `IsImmune = true` with `Damage = 0` when the enemy is Liquid or Vapor. `EnemyActionHandler.ExecuteAttack()` does the same when the player is immune. However, the `HandleDamageDealt` handler in `BattleHUD` produces a generic message: `"[Attacker] attacks! [Defender] takes 0 damage."` — the player has no idea why.

### Design

Add a new event to `BattleController`:

```csharp
public event Action<CharacterStats, CharacterStats> OnPhysicalAttackImmune;
// Parameters: attacker, target
```

Fire it from `FirePlayerDamageVisuals()` when `_pendingPlayerAttack.IsImmune` and from `FireEnemyDamageVisuals()` when `_pendingEnemyAttack.IsImmune`. Do **not** fire `OnDamageDealt` in the immune case — no HP changes, so no bar update is needed, and the floating number spawner must not show a "0".

`BattleHUD` subscribes to `OnPhysicalAttackImmune` and builds the message by querying the target's `ActiveMaterialConditions`:

```
"Meltspawn is Liquid — physical attacks pass right through!"
"Meltspawn is Vapor — physical attacks pass right through!"
```

If the target has both Liquid and Vapor (edge case), the first match wins. The immunity message replaces — not supplements — the normal attack message.

### Scope

- Covers both directions: player→enemy and enemy→player.
- Covers both Liquid and Vapor (the only two conditions that set `IsPhysicallyImmune`).
- Does not affect spell attacks (spells go through `SpellEffectResolver`, not `ExecuteAttack()`).

---

## Feature 2: Condition Turn Badges

### Problem

Active time-limited conditions (status conditions like Frozen, Burning, etc. and temporary material transformations like Solid-from-freeze) have no visual representation on the HUD. Players cannot tell how many turns a condition lasts.

### Design

#### CharacterStats — expose transformation durations

Add one public method:

```csharp
public int GetMaterialTransformTurns(ChemicalCondition condition)
```

Returns the `TurnsRemaining` for a condition that is active as a temporary material transformation, or `0` if it is not active as a transformation (i.e. it is innate/permanent). This keeps `_materialTransformations` private while giving the UI what it needs.

#### ConditionBadgeUI — new MonoBehaviour

A new `ConditionBadgeUI : MonoBehaviour` placed inside each character's HUD panel, below the HP/MP bars.

```csharp
public void Refresh(CharacterStats stats)
```

On `Refresh`:
1. Iterate `stats.ActiveStatusConditions` — each entry has `Condition` and `TurnsRemaining`. Show a badge for each.
2. Iterate `stats.ActiveMaterialConditions` — call `stats.GetMaterialTransformTurns(condition)` for each. If `> 0`, show a badge with that turn count (it is a temporary transformation, not permanent).
3. Permanent innate material conditions (turns == 0) are **not** shown — the badge row is for actionable turn information only.

**Badge appearance:** Colored pill label — condition name + `(N)` turn count. Color-coded by condition family (blue = freeze/Solid, red = fire/Burning, purple = Crystallized, etc.). Implemented as a dynamically-populated row using Unity UI with `HorizontalLayoutGroup`. Badges are pooled or recreated each refresh (refresh is at most once per turn — no performance concern).

#### BattleHUD — new event + wiring

Subscribe to `OnConditionsChanged(CharacterStats target)` (new event, see below). On receipt, call `Refresh()` on the matching `ConditionBadgeUI`.

Also subscribe to `OnPhysicalAttackImmune` (Feature 1 event) for the immunity message.

#### BattleController — new event + firing points

```csharp
public event Action<CharacterStats> OnConditionsChanged;
```

Fired at:
- End of `ProcessPlayerTurnStart()` — after `_playerStats.ProcessConditionTurn()` ticks all conditions on both characters. Fire for both `_playerStats` and `_enemyStats` (material transformations on either character could tick).
- End of `ProcessEnemyTurnStart()` — same reasoning.
- End of `OnSpellCast()` after `_resolver.Resolve()` — spell may have applied a new condition to the enemy (or caster for Heal/Shield spells that inflict).

### What is NOT shown

- Innate permanent material conditions (Liquid, Vapor, etc. with no expiry) — no badge, no turn count.
- Shield HP — already shown via floating numbers.
- The badge row is hidden / shows nothing when no time-limited conditions are active.

---

## New BattleController Events Summary

| Event | Signature | Fired when |
|-------|-----------|------------|
| `OnPhysicalAttackImmune` | `Action<CharacterStats, CharacterStats>` (attacker, target) | A physical attack is blocked by Liquid/Vapor immunity |
| `OnConditionsChanged` | `Action<CharacterStats>` | A character's active condition list may have changed |

---

## Files Affected

| File | Change |
|------|--------|
| `BattleController.cs` | Add 2 events; fire `OnPhysicalAttackImmune` in `FirePlayerDamageVisuals` / `FireEnemyDamageVisuals`; fire `OnConditionsChanged` in `ProcessPlayerTurnStart`, `ProcessEnemyTurnStart`, `OnSpellCast` |
| `CharacterStats.cs` | Add `GetMaterialTransformTurns(ChemicalCondition)` |
| `BattleHUD.cs` | Subscribe to 2 new events; add `[SerializeField] ConditionBadgeUI` refs; wire Refresh calls |
| `ConditionBadgeUI.cs` | New file — `Assets/Scripts/Battle/UI/` |

No changes to `PlayerActionHandler`, `EnemyActionHandler`, `SpellEffectResolver`, `AttackResult`, or `CharacterStats` (beyond the one new method).

---

## Out of Scope

- Animation or particle effects when a condition is first applied (separate polish ticket)
- Condition icons / artwork (text-only badges for Phase 2)
- Showing innate material conditions anywhere in the HUD (separate UX ticket)
