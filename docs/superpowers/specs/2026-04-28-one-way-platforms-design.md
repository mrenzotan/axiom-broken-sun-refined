# One-Way Platforms — Design

**Date:** 2026-04-28
**Status:** Spec
**Phase:** 4 (Scene Bridge — extends platformer foundation)

## Summary

Add Dead Cells–style one-way platforms to the platformer: solid from below (player jumps up through and lands on top), drop-through from above when the player presses **Down + Jump** while grounded on one. System-wide mechanic; reusable in every world scene.

## Behavior

- Player approaches a one-way platform from underneath → passes through freely (no collision response).
- Player jumps up while underneath → passes through the platform on the way up; on descent, lands on top as normal solid ground.
- Player stands on a one-way platform and presses **Down + Jump** → falls through it. Plain Jump (no Down) on a one-way platform = normal jump up.
- Plain Down (no Jump) on a one-way platform = nothing — protects against accidental drop-throughs.
- Drop-through is blocked while movement is locked (attack animation, tutorial-prompt lock, scene-transition lock) — same gate that already blocks Jump today.
- After a drop-through fires, coyote time is canceled — the player cannot immediately jump back up onto the platform they just dropped from.
- Drop-through duration: a fixed **0.2 second** window during which collision between the Player layer and the OneWayPlatform layer is ignored. After the window expires, collision is restored automatically.

## Architecture

### Physics layer

- New User Layer: **`OneWayPlatform`**. Single global definition (Edit → Project Settings → Tags and Layers). Reused across every level scene.

### Tilemap structure (per scene)

Every world scene that uses one-way platforms gets a sibling Tilemap GameObject under the existing `Grid`:

```
Grid
├── Tilemap_Ground               (existing — solid terrain, Default layer)
└── Tilemap_OneWayPlatforms      (new — one-way platforms, OneWayPlatform layer)
    ├── Tilemap
    ├── TilemapCollider2D        (Used By Composite = true, Composite Operation = Merge,
                                  Used By Effector = false — routed through composite)
    ├── Rigidbody2D              (Body Type = Static)
    ├── CompositeCollider2D      (Geometry Type = Polygons, Used By Effector = true)
    │                            — matches the existing Tilemap_Ground convention
    └── PlatformEffector2D       (Use One Way = true, Surface Arc = 180°,
                                  Use Side Friction = false, Use Side Bounce = false)
```

- Sorting Layer / Order on the new tilemap matches `Tilemap_Ground` so painted tiles render correctly relative to background parallax layers.
- The same Tile Palette (`Palette_SnowMountain.prefab`, etc.) is reused — only the active **paint target** changes when the level designer wants a tile to be one-way.

### Code changes

The implementation respects the project's plain-C# / lifecycle-only split. No new files, no new classes — drop-through logic is added as fields and methods on the existing `PlayerMovement` class.

#### `Assets/Scripts/Platformer/PlayerMovement.cs`

New private state:
- `_dropThroughDuration` (float, configured = 0.2f, passed from `PlayerController`)
- `_dropThroughTimer` (float)
- `_dropThroughActive` (bool)
- `_oneWayLayer` (LayerMask, passed from `PlayerController`)
- `_playerLayerIndex` (int, passed from `PlayerController` — resolved once at construction from the player GameObject's layer)

New public methods / properties:
- `bool TryDropThrough()` — preconditions: `_isGrounded` is true AND `_movementLocked` is false AND the ground-check overlap finds a collider on `_oneWayLayer`. If satisfied: calls `Physics2D.IgnoreLayerCollision(_playerLayerIndex, oneWayLayerIndex, true)`, sets `_dropThroughActive = true`, sets `_dropThroughTimer = _dropThroughDuration`, sets `_coyoteTimeCounter = 0f`, returns `true`. Otherwise returns `false`.
- `ResetDropThrough()` — force-restores collision via `IgnoreLayerCollision(..., false)` and clears state. Called from `PlayerController.OnDisable` and `OnDestroy` so global physics state never leaks across scene transitions or player death.
- `bool IsMovementLocked` — new read-only property (`=> _movementLocked;`). Exposes the existing `_movementLocked` field so `PlayerController` can early-out before calling `TryDropThrough()`. The check inside `TryDropThrough()` itself is a defense-in-depth duplicate.

Modified:
- Constructor and `UpdateConfig()` accept the new params (`oneWayLayer`, `playerLayerIndex`, `dropThroughDuration`).
- `Tick()` — inside it, decrement `_dropThroughTimer` if `_dropThroughActive`; when it hits zero, call `IgnoreLayerCollision(..., false)` and clear `_dropThroughActive`.

#### `Assets/Scripts/Platformer/PlayerController.cs`

New Inspector fields under a new `[Header("Drop-Through")]`:
- `[SerializeField] LayerMask oneWayPlatformLayer;`
- `[SerializeField] float dropThroughDuration = 0.2f;`

Modified `OnJumpPerformed(InputAction.CallbackContext)`:
- Reads `Vector2 move = _input.Player.Move.ReadValue<Vector2>()`.
- If `move.y < -0.5f` AND `!_movement.IsMovementLocked`: call `_movement.TryDropThrough()`. If it returns `true`, return early (do not also buffer a jump).
- Otherwise: existing `_movement.BufferJump()` call.
- Why the explicit lock check: the tutorial-lock path already disables the Jump action (`_input.Player.Jump.Disable()` inside `SetTutorialMovementLocked`), so `OnJumpPerformed` does not fire there. The attack-lock path, however, only calls `_movement.SetMovementLocked(true)` — `OnJumpPerformed` still fires during an attack. Drop-through must check the lock flag directly to be suppressed during attack lock.

`OnDisable()` and `OnDestroy()`:
- Add `_movement?.ResetDropThrough();` to guarantee global `IgnoreLayerCollision` state is cleared on scene unload, player destruction, or domain reload.

`Awake()`:
- Constructor call to `PlayerMovement` passes the new params, including `LayerMask.NameToLayer("OneWayPlatform")` resolved once.

#### Player prefab inspector wiring

- `Player Controller → Ground Layer` (existing field): add the **`OneWayPlatform`** layer to the mask. The player must ground-detect on one-way platforms for `IsGrounded` to be true while standing on one — without this, jump from a one-way platform fails.
- `Player Controller → One Way Platform Layer` (new field): set to **`OneWayPlatform`** only. Used by `TryDropThrough()` to filter what's drop-through-able from what's plain ground.
- `Player Controller → Drop Through Duration` (new field): leave at default `0.2`.

### Input

- **No new Input Action.** Drop-through reuses `Move.y` (existing `Move` Vector2 action) sampled at the moment `Jump` is pressed. The pre-existing `Crouch` action is left alone — it is currently unbound to gameplay; co-opting it would conflict if it ever gets a real use.

## Out of scope

- **Level_1-1 retrofit.** No tile repainting in the existing tutorial scene. Only the Player prefab's `Ground Layer` mask and new `One Way Platform Layer` field are wired.
- **Tutorial prompt and Level_1-2 encounter.** The mechanic is taught at the start of Level_1-2 via the existing `TutorialPromptTrigger` + `TutorialOneShotFlagResolver` pattern. The flag, prompt copy, and trigger placement belong to the Level_1-2 implementation ticket, not this mechanic spec.
- **Drop-through animation state.** Existing fall animation suffices. Revisit in a polish pass if it reads poorly in playtest.
- **Per-tile one-way metadata** (mixing solid + one-way on a single tilemap). Not needed — separate tilemap is cleaner and the painter workflow is identical.
- **Edit Mode tests for drop-through.** The logic is entangled with global `Physics2D.IgnoreLayerCollision` state; abstracting that for testability would be premature for a ~30-line addition. Verification is the manual scene smoke test below.

## Verification (manual smoke test in `Platformer.unity`)

After implementation, paint a few one-way platform tiles in `Platformer.unity` for testing. Each step is a pass/fail check.

1. Player runs/walks horizontally under a one-way platform → passes through freely, no collision.
2. Player jumps straight up under a one-way platform → passes through on the way up; lands on top on the way down.
3. Player stands on a one-way platform, presses Down + Jump → falls through, lands on the solid ground below.
4. Player stands on solid `Tilemap_Ground` (not a one-way platform), presses Down + Jump → normal jump fires; no drop-through, no fall-through.
5. Player presses Down + Jump while attack-locked or tutorial-locked → nothing happens; drop-through is suppressed.
6. Within ~0.2 s of a drop-through, jump button press does not re-trigger an upward jump (coyote suppressed).
7. Drop through a one-way platform, then exit and re-enter Play Mode → walking under a one-way platform from below does not pass through (collision restored, no leftover global IgnoreLayerCollision state).
8. Two one-way platforms stacked vertically within roughly 1.5–2 world units → drop-through from the top can pass through both within the 0.2 s window. **This is expected behavior;** level design should avoid stacking one-way platforms within drop-window range. (4-unit gaps verified safe during smoke testing — drop-through duration tuned from the original 0.35 s spec value to 0.2 s after empirical playtest with stacked layouts.)

## Snow Mountain plan adjustment

The Snow Mountain plan (`docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md`) currently treats "platform" palette tiles as paintable onto the same tilemap as rock and ice. After this spec lands, those platform tiles should be painted onto `Tilemap_OneWayPlatforms` instead — a paint-target choice, not a palette change. The tile assets in `Assets/Art/Tilemaps/SnowMountain/Tiles/` are reused as-is. Future world scenes follow the same pattern (sibling `Tilemap_OneWayPlatforms` under `Grid`).
