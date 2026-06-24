# Platformer Spell Feedback (Proximity Cue + Cast Animation) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. For this Unity project, also use `executing-unity-game-dev-plans` (Unity Editor handoffs, UVCS check-ins, Test Runner verification). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show an "aura" proximity cue behind the player when near a spell-interactable puzzle, and play the existing cast animation so the puzzle resolves *after* the animation's fire-frame.

**Architecture:** A non-mutating `HasResolvableTarget` query gates a deferred cast. A plain-C# `PlatformerCastSequencer` triggers the player cast animation and resolves the world effect exactly once on the animation's fire-frame (Unity Animation Event) or a timeout — mirroring the shipped Battle pattern. The aura is a child SpriteRenderer driven by `PlayerAuraCue` (visuals) over `AuraVisibilityState` (logic); the existing per-obstacle proximity forwarders notify it, keeping the cue in lockstep with castability.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, C#, Unity Input System, Unity Test Framework (EditMode), UVCS (Unity Version Control).

## Global Constraints

- MonoBehaviours handle Unity lifecycle only; all logic lives in plain C# classes injected into them.
- No static singletons except `GameManager`.
- No premature abstraction; match existing conventions; delete dead code (never comment out).
- Player scripts (`PlayerController`, `PlayerAnimator`, `PlayerMovement`, `PlayerExplorationAnimator`) use the **global namespace** in the `Axiom.Platformer` assembly. Obstacle/puzzle scripts and interfaces use `namespace Axiom.Platformer`. Voice scripts use `namespace Axiom.Voice`.
- Cast animation plays **only** on a valid puzzle-resolving cast (in-range matching puzzle + enough MP). Casting otherwise does nothing.
- Tests are **EditMode** (plain C#), placed in the existing `VoiceTests` / `PlatformerTests` asmdefs — **no new test asmdef**.
- Commit format: `<type>(DEV-118): <desc>`. **UVCS only — never git.**
- Claude writes all `.cs`; the **user** performs all Unity Editor actions (scene/animator/sprite/prefab wiring) and runs the Test Runner.

## Scope (DEV-118)

In scope (2 of 8 ACs): proximity visual cue; player cast animation. Out of scope (deferred follow-up): mana-fail feedback, mana-consume emphasis, floating-number reuse, mana HUD, "cast animation on any recognized spell."

## Known Limitations (intentional, surfaced)

- The aura reflects proximity to *interactable* puzzles (hidden once a puzzle is solved via `ISpellPuzzle.IsInteractable`), but a puzzle that is solved while the player stays inside its zone relies on the player later exiting (or the obstacle disabling) to drop from the in-range set — the `IsInteractable` check hides it immediately regardless, so this is cosmetic only.
- No guard against casting while movement is already locked by another system (attack/tutorial/transition). Voice-cast + melee-attack overlap is rare; the cast path may briefly unlock movement at its fire-frame. Acceptable for this scope.

## File Structure

| File | Responsibility | New/Modified |
|---|---|---|
| `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs` | Add `HasResolvableTarget` query; `TryCast` reuses it (resolve step) | Modified |
| `Assets/Scripts/Voice/PlatformerCastSequencer.cs` | Deferred single-cast lifecycle (trigger → resolve once) | **New** |
| `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs` | Validate → request deferred cast; wire sequencer + player + timeout | Modified |
| `Assets/Scripts/Platformer/ISpellPuzzle.cs` | `IsInteractable` contract for the aura | **New** |
| `Assets/Scripts/Platformer/AuraVisibilityState.cs` | Pure aura visibility logic | **New** |
| `Assets/Scripts/Platformer/PlayerAuraCue.cs` | Aura SpriteRenderer + frame cycling (MonoBehaviour) | **New** |
| `Assets/Scripts/Platformer/{Meltable,Burnable,Freezable,SteamVent,AcidPuddle}*Controller.cs` | Implement `ISpellPuzzle` | Modified (×5) |
| `Assets/Scripts/Platformer/{Meltable,Burnable,Freezable,SteamVent,AcidPuddle}*ProximityForwarder.cs` | Notify `PlayerAuraCue` on enter/exit/disable | Modified (×5) |
| `Assets/Scripts/Platformer/PlayerAnimator.cs` | `TriggerCast()` | Modified |
| `Assets/Scripts/Platformer/PlayerExplorationAnimator.cs` | `AnimEvent_OnSpellFire()` | Modified |
| `Assets/Scripts/Platformer/PlayerController.cs` | `BeginCast()`, `OnSpellCastFireFrame()`, `SpellCastFired` event | Modified |
| `Assets/Tests/Editor/Voice/PlatformerSpellWorldCasterTests.cs` | Guard-path tests for `HasResolvableTarget` | **New** |
| `Assets/Tests/Editor/Voice/PlatformerCastSequencerTests.cs` | Sequencer behavioral tests | **New** |
| `Assets/Tests/Editor/Platformer/AuraVisibilityStateTests.cs` | Aura visibility tests | **New** |
| `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs` | Adapt 5 existing tests to the deferred fire-frame flow | Modified |

---

## Task 1: `HasResolvableTarget` query in `PlatformerSpellWorldCaster`

**Files:**
- Modify: `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs`
- Test: `Assets/Tests/Editor/Voice/PlatformerSpellWorldCasterTests.cs`

**Interfaces:**
- Produces: `static bool PlatformerSpellWorldCaster.HasResolvableTarget(SpellData spell, IReadOnlyList<MeltableObstacleController>, IReadOnlyList<FreezablePlatformController>, IReadOnlyList<BurnableObstacleController>, IReadOnlyList<SteamVentController>, IReadOnlyList<AcidPuddleController>)` — `true` iff at least one in-range obstacle accepts `spell`; **no mutation**. `TryCast` keeps its existing signature and is reused as the fire-frame resolve step.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/Editor/Voice/PlatformerSpellWorldCasterTests.cs`:

```csharp
using System;
using Axiom.Data;
using Axiom.Platformer;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Voice.Tests
{
    public class PlatformerSpellWorldCasterTests
    {
        private SpellData MakeSpell(string name)
        {
            SpellData spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = name;
            return spell;
        }

        [Test]
        public void HasResolvableTarget_NullSpell_ReturnsFalse()
        {
            bool result = PlatformerSpellWorldCaster.HasResolvableTarget(
                null,
                Array.Empty<MeltableObstacleController>(),
                Array.Empty<FreezablePlatformController>(),
                Array.Empty<BurnableObstacleController>(),
                Array.Empty<SteamVentController>(),
                Array.Empty<AcidPuddleController>());

            Assert.IsFalse(result);
        }

        [Test]
        public void HasResolvableTarget_AllListsNull_ReturnsFalse()
        {
            SpellData spell = MakeSpell("melt");

            bool result = PlatformerSpellWorldCaster.HasResolvableTarget(
                spell, null, null, null, null, null);

            Assert.IsFalse(result, "An empty world must never report a castable target.");
        }

        [Test]
        public void HasResolvableTarget_AllListsEmpty_ReturnsFalse()
        {
            SpellData spell = MakeSpell("melt");

            bool result = PlatformerSpellWorldCaster.HasResolvableTarget(
                spell,
                Array.Empty<MeltableObstacleController>(),
                Array.Empty<FreezablePlatformController>(),
                Array.Empty<BurnableObstacleController>(),
                Array.Empty<SteamVentController>(),
                Array.Empty<AcidPuddleController>());

            Assert.IsFalse(result);
        }
    }
}
```

> **Why these tests:** they encode the AC intent — casting with nothing in range must produce *no* castable target, so the cast animation never plays "at nothing." The positive (obstacle-accepts-spell) path requires live MonoBehaviour controllers with private state and is verified in the Play Mode pass (Task 8), matching how the existing `TryCast` orchestrator is verified.

- [ ] **Step 2: Add the `HasResolvableTarget` method**

In `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs`, add this method **above** `TryCast` (inside the class):

```csharp
public static bool HasResolvableTarget(
    SpellData spell,
    IReadOnlyList<MeltableObstacleController> meltableObstacles,
    IReadOnlyList<FreezablePlatformController> freezablePlatforms,
    IReadOnlyList<BurnableObstacleController> burnableObstacles,
    IReadOnlyList<SteamVentController> steamVents,
    IReadOnlyList<AcidPuddleController> acidPuddles)
{
    if (spell == null || string.IsNullOrWhiteSpace(spell.spellName)) return false;

    if (meltableObstacles != null)
        for (int i = 0; i < meltableObstacles.Count; i++)
        {
            MeltableObstacleController obstacle = meltableObstacles[i];
            if (obstacle != null && obstacle.CanMeltWith(spell.spellName)) return true;
        }

    if (freezablePlatforms != null)
        for (int i = 0; i < freezablePlatforms.Count; i++)
        {
            FreezablePlatformController platform = freezablePlatforms[i];
            if (platform != null && platform.CanFreezeWith(spell.spellName)) return true;
        }

    if (burnableObstacles != null)
        for (int i = 0; i < burnableObstacles.Count; i++)
        {
            BurnableObstacleController obstacle = burnableObstacles[i];
            if (obstacle != null && obstacle.CanIgniteWith(spell.spellName)) return true;
        }

    if (steamVents != null)
        for (int i = 0; i < steamVents.Count; i++)
        {
            SteamVentController vent = steamVents[i];
            if (vent != null && vent.CanIgniteWith(spell.spellName)) return true;
        }

    if (acidPuddles != null)
        for (int i = 0; i < acidPuddles.Count; i++)
        {
            AcidPuddleController puddle = acidPuddles[i];
            if (puddle != null && puddle.CanNeutralizeWith(spell.spellName)) return true;
        }

    return false;
}
```

- [ ] **Step 3: Refactor `TryCast` to reuse the query (DRY)**

Replace the body of `TryCast` from its first guard down to (and including) the `if (!hasWorldTarget) return false;` line — i.e. replace the original lines 19–88 — with:

```csharp
            if (!HasResolvableTarget(spell, meltableObstacles, freezablePlatforms,
                    burnableObstacles, steamVents, acidPuddles))
                return false;
            if (playerState == null) return false;
            if (!playerState.TrySpendMp(spell.mpCost)) return false;
```

Leave the `bool handled = false;` block and all five `Try*` resolution loops (original lines 91–142) unchanged. (Guard order: the spell/target check via `HasResolvableTarget` runs before the `playerState` guard — an empty world short-circuits without needing `playerState`, preserving the original "no target → no spend" invariant.)

- [ ] **Step 4: Run the tests in the Unity Test Runner**

> **Unity Editor task (user):** Window → General → Test Runner → EditMode → run `PlatformerSpellWorldCasterTests`.
> Expected: all 3 tests PASS; no compile errors in the Console.

- [ ] **Step 5: Check in via UVCS**

> **Unity Editor task (user):** Unity Version Control → Pending Changes → stage the files below → Check in with message: `refactor(DEV-118): extract HasResolvableTarget query for deferred cast`
> - `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs`
> - `Assets/Tests/Editor/Voice/PlatformerSpellWorldCasterTests.cs`
> - `Assets/Tests/Editor/Voice/PlatformerSpellWorldCasterTests.cs.meta`

---

## Task 2: `PlatformerCastSequencer` (deferred single-cast lifecycle)

**Files:**
- Create: `Assets/Scripts/Voice/PlatformerCastSequencer.cs`
- Test: `Assets/Tests/Editor/Voice/PlatformerCastSequencerTests.cs`

**Interfaces:**
- Consumes: `SpellData` (Axiom.Data).
- Produces: `PlatformerCastSequencer(Action<SpellData> beginCast, Action<SpellData> resolve, Action endCast)`; `bool RequestCast(SpellData spell)`; `void NotifyFireFrame()`; `bool IsCasting { get; }`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Voice/PlatformerCastSequencerTests.cs`:

```csharp
using System;
using Axiom.Data;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Voice.Tests
{
    public class PlatformerCastSequencerTests
    {
        private SpellData MakeSpell(string name = "melt")
        {
            SpellData spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = name;
            return spell;
        }

        [Test]
        public void RequestCast_WhenIdle_BeginsCastWithSpell()
        {
            SpellData began = null;
            var seq = new PlatformerCastSequencer(s => began = s, _ => { }, () => { });
            SpellData spell = MakeSpell();

            bool started = seq.RequestCast(spell);

            Assert.IsTrue(started);
            Assert.AreSame(spell, began);
            Assert.IsTrue(seq.IsCasting);
        }

        [Test]
        public void RequestCast_NullSpell_ReturnsFalseAndDoesNotBegin()
        {
            int beginCount = 0;
            var seq = new PlatformerCastSequencer(_ => beginCount++, _ => { }, () => { });

            bool started = seq.RequestCast(null);

            Assert.IsFalse(started);
            Assert.AreEqual(0, beginCount);
            Assert.IsFalse(seq.IsCasting);
        }

        [Test]
        public void RequestCast_WhileCasting_IsIgnored()
        {
            int beginCount = 0;
            var seq = new PlatformerCastSequencer(_ => beginCount++, _ => { }, () => { });

            seq.RequestCast(MakeSpell());
            bool secondStarted = seq.RequestCast(MakeSpell());

            Assert.IsFalse(secondStarted);
            Assert.AreEqual(1, beginCount);
        }

        [Test]
        public void NotifyFireFrame_AfterRequest_ResolvesOnceThenEnds()
        {
            int resolveCount = 0;
            int endCount = 0;
            SpellData resolved = null;
            var seq = new PlatformerCastSequencer(
                _ => { },
                s => { resolved = s; resolveCount++; },
                () => endCount++);
            SpellData spell = MakeSpell();

            seq.RequestCast(spell);
            seq.NotifyFireFrame();

            Assert.AreEqual(1, resolveCount);
            Assert.AreEqual(1, endCount);
            Assert.AreSame(spell, resolved);
            Assert.IsFalse(seq.IsCasting);
        }

        [Test]
        public void NotifyFireFrame_CalledTwice_ResolvesOnlyOnce()
        {
            int resolveCount = 0;
            var seq = new PlatformerCastSequencer(_ => { }, _ => resolveCount++, () => { });

            seq.RequestCast(MakeSpell());
            seq.NotifyFireFrame(); // animation event
            seq.NotifyFireFrame(); // timeout fallback fires too

            Assert.AreEqual(1, resolveCount);
        }

        [Test]
        public void NotifyFireFrame_WithoutRequest_DoesNothing()
        {
            int resolveCount = 0;
            var seq = new PlatformerCastSequencer(_ => { }, _ => resolveCount++, () => { });

            seq.NotifyFireFrame();

            Assert.AreEqual(0, resolveCount);
        }

        [Test]
        public void Sequencer_CanCastAgainAfterResolving()
        {
            int resolveCount = 0;
            var seq = new PlatformerCastSequencer(_ => { }, _ => resolveCount++, () => { });

            seq.RequestCast(MakeSpell());
            seq.NotifyFireFrame();
            seq.RequestCast(MakeSpell());
            seq.NotifyFireFrame();

            Assert.AreEqual(2, resolveCount);
        }

        [Test]
        public void Constructor_NullCallback_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new PlatformerCastSequencer(null, _ => { }, () => { }));
            Assert.Throws<ArgumentNullException>(
                () => new PlatformerCastSequencer(_ => { }, null, () => { }));
            Assert.Throws<ArgumentNullException>(
                () => new PlatformerCastSequencer(_ => { }, _ => { }, null));
        }
    }
}
```

- [ ] **Step 2: Create a compiling stub so the tests fail at runtime (red)**

Create `Assets/Scripts/Voice/PlatformerCastSequencer.cs`:

```csharp
using System;
using Axiom.Data;

namespace Axiom.Voice
{
    public class PlatformerCastSequencer
    {
        public PlatformerCastSequencer(
            Action<SpellData> beginCast, Action<SpellData> resolve, Action endCast) { }

        public bool IsCasting => false;
        public bool RequestCast(SpellData spell) => false;
        public void NotifyFireFrame() { }
    }
}
```

- [ ] **Step 3: Run the tests — verify they FAIL**

> **Unity Editor task (user):** Test Runner → EditMode → run `PlatformerCastSequencerTests`.
> Expected: FAIL (stub returns false / never resolves; constructor does not throw).

- [ ] **Step 4: Implement the real sequencer**

Replace the entire file contents of `Assets/Scripts/Voice/PlatformerCastSequencer.cs` with:

```csharp
using System;
using Axiom.Data;

namespace Axiom.Voice
{
    /// <summary>
    /// Plain C# — sequences one deferred platformer spell cast: trigger the cast animation,
    /// then resolve the world effect exactly once on the animation's fire-frame (or a timeout),
    /// never twice. The MonoBehaviour seam supplies the callbacks; logic lives here for EditMode tests.
    /// </summary>
    public class PlatformerCastSequencer
    {
        private readonly Action<SpellData> _beginCast;
        private readonly Action<SpellData> _resolve;
        private readonly Action _endCast;

        private SpellData _pendingSpell;

        public bool IsCasting => _pendingSpell != null;

        public PlatformerCastSequencer(
            Action<SpellData> beginCast,
            Action<SpellData> resolve,
            Action endCast)
        {
            _beginCast = beginCast ?? throw new ArgumentNullException(nameof(beginCast));
            _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
            _endCast = endCast ?? throw new ArgumentNullException(nameof(endCast));
        }

        /// <summary>Starts a cast. No-op (false) if a cast is already in flight or spell is null.</summary>
        public bool RequestCast(SpellData spell)
        {
            if (spell == null) return false;
            if (_pendingSpell != null) return false;

            _pendingSpell = spell;
            _beginCast(spell);
            return true;
        }

        /// <summary>
        /// Called by the cast animation's fire-frame event AND by the timeout fallback.
        /// Resolves the pending cast exactly once; later calls are ignored until the next RequestCast.
        /// </summary>
        public void NotifyFireFrame()
        {
            if (_pendingSpell == null) return;

            SpellData spell = _pendingSpell;
            _pendingSpell = null;
            _resolve(spell);
            _endCast();
        }
    }
}
```

- [ ] **Step 5: Run the tests — verify they PASS**

> **Unity Editor task (user):** Test Runner → EditMode → run `PlatformerCastSequencerTests`.
> Expected: all 8 tests PASS.

- [ ] **Step 6: Check in via UVCS**

> **Unity Editor task (user):** Unity Version Control → Pending Changes → stage the files below → Check in: `feat(DEV-118): add PlatformerCastSequencer for deferred casts`
> - `Assets/Scripts/Voice/PlatformerCastSequencer.cs`
> - `Assets/Scripts/Voice/PlatformerCastSequencer.cs.meta`
> - `Assets/Tests/Editor/Voice/PlatformerCastSequencerTests.cs`
> - `Assets/Tests/Editor/Voice/PlatformerCastSequencerTests.cs.meta`

---

## Task 3: `ISpellPuzzle` + `AuraVisibilityState`

**Files:**
- Create: `Assets/Scripts/Platformer/ISpellPuzzle.cs`
- Create: `Assets/Scripts/Platformer/AuraVisibilityState.cs`
- Test: `Assets/Tests/Editor/Platformer/AuraVisibilityStateTests.cs`

**Interfaces:**
- Produces: `Axiom.Platformer.ISpellPuzzle { bool IsInteractable { get; } }`; `AuraVisibilityState` (global namespace) with `bool IsVisible { get; }`, `void Enter(ISpellPuzzle)`, `void Exit(ISpellPuzzle)`, `void SetSuppressed(bool)`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Platformer/AuraVisibilityStateTests.cs`:

```csharp
using NUnit.Framework;

namespace Axiom.Platformer.Tests
{
    public class AuraVisibilityStateTests
    {
        private sealed class FakePuzzle : ISpellPuzzle
        {
            public bool IsInteractable { get; set; } = true;
        }

        [Test]
        public void NewState_IsHidden()
        {
            var state = new AuraVisibilityState();
            Assert.IsFalse(state.IsVisible);
        }

        [Test]
        public void Enter_InteractablePuzzle_BecomesVisible()
        {
            var state = new AuraVisibilityState();
            state.Enter(new FakePuzzle());
            Assert.IsTrue(state.IsVisible);
        }

        [Test]
        public void Exit_LastPuzzle_Hides()
        {
            var state = new AuraVisibilityState();
            var puzzle = new FakePuzzle();
            state.Enter(puzzle);
            state.Exit(puzzle);
            Assert.IsFalse(state.IsVisible);
        }

        [Test]
        public void TwoPuzzles_ExitOne_StaysVisible()
        {
            var state = new AuraVisibilityState();
            var a = new FakePuzzle();
            var b = new FakePuzzle();
            state.Enter(a);
            state.Enter(b);
            state.Exit(a);
            Assert.IsTrue(state.IsVisible);
        }

        [Test]
        public void SolvedPuzzleInRange_IsNotVisible()
        {
            var state = new AuraVisibilityState();
            state.Enter(new FakePuzzle { IsInteractable = false });
            Assert.IsFalse(state.IsVisible, "A solved (non-interactable) puzzle must not show the cue.");
        }

        [Test]
        public void MixSolvedAndInteractable_IsVisible()
        {
            var state = new AuraVisibilityState();
            state.Enter(new FakePuzzle { IsInteractable = false });
            state.Enter(new FakePuzzle { IsInteractable = true });
            Assert.IsTrue(state.IsVisible);
        }

        [Test]
        public void Suppressed_HidesEvenWithInteractableInRange()
        {
            var state = new AuraVisibilityState();
            state.Enter(new FakePuzzle());
            state.SetSuppressed(true);
            Assert.IsFalse(state.IsVisible);
        }

        [Test]
        public void Unsuppress_RestoresVisibility()
        {
            var state = new AuraVisibilityState();
            state.Enter(new FakePuzzle());
            state.SetSuppressed(true);
            state.SetSuppressed(false);
            Assert.IsTrue(state.IsVisible);
        }

        [Test]
        public void EnterAndExitNull_AreIgnored()
        {
            var state = new AuraVisibilityState();
            state.Enter(null);
            Assert.IsFalse(state.IsVisible);
            state.Exit(null); // must not throw
        }

        [Test]
        public void EnterSamePuzzleTwice_ExitOnce_Hides()
        {
            var state = new AuraVisibilityState();
            var puzzle = new FakePuzzle();
            state.Enter(puzzle);
            state.Enter(puzzle);
            state.Exit(puzzle);
            Assert.IsFalse(state.IsVisible, "HashSet identity — a single Exit clears the entry.");
        }
    }
}
```

- [ ] **Step 2: Create the interface**

Create `Assets/Scripts/Platformer/ISpellPuzzle.cs`:

```csharp
namespace Axiom.Platformer
{
    /// <summary>
    /// A spell-interactable environmental puzzle. The player's aura cue uses this to show the
    /// proximity cue only while the nearby puzzle can still be acted on (hides once solved).
    /// </summary>
    public interface ISpellPuzzle
    {
        /// <summary>True while the puzzle can still be progressed by a spell; false once solved/consumed.</summary>
        bool IsInteractable { get; }
    }
}
```

- [ ] **Step 3: Create a compiling stub so the tests fail (red)**

Create `Assets/Scripts/Platformer/AuraVisibilityState.cs`:

```csharp
using Axiom.Platformer;

public class AuraVisibilityState
{
    public bool IsVisible => false;
    public void Enter(ISpellPuzzle puzzle) { }
    public void Exit(ISpellPuzzle puzzle) { }
    public void SetSuppressed(bool suppressed) { }
}
```

- [ ] **Step 4: Run the tests — verify they FAIL**

> **Unity Editor task (user):** Test Runner → EditMode → run `AuraVisibilityStateTests`.
> Expected: FAIL (stub `IsVisible` is always false).

- [ ] **Step 5: Implement the real logic**

Replace the entire file contents of `Assets/Scripts/Platformer/AuraVisibilityState.cs` with:

```csharp
using System.Collections.Generic;
using Axiom.Platformer;

/// <summary>
/// Plain C# — computes whether the player's proximity aura should be visible: visible when the
/// player is inside the proximity zone of at least one still-interactable puzzle AND the cue is
/// not suppressed (e.g. mid-cast). The MonoBehaviour wrapper (PlayerAuraCue) owns the visuals.
/// </summary>
public class AuraVisibilityState
{
    private readonly HashSet<ISpellPuzzle> _inRange = new();
    private bool _suppressed;

    public bool IsVisible => !_suppressed && AnyInteractableInRange();

    public void Enter(ISpellPuzzle puzzle)
    {
        if (puzzle != null) _inRange.Add(puzzle);
    }

    public void Exit(ISpellPuzzle puzzle)
    {
        if (puzzle != null) _inRange.Remove(puzzle);
    }

    public void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
    }

    private bool AnyInteractableInRange()
    {
        foreach (ISpellPuzzle puzzle in _inRange)
        {
            if (puzzle != null && puzzle.IsInteractable) return true;
        }
        return false;
    }
}
```

- [ ] **Step 6: Run the tests — verify they PASS**

> **Unity Editor task (user):** Test Runner → EditMode → run `AuraVisibilityStateTests`.
> Expected: all 10 tests PASS.

- [ ] **Step 7: Check in via UVCS**

> **Unity Editor task (user):** Unity Version Control → Pending Changes → stage the files below → Check in: `feat(DEV-118): add ISpellPuzzle and AuraVisibilityState`
> - `Assets/Scripts/Platformer/ISpellPuzzle.cs`
> - `Assets/Scripts/Platformer/ISpellPuzzle.cs.meta`
> - `Assets/Scripts/Platformer/AuraVisibilityState.cs`
> - `Assets/Scripts/Platformer/AuraVisibilityState.cs.meta`
> - `Assets/Tests/Editor/Platformer/AuraVisibilityStateTests.cs`
> - `Assets/Tests/Editor/Platformer/AuraVisibilityStateTests.cs.meta`

---

## Task 4: `PlayerAuraCue` MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Platformer/PlayerAuraCue.cs`

**Interfaces:**
- Consumes: `AuraVisibilityState`, `Axiom.Platformer.ISpellPuzzle`.
- Produces: `PlayerAuraCue` (global namespace) with `void EnterPuzzleRange(ISpellPuzzle)`, `void ExitPuzzleRange(ISpellPuzzle)`, `void Suppress(bool)`.

No EditMode test — this is a thin MonoBehaviour (SpriteRenderer + coroutine); all branching logic lives in the EditMode-tested `AuraVisibilityState`. Behavior is verified in the Play Mode pass (Task 8).

- [ ] **Step 1: Create the component**

Create `Assets/Scripts/Platformer/PlayerAuraCue.cs`:

```csharp
using System.Collections;
using Axiom.Platformer;
using UnityEngine;

/// <summary>
/// MonoBehaviour — drives the proximity "aura" SpriteRenderer behind the player.
/// Lifecycle + visuals only; visibility logic lives in <see cref="AuraVisibilityState"/>.
/// Proximity forwarders call EnterPuzzleRange / ExitPuzzleRange; the cast sequencer calls
/// Suppress while a cast animation plays.
/// </summary>
public class PlayerAuraCue : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Aura SpriteRenderer, sorted BEHIND the player sprite (lower sorting order, same layer).")]
    private SpriteRenderer _auraRenderer;

    [SerializeField]
    [Tooltip("Aura frames sliced from Aura-Sheet.png, in play order.")]
    private Sprite[] _frames;

    [SerializeField, Min(1f)]
    [Tooltip("Aura animation frames per second.")]
    private float _fps = 12f;

    private readonly AuraVisibilityState _state = new();
    private Coroutine _cycle;

    private void Awake()
    {
        if (_auraRenderer != null) _auraRenderer.enabled = false;
    }

    public void EnterPuzzleRange(ISpellPuzzle puzzle)
    {
        _state.Enter(puzzle);
        Refresh();
    }

    public void ExitPuzzleRange(ISpellPuzzle puzzle)
    {
        _state.Exit(puzzle);
        Refresh();
    }

    public void Suppress(bool suppressed)
    {
        _state.SetSuppressed(suppressed);
        Refresh();
    }

    private void Refresh()
    {
        if (_auraRenderer == null) return;

        if (_state.IsVisible)
        {
            if (_cycle == null)
            {
                _auraRenderer.enabled = true;
                _cycle = StartCoroutine(CycleFrames());
            }
        }
        else if (_cycle != null)
        {
            StopCoroutine(_cycle);
            _cycle = null;
            _auraRenderer.enabled = false;
        }
    }

    private IEnumerator CycleFrames()
    {
        if (_frames == null || _frames.Length == 0) yield break;

        var wait = new WaitForSeconds(1f / _fps);
        int index = 0;
        while (true)
        {
            _auraRenderer.sprite = _frames[index];
            index = (index + 1) % _frames.Length;
            yield return wait;
        }
    }
}
```

- [ ] **Step 2: Confirm the assembly compiles**

> **Unity Editor task (user):** Return to the Unity Editor and let it recompile. Confirm no errors in the Console. (A full EditMode run in a later task re-confirms.)

- [ ] **Step 3: Check in via UVCS**

> **Unity Editor task (user):** Unity Version Control → Pending Changes → stage the files below → Check in: `feat(DEV-118): add PlayerAuraCue proximity aura component`
> - `Assets/Scripts/Platformer/PlayerAuraCue.cs`
> - `Assets/Scripts/Platformer/PlayerAuraCue.cs.meta`

---

## Task 5: Controllers implement `ISpellPuzzle`; forwarders notify the aura

**Files:**
- Modify: `Assets/Scripts/Platformer/MeltableObstacleController.cs`
- Modify: `Assets/Scripts/Platformer/BurnableObstacleController.cs`
- Modify: `Assets/Scripts/Platformer/FreezablePlatformController.cs`
- Modify: `Assets/Scripts/Platformer/SteamVentController.cs`
- Modify: `Assets/Scripts/Platformer/AcidPuddleController.cs`
- Modify: `Assets/Scripts/Platformer/MeltableObstacleProximityForwarder.cs`
- Modify: `Assets/Scripts/Platformer/BurnableObstacleProximityForwarder.cs`
- Modify: `Assets/Scripts/Platformer/FreezablePlatformProximityForwarder.cs`
- Modify: `Assets/Scripts/Platformer/SteamVentProximityForwarder.cs`
- Modify: `Assets/Scripts/Platformer/AcidPuddleProximityForwarder.cs`

**Interfaces:**
- Consumes: `PlayerAuraCue`, `Axiom.Platformer.ISpellPuzzle`.
- Produces: each `*Controller` now implements `ISpellPuzzle.IsInteractable`; each `*ProximityForwarder` reports its controller to the player's `PlayerAuraCue` on enter/exit/disable.

- [ ] **Step 1: Make each controller implement `ISpellPuzzle`**

In `MeltableObstacleController.cs` change the class declaration to:
```csharp
    public class MeltableObstacleController : MonoBehaviour, ISpellPuzzle
```
and add this property (next to the existing `IsMelted`):
```csharp
        public bool IsInteractable => !IsMelted;
```

In `BurnableObstacleController.cs` change the declaration to:
```csharp
    public class BurnableObstacleController : MonoBehaviour, IExplosionDestructible, ISpellPuzzle
```
and add (next to `IsBurned`):
```csharp
        public bool IsInteractable => !IsBurned;
```

In `FreezablePlatformController.cs` change the declaration to:
```csharp
    public class FreezablePlatformController : MonoBehaviour, ISpellPuzzle
```
and add (next to `IsFrozen`):
```csharp
        public bool IsInteractable => !IsFrozen;
```

In `AcidPuddleController.cs` change the declaration to:
```csharp
    public class AcidPuddleController : MonoBehaviour, ISpellPuzzle
```
and add (next to `IsNeutralized`):
```csharp
        public bool IsInteractable => !IsNeutralized;
```

In `SteamVentController.cs` change the declaration to:
```csharp
    public class SteamVentController : MonoBehaviour, ISpellPuzzle
```
and add (a steam vent is re-ignitable/stateless, so it is always interactable):
```csharp
        public bool IsInteractable => true;
```

- [ ] **Step 2: Make each forwarder notify the aura**

The five forwarders are structurally identical (a `_controller` field of their respective type, which now implements `ISpellPuzzle`). Apply the **same** three edits to each of the five forwarder files. Shown for `MeltableObstacleProximityForwarder.cs`; repeat verbatim in the Burnable, Freezable, SteamVent, and AcidPuddle forwarders.

Add a cached field (below the existing `[SerializeField] private ...Controller _controller;`):
```csharp
        private PlayerAuraCue _auraCue;
```

Replace `OnTriggerEnter2D` with:
```csharp
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_controller == null) return;
            _controller.SetPlayerInRange(true);

            _auraCue = other.GetComponentInParent<PlayerAuraCue>();
            if (_auraCue != null) _auraCue.EnterPuzzleRange(_controller);
        }
```

Replace `OnTriggerExit2D` with:
```csharp
        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_controller == null) return;
            _controller.SetPlayerInRange(false);

            if (_auraCue != null)
            {
                _auraCue.ExitPuzzleRange(_controller);
                _auraCue = null;
            }
        }
```

Add an `OnDisable` (so a puzzle destroyed or disabled while the player is in range — e.g. a burnable cleared by a steam-vent blast — still clears the cue):
```csharp
        private void OnDisable()
        {
            if (_auraCue != null)
            {
                if (_controller != null) _auraCue.ExitPuzzleRange(_controller);
                _auraCue = null;
            }
        }
```

> Note: `EnterPuzzleRange(_controller)` passes the controller as `ISpellPuzzle` (implemented in Step 1). The forwarders are in `namespace Axiom.Platformer`; `PlayerAuraCue` is in the global namespace and is visible without a `using`.

- [ ] **Step 3: Compile and run the full EditMode suite**

> **Unity Editor task (user):** Return to the Editor; confirm no Console errors. Run Test Runner → EditMode (all suites). Expected: all PASS (the five controllers now compile against `ISpellPuzzle`; no regressions in existing Platformer/Voice tests).

- [ ] **Step 4: Check in via UVCS**

> **Unity Editor task (user):** Unity Version Control → Pending Changes → stage the files below → Check in: `feat(DEV-118): obstacles report proximity to player aura cue`
> - `Assets/Scripts/Platformer/MeltableObstacleController.cs`
> - `Assets/Scripts/Platformer/BurnableObstacleController.cs`
> - `Assets/Scripts/Platformer/FreezablePlatformController.cs`
> - `Assets/Scripts/Platformer/SteamVentController.cs`
> - `Assets/Scripts/Platformer/AcidPuddleController.cs`
> - `Assets/Scripts/Platformer/MeltableObstacleProximityForwarder.cs`
> - `Assets/Scripts/Platformer/BurnableObstacleProximityForwarder.cs`
> - `Assets/Scripts/Platformer/FreezablePlatformProximityForwarder.cs`
> - `Assets/Scripts/Platformer/SteamVentProximityForwarder.cs`
> - `Assets/Scripts/Platformer/AcidPuddleProximityForwarder.cs`

---

## Task 6: Player cast animation hooks

**Files:**
- Modify: `Assets/Scripts/Platformer/PlayerAnimator.cs`
- Modify: `Assets/Scripts/Platformer/PlayerExplorationAnimator.cs`
- Modify: `Assets/Scripts/Platformer/PlayerController.cs`

**Interfaces:**
- Produces: `PlayerAnimator.TriggerCast()`; `PlayerExplorationAnimator.AnimEvent_OnSpellFire()`; `PlayerController.BeginCast()`, `PlayerController.OnSpellCastFireFrame()`, `event System.Action PlayerController.SpellCastFired`.
- Consumed by: Task 7 (`PlatformerVoiceSpellController` calls `BeginCast` and subscribes `SpellCastFired`); the Animator's cast clips call `AnimEvent_OnSpellFire` (wired in Task 8).

- [ ] **Step 1: Add `TriggerCast()` to `PlayerAnimator`**

In `Assets/Scripts/Platformer/PlayerAnimator.cs`, add a hash next to the existing `ParamAttack`:
```csharp
    private static readonly int ParamCast = Animator.StringToHash("Cast");
```
and add this method next to `TriggerAttack()`:
```csharp
    /// <summary>
    /// Sets the Cast trigger on the Animator. Called by PlayerController.BeginCast().
    /// The Animator Controller routes to playerCastRight or playerCastLeft based on IsFacingRight.
    /// </summary>
    public void TriggerCast()
    {
        _animator.SetTrigger(ParamCast);
    }
```

- [ ] **Step 2: Add the fire-frame event method to `PlayerExplorationAnimator`**

In `Assets/Scripts/Platformer/PlayerExplorationAnimator.cs`, add this method (mirrors `AnimEvent_OnAttackEnd`):
```csharp
    /// <summary>
    /// Called by an Animation Event on the fire-frame of playerCastRight.anim / playerCastLeft.anim.
    /// Name mirrors PlayerBattleAnimator.AnimEvent_OnSpellFire — the shared cast clips invoke this
    /// method on whichever component sits on the active scene's Animator GameObject.
    /// </summary>
    public void AnimEvent_OnSpellFire()
    {
        _controller?.OnSpellCastFireFrame();
    }
```

- [ ] **Step 3: Add cast entry points + fire event to `PlayerController`**

In `Assets/Scripts/Platformer/PlayerController.cs`, add the event field near the top of the class (after the private fields, before `Awake`):
```csharp
    /// <summary>
    /// Raised on the cast clip's fire-frame (or the voice controller's timeout fallback).
    /// PlatformerVoiceSpellController subscribes to resolve the puzzle effect at this moment.
    /// </summary>
    public event System.Action SpellCastFired;
```

Add these two methods (place them next to `BeginAttack` / `OnAttackAnimationEnd`):
```csharp
    /// <summary>
    /// Starts a platformer spell cast: locks movement and plays the facing-aware cast animation.
    /// Resolution is deferred to <see cref="OnSpellCastFireFrame"/>. Called by PlatformerVoiceSpellController.
    /// </summary>
    public void BeginCast()
    {
        _movement.SetMovementLocked(true);
        _playerAnimator.TriggerCast();
    }

    /// <summary>
    /// Called by PlayerExplorationAnimator on the cast clip's fire-frame.
    /// Unlocks movement and raises SpellCastFired so the cast resolves at the right moment.
    /// </summary>
    public void OnSpellCastFireFrame()
    {
        _movement.SetMovementLocked(false);
        SpellCastFired?.Invoke();
    }
```

- [ ] **Step 4: Compile**

> **Unity Editor task (user):** Return to the Editor; confirm no Console errors. (No EditMode test — these touch the Animator/movement; verified end-to-end in Task 8.)

- [ ] **Step 5: Check in via UVCS**

> **Unity Editor task (user):** Unity Version Control → Pending Changes → stage the files below → Check in: `feat(DEV-118): add platformer player cast animation hooks`
> - `Assets/Scripts/Platformer/PlayerAnimator.cs`
> - `Assets/Scripts/Platformer/PlayerExplorationAnimator.cs`
> - `Assets/Scripts/Platformer/PlayerController.cs`

---

## Task 7: Wire `PlatformerVoiceSpellController` for deferred casts

**Files:**
- Modify: `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs`

**Interfaces:**
- Consumes: `PlatformerSpellWorldCaster.HasResolvableTarget` / `TryCast` (Task 1), `PlatformerCastSequencer` (Task 2), `PlayerController.BeginCast` / `SpellCastFired` (Task 6), `PlayerAuraCue.Suppress` (Task 4), `PlayerState.CurrentMp`.

- [ ] **Step 1: Add serialized references and runtime fields**

In `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs`, add `using System.Collections;` to the usings (for the timeout coroutine), and add these fields after the existing `_acidPuddles` serialized field:
```csharp
        [SerializeField]
        [Tooltip("Player in this scene. Drives the cast animation and raises the fire-frame event.")]
        private PlayerController _player;

        [SerializeField]
        [Tooltip("Player aura cue. Suppressed while a cast animation plays.")]
        private PlayerAuraCue _auraCue;

        [SerializeField, Min(0.1f)]
        [Tooltip("Fallback seconds before resolving a cast if the animation fire-frame event never fires.")]
        private float _spellFireTimeout = 1f;

        private PlatformerCastSequencer _castSequencer;
        private Coroutine _fireTimeout;
```

- [ ] **Step 2: Wire the player fire-frame event**

Add `OnEnable` / `OnDisable` to subscribe/unsubscribe the player's fire-frame event and stop any pending timeout. Place them above the existing `Start`. (The sequencer is created **lazily in `Update`** — Step 3 — not in `Awake`, so it is available to the existing EditMode tests, which invoke `Update` via reflection without the `Awake`/`OnEnable` lifecycle that edit mode does not run.)
```csharp
        private void OnEnable()
        {
            if (_player != null) _player.SpellCastFired += OnPlayerSpellFireFrame;
        }

        private void OnDisable()
        {
            if (_player != null) _player.SpellCastFired -= OnPlayerSpellFireFrame;
            StopFireTimeout();
        }
```

- [ ] **Step 3: Replace `Update` with the validate → deferred-cast flow**

Replace the existing `Update` method body with:
```csharp
        private void Update()
        {
            _castSequencer ??= new PlatformerCastSequencer(BeginCastAction, ResolveAction, EndCastAction);

            while (_resultQueue.TryDequeue(out string voskJson))
            {
                SpellData matched = SpellResultMatcher.Match(voskJson, _unlockedSpells);
                if (matched == null) continue;

                PlayerState playerState = _playerState ?? GameManager.Instance?.PlayerState;
                if (playerState == null) continue;

                bool castable =
                    PlatformerSpellWorldCaster.HasResolvableTarget(
                        matched,
                        ResolveMeltableObstacles(),
                        ResolveFreezablePlatforms(),
                        ResolveBurnableObstacles(),
                        ResolveSteamVents(),
                        ResolveAcidPuddles())
                    && playerState.CurrentMp >= matched.mpCost;

                if (castable) _castSequencer.RequestCast(matched);
            }
        }
```

- [ ] **Step 4: Add the sequencer callbacks + timeout helpers**

Add these methods (anywhere in the class, e.g. below `Update`):
```csharp
        private void BeginCastAction(SpellData spell)
        {
            if (_player != null) _player.BeginCast();
            if (_auraCue != null) _auraCue.Suppress(true);
            StartFireTimeout();
        }

        private void ResolveAction(SpellData spell)
        {
            PlayerState playerState = _playerState ?? GameManager.Instance?.PlayerState;
            PlatformerSpellWorldCaster.TryCast(
                spell,
                ResolveMeltableObstacles(),
                ResolveFreezablePlatforms(),
                ResolveBurnableObstacles(),
                ResolveSteamVents(),
                ResolveAcidPuddles(),
                playerState);
        }

        private void EndCastAction()
        {
            if (_auraCue != null) _auraCue.Suppress(false);
            StopFireTimeout();
        }

        private void OnPlayerSpellFireFrame() => _castSequencer?.NotifyFireFrame();

        private void StartFireTimeout()
        {
            // Edit-mode safe: only arm the coroutine fallback at runtime. EditMode tests drive the
            // fire-frame by invoking OnPlayerSpellFireFrame directly, so they must not hit StartCoroutine.
            if (!Application.isPlaying) return;
            StopFireTimeout();
            _fireTimeout = StartCoroutine(FireTimeoutCoroutine());
        }

        private void StopFireTimeout()
        {
            if (_fireTimeout != null)
            {
                StopCoroutine(_fireTimeout);
                _fireTimeout = null;
            }
        }

        private IEnumerator FireTimeoutCoroutine()
        {
            yield return new WaitForSeconds(_spellFireTimeout);
            _fireTimeout = null;
            _castSequencer.NotifyFireFrame();
        }
```

> Sequencing recap: a valid spell → `RequestCast` → `BeginCastAction` (player cast animation, aura suppressed, timeout armed). The clip's fire-frame → `PlayerController.SpellCastFired` → `OnPlayerSpellFireFrame` → `NotifyFireFrame` → `ResolveAction` (MP spent + obstacle resolved) → `EndCastAction` (aura un-suppressed, timeout stopped). If the event never fires, the timeout calls `NotifyFireFrame`; the sequencer's once-only guard prevents a double resolve.

- [ ] **Step 5: Adapt the existing controller tests to the deferred flow**

`Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs` currently asserts that calling `Update()` resolves the obstacle **immediately**. Resolution is now deferred to the fire-frame, so each of the **four positive tests** must invoke the fire-frame after `Update()`. In each positive test, replace the single line:

```csharp
            InvokePrivateMethod(controller, "Update");
```

with (the comment doubles as the deferral-intent encoding):

```csharp
            InvokePrivateMethod(controller, "Update");
            // Resolution is deferred to the cast animation's fire-frame; Update only requests the cast.
            InvokePrivateMethod(controller, "OnPlayerSpellFireFrame");
```

Apply to: `Update_RecognizedMeltSpell_MeltsInRangeObstacle`, `Update_RecognizedFreezeSpell_FreezesInRangeWaterPlatformAndSpendsMp`, `Update_RecognizedCombustSpell_IgnitesInRangeBurnableObstacleAndSpendsMp`, `Update_RecognizedNeutralizeSpell_DissolvesInRangeAcidPuddleAndSpendsMp`. Each test's existing resolution + MP assertions (e.g. `Assert.IsTrue(obstacle.IsMelted); Assert.AreEqual(12, gameManager.PlayerState.CurrentMp);`) are **unchanged and still pass** — `Update` requests the cast (lazy-creating the sequencer, no coroutine in edit mode), and the manual `OnPlayerSpellFireFrame` invoke runs `ResolveAction` → `TryCast`, which resolves the obstacle and spends MP exactly as before.

To lock in the new intent in the melt test, optionally add a deferral assertion **between** the two invokes:
```csharp
            Assert.IsFalse(obstacle.IsMelted, "resolution must be deferred until the cast fire-frame");
```

For the fifth test, `Update_NeutralizeSpell_PuddleOutOfRange_DoesNotDissolveOrSpendMp`, also add the same `OnPlayerSpellFireFrame` invoke after `Update()` — proving a fire-frame resolves nothing when no cast was requested (out of range → no `RequestCast` → sequencer has no pending spell). Its assertions (`Assert.IsFalse(puddle.IsNeutralized); Assert.AreEqual(20, ...CurrentMp);`) are unchanged.

- [ ] **Step 6: Compile and run the full EditMode suite**

> **Unity Editor task (user):** Return to the Editor; confirm no Console errors. Run Test Runner → EditMode (all suites). Expected: all PASS, including the five adapted `PlatformerVoiceSpellControllerTests`.

- [ ] **Step 7: Check in via UVCS**

> **Unity Editor task (user):** Unity Version Control → Pending Changes → stage the files below → Check in: `feat(DEV-118): defer platformer cast resolution to animation fire-frame`
> - `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs`
> - `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs`

---

## Task 8: Unity Editor wiring, assets, and end-to-end verification

All steps are performed by the **user** in the Unity Editor. No new C#.

> **Current state (verified against the project files 2026-06-23 — the three handoff "open questions" are resolved):**
> - **Player is a prefab:** `Assets/Prefabs/Player/Player (Exploration).prefab` is the platformer player. All Player-side aura work (Steps 2–3) is done on the **prefab asset** so every `Level_*` scene inherits it. (`Player (Battle).prefab` is the combat-scene player and is out of scope.)
> - **`Aura-Sheet.png` exists and is already sliced** into 12 frames `aura-0`…`aura-11` (`Sprite Mode = Multiple`). **Step 1 is done.**
> - **The `AuraCue` child + its `SpriteRenderer` already exist** on the prefab (Sorting Layer `0`, **Order in Layer 4** — behind the player's order 5 — with an aura frame + size assigned). **Step 2 is done.**
> - **The `PlayerAuraCue` component is already on the Player root**, with `Fps = 12`, **but `Aura Renderer` = None and `Frames` = empty.** Assigning those two is the only remaining part of Step 3.
> - **`Cast` trigger and cast transitions do not exist yet** (`playerCastLeft` / `playerCastRight` states are orphaned). Steps 4–5 remain.
> - **`playerCastRight.anim` already has the `AnimEvent_OnSpellFire` event at `time 0.5`; `playerCastLeft.anim` has none.** Only the left clip needs the event. Step 6 remains.
> - **`PlatformerVoiceSpellController` is NOT in `Platformer.unity`.** It lives on the standalone prefab `Assets/Prefabs/Voice/PlatformerVoiceRig.prefab`, instanced (alongside a separate `Player (Exploration)` instance) in each of ~14 `Level_*` gameplay scenes. The controller **auto-finds obstacles** at runtime (`FindObjectsByType<…>`) but does **NOT** auto-find the player/aura — it only acts `if (_player != null)` / `if (_auraCue != null)`. See the revised Step 7.

### Aura sprite & object

- [x] **Step 1:** *(done — `Aura-Sheet.png` is `Sprite Mode = Multiple`, sliced into `aura-0`…`aura-11`.)*
> **Unity Editor task (user):** Select `Assets/Art/Sprites/Player/Aura-Sheet.png`. Set Sprite Mode = Multiple, open the Sprite Editor, and slice into 12 frames (Grid By Cell Count or Automatic). Apply.

- [x] **Step 2:** *(done on the prefab — `AuraCue` child exists with a `SpriteRenderer` on Sorting Layer 0 / Order 4.)*
> **Unity Editor task (user):** On the **`Player (Exploration).prefab`** asset (open in Prefab Mode so all levels inherit), under the Player root (the same hierarchy that holds the Animator) create a child GameObject `AuraCue` with a `SpriteRenderer`. Set its Sorting Layer equal to the player sprite's, and its **Order in Layer lower** than the player sprite (renders behind).
> **"Disabled-looking" clarification:** keep the `AuraCue` **GameObject active**. `PlayerAuraCue.Awake()` disables the **SpriteRenderer *component*** (`_auraRenderer.enabled = false`) at runtime, so the aura starts hidden on its own — you don't disable anything yourself. Do **not** deactivate the GameObject (a renderer on an inactive GameObject can't be re-enabled, which breaks the cue). Unchecking the SpriteRenderer's own `Enabled` box in the editor is cosmetic-only.

- [ ] **Step 3:** *(remaining: assign the two references on the prefab)*
> **Unity Editor task (user):** On the **`Player (Exploration).prefab`** root, the `PlayerAuraCue` component is already present with `Fps = 12`. Assign the two empty fields:
> - **`Aura Renderer`** = the `AuraCue` child's `SpriteRenderer`.
> - **`Frames`** = the 12 sliced aura sprites **in numeric order** `aura-0 … aura-11`.
>   - ⚠️ **Ordering trap:** expand `Aura-Sheet.png` (▸) to reveal the 12 sub-sprites. Multi-selecting and dragging all 12 at once often sorts them *alphabetically*, placing `aura-10`/`aura-11` right after `aura-1`. After dragging, verify the list reads `aura-0, aura-1, aura-2, … aura-11` top-to-bottom and fix any out-of-order slots.
> Save the prefab.

### Player Animator Controller — cast transitions

- [ ] **Step 4:**
> **Unity Editor task (user):** Open `Assets/Animations/Player/Player.controller`. Add a **Trigger** parameter named exactly `Cast` (case-sensitive). *(Existing params: `IsGrounded`, `IsFacingRight`, `VelocityX`, `VelocityY`, `Attack`, `Hurt`, `Defeat`.)*

- [ ] **Step 5:** *(corrected — there is no standalone "Idle" state; locomotion is the `Grounded_R` / `Grounded_L` blend-tree states, exactly as the Attack states return to)*
> **Unity Editor task (user):** Add transitions for the existing (currently orphaned) cast states. The cast states mirror the existing `AttackRight`/`AttackLeft` pattern (verified: `AttackRight → Grounded_R`, Has Exit Time = true, Exit Time = 1, Duration = 0, no conditions).
>
> | Transition | Conditions | Has Exit Time | Exit Time | Duration | Can Transition To Self |
> |---|---|---|---|---|---|
> | `Any State → playerCastRight` | `Cast` **and** `IsFacingRight = true` | false | — | 0 | **unchecked** |
> | `Any State → playerCastLeft` | `Cast` **and** `IsFacingRight = false` | false | — | 0 | **unchecked** |
> | `playerCastRight → Grounded_R` | *(none)* | true | 1 | 0 | n/a |
> | `playerCastLeft → Grounded_L` | *(none)* | true | 1 | 0 | n/a |
>
> Transition **to the `Grounded_R` / `Grounded_L` state** (the box containing the blend tree), not to a node inside it. When the cast clip ends and the player is still (`VelocityX ≈ 0`), the blend tree settles on idle; if moving, it shows move. Match facing on return (`playerCastRight → Grounded_R`, `playerCastLeft → Grounded_L`); the existing `Grounded_R ↔ Grounded_L` (`IsFacingRight`) transitions self-correct a mid-cast flip. The exit transitions carry **no conditions** — they fire purely on exit time.

### Cast clip fire-frame events

- [ ] **Step 6:** *(only the LEFT clip needs the event)*
> **Unity Editor task (user):** Open `Assets/Animations/Player/playerCastLeft.anim` in the Animation window and add an **Animation Event** at `time ≈ 0.5s` (matching the existing event on `playerCastRight.anim`), Function = `AnimEvent_OnSpellFire`. `playerCastRight.anim` already has this event at `0.5` — leave it. The receiver `PlayerExplorationAnimator` is already on the Player's `Animator` child GameObject (for the attack flow), so the event resolves.

### Scene wiring

- [ ] **Step 7:** *(corrected architecture + decided approach: per-scene manual wiring, no code change)*
> **Reality:** `PlatformerVoiceSpellController` lives on `Assets/Prefabs/Voice/PlatformerVoiceRig.prefab`, which is instanced — alongside a *separate* `Player (Exploration)` instance — in each `Level_*` scene. A prefab asset cannot reference another prefab's scene instance, and the controller does **not** auto-find the player/aura. So `_player` / `_auraCue` must be assigned **per scene** on the rig instance.
>
> **Decision (user, 2026-06-23): per-scene manual wiring — no C# auto-find change.**
>
> **Recommended order — wire and verify ONE scene first:**
> 1. Open one gameplay scene that contains puzzles, e.g. `Assets/Scenes/Level_1-1.unity`.
> 2. Select the **`PlatformerVoiceRig`** instance → on `PlatformerVoiceSpellController` assign:
>    - `Player` = that scene's `Player (Exploration)` instance's **`PlayerController`**.
>    - `Aura Cue` = that scene's `Player (Exploration)` instance's **`PlayerAuraCue`**.
>    - `Spell Fire Timeout` = `1`.
>    - Leave the obstacle-list fields empty (runtime auto-find via `FindObjectsByType`).
> 3. Run the Play Mode checks (Steps 8–10) in this scene.
> 4. Once verified, repeat the rig wiring in the remaining gameplay `Level_*` scenes (`Level_1-2 … Level_4-1`). Each is a per-scene override checked into UVCS. **Track which scenes are wired so none is missed** (an unwired scene silently has no cast animation / aura suppression).

### End-to-end Play Mode verification (the integration gate)

- [ ] **Step 8: Verify the proximity cue**
> **Unity Editor task (user):** Enter Play Mode in a wired `Level_*` scene with puzzles. Walk Kaelen toward a meltable/burnable/freezable/steam-vent/acid puzzle. Confirm:
> - The aura appears behind the player on entering the puzzle's proximity zone, and animates (frames cycle).
> - The aura disappears on leaving the zone.
> - With two overlapping zones, the aura stays until both are exited.

- [ ] **Step 9: Verify the cast animation + deferred resolution**
> **Unity Editor task (user):** Stand in a puzzle's proximity zone (aura showing), with enough MP, and speak the correct spell (push-to-talk). Confirm, in order:
> - The aura hides the instant the cast is accepted.
> - The cast animation plays, facing the correct direction (right vs left).
> - The puzzle resolves (melts/burns/freezes/erupts/neutralizes) at the animation's fire-frame — **after** the animation starts, not before.
> - Movement is locked during the cast and restored at the fire-frame.
> - MP is deducted once.

- [ ] **Step 10: Verify the negative cases**
> **Unity Editor task (user):** Confirm:
> - Speaking a spell with **no** matching puzzle nearby → nothing happens (no animation).
> - Speaking the wrong spell while near a puzzle → nothing happens.
> - Speaking the correct spell with **insufficient MP** → nothing happens (no animation).
> - After solving a puzzle, its aura does not re-appear while standing in place.
> - Facing left, the cast still resolves at the fire-frame (the new left-clip event fires).

- [ ] **Step 11: Check in via UVCS**
> **Unity Editor task (user):** Unity Version Control → Pending Changes → stage the changed assets below → Check in: `feat(DEV-118): wire platformer aura cue and cast animation`
> - `Assets/Prefabs/Player/Player (Exploration).prefab` — `AuraCue` child + `PlayerAuraCue` (renderer + frames assigned)
> - `Assets/Animations/Player/Player.controller` — `Cast` trigger + cast transitions
> - `Assets/Animations/Player/playerCastLeft.anim` — fire-frame event
> - `Assets/Art/Sprites/Player/Aura-Sheet.png.meta` — slice metadata (if not already checked in)
> - Each wired `Assets/Scenes/Level_*.unity` — `PlatformerVoiceRig` instance `_player`/`_auraCue`/`_spellFireTimeout` overrides
>
> Whatever already shows in UVCS Pending Changes for the prefab/sheet (prior partial Task 8 work) is included here — there is no earlier per-task check-in for Task 8.

> **Note:** All Player-side aura/cast edits (Steps 2–6 on the prefab, controller, and clip) are on shared **assets**, so they apply to every level automatically. Only the Step 7 `_player`/`_auraCue` reference assignment is per-scene, because the rig and player are independent prefab instances.

---

## Self-Review

**Spec coverage:**
- AC "proximity visual cue when nearby and able to interact" → Tasks 3 (`AuraVisibilityState` + `ISpellPuzzle`), 4 (`PlayerAuraCue`), 5 (controllers/forwarders), 8 (assets/wiring). ✓
- AC "player spell casts have a visible animation" → Tasks 6 (player hooks), 7 (deferral wiring), 8 (animator transitions + clip event). ✓
- Spec "resolution split / deferred resolution" → Tasks 1 (`HasResolvableTarget`), 2 (`PlatformerCastSequencer`), 7 (wiring). ✓
- Spec "aura hides on valid cast, reappears after" → Task 7 (`Suppress(true/false)` around the cast). ✓
- Spec "timeout safety net" → Task 7 (`FireTimeoutCoroutine`). ✓
- Spec EditMode tests (caster split, sequencer, aura state) → Tasks 1, 2, 3. ✓
- Deferred ACs → listed under Scope; no tasks (correct). ✓

**Placeholder scan:** No TBD/TODO; every code step shows complete code; the five-forwarder edit is byte-identical and shown in full (not "similar to"). ✓

**Type consistency:** `HasResolvableTarget` / `TryCast` signatures match Task 1 and their callers in Task 7. `PlatformerCastSequencer(Action<SpellData>, Action<SpellData>, Action)`, `RequestCast(SpellData)`, `NotifyFireFrame()`, `IsCasting` consistent across Tasks 2 and 7. `ISpellPuzzle.IsInteractable`, `AuraVisibilityState.Enter/Exit/SetSuppressed/IsVisible`, `PlayerAuraCue.EnterPuzzleRange/ExitPuzzleRange/Suppress`, `PlayerController.BeginCast/OnSpellCastFireFrame/SpellCastFired`, `PlayerAnimator.TriggerCast`, `PlayerExplorationAnimator.AnimEvent_OnSpellFire` consistent across Tasks 3–7. ✓

**UVCS file audit:** Each new `.cs` is checked in with its `.cs.meta`; modified-only `.cs` files are checked in without re-listing unchanged metas; the new `AuraCue` child/prefab + sprite slice metadata are covered in Task 8. ✓

**Guard-clause ordering (Task 1):** `HasResolvableTarget` (spell/target) runs before the `playerState` guard in `TryCast`, so an empty world short-circuits without needing `playerState` — matches the original "no target → no spend" invariant. ✓

**Regression / existing tests (Task 7):** The 5 existing `PlatformerVoiceSpellControllerTests` assert synchronous resolution from `Update()`. Deferral changes that, so Step 5 adapts them to drive `OnPlayerSpellFireFrame` after `Update()`; their resolution/MP assertions are preserved. Two edit-mode-safety measures make the reflection-driven tests keep working without the `Awake`/`OnEnable`/coroutine lifecycle: the sequencer is **lazy-created in `Update`** (not `Awake`), and `StartFireTimeout` no-ops when `!Application.isPlaying`. ✓

**EditMode-test feasibility:** Every pure branch reachable without scene/animation deps is tested — `PlatformerCastSequencer` (8 tests), `AuraVisibilityState` (10), `HasResolvableTarget` guard paths (3), and the adapted controller integration (5). MonoBehaviour/Animator-bound paths (`PlayerAuraCue` visuals, player cast hooks, animator transitions) are covered by the Task 8 Play Mode checklist, matching the project's convention of testing logic in EditMode and verifying Unity wiring manually. ✓
