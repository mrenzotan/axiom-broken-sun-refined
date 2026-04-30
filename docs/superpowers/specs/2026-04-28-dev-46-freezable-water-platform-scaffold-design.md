# DEV-46 Freezable Water Platform Scaffold — Design

**Status:** Draft for review
**Date:** 2026-04-28
**Related tickets:** DEV-46 (Level 1 — Snow Mountain umbrella), DEV-82 (Voice cast in platformer scene — future), future sibling: Generic Platform Motion (TBD)

## Goal

Add a reusable `P_WaterPlatform.prefab` that the player can stand on only when frozen. The default state is liquid (no collider — the player passes through to whatever the level places below). Casting `freeze` while in proximity flips the platform to solid for a fixed duration, after which it re-melts back to liquid. Establish the runtime architecture and prefab now using the same scaffold pattern as the Ice Wall (debug-key cast in place of voice), so DEV-82 can replace the input layer without touching the platform logic.

## Scope

### In scope (this ticket)

- A reusable `P_WaterPlatform.prefab` placeable in any platformer scene.
- Plain-C# decision logic `FreezablePlatform.CanFreeze(spellId, freezeSpellIds)` with Edit-Mode tests.
- A MonoBehaviour controller that owns frozen state, the re-melt countdown, the end-of-timer flash blink, the sprite swap (water ↔ ice), and the `BoxCollider2D` toggle. Exposes `TryFreeze(spellId)` as the stable seam.
- A debug-key input caster (DEV-46 stub) that fires `TryFreeze` while the player is in proximity.
- A new InputSystem action `DebugFreezeCast` bound to keyboard `F`.
- Two placeholder sprite assets (cyan water rect, pale-white ice rect) in `Assets/Art/Sprites/Platformer/`.

### Out of scope (deferred)

- Voice cast in the platformer scene (DEV-82).
- Platform motion — bobbing, drifting, waypoints. Sibling ticket, separate brainstorming/spec/plan cycle.
- Animated water shader / wave VFX (Phase 7 polish).
- Persistence of frozen state across scene loads / world restore.
- Chemistry-condition-aware matching (`reactsWith == Liquid`); we stick with the explicit `freezeSpellIds` list pattern Ice Wall uses.
- `Level_1-2.unity` scene authoring — that is a follow-up DEV-46 task that *consumes* this prefab.
- Audio (Phase 7 polish).

## Architecture

### Components

| File | Type | Responsibility |
|---|---|---|
| `Assets/Scripts/Platformer/FreezablePlatform.cs` | Plain C# (static) | Pure decision: `CanFreeze(string spellId, IReadOnlyList<string> freezeSpellIds)`. Edit-Mode tested. |
| `Assets/Scripts/Platformer/FreezablePlatformController.cs` | MonoBehaviour | Holds `_isFrozen`, runs the re-melt countdown, runs the end-of-timer flash blink, swaps the SpriteRenderer's sprite (water ↔ ice), toggles the `BoxCollider2D`, exposes `TryFreeze(string spellId)`. |
| `Assets/Scripts/Platformer/FreezablePlatformDebugCaster.cs` | MonoBehaviour | DEV-46 stub. Listens for the `DebugFreezeCast` InputAction; fires `TryFreeze(_debugSpellId)` only while `_isPlayerInRange` on the controller is true. Replaced by DEV-82's voice caster. |
| `Assets/Scripts/Platformer/FreezablePlatformProximityForwarder.cs` | MonoBehaviour | On the child proximity trigger; forwards `OnTriggerEnter2D` / `OnTriggerExit2D` to the parent controller's `SetPlayerInRange(bool)`. |

All four files live under the existing `Axiom.Platformer.asmdef` — no new asmdef required.

The proximity forwarder is intentionally a sibling-but-separate script from `MeltableObstacleProximityForwarder` (rather than refactoring both behind a shared `IProximityListener` interface). Two near-duplicate 15-line scripts is cheaper than touching working Ice Wall code; if a third proximity-gated mechanic shows up that becomes the trigger to unify. See "Risks and decisions" below.

### Prefab

`Assets/Prefabs/Platformer/P_WaterPlatform.prefab`:

```
P_WaterPlatform   (SpriteRenderer (water sprite default) ·
                   BoxCollider2D (enabled=false, sized to sprite) ·
                   FreezablePlatformController ·
                   FreezablePlatformDebugCaster)
└── ProximityTrigger   (BoxCollider2D isTrigger=true, sized to sprite + ~1 unit buffer ·
                        FreezablePlatformProximityForwarder)
```

**Why this layout differs from Ice Wall:**

- No `Grid` / `Tilemap` child — the platform is a discrete sprite (a floating blob), not a contiguous painted region. A water platform is a discrete object, semantically; a wall is a painted shape.
- One `SpriteRenderer`, not two stacked: the controller swaps `.sprite` between two serialized `Sprite` fields (`_waterSprite`, `_iceSprite`). Lighter than two stacked renderers and lets the warning-blink alpha pulse via `SpriteRenderer.color` cleanly without owner-conflict.
- `BoxCollider2D` sits on the root next to the SpriteRenderer (matching sprite bounds). Default `enabled = false` (liquid) — controller flips it `true` on freeze, `false` on melt-back.
- The proximity child is still its own GameObject because it needs to be a trigger volume larger than the sprite, while the root collider is the *solid* surface.

### Trigger flow

```
                       ┌──────────────────────────────────────────────────┐
                       │  FreezablePlatformController.TryFreeze(spellId)  │ ◄── stable seam
                       └──────────────────────────────────────────────────┘
                                ▲                            ▲
                                │                            │
   ┌────────────────────────────┴──────────────┐  ┌──────────┴───────────────────────┐
   │ FreezablePlatformDebugCaster (DEV-46 stub)│  │ Future: PlatformerSpellCaster    │
   │  · listens for DebugFreezeCast            │  │   (DEV-82, voice + Vosk in       │
   │  · only fires while _isPlayerInRange      │  │    platformer scene)             │
   │  · calls TryFreeze(_debugSpellId)         │  │  · same TryFreeze(spellId) call  │
   └───────────────────────────────────────────┘  └──────────────────────────────────┘
```

The seam in code:

```csharp
public bool TryFreeze(string spellId)
{
    if (_isFrozen) return false;
    if (!_isPlayerInRange) return false;

    var freezeSpellIds = new List<string>(_freezeSpells.Count);
    for (int i = 0; i < _freezeSpells.Count; i++)
    {
        SpellData spell = _freezeSpells[i];
        if (spell != null) freezeSpellIds.Add(spell.spellName);
    }

    if (!FreezablePlatform.CanFreeze(spellId, freezeSpellIds)) return false;

    _isFrozen = true;
    StartCoroutine(FreezeCoroutine());
    return true;
}
```

`_freezeSpells` is a serialized `List<SpellData>` on the prefab; for Level 1-2 the value is `[SD_Freeze]`. The list (not a single ID) mirrors Ice Wall — leaves room for future "any phase-change-to-solid spell" matching without refactoring the seam.

The debug caster goes through the **public** `TryFreeze` method — it has no privileged access. This keeps the debug path a true stand-in for the voice path, not a shortcut that hides bugs.

### Behavior detail

**Tunable fields on `FreezablePlatformController`:**

```csharp
[SerializeField] private SpriteRenderer _spriteRenderer;
[SerializeField] private BoxCollider2D _solidCollider;
[SerializeField] private Sprite _waterSprite;
[SerializeField] private Sprite _iceSprite;
[SerializeField] private List<SpellData> _freezeSpells = new();   // [SD_Freeze]
[SerializeField, Min(1f)] private float _freezeDuration = 5f;     // total solid time
[SerializeField, Min(0.1f)] private float _warningWindow = 1.5f;  // last N seconds blink
[SerializeField] private float _warningFlashStartHz = 4f;
[SerializeField] private float _warningFlashEndHz = 12f;
```

**Initial state:** `_isFrozen = false`. Sprite is water. Collider is disabled. Player passes through to whatever the level places below — death pit, hazard, or lower deck (level designer's call).

**Freeze coroutine timeline** (with default 5s / 1.5s warning):

| Time (s) | Action |
|---|---|
| 0.0 | `_isFrozen = true`. Sprite → ice. `_solidCollider.enabled = true`. |
| 0.0 → 3.5 | Solid, no visual change. Player can stand and jump normally. |
| 3.5 → 5.0 | **Warning window.** Alpha square-waves between 1.0 and 0.5. Frequency lerps from 4 Hz → 12 Hz over the window — visibly accelerates as time runs out, telegraphs the imminent melt. |
| 5.0 | Sprite → water. Alpha → 1.0. `_solidCollider.enabled = false`. `_isFrozen = false`. If the player is still standing on it, they fall through (this is the timer-runs-flat decision). |

**Behavioral edge cases — explicit decisions:**

| Case | Behavior | Rationale |
|---|---|---|
| Re-cast `freeze` while already frozen | `TryFreeze` returns `false` (no-op). Timer is *not* refreshed. | Timer-runs-flat decision: "you have N seconds, period." Refreshing would silently undermine that. |
| Re-cast `freeze` during the warning window | Same — no-op. | Same reason. |
| Re-cast `freeze` immediately after melt-back | Works (returns `true`). Platform re-freezes for another full duration. | Proximity-gated cast loop is repeatable as long as the player stays in range. |
| Player leaves proximity mid-freeze | Timer keeps running; collider stays solid until `_freezeDuration` elapses. Player simply can't re-cast on it from afar. | Proximity gates *casting*, not freeze persistence. |
| Cast a non-freeze spell (e.g. `combust`) in proximity | `TryFreeze` returns `false` via `CanFreeze` check. No state change. | Decouples the obstacle's "what spells affect me" list from any global spell registry. |

**Sketch of the freeze coroutine:**

```csharp
private IEnumerator FreezeCoroutine()
{
    SetVisualState(frozen: true);

    float solidWindow = _freezeDuration - _warningWindow;
    yield return new WaitForSeconds(solidWindow);

    float elapsed = 0f;
    Color color = _spriteRenderer.color;
    while (elapsed < _warningWindow)
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / _warningWindow;
        float hz = Mathf.Lerp(_warningFlashStartHz, _warningFlashEndHz, progress);
        float wave = Mathf.Sin(elapsed * hz * 2f * Mathf.PI);
        color.a = wave > 0f ? 1f : 0.5f;
        _spriteRenderer.color = color;
        yield return null;
    }

    color.a = 1f;
    _spriteRenderer.color = color;
    SetVisualState(frozen: false);
    _isFrozen = false;
}

private void SetVisualState(bool frozen)
{
    _spriteRenderer.sprite = frozen ? _iceSprite : _waterSprite;
    _solidCollider.enabled = frozen;
}
```

No spell-impact tint flash on freeze. The sprite swap (cyan water → pale-white ice) is unambiguous and instantaneous; it sells the impact beat without an extra cue. The Ice Wall needs its tint flash because its tilemap fades out gradually; here the transition reads cleanly without one.

### Input

A new `DebugFreezeCast` action is added to `Assets/InputSystem_Actions.inputactions`, bound to `<Keyboard>/f`. The `FreezablePlatformDebugCaster` exposes a serialized `InputActionReference` field that the level designer wires to this action when placing the prefab.

DEV-82 will delete the `DebugFreezeCast` action and the `FreezablePlatformDebugCaster` component together. The controller is not modified.

`<Keyboard>/f` mirrors Ice Wall's `<Keyboard>/m` convention — first letter of the spell. Free key, no conflicts with `PlayerMovement` or existing tutorial actions.

### Placeholder sprites

The mechanic and the art are decoupled. For this scaffold the prefab uses two simple placeholder PNGs created during plan execution:

- `Assets/Art/Sprites/Platformer/water_platform_placeholder.png` — small (~32×8 px) flat cyan rectangle (`#3CC4FF`-ish).
- `Assets/Art/Sprites/Platformer/ice_platform_placeholder.png` — small (~32×8 px) flat pale-white rectangle (`#E6F8FF`-ish).

(File path matches the existing `Assets/Art/Sprites/{Decorations,VFX,Player,...}` convention; lowercase descriptive names match the existing `vfx_combust_1.png` / `vfx_neutralize_sheet.png` style.)

Both imported with `Sprite Mode: Single`, `Pixels Per Unit: 32` (matches existing Snow Mountain palette), `Filter Mode: Point`, `Compression: None`. The `SpriteRenderer` on the prefab uses `Draw Mode: Simple`; per-instance size is controlled by scaling the GameObject's Transform — no 9-slicing needed for placeholder.

Real water/ice art (from a side-scroller pack like Sunny Land, Pixel Adventure, or a custom asset) is a Phase 7 polish swap: drop the new Sprite into `_waterSprite` / `_iceSprite` on the prefab, no code change. The asset hunt does not block 1-2's puzzle from existing.

## Plan integration

This spec ships a self-contained scaffold (prefab + scripts + tests + InputAction). It does **not** modify the umbrella `2026-04-20-dev-46-level-1-snow-mountain.md` plan now.

Future integration with the umbrella plan happens when Task 13 (Build Level_1-2) is actually scheduled:

- Task 13 currently describes Level 1-2 as the Frostbite Creeper / DoT-introduction level. When that gets fleshed out into actual level layout, you'll add steps that place `P_WaterPlatform` instances (with the future `PlatformMotion` sibling component on bobbing/drifting ones), wire each instance's `_freezeSpells = [SD_Freeze]` and `_debugFreezeAction = DebugFreezeCast`, and add a tutorial prompt before the first water platform.
- That umbrella amendment is its own ticket-of-work — not scope for this scaffold spec.

The implementation plan for *this* spec lives at `docs/superpowers/plans/2026-04-28-dev-46-freezable-water-platform-scaffold.md` (created by the writing-plans skill in the next session step).

### Manual verification (in-spec)

The implementation plan's final task is a scratch-scene playtest, mirroring how Ice Wall verified:

1. Create or reuse a scratch scene (e.g. `Assets/Scenes/_ScratchFreezablePlatform.unity` — never committed to Build Settings).
2. Drop a `P_WaterPlatform` instance over a clearly-labelled "death pit" or marker below.
3. Wire `_freezeSpells = [SD_Freeze]`, `_debugFreezeAction = DebugFreezeCast`.
4. Press Play. Verify, in order:
   - Walk under/onto the platform without casting → player passes through (no collider).
   - Walk into proximity, press F outside proximity → no freeze (`_isPlayerInRange` gate).
   - Press F in proximity → sprite swaps to ice, collider engages, player can stand and jump.
   - Press F again while frozen → no-op (no timer refresh).
   - Wait ~3.5s → warning blink starts and accelerates over 1.5s.
   - At ~5s → sprite reverts to water, collider drops. If the player was standing on it, they fall.
   - Re-cast F in proximity post-melt → freezes again.
   - Cast F while in proximity to a platform with `_freezeSpells = [SD_Combust]` instead → no freeze (decision-list gate).

## File-level summary

### New files

- `Assets/Scripts/Platformer/FreezablePlatform.cs`
- `Assets/Scripts/Platformer/FreezablePlatformController.cs`
- `Assets/Scripts/Platformer/FreezablePlatformDebugCaster.cs`
- `Assets/Scripts/Platformer/FreezablePlatformProximityForwarder.cs`
- `Assets/Tests/Editor/Platformer/FreezablePlatformTests.cs`
- `Assets/Prefabs/Platformer/P_WaterPlatform.prefab`
- `Assets/Art/Sprites/Platformer/water_platform_placeholder.png`
- `Assets/Art/Sprites/Platformer/ice_platform_placeholder.png`

### Modified files

- `Assets/InputSystem_Actions.inputactions` — add `DebugFreezeCast` action bound to `<Keyboard>/f`.

### Deleted in DEV-82 (not now)

- `Assets/Scripts/Platformer/FreezablePlatformDebugCaster.cs`
- `DebugFreezeCast` action in `InputSystem_Actions.inputactions`

## Test surface

Edit-Mode (`Axiom.Platformer.Tests`):

| Test | What it pins down |
|---|---|
| `CanFreeze_NullSpellId_ReturnsFalse` | Defensive against caller passing `null`. |
| `CanFreeze_EmptySpellId_ReturnsFalse` | Empty-string guard. |
| `CanFreeze_SpellInList_ReturnsTrue` | Happy path. |
| `CanFreeze_SpellNotInList_ReturnsFalse` | Reject list-mismatch (e.g. `combust` against `[freeze]`). |

The three MonoBehaviours and the coroutine are intentionally untested in Edit Mode — they are thin Unity-side glue with no branching logic that isn't already covered by `CanFreeze`. Verification is the manual scratch-scene playtest above.

## Risks and decisions

- **Why a debug key instead of full voice casting now:** Voice casting in the platformer scene requires bringing the Vosk stack (`MicrophoneInputHandler`, `VoskRecognizerService`, `SpellCastController`, `SpellVocabularyManager`, a `BattleVoiceBootstrap`-equivalent) into a scene that has none of it. That work is DEV-82's scope. Doing it inside this spec would expand the ticket and entangle two epics. Same trade-off Ice Wall made.

- **Why `TryFreeze(string spellId)` instead of `TryFreeze(SpellData spell)`:** A string ID keeps the obstacle decoupled from the `Axiom.Data` assembly. DEV-82 can layer condition-aware matching on top (`bool CanFreeze(SpellData spell)` reading e.g. `spell.transformsTo == Solid`) without breaking the existing call site. The unit test surface stays small.

- **Why `_freezeSpells: List<SpellData>` and not a single `SpellData _freezeSpell`:** Mirrors Ice Wall's `_meltSpells`. Future-proofs against "any phase-change-to-solid spell freezes water" matching once more spells exist (e.g. a hypothetical `crystallize`) without refactoring the seam.

- **Why a single SpriteRenderer with two Sprite refs instead of two stacked SpriteRenderers:** Lighter (one renderer per platform), single owner of `SpriteRenderer.color` so the warning blink can pulse alpha without coordinating across two renderers.

- **Why the timer runs flat regardless of where the player is standing:** Explicit puzzle-pressure choice. Refreshing on re-cast or pausing on stand would silently undermine the decision. The accelerating end-of-timer flash blink is the warning telegraph the player gets in exchange.

- **Why no spell-impact flash on freeze:** The sprite swap is unambiguous and instantaneous — the change *is* the impact beat. Ice Wall needed an extra tint flash because its melt fades the tilemap out gradually; that asymmetry doesn't exist here.

- **Why placeholder sprites instead of waiting for real art:** Mechanic and art are decoupled. Mirrors how Ice Wall shipped (existing palette tiles, not bespoke ice-wall art). Real sprites are a one-line prefab swap later — zero code change.

- **Why a sibling `FreezablePlatformProximityForwarder` rather than refactoring both Ice Wall's and this one's forwarders behind a shared `IProximityListener` interface:** Two near-duplicate 15-line scripts is cheaper than touching working Ice Wall code. CLAUDE.md's "no premature abstraction" backs this — if a third proximity-gated mechanic shows up that's the trigger to unify, not now.

- **Why this spec doesn't author `Level_1-2.unity`:** Mechanic delivery vs. level design are different concerns. The umbrella plan's Task 13 (Build Level_1-2) will compose this prefab with the future `PlatformMotion` sibling, plus Frostbite Creepers, plus level layout — driven by what 1-2 needs as a level, not what the freeze mechanic needs to exist.

- **Why no persistence across scene loads:** A re-melting platform has no meaningful "frozen state" to persist — by the time you'd reload the scene, it would have melted anyway. World restore is a separate DEV-XX concern.
