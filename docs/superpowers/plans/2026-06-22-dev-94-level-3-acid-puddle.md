# DEV-94 Level 3 Acid Puddle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. For this Unity project, also pair with `executing-unity-game-dev-plans` (UVCS check-ins + Editor handoffs).

**Goal:** Add the acid-pool portion of DEV-94 Level 3 — an animated acid puddle that deals escalating damage-over-time and dissolves when the player speaks `neutralize`, letting them cross safely.

**Architecture:** A new `AcidPuddleController` MonoBehaviour composes three proven patterns: a code-swapped `Sprite[]` animation loop (per-instance desynced, like the Level 2 animated lava tile), an escalating DoT modeled on `HazardTrigger` (enter→tick-while-overlapping coroutine→exit, reset on exit), and spell removal + `puzzleId` persistence modeled on `BurnableObstacleController`. All non-trivial logic lives in two pure static helpers (`AcidPuddle` for spell matching, `AcidPuddleDamage` for the escalation curve); the controller reuses the existing `HazardDamageResolver` and `PlayerHurtFeedback`. Dispatch flows through the existing `PlatformerSpellWorldCaster` / `PlatformerVoiceSpellController`.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, C#, New Input System, Cinemachine, Unity Test Framework (Edit Mode/NUnit), UVCS.

## Global Constraints

- **MonoBehaviours = Unity lifecycle only**; all reusable logic in plain C# static helpers (`AcidPuddle`, `AcidPuddleDamage`). (CLAUDE.md architecture standard)
- **No Unity Animator / AnimationClip** — animate by swapping `Sprite[]` frames in a coroutine at a configurable speed. (Project convention; see `2026-06-18-ice-wall-animated-sprite-design.md`)
- **No static singletons except `GameManager`.**
- **ScriptableObject-driven data** — neutralize spells are `SpellData` assets, never hardcoded names.
- **No new assembly definitions.** New runtime scripts live under `Assets/Scripts/Platformer/` (`Axiom.Platformer`). Tests live under `Assets/Tests/Editor/Platformer/` (`PlatformerTests`) and `Assets/Tests/Editor/Voice/` (`VoiceTests`). All four assemblies already exist and reference correctly.
- **Battle chemistry is untouched.** The platformer only string-matches `spell.spellName`; it never references `ChemicalCondition` / `SpellEffectResolver`. (chemistry doc invariant: conditions are battle-scoped)
- **Version control = UVCS only**, never git. Check-in message format: `<type>(DEV-94): <short description>`.
- **Editor/code split:** Claude writes all `.cs`. The user performs all Unity Editor actions (prefab, scene authoring, running Test Runner, Play Mode).
- **Neutralize spell:** `neutralize` (`Assets/Data/Spells/SD_Neutralize.asset`, already exists; `spellName: neutralize`, `mpCost: 6`, unlock `requiredLevel: 3`). No new spell asset.
- **Sprite sheet:** `Assets/Art/Sprites/Platformer/acid puddle.png`, already sliced `Multiple` into 6 frames (`acid puddle-0..-4`, `-6`), `spritePixelsToUnits: 16`.
- **Tuning defaults (serialized, Inspector-adjustable):** `_baseTickPercent = 3`, `_growthFactor = 1.6`, `_maxTickPercent = 25`, `_tickIntervalSeconds = 0.5`, `_minSpeed = 5`, `_maxSpeed = 7`, `_fadeDuration = 0.6`.
- **The puddle is a floor pool, not a wall — no solid collider.** Only triggers: a damage trigger on the root and a larger neutralize-zone trigger on a child. "Cross safely" = once dissolved, the damage trigger is disabled.

**Reference files to mirror exactly:** `Assets/Scripts/Platformer/HazardTrigger.cs` (DoT enter/tick/exit + `PlayerHurtFeedback` usage), `Assets/Scripts/Platformer/BurnableObstacleController.cs` (spell removal + `puzzleId` + success cue + `ApplySolvedImmediate`), `Assets/Scripts/Platformer/BurnableObstacle.cs` (static match helper), `Assets/Scripts/Platformer/BurnableObstacleProximityForwarder.cs`, `Assets/Tests/Editor/Platformer/BurnableObstacleTests.cs`, `Assets/Tests/Editor/Platformer/HazardDamageResolverTests.cs`, `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs`.

---

## File Structure

**Create (C#):**
- `Assets/Scripts/Platformer/AcidPuddle.cs` — pure static neutralize-match helper.
- `Assets/Scripts/Platformer/AcidPuddleDamage.cs` — pure static escalating-percent-per-tick helper.
- `Assets/Scripts/Platformer/AcidPuddleController.cs` — the hybrid MonoBehaviour (animation + DoT + neutralize + fade + persistence).
- `Assets/Scripts/Platformer/AcidPuddleProximityForwarder.cs` — larger neutralize-zone trigger forwarder.
- `Assets/Tests/Editor/Platformer/AcidPuddleTests.cs` — Edit Mode tests for the match helper.
- `Assets/Tests/Editor/Platformer/AcidPuddleDamageTests.cs` — Edit Mode tests for the escalation curve.
- `Assets/Tests/Editor/Platformer/AcidPuddleControllerTests.cs` — Edit Mode tests for the controller's neutralize-gating + restore surface.

**Modify (C#):**
- `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs` — extend `TryCast` with acid puddles.
- `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs` — resolve + pass the acid-puddle array.
- `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs` — restore dissolved puddles on scene load.
- `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs` — add the neutralize dispatch integration tests.

**Create (Unity Editor, user):** `Assets/Prefabs/Platformer/P_AcidPuddle.prefab`; Level 3 (3-1 → 3-3) puddle placements with escalating challenge.

---

### Task 1: Acid neutralize-match helper (TDD)

**Files:**
- Create: `Assets/Scripts/Platformer/AcidPuddle.cs`
- Test: `Assets/Tests/Editor/Platformer/AcidPuddleTests.cs`

**Interfaces:**
- Produces: `public static bool AcidPuddle.CanNeutralize(string spellId, IReadOnlyList<string> neutralizeSpellIds)` — `true` iff `spellId` is non-empty and present in `neutralizeSpellIds` (case-insensitive). Consumed by `AcidPuddleController.CanNeutralizeWith`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/Editor/Platformer/AcidPuddleTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class AcidPuddleTests
    {
        [Test]
        public void CanNeutralize_NullSpellId_ReturnsFalse()
        {
            var ids = new List<string> { "neutralize" };
            Assert.IsFalse(AcidPuddle.CanNeutralize(null, ids));
        }

        [Test]
        public void CanNeutralize_EmptySpellId_ReturnsFalse()
        {
            var ids = new List<string> { "neutralize" };
            Assert.IsFalse(AcidPuddle.CanNeutralize(string.Empty, ids));
        }

        [Test]
        public void CanNeutralize_NullList_ReturnsFalse()
        {
            Assert.IsFalse(AcidPuddle.CanNeutralize("neutralize", null));
        }

        [Test]
        public void CanNeutralize_SpellInList_ReturnsTrue()
        {
            // WHY: only the AcidBase neutralize spell may clear an acid puddle.
            var ids = new List<string> { "neutralize" };
            Assert.IsTrue(AcidPuddle.CanNeutralize("neutralize", ids));
        }

        [Test]
        public void CanNeutralize_SpellNotInList_ReturnsFalse()
        {
            // WHY: a wrong-pillar spell (e.g. combust) must not dissolve acid.
            var ids = new List<string> { "neutralize" };
            Assert.IsFalse(AcidPuddle.CanNeutralize("combust", ids));
        }

        [Test]
        public void CanNeutralize_CaseInsensitive_ReturnsTrue()
        {
            // WHY: spellName is stored lowercase, but matching must not depend on casing.
            var ids = new List<string> { "neutralize" };
            Assert.IsTrue(AcidPuddle.CanNeutralize("Neutralize", ids));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run in Unity: Window → General → Test Runner → EditMode → run `AcidPuddleTests`.
Expected: FAIL to compile — `AcidPuddle` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/Scripts/Platformer/AcidPuddle.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Axiom.Platformer
{
    /// <summary>
    /// Pure spell-match rule for acid puddles: does the spoken spell neutralize this
    /// puddle? Kept separate from the MonoBehaviour so the rule is unit-testable
    /// without a scene. Mirrors <see cref="BurnableObstacle"/>.
    /// </summary>
    public static class AcidPuddle
    {
        public static bool CanNeutralize(string spellId, IReadOnlyList<string> neutralizeSpellIds)
        {
            if (string.IsNullOrEmpty(spellId)) return false;
            if (neutralizeSpellIds == null) return false;

            for (int i = 0; i < neutralizeSpellIds.Count; i++)
            {
                if (string.Equals(neutralizeSpellIds[i], spellId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run in Unity: Test Runner → EditMode → `AcidPuddleTests`.
Expected: PASS (6/6).

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-94): add acid puddle neutralize-match helper`
- `Assets/Scripts/Platformer/AcidPuddle.cs`
- `Assets/Scripts/Platformer/AcidPuddle.cs.meta`
- `Assets/Tests/Editor/Platformer/AcidPuddleTests.cs`
- `Assets/Tests/Editor/Platformer/AcidPuddleTests.cs.meta`
- `docs/superpowers/specs/2026-06-22-dev-94-level-3-acid-puddle-design.md`
- `docs/superpowers/plans/2026-06-22-dev-94-level-3-acid-puddle.md`

---

### Task 2: Escalating damage curve helper (TDD)

**Files:**
- Create: `Assets/Scripts/Platformer/AcidPuddleDamage.cs`
- Test: `Assets/Tests/Editor/Platformer/AcidPuddleDamageTests.cs`

**Interfaces:**
- Produces: `public static int AcidPuddleDamage.PercentForTick(int tickIndex, int baseTickPercent, float growthFactor, int maxTickPercent)` — the HP-percent for a given tick: `baseTickPercent * growthFactor^tickIndex`, rounded away from zero, clamped to `[0, maxTickPercent]`; `tickIndex < 0` is treated as `0`. Consumed by `AcidPuddleController.ApplyTickDamage`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/Editor/Platformer/AcidPuddleDamageTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class AcidPuddleDamageTests
    {
        [Test]
        public void PercentForTick_TickZero_ReturnsBase()
        {
            // WHY: first contact is the mild base tick — acid escalates FROM here.
            Assert.AreEqual(3, AcidPuddleDamage.PercentForTick(0, 3, 1.6f, 25));
        }

        [Test]
        public void PercentForTick_TickOne_ReturnsBaseTimesGrowthRounded()
        {
            // 3 * 1.6 = 4.8 -> 5 (round away from zero).
            Assert.AreEqual(5, AcidPuddleDamage.PercentForTick(1, 3, 1.6f, 25));
        }

        [Test]
        public void PercentForTick_StrictlyEscalatesUntilCap()
        {
            // WHY: the core requirement — damage must INCREASE each tick, not be flat.
            int t0 = AcidPuddleDamage.PercentForTick(0, 3, 1.6f, 25);
            int t1 = AcidPuddleDamage.PercentForTick(1, 3, 1.6f, 25);
            int t2 = AcidPuddleDamage.PercentForTick(2, 3, 1.6f, 25);
            int t3 = AcidPuddleDamage.PercentForTick(3, 3, 1.6f, 25);
            Assert.Less(t0, t1);
            Assert.Less(t1, t2);
            Assert.Less(t2, t3);
        }

        [Test]
        public void PercentForTick_LargeTick_ClampsToMax()
        {
            // WHY: escalation must be BOUNDED — an unbounded curve would one-shot the player.
            Assert.AreEqual(25, AcidPuddleDamage.PercentForTick(20, 3, 1.6f, 25));
        }

        [Test]
        public void PercentForTick_NegativeTick_TreatedAsZero()
        {
            // Defensive: a stray negative index must not produce negative/garbage damage.
            Assert.AreEqual(3, AcidPuddleDamage.PercentForTick(-1, 3, 1.6f, 25));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run in Unity: Test Runner → EditMode → `AcidPuddleDamageTests`.
Expected: FAIL to compile — `AcidPuddleDamage` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/Scripts/Platformer/AcidPuddleDamage.cs`:

```csharp
using System;

namespace Axiom.Platformer
{
    /// <summary>
    /// Pure escalation curve for the acid puddle's damage-over-time. Tick 0 deals the
    /// mild base percent; each subsequent tick multiplies by <paramref name="growthFactor"/>,
    /// rounded away from zero and clamped to <paramref name="maxTickPercent"/>. Kept
    /// separate from the MonoBehaviour so the curve is unit-testable without a scene.
    /// </summary>
    public static class AcidPuddleDamage
    {
        public static int PercentForTick(int tickIndex, int baseTickPercent, float growthFactor, int maxTickPercent)
        {
            if (tickIndex < 0) tickIndex = 0;

            double raw = baseTickPercent * Math.Pow(growthFactor, tickIndex);
            int rounded = (int)Math.Round(raw, MidpointRounding.AwayFromZero);

            if (rounded < 0) rounded = 0;
            if (rounded > maxTickPercent) rounded = maxTickPercent;
            return rounded;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run in Unity: Test Runner → EditMode → `AcidPuddleDamageTests`.
Expected: PASS (5/5).

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-94): add acid puddle escalating damage curve helper`
- `Assets/Scripts/Platformer/AcidPuddleDamage.cs`
- `Assets/Scripts/Platformer/AcidPuddleDamage.cs.meta`
- `Assets/Tests/Editor/Platformer/AcidPuddleDamageTests.cs`
- `Assets/Tests/Editor/Platformer/AcidPuddleDamageTests.cs.meta`

---

### Task 3: AcidPuddleController + proximity forwarder (TDD on public surface)

**Files:**
- Create: `Assets/Scripts/Platformer/AcidPuddleController.cs`
- Create: `Assets/Scripts/Platformer/AcidPuddleProximityForwarder.cs`
- Test: `Assets/Tests/Editor/Platformer/AcidPuddleControllerTests.cs`

**Interfaces:**
- Consumes: `AcidPuddle.CanNeutralize(string, IReadOnlyList<string>)` (Task 1); `AcidPuddleDamage.PercentForTick(int, int, float, int)` (Task 2); existing `HazardDamageResolver.Resolve(...)`, `HazardMode.PercentMaxHpDamage`, `PlayerHurtFeedback`, `GameManager.Instance.PlayerState`, `GameManager.Instance.MarkPuzzleSolved(string)`, `GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(AudioSource)`.
- Produces:
  - `public void AcidPuddleController.SetPlayerInRange(bool inRange)`
  - `public bool AcidPuddleController.CanNeutralizeWith(string spellId)`
  - `public bool AcidPuddleController.TryNeutralize(string spellId)`
  - `public bool AcidPuddleController.IsNeutralized { get; }`
  - `public string AcidPuddleController.PuzzleId { get; }`
  - `public void AcidPuddleController.ApplySolvedImmediate()`
  All consumed by `PlatformerSpellWorldCaster` (Task 4) and `PlatformerWorldRestoreController` (Task 5).

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/Editor/Platformer/AcidPuddleControllerTests.cs`:

```csharp
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Axiom.Data;
using Axiom.Platformer;
using UnityEngine;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class AcidPuddleControllerTests
    {
        [Test]
        public void CanNeutralizeWith_PlayerNotInRange_ReturnsFalse()
        {
            // WHY: the edge-zone gate — a puddle the player isn't near must ignore the cast.
            var (go, controller, spell) = MakePuddle();
            // _isPlayerInRange defaults to false.
            Assert.IsFalse(controller.CanNeutralizeWith("neutralize"));
            Cleanup(go, spell);
        }

        [Test]
        public void CanNeutralizeWith_InRangeAndSpellMatches_ReturnsTrue()
        {
            var (go, controller, spell) = MakePuddle();
            controller.SetPlayerInRange(true);
            Assert.IsTrue(controller.CanNeutralizeWith("neutralize"));
            Cleanup(go, spell);
        }

        [Test]
        public void CanNeutralizeWith_WrongSpell_ReturnsFalse()
        {
            var (go, controller, spell) = MakePuddle();
            controller.SetPlayerInRange(true);
            Assert.IsFalse(controller.CanNeutralizeWith("combust"));
            Cleanup(go, spell);
        }

        [Test]
        public void ApplySolvedImmediate_DisablesDamageAndHidesSprite()
        {
            // WHY: a puddle dissolved before a battle must come back already-gone and
            // non-damaging on scene reload — no DoT, no visible acid.
            var (go, controller, spell) = MakePuddle();
            var renderer = go.GetComponent<SpriteRenderer>();
            var damage = go.GetComponent<BoxCollider2D>();
            SetPrivateField(controller, "_spriteRenderer", renderer);
            SetPrivateField(controller, "_damageCollider", damage);

            controller.ApplySolvedImmediate();

            Assert.IsTrue(controller.IsNeutralized);
            Assert.IsFalse(renderer.enabled);
            Assert.IsFalse(damage.enabled);
            Cleanup(go, spell);
        }

        [Test]
        public void CanNeutralizeWith_AfterSolved_ReturnsFalse()
        {
            // WHY: a dissolved puddle must not be re-castable (and must not re-spend MP).
            var (go, controller, spell) = MakePuddle();
            controller.SetPlayerInRange(true);
            controller.ApplySolvedImmediate();
            Assert.IsFalse(controller.CanNeutralizeWith("neutralize"));
            Cleanup(go, spell);
        }

        private static (GameObject, AcidPuddleController, SpellData) MakePuddle()
        {
            SpellData spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = "neutralize";
            spell.mpCost = 6;

            GameObject go = new GameObject("AcidPuddle");
            go.AddComponent<BoxCollider2D>();              // satisfies [RequireComponent]
            go.AddComponent<SpriteRenderer>();
            var controller = go.AddComponent<AcidPuddleController>();
            SetPrivateField(controller, "_neutralizeSpells", new List<SpellData> { spell });
            return (go, controller, spell);
        }

        private static void Cleanup(GameObject go, SpellData spell)
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(spell);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"field {fieldName} not found");
            field.SetValue(target, value);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run in Unity: Test Runner → EditMode → `AcidPuddleControllerTests`.
Expected: FAIL to compile — `AcidPuddleController` does not exist.

- [ ] **Step 3: Write the proximity forwarder**

Create `Assets/Scripts/Platformer/AcidPuddleProximityForwarder.cs`:

```csharp
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Sits on a child trigger collider slightly LARGER than the puddle's damage area so
    /// the player can neutralize the acid from the edge without taking a damage tick.
    /// Forwards player enter/exit to the parent <see cref="AcidPuddleController"/>.
    /// Mirrors <see cref="BurnableObstacleProximityForwarder"/>.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class AcidPuddleProximityForwarder : MonoBehaviour
    {
        [SerializeField] private AcidPuddleController _controller;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
            if (_controller == null)
                _controller = GetComponentInParent<AcidPuddleController>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_controller == null) return;
            _controller.SetPlayerInRange(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_controller == null) return;
            _controller.SetPlayerInRange(false);
        }
    }
}
```

- [ ] **Step 4: Write the controller**

Create `Assets/Scripts/Platformer/AcidPuddleController.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Acid puddle hazard (DEV-94 Level 3). A floor pool that:
    ///   - loops its 6-frame sprite animation forever, desynced per instance (random
    ///     speed + random start frame), like the Level 2 animated lava tile;
    ///   - deals ESCALATING damage-over-time while the player overlaps it, resetting the
    ///     escalation when they step out (modeled on HazardTrigger's enter/tick/exit);
    ///   - DISSOLVES (alpha fade + particle VFX) when the player casts a neutralize spell
    ///     from within the proximity zone, persisting the cleared state across a Battle
    ///     round-trip (modeled on BurnableObstacleController).
    ///
    /// MonoBehaviour holds lifecycle + Unity refs only. Pure logic lives in the static
    /// helpers AcidPuddle (spell match) and AcidPuddleDamage (escalation curve); damage
    /// math reuses HazardDamageResolver; player feedback reuses PlayerHurtFeedback.
    ///
    /// PlayerDeathHandler observes PlayerState.CurrentHp and dispatches death/respawn —
    /// this component never knows about death.
    ///
    /// Spec: docs/superpowers/specs/2026-06-22-dev-94-level-3-acid-puddle-design.md
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class AcidPuddleController : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Sprite[] _acidFrames;
        [SerializeField, Min(0.1f)] private float _minSpeed = 5f;
        [SerializeField, Min(0.1f)] private float _maxSpeed = 7f;

        [Header("Damage")]
        [SerializeField]
        [Tooltip("Trigger sized to the visible acid. Disabled when the puddle dissolves.")]
        private Collider2D _damageCollider;
        [SerializeField, Range(0, 100)] private int _baseTickPercent = 3;
        [SerializeField, Min(1f)] private float _growthFactor = 1.6f;
        [SerializeField, Range(0, 100)] private int _maxTickPercent = 25;
        [SerializeField, Range(0.1f, 3f)] private float _tickIntervalSeconds = 0.5f;

        [Header("Neutralize")]
        [SerializeField] private List<SpellData> _neutralizeSpells = new();
        [SerializeField, Min(0f)] private float _fadeDuration = 0.6f;

        [SerializeField]
        [Tooltip("Stable, scene-unique ID used to persist the dissolved state across a Battle round-trip. Leave blank to opt out of persistence.")]
        private string _puzzleId;
        public string PuzzleId => _puzzleId;

        [Header("Success cue")]
        [SerializeField] private ParticleSystem _successVfx;
        [SerializeField] private AudioClip _successSfx;
        [SerializeField] private AudioSource _audioSource;

        private bool _isNeutralized;
        private bool _isPlayerInRange;
        private int _tickIndex;
        private PlayerHurtFeedback _feedback;
        private Coroutine _tickCoroutine;
        private Coroutine _animateCoroutine;

        public bool IsNeutralized => _isNeutralized;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void Start()
        {
            if (_audioSource != null && GameManager.Instance != null
                && GameManager.Instance.AudioManager != null)
            {
                GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(_audioSource);
            }

            // Guard against the restore controller (Script Execution Order -10) having
            // already marked this puddle solved before our Start runs — don't re-animate
            // a dissolved puddle.
            if (!_isNeutralized)
                _animateCoroutine = StartCoroutine(AnimateLoop());
        }

        // ── Animation ───────────────────────────────────────────────
        private IEnumerator AnimateLoop()
        {
            if (_spriteRenderer == null || _acidFrames == null || _acidFrames.Length == 0)
                yield break;

            float speed = Random.Range(_minSpeed, _maxSpeed);
            var wait = new WaitForSeconds(1f / speed);
            int frame = Random.Range(0, _acidFrames.Length);
            while (true)
            {
                _spriteRenderer.sprite = _acidFrames[frame];
                frame = (frame + 1) % _acidFrames.Length;
                yield return wait;
            }
        }

        // ── Escalating DoT ──────────────────────────────────────────
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isNeutralized) return;
            if (!other.CompareTag("Player")) return;

            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[AcidPuddleController] GameManager not found — acid ignored.", this);
                return;
            }

            _feedback = other.GetComponentInParent<PlayerHurtFeedback>();
            _tickIndex = 0;
            ApplyTickDamage();                 // immediate mild first tick (tick 0 = base)
            _feedback?.PlayHurtAnimation();
            _feedback?.BeginPainOverlap();

            if (_tickCoroutine != null)
                StopCoroutine(_tickCoroutine);
            _tickCoroutine = StartCoroutine(TickWhileOverlapping());
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            StopTicking();
            _feedback?.EndPainOverlap();
            _feedback = null;
            _tickIndex = 0;                    // reset escalation on exit
        }

        private void OnDisable()
        {
            // Disabling/destroying mid-overlap (e.g. level unload) must not leave the
            // player tinted or a coroutine running on a dead object.
            StopTicking();
            _feedback?.EndPainOverlap();
            _feedback = null;
        }

        private IEnumerator TickWhileOverlapping()
        {
            var wait = new WaitForSeconds(_tickIntervalSeconds);
            while (true)
            {
                yield return wait;
                if (GameManager.Instance == null)
                    continue;
                _tickIndex++;
                ApplyTickDamage();
                _feedback?.FlashOnTick();
            }
        }

        private void ApplyTickDamage()
        {
            int percent = AcidPuddleDamage.PercentForTick(
                _tickIndex, _baseTickPercent, _growthFactor, _maxTickPercent);

            PlayerState state = GameManager.Instance.PlayerState;
            HazardDamageResult result = HazardDamageResolver.Resolve(
                currentHp: state.CurrentHp,
                maxHp: state.MaxHp,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: percent);
            state.SetCurrentHp(result.NewHp);
        }

        private void StopTicking()
        {
            if (_tickCoroutine != null)
            {
                StopCoroutine(_tickCoroutine);
                _tickCoroutine = null;
            }
        }

        // ── Neutralize + removal ────────────────────────────────────
        public void SetPlayerInRange(bool inRange) => _isPlayerInRange = inRange;

        public bool CanNeutralizeWith(string spellId)
        {
            if (_isNeutralized) return false;
            if (!_isPlayerInRange) return false;
            return AcidPuddle.CanNeutralize(spellId, BuildNeutralizeSpellIds());
        }

        public bool TryNeutralize(string spellId)
        {
            if (!CanNeutralizeWith(spellId)) return false;
            Neutralize();
            return true;
        }

        private void Neutralize()
        {
            _isNeutralized = true;

            if (!string.IsNullOrWhiteSpace(_puzzleId) && GameManager.Instance != null)
                GameManager.Instance.MarkPuzzleSolved(_puzzleId);

            // Stop hurting the player immediately — including when they neutralize while
            // standing in the acid.
            StopTicking();
            _feedback?.EndPainOverlap();
            _feedback = null;
            _tickIndex = 0;
            if (_damageCollider != null)
                _damageCollider.enabled = false;

            StopAnimating();
            PlaySuccessCue();
            StartCoroutine(FadeOut());
        }

        private void PlaySuccessCue()
        {
            if (_successVfx != null)
                _successVfx.Play();
            if (_audioSource != null && _successSfx != null)
                _audioSource.PlayOneShot(_successSfx);
        }

        private IEnumerator FadeOut()
        {
            if (_spriteRenderer == null)
                yield break;

            Color baseColor = _spriteRenderer.color;
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / _fadeDuration));
                _spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                yield return null;
            }
            _spriteRenderer.enabled = false;
        }

        /// <summary>
        /// Forces the dissolved state with no VFX, no fade, no animation. Called on scene
        /// load by PlatformerWorldRestoreController when this puddle was already neutralized
        /// earlier in the session.
        /// </summary>
        public void ApplySolvedImmediate()
        {
            _isNeutralized = true;
            StopAnimating();
            if (_damageCollider != null)
                _damageCollider.enabled = false;
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;
        }

        private void StopAnimating()
        {
            if (_animateCoroutine != null)
            {
                StopCoroutine(_animateCoroutine);
                _animateCoroutine = null;
            }
        }

        private List<string> BuildNeutralizeSpellIds()
        {
            var ids = new List<string>(_neutralizeSpells.Count);
            for (int i = 0; i < _neutralizeSpells.Count; i++)
            {
                SpellData spell = _neutralizeSpells[i];
                if (spell != null) ids.Add(spell.spellName);
            }

            return ids;
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run in Unity: Test Runner → EditMode → `AcidPuddleControllerTests`.
Expected: PASS (5/5). (The animation/DoT/fade coroutines are exercised in Play Mode in Task 6; this fixture covers the neutralize-gating + restore surface.)

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-94): add AcidPuddleController and proximity forwarder`
- `Assets/Scripts/Platformer/AcidPuddleController.cs`
- `Assets/Scripts/Platformer/AcidPuddleController.cs.meta`
- `Assets/Scripts/Platformer/AcidPuddleProximityForwarder.cs`
- `Assets/Scripts/Platformer/AcidPuddleProximityForwarder.cs.meta`
- `Assets/Tests/Editor/Platformer/AcidPuddleControllerTests.cs`
- `Assets/Tests/Editor/Platformer/AcidPuddleControllerTests.cs.meta`

---

### Task 4: Wire neutralize dispatch (TDD)

**Files:**
- Modify: `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs`
- Modify: `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs`
- Test: `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs`

**Interfaces:**
- Consumes: `AcidPuddleController.CanNeutralizeWith(string)`, `TryNeutralize(string)`, `IsNeutralized` (Task 3).
- Produces: extended `PlatformerSpellWorldCaster.TryCast(..., IReadOnlyList<AcidPuddleController> acidPuddles, PlayerState playerState)`.

- [ ] **Step 1: Write the failing tests**

Add these two tests inside the `PlatformerVoiceSpellControllerTests` class in `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs` (after the existing `Update_RecognizedCombustSpell_...` test; reuse the existing `CreateCharacterData`, `SetPrivateField`, `InvokePrivateMethod` helpers):

```csharp
        [Test]
        public void Update_RecognizedNeutralizeSpell_DissolvesInRangeAcidPuddleAndSpendsMp()
        {
            SpellData neutralize = ScriptableObject.CreateInstance<SpellData>();
            neutralize.spellName = "neutralize";
            neutralize.mpCost = 6;

            GameObject puddleGo = new GameObject("AcidPuddle");
            puddleGo.AddComponent<BoxCollider2D>();
            var puddle = puddleGo.AddComponent<AcidPuddleController>();
            puddle.SetPlayerInRange(true);
            SetPrivateField(puddle, "_neutralizeSpells",
                new System.Collections.Generic.List<SpellData> { neutralize });

            GameObject gameManagerGo = null;
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                gameManagerGo = new GameObject("GameManager");
                gameManager = gameManagerGo.AddComponent<GameManager>();
            }
            CharacterData characterData = CreateCharacterData();
            gameManager.SetPlayerCharacterDataForTests(characterData);
            gameManager.PlayerState.SetCurrentMp(20);

            GameObject controllerGo = new GameObject("PlatformerVoiceSpellController");
            var controller = controllerGo.AddComponent<PlatformerVoiceSpellController>();
            SetPrivateField(controller, "_acidPuddles", new[] { puddle });

            var resultQueue = new ConcurrentQueue<string>();
            resultQueue.Enqueue("{\"text\": \"neutralize\"}");
            controller.Inject(resultQueue, new[] { neutralize }, gameManager.PlayerState);

            InvokePrivateMethod(controller, "Update");

            Assert.IsTrue(puddle.IsNeutralized);
            Assert.AreEqual(14, gameManager.PlayerState.CurrentMp);

            Object.DestroyImmediate(controllerGo);
            if (gameManagerGo != null)
                Object.DestroyImmediate(gameManagerGo);
            Object.DestroyImmediate(puddleGo);
            Object.DestroyImmediate(neutralize);
            Object.DestroyImmediate(characterData);
        }

        [Test]
        public void Update_NeutralizeSpell_PuddleOutOfRange_DoesNotDissolveOrSpendMp()
        {
            // WHY: the edge-zone gate — casting near a puddle you're not in range of must
            // neither dissolve it nor waste MP.
            SpellData neutralize = ScriptableObject.CreateInstance<SpellData>();
            neutralize.spellName = "neutralize";
            neutralize.mpCost = 6;

            GameObject puddleGo = new GameObject("AcidPuddle");
            puddleGo.AddComponent<BoxCollider2D>();
            var puddle = puddleGo.AddComponent<AcidPuddleController>();
            // SetPlayerInRange NOT called -> out of range.
            SetPrivateField(puddle, "_neutralizeSpells",
                new System.Collections.Generic.List<SpellData> { neutralize });

            GameObject gameManagerGo = null;
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                gameManagerGo = new GameObject("GameManager");
                gameManager = gameManagerGo.AddComponent<GameManager>();
            }
            CharacterData characterData = CreateCharacterData();
            gameManager.SetPlayerCharacterDataForTests(characterData);
            gameManager.PlayerState.SetCurrentMp(20);

            GameObject controllerGo = new GameObject("PlatformerVoiceSpellController");
            var controller = controllerGo.AddComponent<PlatformerVoiceSpellController>();
            SetPrivateField(controller, "_acidPuddles", new[] { puddle });

            var resultQueue = new ConcurrentQueue<string>();
            resultQueue.Enqueue("{\"text\": \"neutralize\"}");
            controller.Inject(resultQueue, new[] { neutralize }, gameManager.PlayerState);

            InvokePrivateMethod(controller, "Update");

            Assert.IsFalse(puddle.IsNeutralized);
            Assert.AreEqual(20, gameManager.PlayerState.CurrentMp);

            Object.DestroyImmediate(controllerGo);
            if (gameManagerGo != null)
                Object.DestroyImmediate(gameManagerGo);
            Object.DestroyImmediate(puddleGo);
            Object.DestroyImmediate(neutralize);
            Object.DestroyImmediate(characterData);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run in Unity: Test Runner → EditMode → `PlatformerVoiceSpellControllerTests`.
Expected: FAIL to compile — `_acidPuddles` field does not exist on `PlatformerVoiceSpellController`.

- [ ] **Step 3: Extend `PlatformerSpellWorldCaster.TryCast`**

In `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs`, change the method signature to add the `acidPuddles` parameter immediately before `playerState`:

```csharp
        public static bool TryCast(
            SpellData spell,
            IReadOnlyList<MeltableObstacleController> meltableObstacles,
            IReadOnlyList<FreezablePlatformController> freezablePlatforms,
            IReadOnlyList<BurnableObstacleController> burnableObstacles,
            IReadOnlyList<SteamVentController> steamVents,
            IReadOnlyList<AcidPuddleController> acidPuddles,
            PlayerState playerState)
```

In **Phase 1** (the `hasWorldTarget` checks), add this block immediately after the `steamVents` check block (after its closing `}` and before `if (!hasWorldTarget) return false;`):

```csharp
            if (!hasWorldTarget && acidPuddles != null)
            {
                for (int i = 0; i < acidPuddles.Count; i++)
                {
                    AcidPuddleController puddle = acidPuddles[i];
                    if (puddle != null && puddle.CanNeutralizeWith(spell.spellName))
                    {
                        hasWorldTarget = true;
                        break;
                    }
                }
            }
```

In **Phase 2** (the cast-to-all loops), add this block immediately after the `steamVents` cast loop (before `return handled;`):

```csharp
            if (acidPuddles != null)
            {
                for (int i = 0; i < acidPuddles.Count; i++)
                {
                    AcidPuddleController puddle = acidPuddles[i];
                    if (puddle != null && puddle.TryNeutralize(spell.spellName))
                        handled = true;
                }
            }
```

- [ ] **Step 4: Extend `PlatformerVoiceSpellController`**

In `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs`:

Add the serialized field after the `_steamVents` field:

```csharp
        [SerializeField]
        [Tooltip("Optional explicit acid puddles. Leave empty to find all active AcidPuddleController instances in the scene.")]
        private AcidPuddleController[] _acidPuddles;
```

Add the cached scene-list after `_sceneSteamVents`:

```csharp
        private readonly List<AcidPuddleController> _sceneAcidPuddles = new();
```

Pass the resolved puddles into the `TryCast` call (add the argument before the `playerState` argument):

```csharp
                PlatformerSpellWorldCaster.TryCast(
                    matched,
                    ResolveMeltableObstacles(),
                    ResolveFreezablePlatforms(),
                    ResolveBurnableObstacles(),
                    ResolveSteamVents(),
                    ResolveAcidPuddles(),
                    _playerState ?? GameManager.Instance?.PlayerState);
```

Add the resolver method after `ResolveSteamVents()`:

```csharp
        private IReadOnlyList<AcidPuddleController> ResolveAcidPuddles()
        {
            if (_acidPuddles != null && _acidPuddles.Length > 0)
                return _acidPuddles;

            _sceneAcidPuddles.Clear();
            _sceneAcidPuddles.AddRange(FindObjectsByType<AcidPuddleController>());
            return _sceneAcidPuddles;
        }
```

- [ ] **Step 5: Run tests to verify they pass**

Run in Unity: Test Runner → EditMode → `PlatformerVoiceSpellControllerTests`.
Expected: PASS — all tests including the two new neutralize tests, and the existing melt/freeze/combust tests still green (their `TryCast` calls now pass `ResolveAcidPuddles()` automatically).

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-94): dispatch neutralize spell to acid puddles`
- `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs`
- `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs.meta`
- `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs`
- `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs.meta`
- `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs`
- `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs.meta`

---

### Task 5: Persist dissolved puddles across a Battle round-trip

**Files:**
- Modify: `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs`

**Interfaces:**
- Consumes: `AcidPuddleController.PuzzleId`, `AcidPuddleController.ApplySolvedImmediate()` (Task 3); existing `GameManager.Instance.IsPuzzleSolved(string)`.

- [ ] **Step 1: Add the acid-puddle restore block**

In `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs`, inside `ReapplySolvedPuzzles()`, add this block immediately after the `ExplodableBarrierController` loop (and before the steam-vent comment):

```csharp
            AcidPuddleController[] acidPuddles =
                FindObjectsByType<AcidPuddleController>(FindObjectsInactive.Exclude);
            foreach (AcidPuddleController puddle in acidPuddles)
            {
                if (!string.IsNullOrWhiteSpace(puddle.PuzzleId)
                    && GameManager.Instance.IsPuzzleSolved(puddle.PuzzleId))
                {
                    puddle.ApplySolvedImmediate();
                }
            }
```

- [ ] **Step 2: Verify compilation**

Return to Unity, let it recompile. Expected: no compile errors in the Console. (`ApplySolvedImmediate` was unit-tested in Task 3; the scene-load restore path itself is verified manually in Task 6's Play Mode round-trip, as it requires a live `GameManager` + loaded scene and has no Edit Mode seam — same as the existing burnable/barrier restore.)

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-94): restore dissolved acid puddles after battle`
- `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs`
- `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs.meta`

---

### Task 6: Build the prefab and place puddles (Unity Editor — user)

All prior tasks compile and pass Edit Mode tests. This task is hands-on in the Unity Editor; Claude writes no code here.

- [ ] **Step 1: Confirm the sprite import**

> **Unity Editor task (user):** Select `Assets/Art/Sprites/Platformer/acid puddle.png`. Confirm Sprite Mode = Multiple and 6 frames are sliced. Set Filter Mode = Point (no filter) and Compression = None if matching the other platformer sprites; set Pixels Per Unit to match neighboring hazards (16).

- [ ] **Step 2: Build `P_AcidPuddle.prefab`**

> **Unity Editor task (user):** Create `Assets/Prefabs/Platformer/P_AcidPuddle.prefab`:
> - Root GameObject `AcidPuddle`, tagged as needed, on the same sorting layer as other platformer hazards.
>   - `SpriteRenderer` — assign frame 0 of the acid sheet.
>   - `BoxCollider2D` (the **damage** trigger) — `Is Trigger = ON`, sized to the visible acid.
>   - `AcidPuddleController` component. Wire fields: `_spriteRenderer` = the SpriteRenderer; `_acidFrames` = all 6 frames in display order; `_damageCollider` = the damage BoxCollider2D; `_neutralizeSpells` = [`SD_Neutralize`]; `_successVfx` / `_successSfx` / `_audioSource` = the cue below; leave `_puzzleId` blank on the prefab (set per-instance in Step 4). Keep the tuning defaults unless playtesting says otherwise.
> - Child `NeutralizeZone`:
>   - `BoxCollider2D`, `Is Trigger = ON`, **larger** than the damage collider (so the player can dissolve from the edge).
>   - `AcidPuddleProximityForwarder` — its `_controller` auto-fills via `Reset`; confirm it points at the root controller.
> - Child `NeutralizeVFX`:
>   - `ParticleSystem` for the dissolve splatter (green/acid). Set `Play On Awake = OFF` (the controller calls `Play()`). Assign it to the controller's `_successVfx`.
>   - `AudioSource` (`Play On Awake = OFF`, `Spatial Blend` per other cues) → controller's `_audioSource`; assign the dissolve SFX clip to `_successSfx`.

- [ ] **Step 3: Confirm the player is tagged `Player`**

> **Unity Editor task (user):** Confirm the platformer player GameObject (the one with `PlayerHurtFeedback`) is tagged `Player` — both triggers gate on `CompareTag("Player")`. (Already true for the existing spike/lava hazards.)

- [ ] **Step 4: Place puddles in Level 3 with escalating difficulty**

> **Unity Editor task (user):** In the Level 3 scene, place `P_AcidPuddle` instances over the acid-pool sections. Give **each instance a unique `_puzzleId`** (e.g. `level3_acid_3_1_a`). Stage difficulty per DEV-94's "simple → complex" requirement: 3-1 a single narrow pool to teach Neutralize; 3-2 wider pools / a pool mid-jump; 3-3 sequences combined with other platforming. Ensure at least one safe path or that Neutralize is always castable so the level never soft-locks (the player reaches L3, so `neutralize` is unlocked).

- [ ] **Step 5: (Optional) Wire the controller explicitly**

> **Unity Editor task (user):** Either leave `PlatformerVoiceSpellController._acidPuddles` empty (it auto-discovers via `FindObjectsByType`) or drag the placed puddles into the array for explicit control — matching how the other obstacle arrays are wired in this scene.

- [ ] **Step 6: Run the full Edit Mode suite**

> **Unity Editor task (user):** Window → General → Test Runner → EditMode → Run All. Confirm `AcidPuddleTests`, `AcidPuddleDamageTests`, `AcidPuddleControllerTests`, and `PlatformerVoiceSpellControllerTests` are green, and nothing else regressed.

- [ ] **Step 7: Play Mode verification (manual)**

> **Unity Editor task (user):** Enter Play Mode in Level 3 and confirm:
> - Multiple puddles animate out of sync (different loop timing).
> - Standing in acid ramps damage tick-over-tick; stepping out and back in restarts mild (escalation resets on exit).
> - Speaking `neutralize` from the **edge** (in the larger zone, not the acid) dissolves the puddle — fade + particle VFX + SFX — without a damage tick; crossing afterward deals no damage.
> - Speaking `neutralize` while standing in the acid stops the DoT immediately and dissolves it.
> - MP drops by 6 per successful neutralize; an out-of-range cast neither dissolves nor spends MP.
> - Solve a puddle, trigger a battle, win, return: the puddle is still gone (persistence). (Give it a `_puzzleId` for this to hold.)

- [ ] **Step 8: Check in via UVCS**

Unity Version Control → Pending Changes → stage the new prefab + its meta and the modified Level 3 scene → Check in with message: `feat(DEV-94): add acid puddle prefab and Level 3 placements`
- `Assets/Prefabs/Platformer/P_AcidPuddle.prefab`
- `Assets/Prefabs/Platformer/P_AcidPuddle.prefab.meta`
- the Level 3 scene `.unity` file (and any new folder `.meta` files created under `Assets/Prefabs/Platformer/`)

---

## Self-Review

**1. Spec coverage** — every spec requirement maps to a task:

| Spec requirement | Task |
|---|---|
| 6-frame looping animation, per-instance desync (random speed + start frame) | Task 3 (`AnimateLoop`) + Task 6 Step 7 |
| Escalating DoT, resets on exit | Task 2 (curve) + Task 3 (`TickWhileOverlapping`/`OnTriggerExit2D`) |
| Reuse `HazardDamageResolver` + `PlayerHurtFeedback` (don't modify `HazardTrigger`) | Task 3 (`ApplyTickDamage`) |
| `neutralize` removal via static match helper | Task 1 + Task 3 (`CanNeutralizeWith`/`TryNeutralize`) |
| Edge-zone proximity (clear without taking damage) | Task 3 (`AcidPuddleProximityForwarder`) + Task 6 Step 2 |
| Fade-out + particle VFX removal (no removal frames) | Task 3 (`FadeOut`/`PlaySuccessCue`) |
| Dispatch through existing caster/controller | Task 4 |
| `puzzleId` persistence across Battle round-trip | Task 3 (`ApplySolvedImmediate`) + Task 5 |
| Floor pool, no solid collider, "cross safely" | Global Constraints + Task 6 Step 2 |
| AC general reqs: no combat, telegraphed, escalating difficulty, A/V feedback | Task 6 Steps 4 & 7 (placement + cue) |

No gaps.

**2. Placeholder scan** — no `TBD`/`TODO`/"similar to"/"add error handling" remain; every code step shows complete code; every Editor step is explicit.

**3. Type consistency** — `CanNeutralize(string, IReadOnlyList<string>)`, `PercentForTick(int, int, float, int)`, `CanNeutralizeWith(string)`, `TryNeutralize(string)`, `IsNeutralized`, `PuzzleId`, `ApplySolvedImmediate()`, and `SetPlayerInRange(bool)` are spelled and typed identically in their producing task, the controller, the tests, and the dispatch/restore call sites. `TryCast`'s new `acidPuddles` parameter sits before `playerState` in both the signature (Task 4 Step 3) and the call site (Task 4 Step 4).

## Notes / deliberate deviations

- **No Context7/Exa research performed.** Every API used (`StartCoroutine`/`WaitForSeconds`, `OnTriggerEnter2D`, `Random.Range`, `Mathf.Lerp`, `FindObjectsByType`, NUnit) is already used verbatim in the files this plan mirrors (`HazardTrigger`, `BurnableObstacleController`, the existing tests), which are the authoritative current-Unity-6 references; an Exa MCP server is not connected to this session.
- **`unity-developer` skill not loaded** (not installed as a local file); the project's own verified code is the idiom source instead.
- **Restore path (Task 5) has no Edit Mode test** — it requires a live `GameManager` + loaded scene with no testable seam, matching the existing burnable/barrier restore. `ApplySolvedImmediate` itself IS unit-tested (Task 3); the wiring is verified in Task 6's Play Mode round-trip.
</content>
