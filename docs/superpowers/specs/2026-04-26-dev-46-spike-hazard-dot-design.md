# DEV-46 Spike Hazard Damage-Over-Time — Design

**Status:** Draft for review
**Date:** 2026-04-26
**Related tickets:** DEV-46 (Level 1 — Snow Mountain)

## Goal

Replace the current one-shot 20%-on-contact spike behavior with a configurable damage-over-time (DoT) model so spike clusters function as recoverable platforming hazards: missing a jump and landing on spikes should hurt and create urgency, but a player who immediately moves to safe ground survives. Standing on spikes drains HP to zero and routes through the existing checkpoint-respawn pipeline. Pit hazards (`HazardMode.InstantKO`) are unchanged.

## Scope

### In scope

- Refactor `HazardTrigger.cs` from `OnTriggerEnter2D`-only to `OnTriggerEnter2D` + `OnTriggerStay2D` + `OnTriggerExit2D` with a tick timer.
- Three new per-instance Inspector fields on `HazardTrigger`: `_firstHitDamagePercent`, `_damagePerTickPercent`, `_tickIntervalSeconds`.
- Extend `HazardDamageResolver.cs` with tick-damage cases (or generalize the existing `Resolve` to be reused for both first-hit and per-tick damage — caller picks the percent).
- Add `Assets/Scripts/Platformer/PlayerHurtFeedback.cs` — a small MonoBehaviour on the Player root that plays the `playerHurt` animation trigger and applies a sustained sprite tint while overlapping any spike hazard (counter-based, supports multiple simultaneous overlaps).
- Extend `HazardDamageResolverTests.cs` with Edit-Mode tests for the tick path.
- Update `docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md` so Task 5, the Level 1-1 hazard placement step, and the DEV-46 summary line all reflect the DoT model.

### Out of scope

- Changes to pit hazards (`HazardMode.InstantKO`).
- Knockback / hitstun / movement disable on contact — player remains fully controllable on top of spikes (this is the *point* of the recoverable DoT model).
- True invincibility frames for *all* damage sources (i-frames are a player-side concern; this spec only addresses hazard tick spacing).
- Hazard variant types (electric, freeze, etc.) — only spikes-style DoT is in scope.
- Audio for spike contact (Phase 7 polish).
- Particle/VFX on spike contact (Phase 7 polish).
- Player-side centralized tick controller (deferred — see Accepted Limitations).
- HUD wiring. `PlatformerHpHudUI.cs` already exists; ensuring it is wired into Level_1-1's Canvas is a separate Editor task tracked under DEV-46, not redesigned here.

## Architecture

### Components

| File | Type | Responsibility |
|---|---|---|
| `Assets/Scripts/Platformer/HazardDamageResolver.cs` | Plain C# (static) | Pure damage math. Existing `Resolve(currentHp, maxHp, mode, percent)` is reused for both the first-hit and per-tick damage applications — the caller chooses which percent to pass. No mode change required: `PercentMaxHpDamage` covers both. |
| `Assets/Scripts/Platformer/HazardTrigger.cs` | MonoBehaviour | Lifecycle: enter → first-hit damage + notify player feedback + start timer; stay → accumulate timer, fire tick when interval reached; exit → notify player feedback. |
| `Assets/Scripts/Platformer/PlayerHurtFeedback.cs` | MonoBehaviour (new) | Lives on the Player root next to `PlayerExplorationAnimator`. Exposes `PlayHurtAnimation()` (sets Animator trigger), `BeginPainOverlap()` (counter +1, applies tint), `EndPainOverlap()` (counter −1, clears tint when count reaches 0). |
| `Assets/Tests/Editor/Platformer/HazardDamageResolverTests.cs` | Edit-Mode test fixture | Existing 5 tests stay. New tests cover tick-damage cases (drain, clamp, combined first-hit + tick lethality, zero-tick no-op). |

All files live under the existing `Axiom.Platformer.asmdef` — no new asmdef required. Tests live under `Axiom.Tests.Editor.asmdef`.

### Data flow

```
Frame N — player's collider crosses into spike trigger
  HazardTrigger.OnTriggerEnter2D(other)
    ├─ if !other.CompareTag("Player") return
    ├─ if GameManager.Instance == null → log warning, return
    ├─ Cache _feedback = other.GetComponentInParent<PlayerHurtFeedback>()
    ├─ Apply first-hit damage:
    │     result = HazardDamageResolver.Resolve(state.CurrentHp, state.MaxHp,
    │              PercentMaxHpDamage, _firstHitDamagePercent)
    │     state.SetCurrentHp(result.NewHp)
    ├─ _feedback?.PlayHurtAnimation()
    ├─ _feedback?.BeginPainOverlap()
    └─ _tickTimer = 0f

Frames N+1 … M-1 — player still overlapping
  HazardTrigger.OnTriggerStay2D(other)
    ├─ if !other.CompareTag("Player") return
    ├─ if GameManager.Instance == null return
    ├─ _tickTimer += Time.deltaTime
    └─ while _tickTimer >= _tickIntervalSeconds:
         ├─ result = HazardDamageResolver.Resolve(state.CurrentHp, state.MaxHp,
         │           PercentMaxHpDamage, _damagePerTickPercent)
         ├─ state.SetCurrentHp(result.NewHp)
         └─ _tickTimer -= _tickIntervalSeconds   // subtract, don't zero —
                                                 // while loop preserves overshoot
                                                 // and catches multi-tick frames

Frame M — player leaves
  HazardTrigger.OnTriggerExit2D(other)
    ├─ if !other.CompareTag("Player") return
    └─ _feedback?.EndPainOverlap()

Meanwhile (every frame, independent):
  PlayerDeathHandler.Update polls state.CurrentHp.
  When it reaches 0 (whether from first-hit or any tick), death dispatch fires.
  HazardTrigger never knows about death.
```

### Inspector fields on HazardTrigger

```csharp
[SerializeField, Tooltip("InstantKO for pits; PercentMaxHpDamage for spikes.")]
private HazardMode _mode = HazardMode.PercentMaxHpDamage;

[SerializeField, Range(0, 100)]
[Tooltip("HP percent dealt on contact entry. Set to 0 for pure-DoT spikes. Ignored when mode is InstantKO.")]
private int _firstHitDamagePercent = 20;

[SerializeField, Range(0, 100)]
[Tooltip("HP percent dealt every tick while overlapping. Set to 0 for one-shot-only spikes. Ignored when mode is InstantKO.")]
private int _damagePerTickPercent = 10;

[SerializeField, Range(0.1f, 3f)]
[Tooltip("Seconds between DoT ticks while overlapping. Lower values are more punishing.")]
private float _tickIntervalSeconds = 0.5f;
```

When `_mode == InstantKO`, all three percent/interval fields are ignored — the existing one-shot KO path is preserved exactly.

### Level-design presets (suggested starting values)

| Preset | First hit | DoT | Interval | Time-to-die from full HP |
|---|---|---|---|---|
| Tutorial gentle (Level 1-1) | 20 | 10 | 0.5s | ~4s |
| Moderate (Level 1-2 clusters) | 20 | 15 | 0.5s | ~3s |
| Spike tunnel (Level 1-3) | 25 | 15 | 0.4s | ~2s |
| Pure attrition (no entry sting) | 0 | 10 | 0.5s | ~5s |

These are starting recommendations only — the level designer tunes per cluster in the Inspector.

## PlayerHurtFeedback contract

```csharp
public class PlayerHurtFeedback : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Color _painTint = new Color(1f, 0.6f, 0.6f, 1f);
    [SerializeField] private string _hurtTriggerName = "Hurt";

    private int _overlapCount;
    private Color _restingColor;

    private void Awake() { _restingColor = _spriteRenderer.color; }

    public void PlayHurtAnimation();   // sets _hurtTriggerName trigger on _animator
    public void BeginPainOverlap();    // _overlapCount++; if 1, apply _painTint
    public void EndPainOverlap();      // _overlapCount = max(0, _overlapCount-1); if 0, restore _restingColor
}
```

**Why a counter:** if the player overlaps two HazardTriggers simultaneously and leaves one, the tint should *not* clear until they leave the second one too. A single bool would flicker off on the first exit.

**Why a separate component, not on PlayerController:** PlayerController already owns movement, attack, and animation-event hooks — keeping spike feedback isolated honors the "small, well-bounded units" rule and means future hazard types (lava, acid) reuse the same contract without further surgery.

**Why direct method calls and not events:** YAGNI — there's exactly one publisher (HazardTrigger) and one subscriber (PlayerHurtFeedback) right now. An event channel adds wiring weight with no benefit until a second hazard type exists.

## Edge cases & accepted limitations

1. **Player overlaps two HazardTriggers at once.** Each instance runs its own tick timer and damage stacks. Mitigation is a *level-design convention* (one HazardTrigger per visual spike cluster, large BoxCollider2D over the whole area) — this matches the existing Pit_Hazards pattern in the DEV-46 plan (Step 1497). For Level 1-1 (two well-spaced clusters) this is a non-issue. If Level 1-2's spike tunnels later require non-stacking behavior, the planned refactor is to move the tick timer onto `PlayerHurtFeedback` (one timer, one damage-per-second value) and have HazardTriggers register their damage rate on enter. That refactor is **deferred**, not Day 1 work.

2. **Player dies from a tick mid-overlap.** `state.SetCurrentHp(0)` triggers `PlayerDeathHandler` next frame, which calls `GameManager.RespawnAtLastCheckpoint`. The respawning player teleports out of the trigger; `OnTriggerExit2D` fires naturally. `PlayerHurtFeedback._overlapCount` decrements to 0 and tint clears. No special death handling required in HazardTrigger.

3. **Player dies from the first-hit damage on entry.** Same path — `OnTriggerEnter2D` calls `state.SetCurrentHp(0)`, death handler fires next frame, respawn teleports out, exit fires. The animation trigger and `BeginPainOverlap` calls still execute on entry (harmless — tint applies for one frame before respawn).

4. **`PlayerHurtFeedback` is missing from the Player prefab.** All calls are null-safe (`_feedback?.…`). HazardTrigger logs no error in this case — damage still lands, only the visual feedback is missing. This keeps the system tolerant during incremental setup.

5. **Tick timer overshoot from a slow frame.** The `while`-loop subtraction shown in the data flow (subtract `_tickIntervalSeconds` until `_tickTimer` is below the interval) preserves any overshoot, so cumulative damage rate stays accurate over time. A long frame won't silently skip a tick's worth of damage.

6. **Tick interval shorter than one frame at low frame rates.** Possible if `_tickIntervalSeconds` is set very low (~0.1s) on a poorly-performing scene. The same `while`-loop fires multiple ticks in one frame, preserving total damage. Tune the Range slider lower bound (currently 0.1s) up if this proves problematic.

7. **Player exits and re-enters the same HazardTrigger quickly.** `_tickTimer` is reset to 0 on each `OnTriggerEnter2D`. Re-entry costs the full `_firstHitDamagePercent` again. This is the intended design — re-entry is a new mistake and should sting.

## Tests

Edit-Mode tests in `Assets/Tests/Editor/Platformer/HazardDamageResolverTests.cs`. The existing 5 tests stay unchanged; new tests cover tick paths.

| New test | Setup | Expectation |
|---|---|---|
| `TickDamage_DrainsHpByPercent` | currentHp=100, maxHp=100, percent=10 | NewHp=90, IsFatal=false |
| `TickDamage_ClampsToZero` | currentHp=5, maxHp=100, percent=10 | NewHp=0, IsFatal=true |
| `FirstHitPlusOneTick_KillsAtThreshold` | start 30, apply firstHit=20 then tick=10 (sequential `Resolve` calls), maxHp=100 | After both: NewHp=0, IsFatal=true |
| `ZeroPercentTick_IsNoOp` | currentHp=50, maxHp=100, percent=0 | NewHp=50, IsFatal=false |

**Not unit-tested (deliberate):**
- Tick timer accumulation — Unity-lifecycle bound (`Time.deltaTime`, `OnTriggerStay2D`).
- Animator trigger firing — Animator state, Play-Mode only.
- Sprite tint application — SpriteRenderer state, Play-Mode only.
- Overlap counter behavior — depends on Unity's trigger lifecycle ordering.

These are verified manually in Play Mode against Level_1-1's two spike clusters.

## Plan doc updates required

`docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md`:

1. **Task 5 spec lines** — update `HazardTrigger.cs` description and the "How HazardTrigger works" block to describe the DoT model with the three new fields.
2. **Step 1514 (place hazards)** — update the Inspector setup instruction for spike-cluster HazardTriggers to set the three new fields (suggest the Tutorial gentle preset for Level 1-1).
3. **Step 1557** — change "Spike cluster deals 20% max HP" to describe the DoT preset values.
4. **Step 1736** — change "Spikes cost 20% max HP" to "Spikes deal a first-hit percent + DoT ticks while standing on them — see DEV-46 spike hazard DoT spec."
5. **Step 1611 / 1638** — Level 1-2 hazard descriptions reference the DoT preset for spike tunnels.
6. **Add a new file row** to the Task 5 file-changes table for `PlayerHurtFeedback.cs`.
7. **Add the spike-DoT spec link** somewhere near the top of Task 5 so future-you can find it.

## Open questions

None at design time — every decision above is locked.

## Implementation note (for the writing-plans phase)

The Player prefab needs `PlayerHurtFeedback` added in the Unity Editor (Inspector wiring of `_animator`, `_spriteRenderer`). This is an Editor-side task that the implementation plan should call out as a user step, not a code step. The `Hurt` animator trigger parameter and any state-machine transitions are also Editor-side — verify whether they already exist (DEV-60 exploration animations) or need to be added.
