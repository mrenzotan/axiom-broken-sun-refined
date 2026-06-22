# DEV-94 Level 2 Combustion Puzzles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. For this Unity project, also pair with `executing-unity-game-dev-plans` (UVCS check-ins + Editor handoffs).

**Goal:** Add the Combustion half of DEV-94 — a burnable crate and a steam vent the player ignites by speaking `combust` / `ancient burn`, mirroring the existing Level 1 Freeze/Melt puzzle pattern.

**Architecture:** Each puzzle object is a `SpriteRenderer` + `BoxCollider2D` MonoBehaviour controller driving code-swapped `Sprite[]` frames (no Animator), fed by a pure static spell-match helper, a child proximity trigger, the existing `PlatformerSpellWorldCaster` dispatch, and `GameManager` puzzle-solved persistence. The steam vent clears separate obstacles through an `IExplosionDestructible` contract.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, C#, New Input System, Cinemachine, Unity Test Framework (Edit Mode/NUnit), UVCS.

## Global Constraints

- **MonoBehaviours = Unity lifecycle only**; all reusable logic in plain C# (the spell-match helper). (CLAUDE.md architecture standard)
- **No Unity Animator / AnimationClip** — animate by swapping `Sprite[]` frames in coroutines at a configurable FPS. (Project convention; see `2026-06-18-ice-wall-animated-sprite-design.md`)
- **No static singletons except `GameManager`.**
- **ScriptableObject-driven data** — ignite spells are `SpellData` assets, never hardcoded names.
- **No new assembly definitions.** New runtime scripts live under `Assets/Scripts/Platformer/` (`Axiom.Platformer`) and `Assets/Scripts/Voice/` (`Axiom.Voice`, already references `Axiom.Platformer`). Tests live under `Assets/Tests/Editor/Platformer/` (`PlatformerTests`).
- **Battle chemistry is untouched.** The platformer only string-matches `spell.spellName`; it never references `ChemicalCondition` / `SpellEffectResolver`. (chemistry doc invariant: conditions are battle-scoped)
- **Version control = UVCS only**, never git. Check-in message format: `<type>(DEV-94): <short description>`.
- **Editor/code split:** Claude writes all `.cs`. The user performs all Unity Editor actions (prefabs, scene authoring, running Test Runner, Play Mode).
- Ignite spells: `combust` (`SD_Combust.asset`), `ancient burn` (`SD_AncientBurn.asset`). Sprite sheet `burnable and geyser-Sheet.png`: `burnable-0..5` (6 frames, intact→charred), `geyser-0..5` (6 frames, split into `geyser-0,1,2` looping idle puff + `geyser-3,4,5` one-shot eruption played on ignite).

**Reference files to mirror exactly:** `Assets/Scripts/Platformer/MeltableObstacle.cs`, `MeltableObstacleController.cs`, `MeltableObstacleProximityForwarder.cs`, `FreezablePlatformController.cs`, and `Assets/Tests/Editor/Platformer/MeltableObstacleTests.cs`.

---

## File Structure

**Create (C#):**
- `Assets/Scripts/Platformer/BurnableObstacle.cs` — pure static ignite-match helper (shared by crate + vent).
- `Assets/Scripts/Platformer/IExplosionDestructible.cs` — contract a vent blast clears.
- `Assets/Scripts/Platformer/BurnableObstacleController.cs` — crate MonoBehaviour.
- `Assets/Scripts/Platformer/BurnableObstacleProximityForwarder.cs` — crate proximity trigger.
- `Assets/Scripts/Platformer/ExplodableBarrierController.cs` — vent-cleared rubble barrier.
- `Assets/Scripts/Platformer/SteamVentController.cs` — vent MonoBehaviour.
- `Assets/Scripts/Platformer/SteamVentProximityForwarder.cs` — vent proximity trigger.
- `Assets/Tests/Editor/Platformer/BurnableObstacleTests.cs` — Edit Mode tests for the helper.

**Modify (C#):**
- `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs` — extend `TryCast` with burnables + vents.
- `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs` — resolve + pass the new arrays.
- `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs` — restore solved burnables/barriers (vents are stateless/re-ignitable, not restored).
- `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs` — add the combust dispatch integration test.

**Create (Unity Editor, user):** `P_BurnableCrate.prefab`, `P_ExplodableBarrier.prefab`, `P_SteamVent.prefab`; 2-1/2-2/2-3 puzzle layouts; optional gradient `SkyFill`.

---

### Task 1: Combustion ignite-match helper (TDD)

**Files:**
- Create: `Assets/Scripts/Platformer/BurnableObstacle.cs`
- Test: `Assets/Tests/Editor/Platformer/BurnableObstacleTests.cs`

**Interfaces:**
- Produces: `public static bool BurnableObstacle.CanIgnite(string spellId, IReadOnlyList<string> igniteSpellIds)` — `true` iff `spellId` is non-empty and present in `igniteSpellIds`. Consumed by `BurnableObstacleController` and `SteamVentController`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/Editor/Platformer/BurnableObstacleTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class BurnableObstacleTests
    {
        [Test]
        public void CanIgnite_NullSpellId_ReturnsFalse()
        {
            var igniteSpellIds = new List<string> { "combust" };
            Assert.IsFalse(BurnableObstacle.CanIgnite(null, igniteSpellIds));
        }

        [Test]
        public void CanIgnite_EmptySpellId_ReturnsFalse()
        {
            var igniteSpellIds = new List<string> { "combust" };
            Assert.IsFalse(BurnableObstacle.CanIgnite(string.Empty, igniteSpellIds));
        }

        [Test]
        public void CanIgnite_NullList_ReturnsFalse()
        {
            Assert.IsFalse(BurnableObstacle.CanIgnite("combust", null));
        }

        [Test]
        public void CanIgnite_SpellInList_ReturnsTrue()
        {
            var igniteSpellIds = new List<string> { "combust", "ancient burn" };
            Assert.IsTrue(BurnableObstacle.CanIgnite("combust", igniteSpellIds));
        }

        [Test]
        public void CanIgnite_SpellNotInList_ReturnsFalse()
        {
            var igniteSpellIds = new List<string> { "combust" };
            Assert.IsFalse(BurnableObstacle.CanIgnite("freeze", igniteSpellIds));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

> **Unity Editor task (user):** Window → General → Test Runner → EditMode tab → Run `BurnableObstacleTests`.
> Expected: compile error / FAIL — `BurnableObstacle` does not exist yet.

- [ ] **Step 3: Write the minimal implementation**

Create `Assets/Scripts/Platformer/BurnableObstacle.cs`:

```csharp
using System.Collections.Generic;

namespace Axiom.Platformer
{
    /// <summary>
    /// Pure combustion spell-match logic, shared by BurnableObstacleController and
    /// SteamVentController. Mirrors <see cref="MeltableObstacle"/>.CanMelt.
    /// </summary>
    public static class BurnableObstacle
    {
        public static bool CanIgnite(string spellId, IReadOnlyList<string> igniteSpellIds)
        {
            if (string.IsNullOrEmpty(spellId)) return false;
            if (igniteSpellIds == null) return false;

            for (int i = 0; i < igniteSpellIds.Count; i++)
            {
                if (igniteSpellIds[i] == spellId) return true;
            }

            return false;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

> **Unity Editor task (user):** Test Runner → EditMode → Run `BurnableObstacleTests`. Expected: all 5 PASS.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-94): add combustion ignite-match helper`
- `Assets/Scripts/Platformer/BurnableObstacle.cs`
- `Assets/Scripts/Platformer/BurnableObstacle.cs.meta`
- `Assets/Tests/Editor/Platformer/BurnableObstacleTests.cs`
- `Assets/Tests/Editor/Platformer/BurnableObstacleTests.cs.meta`

---

### Task 2: Explosion contract + burnable crate controller

**Files:**
- Create: `Assets/Scripts/Platformer/IExplosionDestructible.cs`
- Create: `Assets/Scripts/Platformer/BurnableObstacleController.cs`
- Create: `Assets/Scripts/Platformer/BurnableObstacleProximityForwarder.cs`

**Interfaces:**
- Consumes: `BurnableObstacle.CanIgnite(...)` (Task 1); `GameManager.Instance.MarkPuzzleSolved(string)`, `GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(AudioSource)`; `SpellData.spellName`.
- Produces:
  - `interface IExplosionDestructible { void Detonate(); }`
  - `BurnableObstacleController`: `void SetPlayerInRange(bool)`, `bool CanIgniteWith(string)`, `bool TryIgnite(string)`, `void Detonate()`, `void ApplySolvedImmediate()`, `bool IsBurned`, `string PuzzleId`.

- [ ] **Step 1: Create the interface**

Create `Assets/Scripts/Platformer/IExplosionDestructible.cs`:

```csharp
namespace Axiom.Platformer
{
    /// <summary>
    /// Something a steam-vent explosion can clear (a burnable crate, a rubble barrier).
    /// The vent talks only to this contract, never to concrete obstacle types.
    /// </summary>
    public interface IExplosionDestructible
    {
        /// <summary>Clear/destroy self as a consequence of a nearby explosion. Idempotent.</summary>
        void Detonate();
    }
}
```

- [ ] **Step 2: Create the crate controller**

Create `Assets/Scripts/Platformer/BurnableObstacleController.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;
using UnityEngine.Events;

namespace Axiom.Platformer
{
    public class BurnableObstacleController : MonoBehaviour, IExplosionDestructible
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private BoxCollider2D _solidCollider;
        [SerializeField] private Sprite[] _burnFrames;
        [SerializeField, Min(0.1f)] private float _burnFps = 10f;
        [SerializeField] private List<SpellData> _igniteSpells = new();

        [SerializeField]
        [Tooltip("Stable, scene-unique ID used to persist the solved (burned) state across a Battle round-trip. Leave blank to opt out of persistence.")]
        private string _puzzleId;

        public string PuzzleId => _puzzleId;

        [Header("Success cue")]
        [SerializeField] private ParticleSystem _successVfx;
        [SerializeField] private AudioClip _successSfx;
        [SerializeField] private AudioSource _audioSource;

        [SerializeField]
        [Tooltip("Fired when the crate ignites (direct cast or a vent blast). Wire to a CinemachineImpulseSource.GenerateImpulse (camera shake) or any other scene reaction. Keeps this asmdef free of a Cinemachine reference.")]
        private UnityEvent _onIgnited;

        private static readonly Color FlashTint = new(1f, 0xA5 / 255f, 0x3D / 255f, 1f); // warm orange
        private const float FlashDuration = 0.15f;

        private bool _isBurned;
        private bool _isPlayerInRange;

        public bool IsBurned => _isBurned;

        private void Start()
        {
            if (_audioSource != null && GameManager.Instance != null
                && GameManager.Instance.AudioManager != null)
            {
                GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(_audioSource);
            }
        }

        public void SetPlayerInRange(bool inRange)
        {
            _isPlayerInRange = inRange;
        }

        public bool CanIgniteWith(string spellId)
        {
            if (_isBurned) return false;
            if (!_isPlayerInRange) return false;

            return BurnableObstacle.CanIgnite(spellId, BuildIgniteSpellIds());
        }

        public bool TryIgnite(string spellId)
        {
            if (!CanIgniteWith(spellId)) return false;
            Ignite();
            return true;
        }

        // IExplosionDestructible — a vent's blast ignites this crate regardless of
        // player range or spell. Idempotent: a spent crate ignores repeat detonations.
        public void Detonate()
        {
            if (_isBurned) return;
            Ignite();
        }

        private void Ignite()
        {
            _isBurned = true;

            if (!string.IsNullOrWhiteSpace(_puzzleId) && GameManager.Instance != null)
                GameManager.Instance.MarkPuzzleSolved(_puzzleId);

            PlaySuccessCue();
            StartCoroutine(BurnCoroutine());
        }

        private void PlaySuccessCue()
        {
            if (_successVfx != null)
                _successVfx.Play();
            if (_audioSource != null && _successSfx != null)
                _audioSource.PlayOneShot(_successSfx);
            _onIgnited?.Invoke();
        }

        /// <summary>
        /// Forces the terminal burned state with no animation and no success cue.
        /// Called on scene load by PlatformerWorldRestoreController when this puzzle
        /// was already solved earlier in the session. Leaves the final charred frame
        /// visible as a walkable scorch mark.
        /// </summary>
        public void ApplySolvedImmediate()
        {
            _isBurned = true;
            if (_solidCollider != null)
                _solidCollider.enabled = false;
            if (_spriteRenderer != null && _burnFrames != null && _burnFrames.Length > 0)
                _spriteRenderer.sprite = _burnFrames[_burnFrames.Length - 1];
        }

        private List<string> BuildIgniteSpellIds()
        {
            var ids = new List<string>(_igniteSpells.Count);
            for (int i = 0; i < _igniteSpells.Count; i++)
            {
                SpellData spell = _igniteSpells[i];
                if (spell != null) ids.Add(spell.spellName);
            }

            return ids;
        }

        private IEnumerator BurnCoroutine()
        {
            yield return FlashCoroutine();
            yield return PlayBurnFrames();
            // Charred final frame remains visible (walkable scorch). Renderer stays enabled.
        }

        private IEnumerator FlashCoroutine()
        {
            if (_spriteRenderer == null) yield break;

            float halfFlash = FlashDuration * 0.5f;
            float elapsed = 0f;
            while (elapsed < halfFlash)
            {
                elapsed += Time.deltaTime;
                _spriteRenderer.color = Color.Lerp(Color.white, FlashTint, Mathf.Clamp01(elapsed / halfFlash));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < halfFlash)
            {
                elapsed += Time.deltaTime;
                _spriteRenderer.color = Color.Lerp(FlashTint, Color.white, Mathf.Clamp01(elapsed / halfFlash));
                yield return null;
            }
            _spriteRenderer.color = Color.white;
        }

        private IEnumerator PlayBurnFrames()
        {
            if (_spriteRenderer == null || _burnFrames == null || _burnFrames.Length == 0)
            {
                if (_solidCollider != null) _solidCollider.enabled = false;
                yield break;
            }

            int colliderDisableFrame = _burnFrames.Length / 2;
            var frameWait = new WaitForSeconds(1f / _burnFps);
            for (int i = 0; i < _burnFrames.Length; i++)
            {
                _spriteRenderer.sprite = _burnFrames[i];

                if (i == colliderDisableFrame && _solidCollider != null)
                    _solidCollider.enabled = false;

                yield return frameWait;
            }

            if (_solidCollider != null) _solidCollider.enabled = false;
        }
    }
}
```

- [ ] **Step 3: Create the proximity forwarder**

Create `Assets/Scripts/Platformer/BurnableObstacleProximityForwarder.cs`:

```csharp
using UnityEngine;

namespace Axiom.Platformer
{
    [RequireComponent(typeof(Collider2D))]
    public class BurnableObstacleProximityForwarder : MonoBehaviour
    {
        [SerializeField] private BurnableObstacleController _controller;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
            if (_controller == null)
                _controller = GetComponentInParent<BurnableObstacleController>();
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

- [ ] **Step 4: Verify compile**

> **Unity Editor task (user):** Return to Unity, let it compile. Expected: Console shows **no compile errors**. (Behavior is verified in Play Mode after the prefab exists — Task 7+.)

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-94): add burnable obstacle controller + explosion contract`
- `Assets/Scripts/Platformer/IExplosionDestructible.cs`
- `Assets/Scripts/Platformer/IExplosionDestructible.cs.meta`
- `Assets/Scripts/Platformer/BurnableObstacleController.cs`
- `Assets/Scripts/Platformer/BurnableObstacleController.cs.meta`
- `Assets/Scripts/Platformer/BurnableObstacleProximityForwarder.cs`
- `Assets/Scripts/Platformer/BurnableObstacleProximityForwarder.cs.meta`

---

### Task 3: Explodable rubble barrier controller

**Files:**
- Create: `Assets/Scripts/Platformer/ExplodableBarrierController.cs`

**Interfaces:**
- Consumes: `IExplosionDestructible` (Task 2); `GameManager` persistence + audio bus.
- Produces: `ExplodableBarrierController` implementing `IExplosionDestructible.Detonate()`, plus `void ApplySolvedImmediate()`, `bool IsCleared`, `string PuzzleId`. Vent-only — no spell list, no proximity forwarder.

- [ ] **Step 1: Create the barrier controller**

Create `Assets/Scripts/Platformer/ExplodableBarrierController.cs`:

```csharp
using System.Collections;
using Axiom.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Axiom.Platformer
{
    /// <summary>
    /// A rubble/boulder barrier cleared only by a steam-vent explosion (the player
    /// cannot ignite it directly). Composed of one or more child block SpriteRenderers
    /// (e.g. tiled explodable-blocks) that fade out together as a single unit on
    /// detonation, behind one shared collider and one puzzleId. Implements
    /// IExplosionDestructible.
    /// </summary>
    public class ExplodableBarrierController : MonoBehaviour, IExplosionDestructible
    {
        [SerializeField]
        [Tooltip("The visual child blocks that make up this barrier. Leave empty to auto-collect every child SpriteRenderer on Awake.")]
        private SpriteRenderer[] _blockRenderers;

        [SerializeField] private BoxCollider2D _solidCollider;
        [SerializeField, Min(0.05f)] private float _fadeDuration = 0.4f;

        [SerializeField]
        [Tooltip("Stable, scene-unique ID used to persist the cleared state across a Battle round-trip. Leave blank to opt out of persistence.")]
        private string _puzzleId;

        public string PuzzleId => _puzzleId;

        [Header("Destruction cue")]
        [SerializeField] private ParticleSystem _debrisVfx;
        [SerializeField] private AudioClip _destroySfx;
        [SerializeField] private AudioSource _audioSource;

        [SerializeField]
        [Tooltip("Fired when the barrier is detonated by a vent blast. Wire to a CinemachineImpulseSource.GenerateImpulse (camera shake) or any other scene reaction. Keeps this asmdef free of a Cinemachine reference.")]
        private UnityEvent _onDetonated;

        private bool _isCleared;
        public bool IsCleared => _isCleared;

        private void Awake()
        {
            if (_blockRenderers == null || _blockRenderers.Length == 0)
                _blockRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void Start()
        {
            if (_audioSource != null && GameManager.Instance != null
                && GameManager.Instance.AudioManager != null)
            {
                GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(_audioSource);
            }
        }

        public void Detonate()
        {
            if (_isCleared) return;
            _isCleared = true;

            if (!string.IsNullOrWhiteSpace(_puzzleId) && GameManager.Instance != null)
                GameManager.Instance.MarkPuzzleSolved(_puzzleId);

            if (_debrisVfx != null) _debrisVfx.Play();
            if (_audioSource != null && _destroySfx != null) _audioSource.PlayOneShot(_destroySfx);
            _onDetonated?.Invoke();

            if (_solidCollider != null) _solidCollider.enabled = false;
            StartCoroutine(FadeOutCoroutine());
        }

        public void ApplySolvedImmediate()
        {
            _isCleared = true;
            if (_solidCollider != null) _solidCollider.enabled = false;
            DisableAllBlocks();
        }

        private IEnumerator FadeOutCoroutine()
        {
            if (_blockRenderers == null || _blockRenderers.Length == 0)
                yield break;

            var startColors = new Color[_blockRenderers.Length];
            for (int i = 0; i < _blockRenderers.Length; i++)
                startColors[i] = _blockRenderers[i] != null ? _blockRenderers[i].color : Color.clear;

            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeDuration);
                for (int i = 0; i < _blockRenderers.Length; i++)
                {
                    if (_blockRenderers[i] == null) continue;
                    Color c = startColors[i];
                    c.a = Mathf.Lerp(startColors[i].a, 0f, t);
                    _blockRenderers[i].color = c;
                }
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.85f, t);
                yield return null;
            }

            DisableAllBlocks();
        }

        private void DisableAllBlocks()
        {
            if (_blockRenderers == null) return;
            for (int i = 0; i < _blockRenderers.Length; i++)
                if (_blockRenderers[i] != null) _blockRenderers[i].enabled = false;
        }
    }
}
```

- [ ] **Step 2: Verify compile**

> **Unity Editor task (user):** Let Unity compile. Expected: no compile errors.

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-94): add explodable rubble barrier controller`
- `Assets/Scripts/Platformer/ExplodableBarrierController.cs`
- `Assets/Scripts/Platformer/ExplodableBarrierController.cs.meta`

---

### Task 4: Steam vent controller + proximity forwarder

**Files:**
- Create: `Assets/Scripts/Platformer/SteamVentController.cs`
- Create: `Assets/Scripts/Platformer/SteamVentProximityForwarder.cs`

**Interfaces:**
- Consumes: `BurnableObstacle.CanIgnite(...)` (Task 1); `IExplosionDestructible.Detonate()` (Task 2); `GameManager` audio bus; `UnityEngine.Events.UnityEvent`; `Physics2D.OverlapCircleAll`.
- Produces: `SteamVentController`: `void SetPlayerInRange(bool)`, `bool CanIgniteWith(string)`, `bool TryIgnite(string)`. **Re-ignitable** — no spent flag, no `PuzzleId`, no `ApplySolvedImmediate` (the vent is stateless and never persisted).

- [ ] **Step 1: Create the vent controller**

Create `Assets/Scripts/Platformer/SteamVentController.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;
using UnityEngine.Events;

namespace Axiom.Platformer
{
    /// <summary>
    /// A re-ignitable steam vent: speaking an ignite spell in range erupts it and
    /// detonates its linked/in-radius obstacles. Unlike the one-shot crate/barrier,
    /// the vent is a permanent scene fixture with NO persisted/solved state and NO
    /// puzzleId — it can be re-cast any number of times. Each cast erupts and spends
    /// MP; clearing obstacles is a no-op once they're already gone (each obstacle is
    /// one-shot and persists across a Battle round-trip via its OWN puzzleId).
    /// </summary>
    public class SteamVentController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Tooltip("Looping idle 'puff' frames (e.g. geyser-0,1,2). Plays continuously while idle, and resumes after each eruption settles.")]
        [SerializeField] private Sprite[] _ventFrames;
        [SerializeField, Min(0.1f)] private float _ventFps = 6f;

        [Tooltip("One-shot eruption frames (e.g. geyser-3,4,5) played once when the vent ignites, after which the idle loop resumes. Leave empty to skip the sprite eruption (blast VFX/SFX still fire).")]
        [SerializeField] private Sprite[] _eruptionFrames;
        [SerializeField, Min(0.1f)] private float _eruptionFps = 10f;

        [SerializeField] private List<SpellData> _igniteSpells = new();

        [Header("Explosion targets")]
        [SerializeField]
        [Tooltip("Obstacles cleared when this vent is ignited. Assign BurnableObstacleController / ExplodableBarrierController instances (anything implementing IExplosionDestructible).")]
        private List<MonoBehaviour> _linkedTargets = new();

        [SerializeField, Min(0f)]
        [Tooltip("Optional. If > 0, also clears any IExplosionDestructible within this radius at ignite time.")]
        private float _blastRadius = 0f;

        [SerializeField]
        [Tooltip("Layers searched by the optional blast radius overlap.")]
        private LayerMask _blastMask = ~0;

        [Header("Blast cue")]
        [SerializeField] private ParticleSystem _blastVfx;
        [SerializeField] private AudioClip _blastSfx;
        [SerializeField] private AudioSource _audioSource;

        [SerializeField]
        [Tooltip("Fired when the vent ignites. Wire to a CinemachineImpulseSource.GenerateImpulse (camera shake) or any other scene reaction. Keeps this asmdef free of a Cinemachine reference.")]
        private UnityEvent _onIgnited;

        private bool _isPlayerInRange;
        private Coroutine _ventLoopCoroutine;

        private void Start()
        {
            StartVentLoop();

            if (_audioSource != null && GameManager.Instance != null
                && GameManager.Instance.AudioManager != null)
            {
                GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(_audioSource);
            }
        }

        public void SetPlayerInRange(bool inRange)
        {
            _isPlayerInRange = inRange;
        }

        public bool CanIgniteWith(string spellId)
        {
            if (!_isPlayerInRange) return false;

            return BurnableObstacle.CanIgnite(spellId, BuildIgniteSpellIds());
        }

        public bool TryIgnite(string spellId)
        {
            if (!CanIgniteWith(spellId)) return false;

            PlayBlastCue();
            PlayEruption();
            DetonateTargets();
            return true;
        }

        private void PlayBlastCue()
        {
            if (_blastVfx != null) _blastVfx.Play();
            if (_audioSource != null && _blastSfx != null) _audioSource.PlayOneShot(_blastSfx);
            _onIgnited?.Invoke();
        }

        private void DetonateTargets()
        {
            var seen = new HashSet<IExplosionDestructible>();

            for (int i = 0; i < _linkedTargets.Count; i++)
            {
                if (_linkedTargets[i] is IExplosionDestructible target && seen.Add(target))
                    target.Detonate();
            }

            if (_blastRadius > 0f)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _blastRadius, _blastMask);
                for (int i = 0; i < hits.Length; i++)
                {
                    var target = hits[i].GetComponentInParent<IExplosionDestructible>();
                    if (target != null && seen.Add(target))
                        target.Detonate();
                }
            }
        }

        private List<string> BuildIgniteSpellIds()
        {
            var ids = new List<string>(_igniteSpells.Count);
            for (int i = 0; i < _igniteSpells.Count; i++)
            {
                SpellData spell = _igniteSpells[i];
                if (spell != null) ids.Add(spell.spellName);
            }

            return ids;
        }

        private void StartVentLoop()
        {
            StopVentLoop();
            _ventLoopCoroutine = StartCoroutine(VentLoopCoroutine());
        }

        // Plays the one-shot eruption frames, then settles back into the idle puff loop.
        private void PlayEruption()
        {
            StopVentLoop();
            _ventLoopCoroutine = StartCoroutine(EruptThenResumeLoopCoroutine());
        }

        private void StopVentLoop()
        {
            if (_ventLoopCoroutine != null)
            {
                StopCoroutine(_ventLoopCoroutine);
                _ventLoopCoroutine = null;
            }
        }

        private IEnumerator EruptThenResumeLoopCoroutine()
        {
            yield return PlayEruptionFramesOnce();
            // Eruption settles: resume the idle puff loop (runs until the object is destroyed).
            yield return VentLoopCoroutine();
        }

        private IEnumerator PlayEruptionFramesOnce()
        {
            if (_spriteRenderer == null || _eruptionFrames == null || _eruptionFrames.Length == 0)
                yield break;

            var frameWait = new WaitForSeconds(1f / _eruptionFps);
            for (int i = 0; i < _eruptionFrames.Length; i++)
            {
                _spriteRenderer.sprite = _eruptionFrames[i];
                yield return frameWait;
            }
        }

        private IEnumerator VentLoopCoroutine()
        {
            if (_spriteRenderer == null || _ventFrames == null || _ventFrames.Length == 0)
                yield break;

            var frameWait = new WaitForSeconds(1f / _ventFps);
            int frame = 0;
            while (true)
            {
                _spriteRenderer.sprite = _ventFrames[frame];
                frame = (frame + 1) % _ventFrames.Length;
                yield return frameWait;
            }
        }
    }
}
```

- [ ] **Step 2: Create the proximity forwarder**

Create `Assets/Scripts/Platformer/SteamVentProximityForwarder.cs`:

```csharp
using UnityEngine;

namespace Axiom.Platformer
{
    [RequireComponent(typeof(Collider2D))]
    public class SteamVentProximityForwarder : MonoBehaviour
    {
        [SerializeField] private SteamVentController _controller;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
            if (_controller == null)
                _controller = GetComponentInParent<SteamVentController>();
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

- [ ] **Step 3: Verify compile**

> **Unity Editor task (user):** Let Unity compile. Expected: no compile errors.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-94): add steam vent controller + proximity forwarder`
- `Assets/Scripts/Platformer/SteamVentController.cs`
- `Assets/Scripts/Platformer/SteamVentController.cs.meta`
- `Assets/Scripts/Platformer/SteamVentProximityForwarder.cs`
- `Assets/Scripts/Platformer/SteamVentProximityForwarder.cs.meta`

---

### Task 5: Wire combustion into the platformer spell dispatch

**Files:**
- Modify: `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs` (full rewrite below)
- Modify: `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs` (full rewrite below)
- Modify: `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs` (add combust integration test)

**Interfaces:**
- Consumes: `BurnableObstacleController.CanIgniteWith/TryIgnite`, `SteamVentController.CanIgniteWith/TryIgnite` (Tasks 2, 4); `PlayerState.TrySpendMp(int)`.
- Produces: extended
  `PlatformerSpellWorldCaster.TryCast(SpellData, IReadOnlyList<MeltableObstacleController>, IReadOnlyList<FreezablePlatformController>, IReadOnlyList<BurnableObstacleController>, IReadOnlyList<SteamVentController>, PlayerState)`.

- [ ] **Step 1: Add the combust integration test** (write it first — it won't compile until Steps 2-3 add the `_burnableObstacles` field, which is the failing state TDD wants)

Append this method inside the existing `PlatformerVoiceSpellControllerTests` class in `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs`. It reuses that file's `SetPrivateField`, `InvokePrivateMethod`, and `CreateCharacterData` helpers, and mirrors the existing melt test exactly:

```csharp
        [Test]
        public void Update_RecognizedCombustSpell_IgnitesInRangeBurnableObstacleAndSpendsMp()
        {
            SpellData combust = ScriptableObject.CreateInstance<SpellData>();
            combust.spellName = "combust";
            combust.mpCost = 8;

            GameObject obstacleGo = new GameObject("BurnableObstacle");
            var obstacle = obstacleGo.AddComponent<BurnableObstacleController>();
            obstacle.SetPlayerInRange(true);
            SetPrivateField(obstacle, "_igniteSpells", new System.Collections.Generic.List<SpellData> { combust });

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
            SetPrivateField(controller, "_burnableObstacles", new[] { obstacle });

            var resultQueue = new ConcurrentQueue<string>();
            resultQueue.Enqueue("{\"text\": \"combust\"}");
            controller.Inject(resultQueue, new[] { combust }, gameManager.PlayerState);

            InvokePrivateMethod(controller, "Update");

            Assert.IsTrue(obstacle.IsBurned);
            Assert.AreEqual(12, gameManager.PlayerState.CurrentMp);

            Object.DestroyImmediate(controllerGo);
            if (gameManagerGo != null)
                Object.DestroyImmediate(gameManagerGo);
            Object.DestroyImmediate(obstacleGo);
            Object.DestroyImmediate(combust);
            Object.DestroyImmediate(characterData);
        }
```

- [ ] **Step 2: Rewrite `PlatformerSpellWorldCaster.cs`**

Replace the entire file with:

```csharp
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using Axiom.Platformer;

namespace Axiom.Voice
{
    public static class PlatformerSpellWorldCaster
    {
        public static bool TryCast(
            SpellData spell,
            IReadOnlyList<MeltableObstacleController> meltableObstacles,
            IReadOnlyList<FreezablePlatformController> freezablePlatforms,
            IReadOnlyList<BurnableObstacleController> burnableObstacles,
            IReadOnlyList<SteamVentController> steamVents,
            PlayerState playerState)
        {
            if (spell == null || string.IsNullOrWhiteSpace(spell.spellName)) return false;
            if (playerState == null) return false;

            bool hasWorldTarget = false;
            if (meltableObstacles != null)
            {
                for (int i = 0; i < meltableObstacles.Count; i++)
                {
                    MeltableObstacleController obstacle = meltableObstacles[i];
                    if (obstacle != null && obstacle.CanMeltWith(spell.spellName))
                    {
                        hasWorldTarget = true;
                        break;
                    }
                }
            }

            if (!hasWorldTarget && freezablePlatforms != null)
            {
                for (int i = 0; i < freezablePlatforms.Count; i++)
                {
                    FreezablePlatformController platform = freezablePlatforms[i];
                    if (platform != null && platform.CanFreezeWith(spell.spellName))
                    {
                        hasWorldTarget = true;
                        break;
                    }
                }
            }

            if (!hasWorldTarget && burnableObstacles != null)
            {
                for (int i = 0; i < burnableObstacles.Count; i++)
                {
                    BurnableObstacleController obstacle = burnableObstacles[i];
                    if (obstacle != null && obstacle.CanIgniteWith(spell.spellName))
                    {
                        hasWorldTarget = true;
                        break;
                    }
                }
            }

            if (!hasWorldTarget && steamVents != null)
            {
                for (int i = 0; i < steamVents.Count; i++)
                {
                    SteamVentController vent = steamVents[i];
                    if (vent != null && vent.CanIgniteWith(spell.spellName))
                    {
                        hasWorldTarget = true;
                        break;
                    }
                }
            }

            if (!hasWorldTarget) return false;
            if (!playerState.TrySpendMp(spell.mpCost)) return false;

            bool handled = false;
            if (meltableObstacles != null)
            {
                for (int i = 0; i < meltableObstacles.Count; i++)
                {
                    MeltableObstacleController obstacle = meltableObstacles[i];
                    if (obstacle != null && obstacle.TryMelt(spell.spellName))
                        handled = true;
                }
            }

            if (freezablePlatforms != null)
            {
                for (int i = 0; i < freezablePlatforms.Count; i++)
                {
                    FreezablePlatformController platform = freezablePlatforms[i];
                    if (platform != null && platform.TryFreeze(spell.spellName))
                        handled = true;
                }
            }

            if (burnableObstacles != null)
            {
                for (int i = 0; i < burnableObstacles.Count; i++)
                {
                    BurnableObstacleController obstacle = burnableObstacles[i];
                    if (obstacle != null && obstacle.TryIgnite(spell.spellName))
                        handled = true;
                }
            }

            if (steamVents != null)
            {
                for (int i = 0; i < steamVents.Count; i++)
                {
                    SteamVentController vent = steamVents[i];
                    if (vent != null && vent.TryIgnite(spell.spellName))
                        handled = true;
                }
            }

            return handled;
        }
    }
}
```

*(Note: the old early-return for "both lists empty" is removed — it is now redundant with the `hasWorldTarget` check, which covers all four empty lists. MP is still spent only when a world target matched.)*

- [ ] **Step 3: Rewrite `PlatformerVoiceSpellController.cs`**

Replace the entire file with:

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using Axiom.Platformer;
using UnityEngine;

namespace Axiom.Voice
{
    public class PlatformerVoiceSpellController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional explicit meltable obstacles. Leave empty to find all active MeltableObstacleController instances in the scene.")]
        private MeltableObstacleController[] _meltableObstacles;

        [SerializeField]
        [Tooltip("Optional explicit freezable platforms. Leave empty to find all active FreezablePlatformController instances in the scene.")]
        private FreezablePlatformController[] _freezablePlatforms;

        [SerializeField]
        [Tooltip("Optional explicit burnable obstacles. Leave empty to find all active BurnableObstacleController instances in the scene.")]
        private BurnableObstacleController[] _burnableObstacles;

        [SerializeField]
        [Tooltip("Optional explicit steam vents. Leave empty to find all active SteamVentController instances in the scene.")]
        private SteamVentController[] _steamVents;

        private ConcurrentQueue<string> _resultQueue;
        private IReadOnlyList<SpellData> _unlockedSpells;
        private PlayerState _playerState;
        private readonly List<MeltableObstacleController> _sceneMeltableObstacles = new();
        private readonly List<FreezablePlatformController> _sceneFreezablePlatforms = new();
        private readonly List<BurnableObstacleController> _sceneBurnableObstacles = new();
        private readonly List<SteamVentController> _sceneSteamVents = new();

        public void Inject(ConcurrentQueue<string> resultQueue, IReadOnlyList<SpellData> unlockedSpells, PlayerState playerState = null)
        {
            _resultQueue = resultQueue;
            _unlockedSpells = unlockedSpells;
            _playerState = playerState;
        }

        private void Start()
        {
            _resultQueue ??= new ConcurrentQueue<string>();
            _unlockedSpells ??= Array.Empty<SpellData>();
        }

        private void Update()
        {
            while (_resultQueue.TryDequeue(out string voskJson))
            {
                SpellData matched = SpellResultMatcher.Match(voskJson, _unlockedSpells);
                if (matched == null)
                    continue;

                PlatformerSpellWorldCaster.TryCast(
                    matched,
                    ResolveMeltableObstacles(),
                    ResolveFreezablePlatforms(),
                    ResolveBurnableObstacles(),
                    ResolveSteamVents(),
                    _playerState ?? GameManager.Instance?.PlayerState);
            }
        }

        private IReadOnlyList<MeltableObstacleController> ResolveMeltableObstacles()
        {
            if (_meltableObstacles != null && _meltableObstacles.Length > 0)
                return _meltableObstacles;

            _sceneMeltableObstacles.Clear();
            _sceneMeltableObstacles.AddRange(FindObjectsByType<MeltableObstacleController>());
            return _sceneMeltableObstacles;
        }

        private IReadOnlyList<FreezablePlatformController> ResolveFreezablePlatforms()
        {
            if (_freezablePlatforms != null && _freezablePlatforms.Length > 0)
                return _freezablePlatforms;

            _sceneFreezablePlatforms.Clear();
            _sceneFreezablePlatforms.AddRange(FindObjectsByType<FreezablePlatformController>());
            return _sceneFreezablePlatforms;
        }

        private IReadOnlyList<BurnableObstacleController> ResolveBurnableObstacles()
        {
            if (_burnableObstacles != null && _burnableObstacles.Length > 0)
                return _burnableObstacles;

            _sceneBurnableObstacles.Clear();
            _sceneBurnableObstacles.AddRange(FindObjectsByType<BurnableObstacleController>());
            return _sceneBurnableObstacles;
        }

        private IReadOnlyList<SteamVentController> ResolveSteamVents()
        {
            if (_steamVents != null && _steamVents.Length > 0)
                return _steamVents;

            _sceneSteamVents.Clear();
            _sceneSteamVents.AddRange(FindObjectsByType<SteamVentController>());
            return _sceneSteamVents;
        }
    }
}
```

- [ ] **Step 4: Run the Voice tests (regression + new combust path)**

> **Unity Editor task (user):** Let Unity compile (expected: no errors — `PlatformerVoiceSpellController.Update` is the only caller of `TryCast`). Then Test Runner → EditMode → run `PlatformerVoiceSpellControllerTests`. Expected: all three PASS — `...MeltsInRangeObstacle`, `...FreezesInRangeWaterPlatformAndSpendsMp`, and the new `...IgnitesInRangeBurnableObstacleAndSpendsMp`. The two pre-existing tests passing confirms the signature change did not regress melt/freeze dispatch.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-94): dispatch combustion spells to burnables and vents`
- `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs`
- `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs.meta`
- `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs`
- `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs.meta`
- `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs`
- `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs.meta`

---

### Task 6: Persist & restore solved combustion puzzles

**Files:**
- Modify: `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs` — `ReapplySolvedPuzzles()` only.

**Interfaces:**
- Consumes: `BurnableObstacleController`, `ExplodableBarrierController` `PuzzleId` + `ApplySolvedImmediate()`; `GameManager.Instance.IsPuzzleSolved(string)`. (Steam vents are stateless — never restored.)

- [ ] **Step 1: Replace `ReapplySolvedPuzzles()`**

In `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs`, replace the existing `ReapplySolvedPuzzles()` method (currently lines ~66-78) with:

```csharp
        private void ReapplySolvedPuzzles()
        {
            MeltableObstacleController[] obstacles =
                FindObjectsByType<MeltableObstacleController>(FindObjectsInactive.Exclude);
            foreach (MeltableObstacleController obstacle in obstacles)
            {
                if (!string.IsNullOrWhiteSpace(obstacle.PuzzleId)
                    && GameManager.Instance.IsPuzzleSolved(obstacle.PuzzleId))
                {
                    obstacle.ApplySolvedImmediate();
                }
            }

            BurnableObstacleController[] burnables =
                FindObjectsByType<BurnableObstacleController>(FindObjectsInactive.Exclude);
            foreach (BurnableObstacleController burnable in burnables)
            {
                if (!string.IsNullOrWhiteSpace(burnable.PuzzleId)
                    && GameManager.Instance.IsPuzzleSolved(burnable.PuzzleId))
                {
                    burnable.ApplySolvedImmediate();
                }
            }

            ExplodableBarrierController[] barriers =
                FindObjectsByType<ExplodableBarrierController>(FindObjectsInactive.Exclude);
            foreach (ExplodableBarrierController barrier in barriers)
            {
                if (!string.IsNullOrWhiteSpace(barrier.PuzzleId)
                    && GameManager.Instance.IsPuzzleSolved(barrier.PuzzleId))
                {
                    barrier.ApplySolvedImmediate();
                }
            }

            // Steam vents intentionally have no persisted state — they are re-ignitable
            // permanent fixtures. The obstacles a vent clears restore themselves above
            // via their own puzzleIds.
        }
```

- [ ] **Step 2: Verify compile**

> **Unity Editor task (user):** Let Unity compile. Expected: no compile errors.

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-94): restore solved combustion puzzles after battle`
- `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs`
- `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs.meta`

---

### Task 7: Build the three combustion prefabs

> **Unity Editor task (user).** No code. Slice the sprite sheet, build prefabs, wire components. The crate mirrors `P_IceWall`; the vent mirrors the freezable water platform's loop wiring.

- [ ] **Step 1: Confirm sprite slicing.** Select `Assets/Art/Sprites/Platformer/burnable and geyser-Sheet.png`. It is already sliced (Sprite Mode = Multiple) into `geyser-0..5` and `burnable-0..5`. If not, open Sprite Editor → Slice → Grid by Cell Size 128×128.

- [ ] **Step 2: `P_BurnableCrate.prefab`** (save under `Assets/Prefabs/Platformer/`):
  - Root: `SpriteRenderer` (sprite `burnable-0`, material guid `a97c105638bdf8b4a8650670310a4cd3`, volcanic sorting order) + `BoxCollider2D` sized to the crate + `BurnableObstacleController` + `AudioSource` + optional `CinemachineImpulseSource` (camera shake).
  - Root layer = the solid/ground collision layer used by `P_IceWall` (check `P_IceWall` to match).
  - Child `ProximityTrigger`: trigger `BoxCollider2D` (slightly larger than the crate) + `BurnableObstacleProximityForwarder` (assign `_controller` = root).
  - Child `SuccessVFX_Burn`: `ParticleSystem` (fire/smoke burst).
  - Wire `BurnableObstacleController`: `_spriteRenderer`, `_solidCollider`, `_burnFrames` = the 6 `burnable-0..5` sprites in order, `_burnFps` = 10, `_igniteSpells` = `[SD_Combust, SD_AncientBurn]`, `_successVfx`, `_audioSource`, `_successSfx` (optional). For shake: add a persistent listener to `_onIgnited` → root `CinemachineImpulseSource.GenerateImpulse()` (reuse the single vcam `CinemachineImpulseListener` from Step 4 — do not add a second). Leave `_puzzleId` blank on the prefab (set per-instance in Task 8).

- [ ] **Step 3: `P_ExplodableBarrier.prefab`** (a **parent container** + tiled child blocks):
  - Sprite: `Assets/Art/Sprites/Platformer/explodable-block.png` (AI-generated dark basalt block with glowing lava cracks). **Set its import PPU to 32** (to match the 32-px lava-ground tiles → 1 block = 1×1 world unit), Filter = Point, Alpha Is Transparency on.
  - Root (empty container at the barrier's visual center): `ExplodableBarrierController` + one `BoxCollider2D` spanning the whole barrier + `AudioSource` + optional `CinemachineImpulseSource` (camera shake). The root has **no** SpriteRenderer.
  - Child **blocks**: one `SpriteRenderer` GameObject per block (`explodable-block` sprite), positioned on the tile grid into the barrier/boulder shape. Flip/rotate per block (or use 2–3 crack variants) so the pattern doesn't visibly repeat.
  - Child `DebrisVFX`: one `ParticleSystem` at the barrier center (debris; `lava-ball1/2/3` sprites work well) — one burst for the whole barrier.
  - No proximity trigger, no spell list.
  - Wire: `_blockRenderers` = the block children (or **leave empty** to auto-collect all child SpriteRenderers on Awake), `_solidCollider` = the root collider, `_debrisVfx`, `_audioSource`, `_destroySfx` (optional), `_fadeDuration` = 0.4. For shake: add a persistent listener to `_onDetonated` → root `CinemachineImpulseSource.GenerateImpulse()` (reuse the single vcam listener from Step 4). `_puzzleId` blank on prefab (set per-instance in Task 8). All blocks fade out together as one unit on detonate.

- [ ] **Step 4: `P_SteamVent.prefab`**:
  - Root: `SpriteRenderer` (sprite `geyser-0`) + `SteamVentController` + `AudioSource`. No solid collider (player walks past).
  - Child `ProximityTrigger`: trigger `BoxCollider2D` + `SteamVentProximityForwarder` (assign `_controller` = root).
  - Child `BlastVFX`: `ParticleSystem` (explosion burst; optionally driven by `+40FXPack_NYKNCK/Explosion` frames once converted from GIF).
  - Wire `SteamVentController`: `_spriteRenderer`, `_ventFrames` = `geyser-0,1,2` (idle puff loop), `_eruptionFrames` = `geyser-3,4,5` (one-shot eruption played on ignite, then idle resumes), `_ventFps` = 6, `_eruptionFps` = 10, `_igniteSpells` = `[SD_Combust, SD_AncientBurn]`, `_blastVfx`, `_audioSource`, `_blastSfx` (optional), `_blastRadius` = 0 (set per-puzzle), `_blastMask` (set to the layer barriers/crates sit on). Leave `_linkedTargets` and `_onIgnited` for per-instance wiring. (The vent has **no** `_puzzleId` — it is re-ignitable and stateless.)
  - **Optional camera shake:** add a `CinemachineImpulseSource` to the vent root; add a `CinemachineImpulseListener` to the platformer vcam once; in each placed vent instance, add a persistent listener to `_onIgnited` → `CinemachineImpulseSource.GenerateImpulse`.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-94): add combustion puzzle prefabs`
- `Assets/Prefabs/Platformer/P_BurnableCrate.prefab` (+ `.meta`)
- `Assets/Prefabs/Platformer/P_ExplodableBarrier.prefab` (+ `.meta`)
- `Assets/Prefabs/Platformer/P_SteamVent.prefab` (+ `.meta`)
- Any new `.asset` sprite/material metas Unity generated from slicing (stage all pending under `Assets/Art/Sprites/Platformer/`).

---

### Task 8: Author the 2-1 / 2-2 puzzle layouts (2-3 waived — boss level)

> **Unity Editor task (user).** Place prefab instances in the Level 2 scenes and give each **crate/barrier** a unique `_puzzleId`. Steam vents have **no** `_puzzleId` (stateless / re-ignitable).

- [ ] **Step 1: 2-1 (teach direct ignite).** Place one `P_BurnableCrate` blocking a narrow ledge in the Level 2-1 scene. Set its `_puzzleId` (e.g. `L2-1_crate_a`). Optionally add a second crate (`L2-1_crate_b`).

- [ ] **Step 2: 2-2 (teach vent explosion).** Place a `P_ExplodableBarrier` blocking the path, positioned out of direct cast range; place a `P_SteamVent` within player range nearby. On the vent: assign the barrier to `_linkedTargets`. Set the **barrier's** `_puzzleId` (`L2-2_barrier`); the vent has none.

- [x] **Step 3: 2-3 (combine) — WAIVED (2026-06-21).** Level 2-3 is the boss level (flat ground leading to the boss arena, no platforming), so no combustion puzzle is authored there. DEV-94 ACs are met by 2-1 (crate) + 2-2 (vent); the simple→complex escalation holds across 2-1→2-2 (direct ignite → indirect vent explosion). The vent multi-target path (`_linkedTargets` with 2+ entries, or `_blastRadius` + `_blastMask`) stays code-complete for any future platforming level — no in-game showcase is required for DEV-94.

- [ ] **Step 4: Confirm the scene has a `PlatformerVoiceSpellController`** (it auto-finds the new objects via `FindObjectsByType`, so the explicit arrays can be left empty) **and a `PlatformerWorldRestoreController`.**

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the modified Level 2 scene files (`Assets/Scenes/.../Level_2-*.unity` + `.meta`) → Check in with message: `feat(DEV-94): author Level 2 combustion puzzle layouts`

---

### Task 9: (Optional) Gradient sky-fill for extra vertical headroom

> **Unity Editor task (user).** Only if a Level 2 pocket needs to be taller than the ~17-unit horizon strip. Skip otherwise.

- [ ] **Step 1: Make the gradient texture.** A 1×256 px PNG, vertical gradient: top = upper-sky color, bottom = the exact color of the top edge of the volcanic strip (eyedrop from a parallax layer).

- [ ] **Step 2: Import settings.** Sprite (2D and UI), Filter **Bilinear**, Wrap **Clamp**, Compression **None**, Mesh Type Full Rect.

- [ ] **Step 3: Add `SkyFill`** to the scene: `SpriteRenderer`, Draw Mode Simple, same Sorting Layer as the BG, Order in Layer **below `Layer_Far` (e.g. −50)**. Scale X to level width, scale Y to the headroom; position so its bottom edge overlaps the top of the horizon strip (matched colors → invisible seam). Parent under `Layer_Far` or give it a `ParallaxController` with `parallaxFactor ≈ 0`.

- [ ] **Step 4: Confine the camera** so the Cinemachine vcam never scrolls above `SkyFill`'s top.

- [ ] **Step 5: Check in via UVCS** the new texture + scene changes with message: `feat(DEV-94): add gradient sky-fill for Level 2 headroom`

---

### Task 10: Play Mode verification against success criteria

> **Unity Editor task (user).** Enter Play Mode in a Level 2 scene with a mic and verify each criterion. Use the existing debug caster if voice is unavailable.

- [ ] **Crate:** at rest shows `burnable-0`; casting `combust`/`ancient burn` in range → orange flash → frames 0→5 → collider opens mid-burn → charred frame remains and is walkable → success VFX + SFX.
- [ ] **Wrong spell / out of range:** nothing happens and **MP is not spent.**
- [ ] **Vent:** loops its puff; igniting in range → blast VFX (+ shake if wired) → every linked / in-radius obstacle clears (crate burns, barrier debris-fades).
- [ ] **MP spent once** per successful cast even when a vent clears multiple targets.
- [ ] **Persistence:** solve a puzzle, trigger a battle and return — each solved object is already in its cleared state (restored by `puzzleId`).
- [ ] **Sky-fill (if added):** no visible seam and no camera over-scroll past its top.
- [ ] **Difficulty curve:** 2-1 → 2-2 escalate as intended (direct ignite → vent explosion) and each is telegraphed. (2-3 combine beat waived — boss level.)

If any criterion fails, debug in the Editor and, if a script change is needed, return to the relevant code task.

---

## Acceptance-criteria mapping (DEV-94 Level 2)

| AC bullet | Task |
|---|---|
| Steam vents that cause explosions to destroy obstacles | 2, 3, 4 (Detonate), 8 |
| Burnable obstacles (wooden crates) cleared with fire | 1, 2, 7, 8 |
| Burnable obstacles (tar pits) | **Deferred** — no art |
| Thermal updrafts that lift the player | **Deferred** — BG height + art |
| Teaches without combat / telegraphed / escalates / A-V feedback | 7 (cues), 8 (layouts), 10 (verify) |

## Out of scope / deferred

- Thermal-updraft traversal mechanic; tar-pit obstacle (need art / a future ticket).
- Converting `+40FXPack_NYKNCK` explosion GIFs to sprite sheets (optional polish).
- Any battle-chemistry change.
- The 2-3 "combine" puzzle layout — Level 2-3 is the boss level (flat ground, no platforming). DEV-94 ACs are satisfied by 2-1 + 2-2; the vent multi-target path stays code-complete for a future platforming level.
- A standalone `PlatformerSpellWorldCaster` unit test — its dispatch is instead covered through the `PlatformerVoiceSpellController` Edit Mode integration test added in Task 5 (mirroring the existing melt/freeze tests); full multi-target / persistence behavior is verified in Play Mode (Task 10).
