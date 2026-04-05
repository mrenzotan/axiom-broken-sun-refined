# DEV-16 Battle UI — Design Spec

**Ticket:** DEV-16
**Phase:** 2 — Combat System
**Date:** 2026-03-30
**Status:** Approved

---

## Overview

A fully functional Battle UI for the turn-based combat scene. Implemented with Unity UI Canvas + TextMeshPro (no UI Toolkit / USS). Five single-responsibility MonoBehaviours coordinated by a thin `BattleHUD` façade.

---`

## Visual Design Decisions

| Element               | Decision                                                                                                       |
| --------------------- | -------------------------------------------------------------------------------------------------------------- |
| Layout                | Mirrored face-off — player left, enemy right, facing each other                                                |
| Action menu           | 2×2 grid (Attack, Spell, Item, Flee)                                                                           |
| Health / MP bars      | Smooth gradient fill — green HP, blue MP, red enemy HP                                                         |
| Turn indicator        | Hovering ▼ arrow above active sprite; inactive sprite dims. Party-ready by design, single character in Phase 2 |
| Damage / heal numbers | Float up from sprite — red damage, green heal, gold + larger for crit                                          |
| Status messages       | Message log box between battle area and action menu, 1–2 lines, replaces on new event                          |

---

## Canvas Hierarchy

Single Screen Space Overlay Canvas in the Battle scene:

```
Canvas (BattleHUD)
├── TurnIndicator              ← ▼ arrow TMP, repositioned above active sprite
├── EnemyPanel
│   ├── EnemyNameText          ← TMP
│   ├── EnemyHPBar             ← Image (fill amount)
│   └── EnemyHPText            ← "55 / 100" TMP
├── PartyPanel                 ← Horizontal Layout Group (1 slot now, expandable)
│   └── PartyMemberSlot (prefab)
│       ├── MemberNameText     ← TMP
│       ├── HPBar              ← Image (fill amount)
│       ├── HPText             ← TMP
│       ├── MPBar              ← Image (fill amount)
│       └── MPText             ← TMP
├── ActionMenu                 ← 2×2 grid of UI Buttons
│   ├── AttackButton
│   ├── SpellButton
│   ├── ItemButton
│   └── FleeButton
├── MessageLog                 ← 2-line TMP text area
└── FloatingNumberPool         ← Object pool parent for floating TMP prefabs
```

`PartyPanel` uses a Horizontal Layout Group. Adding a future party member = instantiate another `PartyMemberSlot` prefab and call `BattleHUD.RegisterPartyMember()`. No structural rewrite required.

---

## UI Components

All scripts live under `Assets/Scripts/Battle/UI/` (part of the `Axiom.Battle` assembly — no separate `Axiom.UI` asmdef). Platformer UI, when needed, goes in `Assets/Scripts/Platformer/UI/`.

### `HealthBarUI.cs`

MonoBehaviour on each `PartyMemberSlot` and `EnemyPanel`. Drives bar fill and numeric text.

```csharp
public void SetHP(int current, int max)
public void SetMP(int current, int max) // enemy panel omits MP
```

Animates bar fill with `Mathf.Lerp` in `Update` toward a target value for smooth visual feedback.

---

### `ActionMenuUI.cs`

MonoBehaviour on `ActionMenu`. Exposes callbacks wired by `BattleHUD`.

```csharp
public System.Action OnAttack, OnSpell, OnItem, OnFlee;
public void SetInteractable(bool interactable)
```

Disables all buttons on the enemy's turn. `BattleHUD` wires the callbacks to `BattleManager`.

---

### `TurnIndicatorUI.cs`

MonoBehaviour that repositions and bobs the ▼ arrow above a given `RectTransform`.

```csharp
public void SetActiveTarget(RectTransform target)
```

Uses a simple coroutine for the bob animation. Party-ready: accepts any slot's `RectTransform` as the target.

---

### `FloatingNumberSpawner.cs`

MonoBehaviour on `FloatingNumberPool`. Maintains a small object pool of TMP prefab instances.

```csharp
public enum NumberType { Damage, Heal, Crit }
public void Spawn(RectTransform origin, int amount, NumberType type)
// Spawns the number at the origin slot's canvas position
// Damage → red, floats up
// Heal   → green, floats up
// Crit   → gold, larger, floats up faster
```

Animates float-up + fade-out via coroutine, then returns to pool.

---

### `StatusMessageUI.cs`

MonoBehaviour on `MessageLog`. Queues and displays battle narration lines.

```csharp
public void Post(string message)
```

Displays 1–2 lines at a time. New messages replace the oldest line. Used for: "Kael used Attack!", "Critical hit!", "Void Wraith is burning!", "Kael was defeated!".

---

### `BattleHUD.cs`

Thin coordinator MonoBehaviour. Holds serialized references to all five components above and exposes the API that `BattleManager` calls directly.

```csharp
public void OnTurnChanged(CharacterStats activeCharacter)
public void OnDamageDealt(CharacterStats target, int amount, bool isCrit)
public void OnHealReceived(CharacterStats target, int amount)
public void OnCharacterDefeated(CharacterStats character)
public void SetActionMenuInteractable(bool interactable)

// Party registration
public void RegisterPartyMember(CharacterStats stats, HealthBarUI slot)
```

Maps `CharacterStats` instances to their `HealthBarUI` slots via `Dictionary<CharacterStats, HealthBarUI>`. Phase 2 registers one entry.

---

## Data Flow

`BattleManager` holds a serialized reference to `BattleHUD` (assigned in Inspector). It calls `BattleHUD` methods at state transition points — no events, no ScriptableObject channels in Phase 2.

```
BattleManager (plain C#)
    │  Direct method calls
    ▼
BattleHUD : MonoBehaviour
    ├── OnTurnChanged()       → TurnIndicatorUI.SetActiveTarget()
    │                         → ActionMenuUI.SetInteractable()
    ├── OnDamageDealt()       → HealthBarUI.SetHP()
    │                         → FloatingNumberSpawner.Spawn(slot, amount, Damage or Crit)
    │                         → StatusMessageUI.Post()
    ├── OnHealReceived()      → HealthBarUI.SetHP()
    │                         → FloatingNumberSpawner.Spawn(slot, amount, Heal)
    │                         → StatusMessageUI.Post()
    └── OnCharacterDefeated() → HealthBarUI.SetHP(0, max)
                              → StatusMessageUI.Post()
```

When Phase 4–5 introduces full decoupling, only `BattleHUD` needs to change (method calls → event subscriptions). The five UI components remain untouched.

---

## Party-Readiness (Phase 2 → Future)

The design is scoped to a single character in Phase 2 but structured to expand without rewrite:

- `PartyPanel` Horizontal Layout Group: add a slot prefab to add a party member
- `TurnIndicatorUI.SetActiveTarget(RectTransform)`: works for 1 or N characters
- `BattleHUD` `Dictionary<CharacterStats, HealthBarUI>`: registers N members

**Explicitly deferred:**

- Turn order queue / timeline visualization (Honkai-style) — Phase 6
- Party member switching — Phase 6
- Status effect icons per slot — Phase 6

---

## Out of Scope for DEV-16

- Spell sub-menu UI (Phase 3 — Voice Spell System)
- Item sub-menu UI (Phase 5 — Data Layer)
- Status effect icon badges on character slots (Phase 6)
- Battle animations (separate DEV ticket in Phase 2)
- Any USS / UI Toolkit — project uses Canvas + TextMeshPro exclusively
