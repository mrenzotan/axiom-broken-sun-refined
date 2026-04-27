# DEV-46 Spike Hazard DoT Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Pair with the `executing-unity-game-dev-plans` skill for the Unity Editor handoffs and UVCS check-in cadence.

**Goal:** Replace the one-shot 20%-on-contact spike hazard with a configurable first-hit + damage-over-time (DoT) model that lets a player who falls on spikes survive by moving off, while still draining to zero if they stand still. Pit hazards (`HazardMode.InstantKO`) are unchanged in behavior.

**Architecture:** `HazardTrigger` (MonoBehaviour) gains `OnTriggerStay2D` + `OnTriggerExit2D` and a tick timer; the existing `HazardDamageResolver` static class is reused for both first-hit and per-tick damage (no resolver code change required). A new tiny `PlayerHurtFeedback` MonoBehaviour on the Player root owns the `Hurt` animator trigger and a sustained sprite tint during overlap. Death routing is unchanged — `PlayerDeathHandler` already polls `PlayerState.CurrentHp` and respawns on zero, so the DoT path inherits the death pipeline for free.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, Mono scripting backend, Unity Test Framework (Edit Mode, NUnit), Unity Version Control (UVCS) for check-ins.

**Spec:** [`docs/superpowers/specs/2026-04-26-dev-46-spike-hazard-dot-design.md`](../specs/2026-04-26-dev-46-spike-hazard-dot-design.md) — the source of truth for behavior, Inspector field shape, edge cases, and accepted limitations. This plan implements that spec.

**Related plan:** [`docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md`](2026-04-20-dev-46-level-1-snow-mountain.md) — DEV-46 parent plan. Task 5 of that plan now points at this spike-DoT spec/plan as the implementation source.

**Project conventions to honor:**

- **MonoBehaviours own Unity lifecycle only**; logic lives in plain C# classes (already true here — `HazardDamageResolver` is static).
- **No new singletons.** `HazardTrigger` looks up `PlayerHurtFeedback` from the colliding `other` Transform on enter (cached for the duration of the overlap).
- **No premature abstraction.** No `IHazardEffect` interface; no event channels. Direct method calls only. One publisher, one subscriber.
- **UVCS commit format:** `<type>(DEV-46): <short description>` — no Co-Authored-By footer.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Platformer/HazardDamageResolver.cs` | **Unchanged** | Pure static damage math. Existing `Resolve(currentHp, maxHp, mode, percent)` already handles both first-hit and per-tick cases; the caller picks the percent. |
| `Assets/Scripts/Platformer/HazardTrigger.cs` | **Modified (full rewrite of the file body)** | Adds `OnTriggerStay2D`, `OnTriggerExit2D`, three new SerializeFields, tick timer with `while`-loop overshoot, `PlayerHurtFeedback` notification on enter/exit. InstantKO path unchanged. |
| `Assets/Scripts/Platformer/PlayerHurtFeedback.cs` | **Created** | New MonoBehaviour on the Player root. `PlayHurtAnimation()` (sets Animator trigger), `BeginPainOverlap()` / `EndPainOverlap()` (overlap counter + sprite tint). |
| `Assets/Tests/Editor/Platformer/HazardDamageResolverTests.cs` | **Modified (append 4 new tests)** | Adds tick-damage cases. Existing 5 tests stay untouched. |
| `Assets/Animations/Player/Player.controller` | **Modified (Editor)** | Add `Hurt` Trigger parameter; add Any State → `playerHurtLeft` and Any State → `playerHurtRight` transitions; add transitions back to Grounded blend trees on exit. **Critical: uncheck "Can Transition To Self" on the Any State transitions.** |
| Player prefab (path discovered in Task 5 Step 1 — likely `Assets/Prefabs/Player/...` or scene-only inside `Level_1-1.unity`) | **Modified (Editor)** | Add `PlayerHurtFeedback` component to the Player root; wire `_animator` and `_spriteRenderer` SerializeField references. |
| `Assets/Scenes/Level_1-1.unity` | **Modified (Editor)** | Verify the 2 existing pit `HazardTrigger` instances still read `_mode = InstantKO` correctly post field-shape change. Optionally add a single test spike cluster (Tutorial gentle preset) for Play-Mode verification in Task 7. |

---

## Task 1: Add tick-damage tests to HazardDamageResolverTests

**Why TDD-style additions for an unchanged resolver:** the existing `Resolve` method already handles the new cases mathematically, but the spec defines four behavioral guarantees (drain, clamp, combined first-hit + tick lethality, zero-percent no-op). These tests lock those guarantees so any future resolver refactor cannot silently break the DoT path. The new tests will pass on first run — that is correct, not a test-design failure.

**Files:**

- Modify: `Assets/Tests/Editor/Platformer/HazardDamageResolverTests.cs`

- [ ] **Step 1: Append four new tests to the existing fixture**

Open `Assets/Tests/Editor/Platformer/HazardDamageResolverTests.cs` and add the following four `[Test]` methods inside the `HazardDamageResolverTests` class, after the existing `Resolve_MaxHpZero_ThrowsArgumentOutOfRange` test:

```csharp
[Test]
public void TickDamage_DrainsHpByPercent()
{
    // Spec: a 10% tick on a 100/100 player should leave them at 90 HP.
    var result = HazardDamageResolver.Resolve(
        currentHp: 100,
        maxHp: 100,
        mode: HazardMode.PercentMaxHpDamage,
        percentMaxHpDamage: 10);

    Assert.AreEqual(90, result.NewHp);
    Assert.IsFalse(result.IsFatal);
}

[Test]
public void TickDamage_ClampsToZero()
{
    // Spec: a tick that would overshoot zero clamps and is fatal.
    var result = HazardDamageResolver.Resolve(
        currentHp: 5,
        maxHp: 100,
        mode: HazardMode.PercentMaxHpDamage,
        percentMaxHpDamage: 10);

    Assert.AreEqual(0, result.NewHp);
    Assert.IsTrue(result.IsFatal);
}

[Test]
public void FirstHitPlusOneTick_KillsAtThreshold()
{
    // Spec: 30 HP player takes a 20% first hit (→10 HP) then a 10% tick (→0 HP, fatal).
    var afterFirstHit = HazardDamageResolver.Resolve(
        currentHp: 30,
        maxHp: 100,
        mode: HazardMode.PercentMaxHpDamage,
        percentMaxHpDamage: 20);

    Assert.AreEqual(10, afterFirstHit.NewHp);
    Assert.IsFalse(afterFirstHit.IsFatal);

    var afterTick = HazardDamageResolver.Resolve(
        currentHp: afterFirstHit.NewHp,
        maxHp: 100,
        mode: HazardMode.PercentMaxHpDamage,
        percentMaxHpDamage: 10);

    Assert.AreEqual(0, afterTick.NewHp);
    Assert.IsTrue(afterTick.IsFatal);
}

[Test]
public void ZeroPercentTick_IsNoOp()
{
    // Spec: a 0% tick must not damage. Supports the "first-hit-only" preset
    // (where _damagePerTickPercent = 0) and the "pure attrition" preset
    // (where _firstHitDamagePercent = 0).
    var result = HazardDamageResolver.Resolve(
        currentHp: 50,
        maxHp: 100,
        mode: HazardMode.PercentMaxHpDamage,
        percentMaxHpDamage: 0);

    Assert.AreEqual(50, result.NewHp);
    Assert.IsFalse(result.IsFatal);
}
```

- [ ] **Step 2: Verify compile in the Editor**

> **Unity Editor task (user):** Return to the Unity Editor, wait for recompile, confirm no compile errors in the Console.

- [ ] **Step 3: Run all HazardDamageResolverTests in Edit Mode**

> **Unity Editor task (user):** Window → General → Test Runner → switch to Edit Mode → expand `Axiom.Platformer.Tests.HazardDamageResolverTests` → click "Run All". Expected: **9 tests pass, 0 fail** (the original 5 plus the 4 new ones).

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message: `test(DEV-46): add tick-damage cases to HazardDamageResolverTests`

- `Assets/Tests/Editor/Platformer/HazardDamageResolverTests.cs`

---

## Task 2: Create PlayerHurtFeedback.cs

**Files:**

- Create: `Assets/Scripts/Platformer/PlayerHurtFeedback.cs`

- [ ] **Step 1: Create the file with the spec contract**

Create `Assets/Scripts/Platformer/PlayerHurtFeedback.cs`:

```csharp
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Lives on the Player root. Plays the Hurt animator trigger on demand,
    /// and applies a sustained sprite tint while one or more spike hazards
    /// overlap the player. The tint is counter-based so that overlapping two
    /// hazards then exiting one does not flicker the tint off — it stays on
    /// until the player has left every overlapping hazard.
    /// </summary>
    public class PlayerHurtFeedback : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Animator on the player whose 'Hurt' trigger plays the playerHurt clip.")]
        private Animator _animator;

        [SerializeField]
        [Tooltip("Sprite renderer that gets tinted while overlapping a spike hazard.")]
        private SpriteRenderer _spriteRenderer;

        [SerializeField]
        [Tooltip("Color applied to the sprite while overlapping at least one spike hazard.")]
        private Color _painTint = new Color(1f, 0.6f, 0.6f, 1f);

        [SerializeField]
        [Tooltip("Animator trigger parameter name fired by spike contact. Must match the parameter you add to Player.controller.")]
        private string _hurtTriggerName = "Hurt";

        private int _overlapCount;
        private Color _restingColor = Color.white;
        private bool _restingColorCaptured;

        private void Awake()
        {
            if (_spriteRenderer != null)
            {
                _restingColor = _spriteRenderer.color;
                _restingColorCaptured = true;
            }
        }

        public void PlayHurtAnimation()
        {
            if (_animator == null) return;
            _animator.SetTrigger(_hurtTriggerName);
        }

        public void BeginPainOverlap()
        {
            _overlapCount++;
            if (_overlapCount == 1 && _spriteRenderer != null)
                _spriteRenderer.color = _painTint;
        }

        public void EndPainOverlap()
        {
            if (_overlapCount > 0)
                _overlapCount--;
            if (_overlapCount == 0 && _spriteRenderer != null && _restingColorCaptured)
                _spriteRenderer.color = _restingColor;
        }
    }
}
```

- [ ] **Step 2: Verify compile in the Editor**

> **Unity Editor task (user):** Return to the Unity Editor, wait for recompile, confirm no compile errors in the Console.

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-46): add PlayerHurtFeedback component`

- `Assets/Scripts/Platformer/PlayerHurtFeedback.cs` + `.meta`

---

## Task 3: Refactor HazardTrigger to first-hit + DoT lifecycle

**Files:**

- Modify: `Assets/Scripts/Platformer/HazardTrigger.cs` (full body rewrite — old `_percentMaxHpDamage` field is removed)

- [ ] **Step 1: Replace the file contents**

Open `Assets/Scripts/Platformer/HazardTrigger.cs`. Replace the entire file with:

```csharp
using Axiom.Core;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Attach to a trigger collider in a level scene. On player contact:
    ///   - InstantKO mode: sets PlayerState.CurrentHp to 0 immediately (pit behavior — unchanged).
    ///   - PercentMaxHpDamage mode: applies _firstHitDamagePercent on entry, then ticks
    ///     _damagePerTickPercent every _tickIntervalSeconds while the player remains overlapping,
    ///     and stops on exit. Notifies the player's PlayerHurtFeedback for animation + tint.
    ///
    /// PlayerDeathHandler observes PlayerState.CurrentHp and dispatches death/respawn —
    /// this component never knows about death.
    ///
    /// Spec: docs/superpowers/specs/2026-04-26-dev-46-spike-hazard-dot-design.md
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HazardTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("InstantKO for pits; PercentMaxHpDamage for spikes.")]
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

        private PlayerHurtFeedback _feedback;
        private float _tickTimer;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[HazardTrigger] GameManager not found — hazard ignored.", this);
                return;
            }

            if (_mode == HazardMode.InstantKO)
            {
                ApplyPercentDamage(0, HazardMode.InstantKO);
                return;
            }

            _feedback = other.GetComponentInParent<PlayerHurtFeedback>();
            ApplyPercentDamage(_firstHitDamagePercent, HazardMode.PercentMaxHpDamage);
            _feedback?.PlayHurtAnimation();
            _feedback?.BeginPainOverlap();
            _tickTimer = 0f;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (_mode == HazardMode.InstantKO)
                return;
            if (!other.CompareTag("Player"))
                return;
            if (GameManager.Instance == null)
                return;

            _tickTimer += Time.deltaTime;
            // While-loop preserves overshoot from a long frame and catches multi-tick frames
            // when _tickIntervalSeconds is shorter than one frame's deltaTime.
            while (_tickTimer >= _tickIntervalSeconds)
            {
                ApplyPercentDamage(_damagePerTickPercent, HazardMode.PercentMaxHpDamage);
                _tickTimer -= _tickIntervalSeconds;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_mode == HazardMode.InstantKO)
                return;
            if (!other.CompareTag("Player"))
                return;

            _feedback?.EndPainOverlap();
            _feedback = null;
        }

        private void ApplyPercentDamage(int percent, HazardMode mode)
        {
            PlayerState state = GameManager.Instance.PlayerState;
            HazardDamageResult result = HazardDamageResolver.Resolve(
                currentHp: state.CurrentHp,
                maxHp: state.MaxHp,
                mode: mode,
                percentMaxHpDamage: percent);
            state.SetCurrentHp(result.NewHp);
        }
    }
}
```

- [ ] **Step 2: Verify compile in the Editor**

> **Unity Editor task (user):** Return to the Unity Editor, wait for recompile, confirm no compile errors in the Console. (PlayerHurtFeedback from Task 2 must already exist or this file will fail to compile.)

- [ ] **Step 3: Re-run the Edit-Mode tests as a regression check**

> **Unity Editor task (user):** Window → General → Test Runner → Edit Mode → Run All. Expected: **9/9 HazardDamageResolverTests pass.** (HazardTrigger has no Edit-Mode tests of its own — the trigger lifecycle is verified in Play Mode in Task 7.)

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `refactor(DEV-46): convert HazardTrigger to first-hit + DoT lifecycle`

- `Assets/Scripts/Platformer/HazardTrigger.cs`

---

## Task 4: Add Hurt animator parameter and Any State transitions

**Files (Editor):**

- Modify: `Assets/Animations/Player/Player.controller`

**Context:** `playerHurtLeft.anim` and `playerHurtRight.anim` already exist in the controller as states (added in DEV-60), but there is no parameter to transition into them and no transitions exist. We add both here.

- [ ] **Step 1: Open the Player.controller**

> **Unity Editor task (user):** Project window → `Assets/Animations/Player/Player.controller` → double-click. The Animator window opens.

- [ ] **Step 2: Add the `Hurt` Trigger parameter**

> **Unity Editor task (user):** In the Animator window's left-hand "Parameters" tab, click **+** → **Trigger** → name it exactly `Hurt` (capital H, matches `PlayerHurtFeedback._hurtTriggerName` default).

- [ ] **Step 3: Add the Any State → `playerHurtLeft` transition**

> **Unity Editor task (user):**
>
> 1. In the Animator graph, right-click on **Any State** → **Make Transition** → click on the `playerHurtLeft` state.
> 2. Select the new transition arrow (it appears as a thin line from Any State to playerHurtLeft).
> 3. In the Inspector:
>    - **Has Exit Time:** unchecked (we want the trigger to fire immediately).
>    - **Settings → Transition Duration:** `0.05` (very short crossfade).
>    - **Settings → Can Transition To Self:** **UNCHECKED.** *Critical:* if checked, the Hurt animation will restart every frame the trigger is set, per project memory `feedback_animator_any_state.md`.
>    - **Conditions:** add two conditions:
>      - `Hurt` (no value to set — it's a Trigger)
>      - `IsFacingRight` `false`

- [ ] **Step 4: Add the Any State → `playerHurtRight` transition**

> **Unity Editor task (user):** Repeat Step 3 but target `playerHurtRight` and use:
>
> - **Conditions:**
>   - `Hurt`
>   - `IsFacingRight` `true`
> - All other settings identical (Has Exit Time unchecked, Transition Duration 0.05, **Can Transition To Self unchecked**).

- [ ] **Step 5: Add transitions back from playerHurtLeft / playerHurtRight to the Grounded blend trees on exit**

> **Unity Editor task (user):**
>
> 1. Right-click `playerHurtLeft` → **Make Transition** → click `Grounded_L` (the existing blend tree). In the Inspector:
>    - **Has Exit Time:** checked.
>    - **Exit Time:** `0.95` (return to Grounded after the Hurt clip is 95% done).
>    - **Transition Duration:** `0.05`.
>    - **Conditions:** none.
> 2. Repeat from `playerHurtRight` → `Grounded_R` with identical settings.
>
> *Why exit back to Grounded specifically:* the existing controller's blend trees (`Grounded_L` / `Grounded_R`) handle idle/walk/jump-fall internally based on `IsGrounded` / `VelocityY` — returning to Grounded gives the controller back to the existing logic without duplicating jump-fall transitions out of Hurt.

- [ ] **Step 6: Save the controller**

> **Unity Editor task (user):** Ctrl/Cmd+S to save. Close the Animator window.

- [ ] **Step 7: Smoke-test in Play Mode (no spike contact yet)**

> **Unity Editor task (user):** Open `Assets/Scenes/Level_1-1.unity`. Enter Play Mode. Open the Animator window with the Player selected. In the Parameters tab, manually click the **Hurt** trigger. Expected: the Player's sprite plays the playerHurt clip once (left- or right-facing matching `IsFacingRight`), then returns to Grounded. Exit Play Mode.

- [ ] **Step 8: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message: `feat(DEV-46): add Hurt trigger and Any State transitions to Player.controller`

- `Assets/Animations/Player/Player.controller`

---

## Task 5: Add PlayerHurtFeedback to the Player prefab

**Files (Editor):**

- Modify: the Player prefab. The exact path needs confirming — it is referenced by `Level_1-1.unity` and is the same Player used in the existing Platformer scene. If it lives at `Assets/Prefabs/Player/...`, modify there; if it is a scene-only object in `Level_1-1.unity`, modify the scene instance instead.

- [ ] **Step 1: Locate the Player prefab (or scene-instance) source-of-truth**

> **Unity Editor task (user):** Open `Assets/Scenes/Level_1-1.unity`. In the Hierarchy, find the `Player` GameObject. Look at the Inspector — the GameObject name will be **blue** if it is a prefab instance (in which case click "Open" or check the prefab path under the name). If it is **white**, it is a scene-only object. Note which is the source of truth so the modification in Step 2 happens in the right place.

- [ ] **Step 2: Add the PlayerHurtFeedback component to the Player root**

> **Unity Editor task (user):**
>
> - If prefab: open the prefab in Prefab Mode (double-click in Project window). Select the root GameObject. Click **Add Component** → search "PlayerHurtFeedback" → add it.
> - If scene-only: select the Player GameObject in the Hierarchy. Click **Add Component** → search "PlayerHurtFeedback" → add it.

- [ ] **Step 3: Wire the SerializeField references**

> **Unity Editor task (user):** On the new `Player Hurt Feedback` Inspector section:
>
> - **Animator:** drag the Player's child GameObject that holds the Animator component (this is the same child that holds `PlayerExplorationAnimator` per its existing setup) into the `_animator` slot.
> - **Sprite Renderer:** drag the Player's child GameObject that holds the SpriteRenderer into the `_spriteRenderer` slot. (Likely the same child as the Animator — confirm by selecting it.)
> - **Pain Tint:** leave default `(1.0, 0.6, 0.6, 1.0)` — a soft red. Adjust later if it reads too pink.
> - **Hurt Trigger Name:** leave default `Hurt` (matches the parameter added in Task 4 Step 2).

- [ ] **Step 4: Save (and apply if prefab)**

> **Unity Editor task (user):** Ctrl/Cmd+S. If editing a prefab, also exit Prefab Mode — overrides apply automatically.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message: `feat(DEV-46): wire PlayerHurtFeedback onto Player`

- The Player prefab `.prefab` file (path determined in Step 1) **OR** `Assets/Scenes/Level_1-1.unity` if scene-only.

---

## Task 6: Verify existing Level_1-1 HazardTriggers + add a spike test cluster

**Files (Editor):**

- Modify: `Assets/Scenes/Level_1-1.unity`

**Context:** The existing 2 HazardTrigger instances in Level_1-1 are pit hazards (`_mode = InstantKO`). The field-shape change of HazardTrigger means the Inspector now shows three new fields with default values, but those fields are ignored when mode is InstantKO. We confirm pits still behave as InstantKO, then optionally add a single test spike cluster so Task 7's Play-Mode verification has something to land on.

- [ ] **Step 1: Verify the 2 existing pit HazardTriggers still read `_mode = InstantKO`**

> **Unity Editor task (user):** Open `Assets/Scenes/Level_1-1.unity`. In the Hierarchy, find each HazardTrigger GameObject (search "HazardTrigger" in the Hierarchy search bar, or look under the `Pit_Hazards` parent if it exists per the DEV-46 plan Step 1497). Click each one and confirm:
>
> - **Mode:** still `InstantKO` (was carried over by Unity's serialization — the field name `_mode` and its enum values are unchanged).
> - The three new fields (`First Hit Damage Percent`, `Damage Per Tick Percent`, `Tick Interval Seconds`) appear in the Inspector with their default values (20 / 10 / 0.5). These are **ignored** in InstantKO mode — leave at defaults.

- [ ] **Step 2: Add a single test spike cluster for Task 7 verification**

> **Unity Editor task (user):** In `Level_1-1.unity`:
>
> 1. Choose a flat ground area near the start checkpoint where the player can comfortably walk onto and off of it (we want recoverable spikes for testing, not a death trap).
> 2. Create a new empty GameObject named `Spike_Test_Cluster_01` parented under a `Hazards` parent (create the parent if it does not exist).
> 3. Add a `BoxCollider2D` component:
>    - **Is Trigger:** checked.
>    - **Size:** roughly 3 tiles wide × 0.5 tiles tall, sitting on top of the ground tiles where you want spikes.
>    - **Offset:** adjust so the box sits at floor level.
> 4. Add a `HazardTrigger` component. Configure with the **Tutorial gentle preset** from the spec:
>    - **Mode:** `PercentMaxHpDamage`
>    - **First Hit Damage Percent:** `20`
>    - **Damage Per Tick Percent:** `10`
>    - **Tick Interval Seconds:** `0.5`

> *Visual note:* you can leave this without spike sprites for testing — the trigger volume is what matters for gameplay. Add visual spike tiles later when authoring final levels per the DEV-46 plan Step 1507.

- [ ] **Step 3: Save the scene**

> **Unity Editor task (user):** Ctrl/Cmd+S.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message: `feat(DEV-46): add test spike cluster to Level_1-1 for DoT verification`

- `Assets/Scenes/Level_1-1.unity`

---

## Task 7: Play Mode verification

**Files:** None modified — pure verification pass. Document any issues found and loop back to fix the relevant earlier task.

**Context:** The trigger lifecycle, sprite tint, animator firing, and overlap counter are all Play-Mode-only behaviors per the spec's "Not unit-tested (deliberate)" list. This task confirms they all work end-to-end.

**Pre-flight:**

- The HUD (`PlatformerHpHudUI`) must be wired into Level_1-1's Canvas to see HP changes visually. If it is not, add it: a TextMeshProUGUI element on the HUD Canvas, then add a `PlatformerHpHudUI` component on the same GameObject and drag the TMP element into its `_hpLabel` slot. (This is technically out of scope for the spike-DoT spec but blocks visual verification — flagging it inline.)
- `GameManager` must be present in the scene with `PlayerState` initialized. If `[HazardTrigger] GameManager not found` warnings fire on first contact, the GameManager is missing — see DEV-46 plan Task 6.

- [ ] **Step 1: Enter Play Mode in Level_1-1**

> **Unity Editor task (user):** Open `Assets/Scenes/Level_1-1.unity`. Press Play. Walk the player up to the test spike cluster from Task 6.

- [ ] **Step 2: Verify first-hit + tint + animation on contact**

> **Unity Editor task (user):** Walk the player into the test spike cluster (do not jump off immediately). Expected, all simultaneously:
>
> - HUD HP drops by 20 (e.g., 100 → 80) at the moment of contact.
> - Player sprite tints to soft red and stays red while overlapping.
> - `playerHurtLeft` or `playerHurtRight` clip plays once on contact (matching facing direction). Confirm in the Animator window with the Player selected.

- [ ] **Step 3: Verify ticks while standing still**

> **Unity Editor task (user):** Stop input — let the player stand on the spikes. Expected:
>
> - HUD HP drops by 10 every 0.5 seconds (i.e., 80 → 70 → 60 → 50 …).
> - Sprite tint stays red the entire time.
> - The `playerHurt` animation does NOT replay on every tick (it should only have played on first contact).

- [ ] **Step 4: Verify recovery — walk off the spikes mid-DoT**

> **Unity Editor task (user):** Move the player back onto safe ground before HP reaches 0. Expected:
>
> - Sprite tint clears immediately (back to normal color).
> - HP stops dropping.
> - Player can walk and jump normally — no stickiness, no animation lock.

- [ ] **Step 5: Verify re-entry costs the first-hit again**

> **Unity Editor task (user):** Walk back onto the spikes. Expected: HUD HP drops by another 20 immediately on re-entry, then ticks resume as before. (Per the spec edge case 7 — re-entry is intended to sting.)

- [ ] **Step 6: Verify death from DoT routes to respawn**

> **Unity Editor task (user):** Reset (Stop → Play). First, briefly touch the start checkpoint trigger so respawn is enabled. Then walk onto the spikes and stand still until HP reaches 0. Expected:
>
> - Scene fades to white (or whatever `TransitionStyle` the `PlayerDeathHandler` uses) and reloads with player at the checkpoint at full HP.
> - No errors or warnings in the Console.

- [ ] **Step 7: Verify pit hazards are still instant-KO**

> **Unity Editor task (user):** Walk into one of the existing pit HazardTriggers. Expected: instant respawn at checkpoint (no DoT, no first-hit math) — pit behavior unchanged.

- [ ] **Step 8: Verify overlap-counter behavior (optional, only if you authored two overlapping spike clusters)**

> **Unity Editor task (user):** This is only relevant if you placed two overlapping spike triggers. Expected: tint stays red as long as the player overlaps at least one trigger, and clears only when the player has left both. Skip this step if you have only the single test cluster from Task 6.

- [ ] **Step 9: Document issues and fix**

If any step above fails, capture which one and the symptom, then loop back to the relevant task:

| Symptom | Likely task to revisit |
|---|---|
| Sprite does not tint | Task 5 Step 3 — `_spriteRenderer` not wired |
| `playerHurt` clip does not play | Task 4 (parameter or transition setup) or Task 5 Step 3 (`_animator` not wired) |
| HP does not change | Task 3 (HazardTrigger code) or HUD not wired (pre-flight) |
| Pit hazard now does DoT | Task 3 — `_mode == InstantKO` short-circuit broken |
| `playerHurt` re-fires every tick | Task 4 Step 3/4 — "Can Transition To Self" was left checked |
| Animation locks the player on spikes | Task 4 Step 5 — exit transition from playerHurtLeft/Right back to Grounded missing |

After the fix, return to Task 7 Step 1 and re-verify the full checklist.

- [ ] **Step 10: Final UVCS check-in if any scene/prefab tweaks were made during verification**

Unity Version Control → Pending Changes → if there are any modified files, stage them → Check in with message describing the fix, e.g. `fix(DEV-46): reset Animator Can Transition To Self on Hurt Any State transitions`

If no changes were needed, skip this step.

---

## Done

When all of Task 7 passes:

1. Spike hazards behave per the spec (first-hit + DoT + tint + animation + recovery + death routing).
2. Pit hazards are unchanged.
3. The `PlayerHurtFeedback` contract is reusable for future hazard types (lava, acid) without further surgery.
4. The DEV-46 parent plan (Task 5 in `2026-04-20-dev-46-level-1-snow-mountain.md`) is now fully implemented for the hazard system.

Loop back to the DEV-46 parent plan to continue with the next pending task.
