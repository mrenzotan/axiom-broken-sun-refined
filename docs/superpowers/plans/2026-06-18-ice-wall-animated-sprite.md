# Ice Wall Animated Sprite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `P_IceWall`'s Tilemap visual with the animated `ice wall-Sheet.png`, driven by code-based frame swapping in `MeltableObstacleController` — mirroring how `FreezablePlatformController` drives `P_WaterPlatform_Long`.

**Architecture:** Rendering swap only. The controller's public API and melt-detection logic are unchanged; only its private rendering backend moves from `Tilemap` + `TilemapCollider2D` to `SpriteRenderer` + `BoxCollider2D`. The melt sequence becomes an ice-blue flash followed by a code-driven 6-frame melt animation. The prefab is restructured by hand in the Unity Editor to put the `SpriteRenderer` + `BoxCollider2D` on the root, matching the water platform.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, C# (coroutines, `SpriteRenderer` frame swap). No Unity Animator, no new assets, no new asmdef.

## Global Constraints

- **Module:** `Axiom.Platformer` (existing `Assets/Scripts/Platformer/Platformer.asmdef`). No new asmdef — this edits an existing file in the module.
- **MonoBehaviour rule:** `MeltableObstacleController` stays a MonoBehaviour; the unchanged matching logic remains in the plain C# `MeltableObstacle` helper. No new logic added to the MonoBehaviour beyond rendering/coroutine wiring.
- **Public API frozen:** `TryMelt`, `CanMeltWith`, `SetPlayerInRange`, `ApplySolvedImmediate`, `IsMelted`, `PuzzleId` must keep identical signatures — 5 external consumers depend on them (`PlatformerSpellWorldCaster`, `PlatformerWorldRestoreController`, `MeltableObstacleDebugCaster`, `MeltableObstacleProximityForwarder`, `PlatformerVoiceSpellController`).
- **Version control:** UVCS only. Never `git add`/`git commit`. Check-in message format: `<type>(DEV-94): <desc>` per `docs/VERSION_CONTROL.md`.
- **Ticket:** DEV-94 (ice-wall sprite conversion) — substituted into all check-in messages below.
- **Asset facts:** `ice wall-Sheet.png` (guid `768f8c08b0bc245bba7e7d03723bc312`) is already sliced into 6 sprites `ice wall-Sheet_0..5`, 64×96 px @ 16 PPU (4×6 world units). Frame 0 = full wall → frame 5 = melted/gone.
- **Sprite material:** same one the water platform uses, guid `a97c105638bdf8b4a8650670310a4cd3`.
- **Solid layer:** the old `TilemapCollider2D` was on layer 7; the new `BoxCollider2D` must be on layer 7 so player collision is preserved.

---

### Task 1: Rewrite `MeltableObstacleController` to drive a `SpriteRenderer`

**Files:**
- Modify (full rewrite): `Assets/Scripts/Platformer/MeltableObstacleController.cs`
- Unchanged (reference only): `Assets/Scripts/Platformer/MeltableObstacle.cs`, `Assets/Tests/Editor/Platformer/MeltableObstacleTests.cs`

**Interfaces:**
- Consumes: `MeltableObstacle.CanMelt(string spellId, List<string> meltSpellIds)` (unchanged static helper); `GameManager.Instance.MarkPuzzleSolved(string)`, `GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(AudioSource)` (unchanged).
- Produces (public API — must stay identical): `bool TryMelt(string)`, `bool CanMeltWith(string)`, `void SetPlayerInRange(bool)`, `void ApplySolvedImmediate()`, `bool IsMelted { get; }`, `string PuzzleId { get; }`.
- New serialized fields (the prefab wires these in Task 2): `SpriteRenderer _spriteRenderer`, `BoxCollider2D _solidCollider`, `Sprite[] _meltFrames`, `float _meltFps`.

**Note on testing:** There is no new pure-C# logic in this task — the matching logic in `MeltableObstacle.CanMelt` is untouched and already covered by `MeltableObstacleTests.cs` (4 Edit Mode tests). The new code is coroutine/visual behavior (frame swapping, renderer/collider toggling) that requires Play Mode and a real `SpriteRenderer`, so it is verified manually in Task 4 rather than with a unit test. Adding an Edit Mode test here would be a test that cannot fail when the rendering changes (violates "tests verify intent"). The TDD checkpoint for this task is: existing tests stay green + the file compiles clean.

- [ ] **Step 1: Replace the entire contents of `Assets/Scripts/Platformer/MeltableObstacleController.cs` with:**

```csharp
using System.Collections;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Platformer
{
    public class MeltableObstacleController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private BoxCollider2D _solidCollider;
        [SerializeField] private Sprite[] _meltFrames;
        [SerializeField, Min(0.1f)] private float _meltFps = 10f;
        [SerializeField] private List<SpellData> _meltSpells = new();

        [SerializeField]
        [Tooltip("Stable, scene-unique ID used to persist the solved (melted) state across a Battle round-trip. Leave blank to opt out of persistence.")]
        private string _puzzleId;

        public string PuzzleId => _puzzleId;

        [Header("Success cue")]
        [SerializeField]
        [Tooltip("Optional particle burst played once when this obstacle is successfully melted.")]
        private ParticleSystem _successVfx;

        [SerializeField]
        [Tooltip("Optional one-shot played when this obstacle is successfully melted. Routed through the SFX mixer bus.")]
        private AudioClip _successSfx;

        [SerializeField]
        [Tooltip("AudioSource on this prefab used to play the success SFX. Auto-routed through the SFX bus on Start.")]
        private AudioSource _audioSource;

        private static readonly Color FlashTint = new(0xBF / 255f, 0xE9 / 255f, 1f, 1f);
        private const float FlashDuration = 0.15f;

        private bool _isMelted;
        private bool _isPlayerInRange;

        public bool IsMelted => _isMelted;

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

        public bool CanMeltWith(string spellId)
        {
            if (_isMelted) return false;
            if (!_isPlayerInRange) return false;

            return MeltableObstacle.CanMelt(spellId, BuildMeltSpellIds());
        }

        public bool TryMelt(string spellId)
        {
            if (!CanMeltWith(spellId)) return false;

            _isMelted = true;

            if (!string.IsNullOrWhiteSpace(_puzzleId) && GameManager.Instance != null)
                GameManager.Instance.MarkPuzzleSolved(_puzzleId);

            PlaySuccessCue();
            StartCoroutine(MeltCoroutine());
            return true;
        }

        private void PlaySuccessCue()
        {
            if (_successVfx != null)
                _successVfx.Play();
            if (_audioSource != null && _successSfx != null)
                _audioSource.PlayOneShot(_successSfx);
        }

        /// <summary>
        /// Forces the terminal melted state with no animation and no success cue.
        /// Called on scene load by PlatformerWorldRestoreController when this puzzle
        /// was already solved earlier in the session.
        /// </summary>
        public void ApplySolvedImmediate()
        {
            if (_isMelted
                && (_solidCollider == null || !_solidCollider.enabled)
                && (_spriteRenderer == null || !_spriteRenderer.enabled))
                return; // already in terminal state

            _isMelted = true;
            if (_solidCollider != null)
                _solidCollider.enabled = false;
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;
        }

        private List<string> BuildMeltSpellIds()
        {
            var meltSpellIds = new List<string>(_meltSpells.Count);
            for (int i = 0; i < _meltSpells.Count; i++)
            {
                SpellData spell = _meltSpells[i];
                if (spell != null) meltSpellIds.Add(spell.spellName);
            }

            return meltSpellIds;
        }

        private IEnumerator MeltCoroutine()
        {
            yield return FlashCoroutine();

            yield return PlayMeltFrames();

            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;
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

        private IEnumerator PlayMeltFrames()
        {
            if (_spriteRenderer == null || _meltFrames == null || _meltFrames.Length == 0)
            {
                if (_solidCollider != null) _solidCollider.enabled = false;
                yield break;
            }

            int colliderDisableFrame = _meltFrames.Length / 2;
            var frameWait = new WaitForSeconds(1f / _meltFps);
            for (int i = 0; i < _meltFrames.Length; i++)
            {
                _spriteRenderer.sprite = _meltFrames[i];

                if (i == colliderDisableFrame && _solidCollider != null)
                    _solidCollider.enabled = false;

                yield return frameWait;
            }

            if (_solidCollider != null) _solidCollider.enabled = false;
        }
    }
}
```

What changed from the prior version, and why:
- Removed `using UnityEngine.Tilemaps;`, the `_tilemap` field, `_fadeDuration`, `FadeAndSinkCoroutine`, `EaseOutQuad`, and the `SinkScaleY` constant — the 6-frame sheet now depicts the melt, so the procedural fade/sink is gone.
- `_solidCollider` retyped `TilemapCollider2D` → `BoxCollider2D` (matches the water platform; only `.enabled` is ever touched).
- Added `_spriteRenderer`, `_meltFrames`, `_meltFps`.
- `FlashCoroutine` now tints `_spriteRenderer.color` instead of `_tilemap.color`.
- New `PlayMeltFrames` mirrors `FreezablePlatformController.PlayFreezeFrames`: advances frames 0→5 at `_meltFps`, disables `_solidCollider` at the midpoint frame (index `length/2` = 3 of 6), and disables it defensively if there are no frames.
- `MeltCoroutine` = flash → play frames → hide renderer.
- `ApplySolvedImmediate` now disables the collider + renderer instead of deactivating the Tilemap child; its early-return guard checks the new terminal state.

- [ ] **Step 2 — Unity Editor task (user): Confirm the script compiles clean.**

> **Unity Editor task (user):** Return focus to the Unity Editor to trigger a recompile. Open **Console** and confirm there are **no compile errors** for `MeltableObstacleController.cs`. (Expected: the prefab's controller component will show 4 "missing"/unassigned fields — `Sprite Renderer`, `Solid Collider`, `Melt Frames`, `Melt Fps` — that is normal and gets wired in Task 2.)

- [ ] **Step 3 — Unity Editor task (user): Confirm existing Edit Mode tests still pass.**

> **Unity Editor task (user):** Open **Window → General → Test Runner → EditMode**, run the `Axiom.Platformer.Tests` group, and confirm `MeltableObstacleTests` (4 tests) and `FreezablePlatformTests` are still green. The matching logic was not touched, so they must pass unchanged.

- [ ] **Step 4: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-94): drive ice wall via SpriteRenderer instead of Tilemap`
  - `Assets/Scripts/Platformer/MeltableObstacleController.cs`
  - `Assets/Scripts/Platformer/MeltableObstacleController.cs.meta`

---

### Task 2: Rewire the `P_IceWall` prefab (Unity Editor)

**Files:**
- Modify: `Assets/Prefabs/Platformer/P_IceWall.prefab` (and its auto-updated `.meta` if changed)

All steps here are Unity Editor actions performed by the user. Open the prefab in Prefab Mode (double-click `P_IceWall.prefab`).

- [ ] **Step 1 — Unity Editor task (user): Remove the Tilemap rendering stack.**

> **Unity Editor task (user):** On the root `P_IceWall`, remove the **Grid** component. Then delete the child **`Tilemap`** GameObject entirely (this removes its `Tilemap`, `TilemapRenderer`, and `TilemapCollider2D`).

- [ ] **Step 2 — Unity Editor task (user): Add the SpriteRenderer to the root.**

> **Unity Editor task (user):** On the root `P_IceWall`, **Add Component → Sprite Renderer**. Set **Sprite** = `ice wall-Sheet_0`, **Material** = the sprite material used by the water platform (guid `a97c105638bdf8b4a8650670310a4cd3` — the same one the old TilemapRenderer used), and **Sorting Order** = `7` (match `P_WaterPlatform_Long`). Adjust sorting later if it renders in front of / behind the player incorrectly.

- [ ] **Step 3 — Unity Editor task (user): Add and size the solid BoxCollider2D.**

> **Unity Editor task (user):** On the root `P_IceWall`, **Add Component → Box Collider 2D**. Size/offset it to cover the solid standing portion of frame 0 (the intact wall). Leave **Is Trigger** unchecked. This is the collider the player walks into.

- [ ] **Step 4 — Unity Editor task (user): Put the solid collider on layer 7.**

> **Unity Editor task (user):** Set the root `P_IceWall` GameObject's **Layer** to **7** (the layer the old `TilemapCollider2D` used). If prompted about children, do **not** change the `ProximityTrigger` child's layer — only the object holding the new `BoxCollider2D` needs layer 7.

- [ ] **Step 5 — Unity Editor task (user): Wire the controller fields.**

> **Unity Editor task (user):** Select the root `P_IceWall` and on the **Meltable Obstacle Controller** component set:
> - **Sprite Renderer** → the root's new `SpriteRenderer`
> - **Solid Collider** → the root's new `BoxCollider2D`
> - **Melt Frames** → size 6, elements `ice wall-Sheet_0`, `_1`, `_2`, `_3`, `_4`, `_5` in order
> - **Melt Fps** → `10`
> - Confirm the still-present fields are intact: **Melt Spells**, **Puzzle Id**, **Success Vfx** (`SuccessVFX_Melt`), **Success Sfx**, **Audio Source**.

- [ ] **Step 6 — Unity Editor task (user): Sanity-check the prefab visually.**

> **Unity Editor task (user):** In Prefab Mode, confirm the wall now shows the `ice wall-Sheet_0` sprite (not the old tilemap), and that the `ProximityTrigger` and `SuccessVFX_Melt` children are still present and unchanged. Save the prefab (Ctrl/Cmd+S).

- [ ] **Step 7: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-94): rebuild P_IceWall prefab with animated SpriteRenderer`
  - `Assets/Prefabs/Platformer/P_IceWall.prefab`
  - `Assets/Prefabs/Platformer/P_IceWall.prefab.meta` (only if it changed)

---

### Task 3: Reconcile the prefab instances across all 5 scenes (Unity Editor)

**Files:**
- Modify (as needed): `Assets/Scenes/Level_1-1.unity`, `Assets/Scenes/Level_1-1 Redesign.unity`, `Assets/Scenes/Level_1-2.unity`, `Assets/Scenes/Level_1-2 Redesign.unity`, `Assets/Scenes/Level_1-3 Redesign.unity`

The prefab structure changed (Grid + Tilemap child removed, SpriteRenderer + BoxCollider2D added), so each placed instance may carry orphaned overrides or a stale transform/scale. Check each scene.

- [ ] **Step 1 — Unity Editor task (user): Reconcile each scene's ice-wall instance.**

> **Unity Editor task (user):** For **each** of the 5 scenes above: open the scene, select the `P_IceWall` instance(s), and confirm:
> - The `SpriteRenderer` shows the ice-wall sprite (no pink/missing material, no leftover Tilemap).
> - In the Overrides dropdown there are no orphaned overrides pointing at the deleted Tilemap/Grid; revert any that appear.
> - The instance is positioned/scaled so the wall sits correctly in the level (old wall ≈ 2×5 units, new sprite = 4×6 units — reposition/rescale as needed so it blocks the intended path).
> - The solid `BoxCollider2D` lines up with the visible wall.
> Save each scene after fixing.

- [ ] **Step 2: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the modified scene files below → Check in with message: `chore(DEV-94): reconcile P_IceWall instances after prefab rebuild`
  - `Assets/Scenes/Level_1-1.unity`
  - `Assets/Scenes/Level_1-1 Redesign.unity`
  - `Assets/Scenes/Level_1-2.unity`
  - `Assets/Scenes/Level_1-2 Redesign.unity`
  - `Assets/Scenes/Level_1-3 Redesign.unity`
  - (Stage only the scenes that actually show pending changes.)

---

### Task 4: Play Mode acceptance test against the success criteria (Unity Editor)

**Files:** none (manual verification)

- [ ] **Step 1 — Unity Editor task (user): Verify idle + melt + persistence in Play Mode.**

> **Unity Editor task (user):** Enter Play Mode in a level containing an ice wall (e.g. `Level_1-2`) and confirm each acceptance criterion:
> 1. At rest the wall shows frame 0 and is static (no looping animation).
> 2. With the player **in range**, casting the correct melt spell: ice-blue flash → frames 0→5 play → the wall becomes passable partway through (collider disables at the midpoint) → the renderer hides at the end → the `SuccessVFX_Melt` burst and success SFX fire.
> 3. Casting the **wrong** spell, or casting **out of range**, does nothing.
> 4. The melt **persists across a Battle round-trip**: trigger a battle and return (or otherwise reload the scene with the puzzle marked solved) — on return the wall is already hidden and passable (`ApplySolvedImmediate` path). Requires a non-blank **Puzzle Id** on the instance.
> 5. Repeat the spot-check in at least one other scene to confirm consistency.
>
> If any criterion fails, stop and report which one — do not mark the plan complete with a failing criterion (fail loud).

---

## Notes / deviations from the reference pattern

- **No idle loop.** Unlike the water platform (which loops `_waterLoopFrames`), the ice wall has no idle-shimmer frames in its sheet, so idle is a static frame 0. This is intentional and the only deliberate divergence from the water-platform pattern. A resting shimmer would need new art (out of scope).
- **No new asmdef / no new tests.** The change is rendering-only; the matching logic and its Edit Mode tests are untouched. Verification is the existing Edit Mode suite (must stay green) plus the Play Mode acceptance pass in Task 4.
