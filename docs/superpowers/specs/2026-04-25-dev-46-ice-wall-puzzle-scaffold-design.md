# DEV-46 Ice Wall Puzzle Scaffold — Design

**Status:** Draft for review
**Date:** 2026-04-25
**Related tickets:** DEV-46 (Level 1 — Snow Mountain), DEV-82 (Environmental chemistry-spell puzzles — future)

## Goal

Add a meltable Ice Wall obstacle at the end of Level_1-1 that blocks the player's path and is removed by a "spell cast" action. Establish the runtime architecture and prefab now, with a debug-key input fallback in place of voice casting, so DEV-82 can replace the input layer without touching the obstacle logic.

## Scope

### In scope (DEV-46)

- A reusable `P_IceWall.prefab` placeable in any platformer scene.
- Plain-C# decision logic for "does this spell ID melt this obstacle?" with Edit-Mode tests.
- A MonoBehaviour controller that owns melted state, the visual fade coroutine, and a public `TryMelt(spellId)` method.
- A debug-key input caster (DEV-46 stub) that fires `TryMelt` while the player is in proximity.
- One Ice Wall instance placed in Level_1-1 between the Meltspawn fight and the level exit.
- A new InputSystem action `DebugMeltCast` bound to keyboard `M`.
- A `TutorialPromptTrigger` adjacent to the Ice Wall hinting at the test-melt key.

### Out of scope (deferred to DEV-82)

- Voice spell casting in the platformer scene (no `MicrophoneInputHandler` / `SpellCastController` setup).
- Persistence of melted state across scene loads / world restore.
- Chemistry-condition-based matching (`reactsWith` / `transformsTo` aware obstacles).
- Multiple obstacle types (lava walls, acid pools, etc.).
- Audio (Phase 7 polish).
- Particle effects.

## Architecture

### Components

| File | Type | Responsibility |
|---|---|---|
| `Assets/Scripts/Platformer/MeltableObstacle.cs` | Plain C# (static) | Pure decision: `CanMelt(string spellId, IReadOnlyList<string> meltSpellIds)`. Edit-Mode tested. |
| `Assets/Scripts/Platformer/MeltableObstacleController.cs` | MonoBehaviour | Holds melted state, runs the fade coroutine, toggles the `TilemapCollider2D`, exposes `TryMelt(string spellId)` as the stable seam. |
| `Assets/Scripts/Platformer/MeltableObstacleDebugCaster.cs` | MonoBehaviour | DEV-46 stub. Listens for `DebugMeltCast` InputAction; fires `TryMelt(_debugSpellId)` only while `_isPlayerInRange` on the controller is true. Replaced by DEV-82's voice caster. |
| `Assets/Scripts/Platformer/MeltableObstacleProximityForwarder.cs` | MonoBehaviour | Sits on the child proximity trigger; forwards `OnTriggerEnter2D` / `OnTriggerExit2D` to the parent controller's `_isPlayerInRange` flag. |

All four files live under the existing `Axiom.Platformer.asmdef` — no new asmdef required.

### Prefab

`Assets/Prefabs/Platformer/P_IceWall.prefab`:

```
P_IceWall  (Grid · MeltableObstacleController · MeltableObstacleDebugCaster)
├── Tilemap            (Tilemap · TilemapRenderer · TilemapCollider2D solid)
│                       paint ice tiles here from Palette_SnowMountain
└── ProximityTrigger   (BoxCollider2D isTrigger=true · MeltableObstacleProximityForwarder)
                        sized to painted bounds + ~1 unit buffer
```

**Why this layout:**

- The prefab is self-contained — its own `Grid` (cell size `1, 1, 0` matching the scene's main Grid per the DEV-46 plan, Task 1 Step 3) means it can be dropped anywhere without scene-level setup. Painted tiles align with the rest of the world automatically.
- The proximity trigger is a child GameObject because the root collider (`TilemapCollider2D`) is the *solid* wall — the player physically cannot enter it, so we need a separate trigger volume around it.
- `MeltableObstacleProximityForwarder` is a thin script (one job: relay enter/exit to parent) so the proximity child can be authored as a generic trigger volume.

### Trigger flow

```
                          ┌──────────────────────────────────────────────┐
                          │  MeltableObstacleController.TryMelt(spellId) │ ◄── stable seam
                          └──────────────────────────────────────────────┘
                                  ▲                              ▲
                                  │                              │
   ┌──────────────────────────────┴────────────┐    ┌────────────┴───────────────────────┐
   │ MeltableObstacleDebugCaster (DEV-46 stub) │    │ Future: PlatformerSpellCaster      │
   │  · listens for DebugMeltCast InputAction  │    │   (DEV-82, voice + Vosk in         │
   │  · only fires while _isPlayerInRange      │    │    platformer scene)               │
   │  · calls TryMelt(_debugSpellId)           │    │  · same TryMelt(spellId) call      │
   └───────────────────────────────────────────┘    └────────────────────────────────────┘
```

The seam in code:

```csharp
public bool TryMelt(string spellId)
{
    if (_isMelted) return false;
    if (!_isPlayerInRange) return false;
    if (!MeltableObstacle.CanMelt(spellId, _meltSpellIds)) return false;
    _isMelted = true;
    StartCoroutine(MeltCoroutine());
    return true;
}
```

`_meltSpellIds` is a serialized `List<string>` on the prefab; for Level_1-1 the value is `["combust"]`. The list (not a single ID) is intentional — DEV-82 may decide several heat-class spells should melt ice, and the matching logic should not need to be refactored to support that.

The debug caster goes through the **public** `TryMelt` method — it has no privileged access. This keeps the debug path a true stand-in for the voice path, not a shortcut that hides bugs.

### Melt visual

`MeltCoroutine` runs ~0.7 s total:

| Time | Action |
|---|---|
| 0.0 → 0.15 s | Tween `Tilemap.color` from white → pale cyan tint `#BFE9FF` and back. Sells the spell-impact beat. |
| 0.15 s | Disable `TilemapCollider2D`. Player can walk through immediately even though the visual is still fading — keeps melt feeling responsive. |
| 0.15 → 0.7 s | Lerp `Tilemap.color.a` from 1 → 0 with `EaseOutQuad` (faster early, slow tail). Concurrently lerp `Tilemap.transform.localScale.y` from 1 → 0.6 so the wall visibly sinks as it disappears. |
| 0.7 s | Disable the `Tilemap` GameObject. |

Tilemap color tints all painted tiles uniformly, so no per-tile work is needed. No new sprites, no particle systems, no audio — DEV-46 ships purely with the existing tile palette art.

### Input

A new `DebugMeltCast` action is added to `Assets/InputSystem_Actions.inputactions`, bound to `<Keyboard>/m`. The `MeltableObstacleDebugCaster` exposes a serialized `InputActionReference` field that the level designer wires to this action when placing the prefab.

DEV-82 will delete the `DebugMeltCast` action and the `MeltableObstacleDebugCaster` component together. The controller is not modified.

## Plan integration

Slots into the existing DEV-46 plan as follows:

### New tasks

**Task 11A** (after Task 11, before Task 12): MeltableObstacle scripts and tests.

- Write `Assets/Tests/Editor/Platformer/MeltableObstacleTests.cs` with 4 cases:
  - `CanMelt_NullSpellId_ReturnsFalse`
  - `CanMelt_EmptySpellId_ReturnsFalse`
  - `CanMelt_SpellInList_ReturnsTrue`
  - `CanMelt_SpellNotInList_ReturnsFalse`
- Run tests, confirm fail (no implementation).
- Create `Assets/Scripts/Platformer/MeltableObstacle.cs` (static class with `CanMelt`).
- Run tests, confirm pass.
- Create `Assets/Scripts/Platformer/MeltableObstacleController.cs`.
- Create `Assets/Scripts/Platformer/MeltableObstacleDebugCaster.cs`.
- Create `Assets/Scripts/Platformer/MeltableObstacleProximityForwarder.cs`.
- UVCS check-in: `feat(DEV-46): add MeltableObstacle scripts and tests`.

**Task 11B** (after Task 11A): InputAction + P_IceWall prefab.

- Add `DebugMeltCast` action to `Assets/InputSystem_Actions.inputactions`, bound to `<Keyboard>/m`.
- Build `P_IceWall.prefab` per the Architecture → Prefab section above. Paint a small placeholder shape into the child Tilemap so the prefab is non-empty when previewed.
- Wire the `MeltableObstacleController` serialized fields (`_tilemap`, `_solidCollider`, `_meltSpellIds = ["combust"]`, `_fadeDuration = 0.7`).
- Wire `MeltableObstacleDebugCaster._debugMeltAction` to the new InputActionReference.
- Manual verification step: drop the prefab into a scratch scene, press Play, walk into proximity, press M, watch the wall fade.
- UVCS check-in: `feat(DEV-46): add P_IceWall prefab and DebugMeltCast input action`.

### Modified task

**Task 12 (Build Level_1-1)** — add **Step 5.5** between Meltspawn placement (current Step 5) and Play-mode verification (current Step 6):

- Drop a `P_IceWall` instance into the scene immediately before the `LevelExitTrigger`. Paint the wall shape into its child Tilemap using `Palette_SnowMountain` ice tiles, sized to span the level's vertical playfield.
- Wire `MeltableObstacleDebugCaster._debugMeltAction` to the `DebugMeltCast` InputActionReference.
- Add a `TutorialPromptTrigger` immediately before the wall with message: `"An icy wall blocks your path. Press M to test-melt it. (Voice cast comes later.)"`.
- Verify Play-mode end-to-end: tutorial prompts appear → Meltspawn fight resolves → Ice Wall hint appears → press M while adjacent → wall fades and collider drops → walk through to LevelExitTrigger → Level_1-2 loads.

The plan amendment itself can be made after this spec is reviewed, in the same commit as the implementation plan.

## File-level summary

### New files

- `Assets/Scripts/Platformer/MeltableObstacle.cs`
- `Assets/Scripts/Platformer/MeltableObstacleController.cs`
- `Assets/Scripts/Platformer/MeltableObstacleDebugCaster.cs`
- `Assets/Scripts/Platformer/MeltableObstacleProximityForwarder.cs`
- `Assets/Tests/Editor/Platformer/MeltableObstacleTests.cs`
- `Assets/Prefabs/Platformer/P_IceWall.prefab`

### Modified files

- `Assets/InputSystem_Actions.inputactions` — add `DebugMeltCast` action.
- `Assets/Scenes/Level_1-1.unity` — place `P_IceWall` instance + adjacent `TutorialPromptTrigger`.
- `docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md` — insert Tasks 11A and 11B, add Step 5.5 to Task 12.

### Deleted in DEV-82 (not now)

- `Assets/Scripts/Platformer/MeltableObstacleDebugCaster.cs`
- `DebugMeltCast` action in `InputSystem_Actions.inputactions`

## Test surface

Edit-Mode (`Axiom.Platformer.Tests`):

- `MeltableObstacleTests` — 4 tests covering `CanMelt` decision logic.

The three MonoBehaviours and the coroutine are intentionally untested in Edit Mode — they are thin Unity-side glue with no branching logic. Verification is manual:

- Press M outside proximity → no melt (validated by `_isPlayerInRange` gate).
- Press M inside proximity with a non-listed spell ID → no melt (validated by `CanMelt` returning false).
- Press M inside proximity with `combust` → wall fades, collider drops, player walks through.
- Press M inside proximity again after melt → no-op (validated by `_isMelted` gate).

## Risks and decisions

- **Why a debug key instead of full voice casting now:** Voice casting in the platformer scene requires bringing the Vosk stack (`MicrophoneInputHandler`, `VoskRecognizerService`, `SpellCastController`, `SpellVocabularyManager`, `BattleVoiceBootstrap`-equivalent) into a scene that has none of it. That work is DEV-82's scope. Doing it inside DEV-46 would expand the ticket and entangle two epics.
- **Why `TryMelt(string spellId)` instead of `TryMelt(SpellData spell)`:** A string ID keeps the obstacle decoupled from the `Axiom.Data` assembly. DEV-82 can layer condition-aware matching on top (`bool CanMelt(SpellData spell)` reading `spell.transformsTo == Liquid`) without breaking the existing call site. The unit test surface stays small.
- **Why a self-contained prefab (Grid + Tilemap) instead of a scene-level Tilemap layer:** Drop-in placement, per-instance fade isolation, and per-instance persistence (when DEV-82 lands) are all simpler. The slight overhead of one extra Grid component per Ice Wall is negligible.
- **Why the player can walk through at t=0.15s (mid-fade):** Waiting for full fade before disabling the collider feels unresponsive — the player sees the spell hit but can't move. Disabling collider early prioritizes input responsiveness; the fading sprite sells the "is melting" verb without gating gameplay on the animation length.
