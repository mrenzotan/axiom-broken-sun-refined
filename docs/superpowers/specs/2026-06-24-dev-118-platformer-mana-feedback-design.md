# Platformer Mana Feedback — Spend & Insufficient-MP Floating Numbers — Design Spec

**Date:** 2026-06-24
**Status:** Approved
**Jira:** DEV-118 — *Visual Feedback on Environmental Puzzle Spell Casts in Platformer Scenes*
**Scope:** The remaining DEV-118 acceptance criteria not marked DONE — the mana-feedback cluster. The two prior ACs (proximity aura cue, player cast animation) shipped under [`2026-06-23-dev-118-platformer-spell-feedback-design.md`](2026-06-23-dev-118-platformer-spell-feedback-design.md).

---

## Summary

Today a platformer spell cast gives **no mana feedback**:

- **Insufficient MP is a silent fail.** [`PlatformerVoiceSpellController.Update()`](../../../Assets/Scripts/Voice/PlatformerVoiceSpellController.cs) gates `castable = HasResolvableTarget && CurrentMp >= mpCost`; when MP is short, `RequestCast` is simply never called — nothing tells the player why.
- **Successful consumption is invisible** beyond the HUD mana bar lerping down ([`PlatformerHpHudUI`](../../../Assets/Scripts/Platformer/UI/PlatformerHpHudUI.cs) → `HealthBarUI.SetMP`).

This feature adds **world-space floating-number feedback** at the player for both cases, reusing the platformer's existing pooled spawner:

- On a **successful cast**, a `-N MP` number rises from the player at the cast's fire-frame (synced with the cast animation).
- On a recognized spell the player **can't afford** *while near a puzzle that spell would resolve*, a `Not enough MP` message rises from the player.

No HUD changes, no new animation, no new colliders. The feedback is a thin layer over the already-shipped deferred-cast flow.

### Design decisions (locked with the user)

1. **Insufficient-MP feedback fires only near a resolvable puzzle** — i.e. the spoken spell *would* resolve an in-range obstacle but the player lacks MP. Muttering a known spell with nothing nearby stays silent. (Mirrors the existing puzzle-only cast scope.)
2. **Floating numbers only** — no mana-bar flash/shake in the HUD. The world-space float alone satisfies "more apparent than only the HUD bar."
3. **No fizzle animation** — an insufficient-MP attempt shows only the float; the cast clip still plays solely on a real, MP-paid cast.

---

## Approach — extend the platformer spawner, not the Battle one

AC5 asks to "reuse or adapt the floating number feedback from `Battle.unity` if appropriate." The investigation conclusion **is the design choice**:

- Battle's [`FloatingNumberSpawner`](../../../Assets/Scripts/Battle/UI/FloatingNumberSpawner.cs) is **Canvas / `RectTransform` (UI-space)** — its `Spawn(RectTransform origin, int amount, NumberType type)` anchors to a UI slot. Wrong coordinate space for feedback that should appear at the player in the world.
- The platformer already has [`PlatformerFloatingNumberSpawner`](../../../Assets/Scripts/Platformer/PlatformerFloatingNumberSpawner.cs) — a **world-space, pooled** adaptation of that same pattern (`SpawnHealNumbers` → green `+N HP` / cyan `+N MP`). It is already in the scene and already used by [`SavePointTrigger`](../../../Assets/Scripts/Platformer/SavePointTrigger.cs).

So the appropriate reuse already exists. We add two methods to the platformer spawner. (This documented conclusion is the deliverable for the "Investigate reusing floating number spawner from `Battle.unity`" subtask.)

---

## Runtime Flow

```
Player speaks a spell → PlatformerVoiceSpellController.Update()
  → SpellResultMatcher.Match(...) → matched SpellData (else skip)
  → resolve PlayerState (injected or GameManager.Instance.PlayerState)
  → PlatformerSpellWorldCaster.EvaluateCast(matched, ps.CurrentMp, ...5 lists...)
       ├─ NoTarget          → do nothing            (no nearby puzzle accepts it)
       ├─ InsufficientMana  → _floatingNumbers?.SpawnInsufficientMana(player.pos)   ← AC2
       └─ Castable          → _castSequencer.RequestCast(matched)                   (unchanged)

Cast fire-frame (deferred, existing flow) → PlatformerVoiceSpellController.ResolveAction(spell)
  → before = PlayerState.CurrentMp
  → PlatformerSpellWorldCaster.TryCast(spell, ...lists..., playerState)   (spends MP, resolves obstacle)
  → spent = before - PlayerState.CurrentMp
  → if spent > 0: _floatingNumbers.SpawnManaSpent(player.pos, spent)      ← AC3 / AC4
```

The spend float is emitted at the **fire-frame**, so it appears in sync with the cast animation and only when MP was actually consumed — measured as a real before/after delta rather than assuming `spell.mpCost`, so an interrupted or no-op resolve never shows a phantom number.

---

## Component Design

### `PlatformerFloatingNumberSpawner` (Assets/Scripts/Platformer/) — two new methods

Reuse the existing private `Spawn(Vector2 position, string text, Color color)` and the object pool. Both new methods apply the existing `MpVerticalOffset` so the number rises from above the player, consistent with the MP heal number.

```csharp
// "-N MP" in cyan — mana-color identity; the leading minus distinguishes spend from the "+N MP" heal.
public void SpawnManaSpent(Vector2 worldPosition, int spentMp);

// "Not enough MP" in a soft red.
public void SpawnInsufficientMana(Vector2 worldPosition);
```

Both guard `_prefab == null` with the same warning the existing method uses. Colors and the offset are tunables (cyan for spend; `new Color(0.9f, 0.3f, 0.3f)` for insufficient — final values dialed in Play Mode).

### `PlatformerSpellWorldCaster` (Assets/Scripts/Voice/) — 3-state decision

Replace the controller's inline 2-state `castable` bool with a pure, unit-testable 3-state evaluation. This is a thin wrapper over the existing `HasResolvableTarget` plus the MP comparison — the MP threshold check simply *moves* from the controller into the helper so the branching is testable in one place.

```csharp
public enum CastEvaluation { NoTarget, InsufficientMana, Castable }

public static CastEvaluation EvaluateCast(
    SpellData spell,
    int currentMp,
    IReadOnlyList<MeltableObstacleController> meltableObstacles,
    IReadOnlyList<FreezablePlatformController> freezablePlatforms,
    IReadOnlyList<BurnableObstacleController> burnableObstacles,
    IReadOnlyList<SteamVentController> steamVents,
    IReadOnlyList<AcidPuddleController> acidPuddles);
//   !HasResolvableTarget(...)      → NoTarget
//   currentMp < spell.mpCost       → InsufficientMana
//   otherwise                      → Castable
```

`HasResolvableTarget` and `TryCast` are unchanged. `EvaluateCast` calls `HasResolvableTarget` internally; no resolution logic is duplicated.

### `PlatformerVoiceSpellController` (Assets/Scripts/Voice/)

- **New serialized field:** `[SerializeField] private PlatformerFloatingNumberSpawner _floatingNumbers;` — assigned in `Platformer.unity` to the same spawner instance `SavePointTrigger` already references. Null-safe everywhere (so EditMode tests that leave it unset are unaffected).
- **`Update()`** switches on `EvaluateCast(...)`:
  - `Castable` → `_castSequencer.RequestCast(matched)` (unchanged behavior).
  - `InsufficientMana` → `if (_floatingNumbers != null && _player != null) _floatingNumbers.SpawnInsufficientMana(_player.transform.position);`
  - `NoTarget` → nothing.
- **`ResolveAction(spell)`** measures the MP delta around the existing `TryCast` call and spawns `SpawnManaSpent(_player.transform.position, spent)` when `spent > 0`. `TryCast`'s call and arguments are otherwise unchanged.

Player world position comes from the existing `_player` (`PlayerController`) reference, matching how `SavePointTrigger` passes `other.transform.position`.

---

## Tests (EditMode, plain C#)

- **`PlatformerSpellWorldCaster.EvaluateCast`** (new — the AC-critical intent):
  - `NoTarget` when no in-range obstacle accepts the spell (regardless of MP).
  - `InsufficientMana` when an in-range obstacle accepts the spell **but** `currentMp < mpCost`.
  - `Castable` when accepted **and** `currentMp >= mpCost`.
  - *Why it matters:* encodes the chosen trigger — insufficient-MP feedback must fire **only** near a resolvable puzzle, never on a stray recognized spell and never when MP is actually sufficient. This is the branch most likely to regress.
- **`PlatformerVoiceSpellController`** (extend the existing suite): in-range accepting obstacle + `CurrentMp < mpCost` → after `Update()` then a fire-frame, the obstacle is **not** resolved and MP is **unchanged** (no cast was requested). Verifies the controller honors `InsufficientMana` by not casting.
- The 5 existing `PlatformerVoiceSpellControllerTests` stay green: `_floatingNumbers` is left null (null-guarded → no-op), and the spend-delta path doesn't alter MP accounting.

The actual float spawns (visual, pooled `Instantiate`) are verified in Play Mode, not asserted in EditMode.

---

## Acceptance Criteria Coverage

| Acceptance Criterion | Met by |
| --- | --- |
| Visible feedback when casting **without enough mana** | `InsufficientMana` → `SpawnInsufficientMana` ("Not enough MP" red) |
| **Mana consumption** visually emphasized on a successful cast | `SpawnManaSpent` ("-N MP" cyan) at the fire-frame |
| Mana-loss feedback **more apparent than only the HUD bar** | World-space float at the player, distinct from the HUD bar lerp |
| Reuse/adapt the **Battle floating-number** feedback if appropriate | Extend world-space `PlatformerFloatingNumberSpawner` (Battle's is UI-space); investigation documented above |
| Feedback **readable** during movement/camera framing | Pooled world-space TMP, rises + fades over ~1s, offset above the player — Play-Mode verified |
| Feedback **doesn't interfere** with control / puzzle readability | No colliders, no input capture, short-lived, above the player not over the puzzle — Play-Mode verified |

AC "readable" and "no interference" are quality constraints satisfied by the float's existing properties and confirmed in Play Mode, not by additional code.

---

## What Is Not Changing

- **No HUD changes** — `PlatformerHpHudUI` / `HealthBarUI` untouched; no mana-bar flash or shake.
- **No new animation** — the cast clip still plays only on a real, MP-paid cast; an insufficient-MP attempt shows only the float.
- **`HasResolvableTarget`, `TryCast`, obstacle resolution, `PlatformerCastSequencer`, `PlayerAuraCue`, the Vosk pipeline** — untouched.
- **The Battle scene** and its `FloatingNumberSpawner`.

---

## Unity Editor / Asset Changes (no code — done by the user)

1. **Scene wire:** on `PlatformerVoiceSpellController` in `Assets/Scenes/Platformer.unity`, assign the new `_floatingNumbers` field to the scene's existing `PlatformerFloatingNumberSpawner` (the same instance `SavePointTrigger` uses).
2. **Play-Mode verification:** near a puzzle that the spell resolves —
   - with enough MP: cast resolves and a cyan `-N MP` rises from the player at the cast fire-frame;
   - with MP drained below the spell's cost: a red `Not enough MP` rises, no cast, no animation;
   - while moving and with the camera following: both remain legible, don't hitch control, and don't obscure the puzzle.

---

## Out of Scope

- Any mana-bar HUD treatment (flash, shake, color), per the "floating numbers only" decision.
- A fizzle / failed-cast animation.
- Battle-scene mana feedback (`FloatingNumberSpawner.NumberType.Mana` exists but is unused there; not part of this ticket).
