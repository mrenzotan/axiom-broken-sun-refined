# Platformer Spell Feedback — Proximity Cue & Cast Animation — Design Spec

**Date:** 2026-06-23
**Status:** Approved
**Jira:** DEV-118 — *Visual Feedback on Environmental Puzzle Spell Casts in Platformer Scenes*
**Scope:** Two of DEV-118's eight acceptance criteria, prioritized first:

1. Environmental puzzles provide a clear visual cue when the player is nearby and able to interact with them using spells.
2. Player spell casts have a visible animation in platformer scenes.

The remaining six ACs (mana-fail feedback, mana-consume emphasis, floating-number reuse, readability/HUD) are **deferred to a follow-up spec**.

---

## Summary

Today a platformer spell cast is **synchronous and silent**: [`PlatformerVoiceSpellController.Update()`](../../../Assets/Scripts/Voice/PlatformerVoiceSpellController.cs) matches a spoken spell and immediately calls [`PlatformerSpellWorldCaster.TryCast()`](../../../Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs), which validates, spends MP, and resolves the obstacle in the same frame. There is no proximity cue and no cast animation.

This feature adds two pieces of feedback and **inverts the resolution timing** so the obstacle progresses *after* the cast animation, not before:

- **Aura proximity cue** — a 12-frame looping "aura" (`Assets/Art/Sprites/Player/Aura-Sheet.png`) renders behind Kaelen whenever the player is inside *any* environmental puzzle's proximity trigger. It signals "a nearby obstacle is spell-interactable."
- **Cast animation** — when the player speaks a spell that a nearby puzzle accepts (and has the MP for it), the aura hides, the existing `playerCastRight`/`playerCastLeft` clips play, and the obstacle resolves on the clip's fire-frame.

The cast sequencing mirrors the **already-shipped Battle pattern** ([`2026-04-09-spell-cast-animations-design.md`](2026-04-09-spell-cast-animations-design.md)): trigger the cast clip, defer resolution to a Unity Animation Event (`AnimEvent_OnSpellFire`), with a timeout coroutine as the safety net.

Per the prioritized scope, the cast animation plays **only on a valid puzzle-resolving cast**. Casting a spell with no matching in-range puzzle (or with insufficient MP) does nothing — no animation, aura unaffected.

---

## Runtime Flow

```
Player enters a puzzle's proximity trigger
  → *ProximityForwarder.OnTriggerEnter2D (Player)
  → existing: controller.SetPlayerInRange(true)   ← unchanged (governs castability)
  → NEW: PlayerAuraCue.EnterPuzzleRange(forwarder)
  → aura SpriteRenderer enabled, frames cycle behind Kaelen

Player speaks a spell → PlatformerVoiceSpellController.Update()
  → SpellResultMatcher.Match(...) → matched SpellData
  → PlatformerSpellWorldCaster.HasResolvableTarget(spell, ...lists...)   ← NEW (pure query)
       AND playerState.CurrentMp >= spell.mpCost
  → if FALSE: do nothing (no animation, aura stays)
  → if TRUE:  PlatformerCastSequencer.RequestCast(spell)

RequestCast(spell)
  → PlayerAuraCue suppressed (aura hides)
  → PlayerController.BeginCast() → PlayerAnimator.TriggerCast()
       → Cast trigger; IsFacingRight routes to playerCastRight / playerCastLeft
  → movement locked for the cast (mirrors BeginAttack)
  → start fire-frame timeout coroutine (safety net)

Cast clip fire-frame (Unity Animation Event)
  → PlayerExplorationAnimator.AnimEvent_OnSpellFire()   ← NEW
  → PlayerController.OnSpellCastFireFrame()             ← NEW
  → PlatformerCastSequencer.NotifyFireFrame() (once-only guard)
  → PlatformerSpellWorldCaster.Resolve(spell, ...lists..., playerState)  ← NEW
       → playerState.TrySpendMp(spell.mpCost)
       → Try{Melt,Freeze,Ignite,Neutralize}(...) on in-range matching obstacles
  → movement unlocked; aura un-suppressed → re-evaluates proximity
```

If the animation event never fires (missing event, interrupted clip), the timeout coroutine calls `NotifyFireFrame()` instead — identical to the Battle `_spellFireTimeout` safety net.

---

## Component Design

### Resolution split — `PlatformerSpellWorldCaster` (Assets/Scripts/Voice/)

Split the single synchronous `TryCast` into a non-mutating query and a mutating resolve, so the caller can gate the animation on the query and defer the mutation to the fire-frame.

```csharp
// NEW — pure query, no mutation (existing lines 22–88, minus the MP spend)
public static bool HasResolvableTarget(
    SpellData spell,
    IReadOnlyList<MeltableObstacleController> meltableObstacles,
    IReadOnlyList<FreezablePlatformController> freezablePlatforms,
    IReadOnlyList<BurnableObstacleController> burnableObstacles,
    IReadOnlyList<SteamVentController> steamVents,
    IReadOnlyList<AcidPuddleController> acidPuddles);

// NEW — spend MP + resolve (existing lines 89–142)
public static bool Resolve(
    SpellData spell, /* ...same 5 lists... */, PlayerState playerState);
```

`TryCast` may remain as `HasResolvableTarget(...) && Resolve(...)` for any synchronous caller (e.g. debug casters), so existing call sites keep working.

> **Why MP is checked in the query but spent in `Resolve`:** the player must have the MP to *start* a cast (so we don't animate a cast that can't land), but the actual deduction happens at the fire-frame, atomically with resolution. An aborted or interrupted cast costs nothing. Insufficient-MP feedback is a deferred AC; for now insufficient MP simply means "no cast."

### Cast sequencing — `PlatformerCastSequencer` (NEW, plain C#)

Owns the pending-cast lifecycle so the logic is EditMode-testable (project rule: logic in plain C#, MonoBehaviours for lifecycle only). Constructed with:

- `Action triggerCastAnimation` — calls `PlayerAnimator.TriggerCast()` via the player.
- `Action onCastBegan` / `Action onCastEnded` — aura suppress / un-suppress + movement lock/unlock hooks.
- `Func<bool> resolve` — the fire-frame resolution (`PlatformerSpellWorldCaster.Resolve(...)`).

Surface:

- `RequestCast(SpellData spell)` — no-op if a cast is already pending; otherwise stores the pending spell, fires `onCastBegan`, triggers the animation.
- `NotifyFireFrame()` — once-only per cast: runs `resolve`, fires `onCastEnded`, clears pending. Guarded so the animation event **and** the timeout can't double-resolve.

The MonoBehaviour seam ([`PlatformerVoiceSpellController`](../../../Assets/Scripts/Voice/PlatformerVoiceSpellController.cs)) provides the callbacks, subscribes the player's fire event to `NotifyFireFrame`, and runs the timeout coroutine.

### Voice controller — `PlatformerVoiceSpellController` (Assets/Scripts/Voice/)

`Update()` changes from "match → `TryCast`" to "match → query → request deferred cast":

```csharp
SpellData matched = SpellResultMatcher.Match(voskJson, _unlockedSpells);
if (matched == null) continue;

PlayerState ps = _playerState ?? GameManager.Instance?.PlayerState;
if (ps == null) continue;

bool castable =
    PlatformerSpellWorldCaster.HasResolvableTarget(matched, /* 5 lists */)
    && ps.CurrentMp >= matched.mpCost;

if (castable)
    _castSequencer.RequestCast(matched);   // resolution deferred to fire-frame
```

New serialized references (assigned in the scene / via `PlatformerVoiceBootstrap`): the `PlayerController` (or a thin cast presenter) and the timeout duration. The obstacle-list resolvers (`ResolveMeltableObstacles()` etc.) are reused unchanged for both the query and the deferred resolve.

### Player-side animation — `PlayerAnimator`, `PlayerExplorationAnimator`, `PlayerController` (Assets/Scripts/Platformer/)

Mirror the existing attack flow (`BeginAttack` → `TriggerAttack` → `AnimEvent_OnAttackEnd` → `OnAttackAnimationEnd`):

- **`PlayerAnimator`** — add a `Cast` hash and `TriggerCast()`, twin of `TriggerAttack()`. The existing `IsFacingRight` bool routes the Animator to `playerCastRight` vs `playerCastLeft`.
- **`PlayerExplorationAnimator`** — add `AnimEvent_OnSpellFire()` that routes to `PlayerController.OnSpellCastFireFrame()`. (The shared cast clips already carry an `AnimEvent_OnSpellFire` event; Battle's `PlayerBattleAnimator` defines the same-named method, so the clip works in both scenes against whichever receiver sits on that scene's Animator GameObject.)
- **`PlayerController`** — add `BeginCast()` (lock movement, trigger the cast animation) and `OnSpellCastFireFrame()` (forward to the sequencer; unlock movement). Movement is locked from cast start **until the fire-frame** so the player can't walk out of range during the ~0.5s before resolution. The fire-frame is mid-clip; the Animator's `exit time = 1` transition lets the remaining cast frames finish before returning to locomotion even though input is already unlocked.

### Aura proximity cue — `PlayerAuraCue` (NEW MonoBehaviour) + 5 `*ProximityForwarder` edits

The aura is a child SpriteRenderer under the player, sorted *behind* the main sprite (lower sorting order, same sorting layer). `PlayerAuraCue` owns it:

- Holds the 12 sliced aura `Sprite[]` + an fps; while visible, a coroutine cycles frames (mirrors the obstacles' own frame-cycling idiom, e.g. `MeltableObstacleController.PlayMeltFrames`). While hidden, the SpriteRenderer is disabled.
- Tracks in-range puzzles in a `HashSet<Component>` keyed by the reporting forwarder. **Visible** = set contains any non-null entry **AND** not currently suppressed (mid-cast). Destroyed/solved entries are pruned on evaluation, so a missed `OnTriggerExit2D` can't strand the aura on.
- `EnterPuzzleRange(forwarder)` / `ExitPuzzleRange(forwarder)` add/remove; `Suppress(bool)` is toggled by the cast sequencer.

Each of the five forwarders ([`MeltableObstacleProximityForwarder`](../../../Assets/Scripts/Platformer/MeltableObstacleProximityForwarder.cs), Burnable, Freezable, SteamVent, Acid) gains a small notify in its existing trigger callbacks — the player's collider is already in hand, so the cue is discovered from the contact (`other.GetComponentInParent<PlayerAuraCue>()`), requiring **no serialized reference on obstacle prefabs**:

```csharp
private void OnTriggerEnter2D(Collider2D other)
{
    if (!other.CompareTag("Player")) return;
    if (_controller != null) _controller.SetPlayerInRange(true);          // existing
    other.GetComponentInParent<PlayerAuraCue>()?.EnterPuzzleRange(this);  // NEW
}
// OnTriggerExit2D: SetPlayerInRange(false) + ExitPuzzleRange(this)
```

**Why reuse the obstacles' zones** (rather than a single player-side sensor radius): cast validity uses each obstacle's *own* proximity zone — acid puddles deliberately use a larger one than their damage collider — so a separate player radius would let the aura and actual castability disagree. Reusing the existing zones keeps the cue and the castability check in lockstep.

The aura is **purely proximity-driven**: it shows for any puzzle the player is near, regardless of whether the player has that spell unlocked or enough MP. It signals "interactable," not "currently solvable."

---

## Unity Editor / Asset Changes (no code — done by the user)

### Aura sprite & object
1. Slice `Assets/Art/Sprites/Player/Aura-Sheet.png` into 12 sprites (Sprite Mode: Multiple).
2. Add an **Aura child GameObject** under the player with a `SpriteRenderer` on the same sorting layer as Kaelen but a **lower sorting order** (renders behind). Add `PlayerAuraCue`; assign the 12 frames + fps.

### Player Animator Controller (`Assets/Animations/Player/Player.controller`)
3. Add a **`Cast` trigger** parameter.
4. Add transitions into the existing (currently orphaned) cast states:
   - `Any State → playerCastRight`: conditions `Cast` (trigger) + `IsFacingRight == true`.
   - `Any State → playerCastLeft`: conditions `Cast` (trigger) + `IsFacingRight == false`.
   - `playerCastRight → ` and `playerCastLeft → ` the base Idle/locomotion state: exit time = 1, transition duration = 0.
   - On the Any-State transitions, **uncheck "Can Transition To Self"** to prevent re-trigger restarts.

### Cast clip (`Assets/Animations/Player/playerCastLeft.anim`)
5. Add an **Animation Event** at the fire-frame of `playerCastLeft.anim`, function `AnimEvent_OnSpellFire`. (`playerCastRight.anim` already has one at ~0.5s; the platformer now uses facing, so the left clip — previously out of scope for Battle — needs the matching event.)

### Scene wiring (`Assets/Scenes/Platformer.unity` / `PlatformerVoiceBootstrap`)
6. Assign the new `PlatformerVoiceSpellController` references (player / cast presenter, timeout). Confirm `PlayerExplorationAnimator` sits on the player's Animator GameObject (it already does for the attack flow).

---

## Tests (EditMode, plain C#)

- **`PlatformerSpellWorldCaster`** — `HasResolvableTarget` returns true only when an in-range obstacle accepts the spell, and never mutates MP; `Resolve` spends MP once and progresses matching obstacles. (Tests encode *why*: the query must be side-effect-free so it can gate the animation without committing the cast.)
- **`PlatformerCastSequencer`** — resolves exactly once on a fire-frame; resolves exactly once on timeout; never twice when both occur; ignores a second `RequestCast` while one is pending; fires `onCastBegan`/`onCastEnded` in order.
- **`PlayerAuraCue`** — visible when ≥1 puzzle in range; hidden at zero; correct with overlapping enters/exits; prunes destroyed entries; stays hidden while suppressed (mid-cast) even with puzzles in range.

---

## What Is Not Changing

- Obstacle controllers' resolution internals (`TryMelt`/`TryFreeze`/`TryIgnite`/`TryNeutralize` and their coroutines) — unchanged; only *when* they're invoked moves to the fire-frame.
- `SpellResultMatcher`, the Vosk pipeline, `MicrophoneInputHandler`, `PlayerMovement`.
- The Battle scene's cast flow — the shared clips/event method are reused, not modified.
- Existing `controller.SetPlayerInRange(...)` proximity semantics that govern castability.

---

## Out of Scope (deferred DEV-118 follow-up)

- Visible feedback when casting without enough mana (the broke case currently just no-ops).
- Emphasized mana-consumption feedback on a successful cast; mana-loss feedback beyond the HUD bar.
- Reusing/adapting the Battle floating-number spawner — note `PlatformerFloatingNumberSpawner` / `PlatformerFloatingNumberInstance` already exist in `Assets/Scripts/Platformer/`.
- A platformer mana HUD bar.
- General "cast animation on *any* recognized spell" (cut in favor of puzzle-only casts; revisit if open-world spell effects are added).
