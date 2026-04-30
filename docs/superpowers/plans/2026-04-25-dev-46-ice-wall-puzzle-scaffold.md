# DEV-46 Ice Wall Puzzle Scaffold Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Pair with the `executing-unity-game-dev-plans` skill for Unity Editor handoffs and UVCS check-in cadence.

**Goal:** Add a meltable Ice Wall obstacle at the end of Level_1-1 that blocks the player's path and is removed by a "spell cast" action, with a debug-key input layer that DEV-82 will later replace with voice casting — without touching the obstacle logic.

**Architecture:** Pure C# decision class (`MeltableObstacle.CanMelt`) Edit-Mode tested. A `MeltableObstacleController` MonoBehaviour owns melted state and the fade coroutine, exposing `TryMelt(spellId)` as the stable seam shared by today's debug caster and tomorrow's voice caster. A child proximity trigger forwards player enter/exit to the controller via a thin forwarder. The prefab is self-contained (`Grid + Tilemap + ProximityTrigger`) so it drops into any platformer scene.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, Unity 2D Tilemap, New Input System, Unity Test Framework (Edit Mode, NUnit).

**Related tickets:**
- **DEV-46** — Level 1 (Snow Mountain) implementation. This plan slots in between DEV-46's Task 11 and Task 12.
- **DEV-82** — Environmental chemistry-spell puzzles (future). DEV-82 will delete `MeltableObstacleDebugCaster.cs` and the `DebugMeltCast` action and replace them with a voice caster that calls the same `TryMelt(spellId)` seam.

**Source spec:** `docs/superpowers/specs/2026-04-25-dev-46-ice-wall-puzzle-scaffold-design.md`

**Prerequisites:**
- DEV-46 Task 10 complete (`TutorialPromptTrigger` and `TutorialPromptPanelUI` exist).
- DEV-46 Task 12 Steps 1–6 complete (Level_1-1 has a base hierarchy, a `Meltspawn_01` encounter, and a `LevelExit_To_1-2` `LevelExitTrigger`). Task 12 Step 5.5 (this plan's Task 10) inserts the Ice Wall between those two existing scene elements.
- The Snow Mountain Tile Palette (`Palette_SnowMountain`) is built (DEV-46 Task 1).

---

## File Structure

### New runtime scripts (`Assets/Scripts/Platformer/`, covered by existing `Axiom.Platformer.asmdef`)

| File | Type | Responsibility |
|---|---|---|
| `MeltableObstacle.cs` | Plain C# (static) | Pure decision: `CanMelt(string spellId, IReadOnlyList<string> meltSpellIds)`. Edit-Mode tested. |
| `MeltableObstacleController.cs` | MonoBehaviour | Holds melted state, runs the fade coroutine, toggles the `TilemapCollider2D`, exposes `TryMelt(string spellId)` as the stable seam. |
| `MeltableObstacleProximityForwarder.cs` | MonoBehaviour | Sits on the child proximity trigger; forwards `OnTriggerEnter2D` / `OnTriggerExit2D` to the parent controller's `_isPlayerInRange` flag. |
| `MeltableObstacleDebugCaster.cs` | MonoBehaviour | DEV-46 stub. Listens for the `DebugMeltCast` `InputAction`; calls `TryMelt(_debugSpellId)` on the controller. Replaced by DEV-82's voice caster. |

### New tests (`Assets/Tests/Editor/Platformer/`, covered by existing `PlatformerTests.asmdef`)

| File | Tests |
|---|---|
| `MeltableObstacleTests.cs` | 9 cases covering the `CanMelt` decision logic — null/empty/whitespace spell ID, null/empty list, single-element match, multi-element match, no-match, and case-sensitivity. |

### New Unity Editor assets

| Asset | Location |
|---|---|
| `P_IceWall.prefab` | `Assets/Prefabs/Platformer/` |

### Modified

| File | Change |
|---|---|
| `Assets/InputSystem_Actions.inputactions` | Add `DebugMeltCast` button action under the `Player` map, bound to `<Keyboard>/m`. The auto-generated `Assets/Scripts/Platformer/InputSystem_Actions.cs` regenerates on save. |
| `Assets/Scenes/Level_1-1.unity` | Place a `P_IceWall` instance between `Meltspawn_01` and `LevelExit_To_1-2`, plus an adjacent `TutorialPromptTrigger` hint. |
| `docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md` | Insert Task 11A and Task 11B references; add Step 5.5 to Task 12. (Pointer to this plan, not duplication of code.) |

### Deferred deletion (DEV-82, not now)

| File | Reason |
|---|---|
| `Assets/Scripts/Platformer/MeltableObstacleDebugCaster.cs` | Replaced by voice caster |
| `DebugMeltCast` action in `InputSystem_Actions.inputactions` | Replaced by voice caster |

---

## Task 1: Write failing tests for `MeltableObstacle.CanMelt`

**Files:**
- Create: `Assets/Tests/Editor/Platformer/MeltableObstacleTests.cs`

This is the TDD step: write the tests first against a class that does not yet exist, then verify they fail to compile.

- [ ] **Step 1: Create `MeltableObstacleTests.cs`**

Create `Assets/Tests/Editor/Platformer/MeltableObstacleTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class MeltableObstacleTests
    {
        [Test]
        public void CanMelt_NullSpellId_ReturnsFalse()
        {
            bool result = MeltableObstacle.CanMelt(
                spellId: null,
                meltSpellIds: new List<string> { "combust" });

            Assert.IsFalse(result);
        }

        [Test]
        public void CanMelt_EmptySpellId_ReturnsFalse()
        {
            bool result = MeltableObstacle.CanMelt(
                spellId: string.Empty,
                meltSpellIds: new List<string> { "combust" });

            Assert.IsFalse(result);
        }

        [Test]
        public void CanMelt_WhitespaceSpellId_ReturnsFalse()
        {
            bool result = MeltableObstacle.CanMelt(
                spellId: "   ",
                meltSpellIds: new List<string> { "combust" });

            Assert.IsFalse(result);
        }

        [Test]
        public void CanMelt_NullMeltList_ReturnsFalse()
        {
            bool result = MeltableObstacle.CanMelt(
                spellId: "combust",
                meltSpellIds: null);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanMelt_EmptyMeltList_ReturnsFalse()
        {
            bool result = MeltableObstacle.CanMelt(
                spellId: "combust",
                meltSpellIds: new List<string>());

            Assert.IsFalse(result);
        }

        [Test]
        public void CanMelt_SpellInList_ReturnsTrue()
        {
            bool result = MeltableObstacle.CanMelt(
                spellId: "combust",
                meltSpellIds: new List<string> { "combust" });

            Assert.IsTrue(result);
        }

        [Test]
        public void CanMelt_SpellNotInList_ReturnsFalse()
        {
            bool result = MeltableObstacle.CanMelt(
                spellId: "shock",
                meltSpellIds: new List<string> { "combust" });

            Assert.IsFalse(result);
        }

        [Test]
        public void CanMelt_SpellInMultiElementList_ReturnsTrue()
        {
            bool result = MeltableObstacle.CanMelt(
                spellId: "blaze",
                meltSpellIds: new List<string> { "combust", "blaze", "incinerate" });

            Assert.IsTrue(result);
        }

        [Test]
        public void CanMelt_IsCaseSensitive()
        {
            bool result = MeltableObstacle.CanMelt(
                spellId: "Combust",
                meltSpellIds: new List<string> { "combust" });

            Assert.IsFalse(result);
        }
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail to compile**

> **Unity Editor task (user):** Window → General → Test Runner → EditMode tab → click **Run All**.
> Expected: compile error in `PlatformerTests.asmdef` because `MeltableObstacle` does not exist yet. The Console will show `error CS0103: The name 'MeltableObstacle' does not exist...`. This is the desired failing state — it confirms the test file is wired into the `PlatformerTests` asmdef and will exercise the type once it exists.

---

## Task 2: Implement `MeltableObstacle.CanMelt`

**Files:**
- Create: `Assets/Scripts/Platformer/MeltableObstacle.cs`

Pure decision logic — no Unity dependencies. Mirrors the structure of `BossVictoryChecker` (null/empty guard first, then null collection guard, then iterate).

- [ ] **Step 1: Create `MeltableObstacle.cs`**

Create `Assets/Scripts/Platformer/MeltableObstacle.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Axiom.Platformer
{
    /// <summary>
    /// Pure decision: does the given spell ID appear in the obstacle's melt-spell whitelist?
    /// Used by MeltableObstacleController.TryMelt and Edit-Mode tested in MeltableObstacleTests.
    /// Match is Ordinal (case-sensitive) — spell IDs are config strings, not display text.
    /// </summary>
    public static class MeltableObstacle
    {
        public static bool CanMelt(string spellId, IReadOnlyList<string> meltSpellIds)
        {
            if (string.IsNullOrWhiteSpace(spellId))
                return false;

            if (meltSpellIds == null)
                return false;

            for (int i = 0; i < meltSpellIds.Count; i++)
            {
                if (string.Equals(meltSpellIds[i], spellId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
```

- [ ] **Step 2: Run the tests and verify they pass**

> **Unity Editor task (user):** Window → General → Test Runner → EditMode tab → click **Run All**.
> Expected: all 9 tests in `MeltableObstacleTests` pass (green check).

---

## Task 3: Implement `MeltableObstacleController`

**Files:**
- Create: `Assets/Scripts/Platformer/MeltableObstacleController.cs`

MonoBehaviour wrapper. Owns melted state, the proximity flag, and the fade coroutine. The `TryMelt` method is the stable seam shared by the debug caster and (future) voice caster.

- [ ] **Step 1: Create `MeltableObstacleController.cs`**

Create `Assets/Scripts/Platformer/MeltableObstacleController.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using Axiom.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Axiom.Platformer
{
    /// <summary>
    /// MonoBehaviour wrapper for an in-scene meltable obstacle (e.g. an Ice Wall).
    /// Owns melted state and the visual fade coroutine. TryMelt(spellId) is the
    /// stable seam shared by DEV-46's debug caster and DEV-82's future voice caster.
    /// The Inspector takes SpellData assets (not raw strings) so the melt whitelist
    /// is grounded in real spells from Assets/Data/Spells/ — typos and orphan IDs
    /// are impossible. The pure decision class still operates on strings so the
    /// runtime seam matches what voice recognition produces.
    /// </summary>
    public class MeltableObstacleController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Tilemap painted with the obstacle tiles. Color is tinted during the impact pulse, then alpha-faded.")]
        private Tilemap _tilemap;

        [SerializeField]
        [Tooltip("Solid collider that blocks the player. Disabled mid-fade so the player can walk through immediately.")]
        private TilemapCollider2D _solidCollider;

        [SerializeField]
        [Tooltip("SpellData assets that melt this obstacle. For Level_1-1's Ice Wall: SD_Combust.")]
        private List<SpellData> _meltSpells = new();

        [SerializeField]
        [Tooltip("Total fade duration in seconds, including the 0.15s impact pulse.")]
        private float _fadeDuration = 0.7f;

        private const float ImpactDuration = 0.15f;
        private static readonly Color ImpactTint = new Color(0.749f, 0.914f, 1f, 1f); // #BFE9FF

        private bool _isMelted;
        private bool _isPlayerInRange;

        public bool IsMelted => _isMelted;
        public bool IsPlayerInRange => _isPlayerInRange;

        public void SetPlayerInRange(bool inRange)
        {
            _isPlayerInRange = inRange;
        }

        public bool TryMelt(string spellId)
        {
            if (_isMelted) return false;
            if (!_isPlayerInRange) return false;

            var meltSpellIds = new List<string>(_meltSpells.Count);
            for (int i = 0; i < _meltSpells.Count; i++)
            {
                SpellData spell = _meltSpells[i];
                if (spell != null) meltSpellIds.Add(spell.spellName);
            }

            if (!MeltableObstacle.CanMelt(spellId, meltSpellIds)) return false;

            _isMelted = true;
            StartCoroutine(MeltCoroutine());
            return true;
        }

        private IEnumerator MeltCoroutine()
        {
            Color baseColor = _tilemap != null ? _tilemap.color : Color.white;

            float impactElapsed = 0f;
            while (impactElapsed < ImpactDuration)
            {
                impactElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(impactElapsed / ImpactDuration);
                float pulse = 1f - Mathf.Abs(t - 0.5f) * 2f;
                if (_tilemap != null)
                    _tilemap.color = Color.Lerp(baseColor, ImpactTint, pulse);
                yield return null;
            }

            if (_tilemap != null)
                _tilemap.color = baseColor;

            if (_solidCollider != null)
                _solidCollider.enabled = false;

            Vector3 startScale = _tilemap != null ? _tilemap.transform.localScale : Vector3.one;
            Vector3 endScale = new Vector3(startScale.x, startScale.y * 0.6f, startScale.z);

            float fadeWindow = Mathf.Max(0.01f, _fadeDuration - ImpactDuration);
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeWindow)
            {
                fadeElapsed += Time.deltaTime;
                float u = Mathf.Clamp01(fadeElapsed / fadeWindow);
                float ease = 1f - (1f - u) * (1f - u); // EaseOutQuad
                float alpha = Mathf.Lerp(1f, 0f, ease);

                if (_tilemap != null)
                {
                    Color c = baseColor;
                    c.a = alpha;
                    _tilemap.color = c;
                    _tilemap.transform.localScale = Vector3.Lerp(startScale, endScale, ease);
                }
                yield return null;
            }

            if (_tilemap != null)
                _tilemap.gameObject.SetActive(false);
        }
    }
}
```

---

## Task 4: Implement `MeltableObstacleProximityForwarder`

**Files:**
- Create: `Assets/Scripts/Platformer/MeltableObstacleProximityForwarder.cs`

Thin trigger glue. The root collider on the prefab is the *solid* `TilemapCollider2D` — players cannot enter it — so a child trigger volume is needed to detect proximity.

- [ ] **Step 1: Create `MeltableObstacleProximityForwarder.cs`**

Create `Assets/Scripts/Platformer/MeltableObstacleProximityForwarder.cs`:

```csharp
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Sits on a child trigger volume around a MeltableObstacleController. Forwards
    /// player enter/exit events to the parent controller's range flag. Kept thin so
    /// the proximity collider can be authored as a generic trigger.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class MeltableObstacleProximityForwarder : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Parent controller whose _isPlayerInRange flag this forwarder updates. Defaults to first controller found in parents.")]
        private MeltableObstacleController _controller;

        private void Reset()
        {
            Collider2D c = GetComponent<Collider2D>();
            if (c != null) c.isTrigger = true;
            if (_controller == null)
                _controller = GetComponentInParent<MeltableObstacleController>();
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

---

## Task 5: Implement `MeltableObstacleDebugCaster`

**Files:**
- Create: `Assets/Scripts/Platformer/MeltableObstacleDebugCaster.cs`

DEV-46 stub. Drives the controller via the `DebugMeltCast` `InputAction`. The caster has no privileged access — it goes through the public `TryMelt` method exactly like the future voice caster will.

- [ ] **Step 1: Create `MeltableObstacleDebugCaster.cs`**

Create `Assets/Scripts/Platformer/MeltableObstacleDebugCaster.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Axiom.Platformer
{
    /// <summary>
    /// DEV-46 stub. Listens for the DebugMeltCast InputAction (default: keyboard M)
    /// and calls TryMelt(_debugSpellId) on the controller. Replaced by DEV-82's voice
    /// caster — at that point delete this component and the DebugMeltCast action.
    /// </summary>
    public class MeltableObstacleDebugCaster : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Controller this caster targets. Defaults to one on the same GameObject.")]
        private MeltableObstacleController _controller;

        [SerializeField]
        [Tooltip("InputActionReference for the DebugMeltCast action defined in InputSystem_Actions.inputactions.")]
        private InputActionReference _debugMeltAction;

        [SerializeField]
        [Tooltip("Spell ID passed to TryMelt when the action fires. Default \"combust\".")]
        private string _debugSpellId = "combust";

        private void Reset()
        {
            if (_controller == null)
                _controller = GetComponent<MeltableObstacleController>();
        }

        private void OnEnable()
        {
            if (_debugMeltAction == null || _debugMeltAction.action == null) return;
            _debugMeltAction.action.performed += OnDebugMeltPerformed;
            _debugMeltAction.action.Enable();
        }

        private void OnDisable()
        {
            if (_debugMeltAction == null || _debugMeltAction.action == null) return;
            _debugMeltAction.action.performed -= OnDebugMeltPerformed;
            _debugMeltAction.action.Disable();
        }

        private void OnDebugMeltPerformed(InputAction.CallbackContext _)
        {
            if (_controller == null) return;
            _controller.TryMelt(_debugSpellId);
        }
    }
}
```

---

## Task 6: Check in scripts and tests

- [ ] **Step 1: Re-run all PlatformerTests in Test Runner**

> **Unity Editor task (user):** Window → General → Test Runner → EditMode tab → click **Run All**.
> Expected: all `PlatformerTests` pass, including the 9 new `MeltableObstacleTests`. No compile errors.

- [ ] **Step 2: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-46): add MeltableObstacle scripts and tests`
- `Assets/Scripts/Platformer/MeltableObstacle.cs`
- `Assets/Scripts/Platformer/MeltableObstacle.cs.meta`
- `Assets/Scripts/Platformer/MeltableObstacleController.cs`
- `Assets/Scripts/Platformer/MeltableObstacleController.cs.meta`
- `Assets/Scripts/Platformer/MeltableObstacleProximityForwarder.cs`
- `Assets/Scripts/Platformer/MeltableObstacleProximityForwarder.cs.meta`
- `Assets/Scripts/Platformer/MeltableObstacleDebugCaster.cs`
- `Assets/Scripts/Platformer/MeltableObstacleDebugCaster.cs.meta`
- `Assets/Tests/Editor/Platformer/MeltableObstacleTests.cs`
- `Assets/Tests/Editor/Platformer/MeltableObstacleTests.cs.meta`

---

## Task 7: Add `DebugMeltCast` InputAction

**Files:**
- Modify: `Assets/InputSystem_Actions.inputactions`
- Modify (auto-regenerated): `Assets/Scripts/Platformer/InputSystem_Actions.cs`

- [ ] **Step 1: Add the action and binding**

> **Unity Editor task (user):**
> 1. Project window → double-click `Assets/InputSystem_Actions.inputactions` to open the Input Actions editor.
> 2. In the **Action Maps** column on the left, select **Player**.
> 3. In the **Actions** column, click the **+** button → name the new action `DebugMeltCast`.
> 4. With `DebugMeltCast` selected, set **Action Type = Button** in the right-hand properties panel.
> 5. Expand `DebugMeltCast`, click **<No Binding>**, then in **Path** enter `<Keyboard>/m` (or use Listen, then press M).
> 6. Click **Save Asset** at the top of the Input Actions editor.
> 7. Return to the Project window. Confirm `Assets/Scripts/Platformer/InputSystem_Actions.cs` has been regenerated with a `@DebugMeltCast` property on the `Player` partial. If it has not, open the `.inputactions` asset's Inspector and verify **Generate C# Class** is enabled at the path `Assets/Scripts/Platformer/InputSystem_Actions.cs`.

- [ ] **Step 2: Sanity-check by entering Play mode in any scene that already has a Player**

> **Unity Editor task (user):** Open `Assets/Scenes/Platformer.unity` (or another platformer scene). Enter Play mode. The action does not yet drive anything — this is just verifying the project still compiles cleanly with the regenerated `InputSystem_Actions.cs`. Exit Play mode.

---

## Task 8: Build `P_IceWall.prefab`

**Files:**
- Create: `Assets/Prefabs/Platformer/P_IceWall.prefab`

A self-contained prefab with its own `Grid` so it drops into any platformer scene without scene-level setup.

- [ ] **Step 1: Open a scratch scene for prefab authoring**

> **Unity Editor task (user):** File → New Scene → choose **Basic 2D** template → **don't save**. This is a scratch context for building the prefab; nothing in this scene gets committed.

- [ ] **Step 2: Build the prefab hierarchy**

> **Unity Editor task (user):** In the Hierarchy window, create the following:
>
> 1. Right-click → 2D Object → Tilemap → Rectangular. Unity creates a `Grid` GameObject with a child `Tilemap`. Rename the `Grid` to **`P_IceWall`**. On `P_IceWall.Grid`, set **Cell Size = (1, 1, 0)** to match the scene Grid built in DEV-46 Task 1.
> 2. On the child `Tilemap`, ensure it has `Tilemap`, `TilemapRenderer`, and `TilemapCollider2D` (the default Rectangular template adds these). Confirm the `TilemapCollider2D` is **not** marked Is Trigger — this is the *solid* wall.
> 3. Right-click `P_IceWall` → Create Empty → name the child **`ProximityTrigger`**. Add a `BoxCollider2D` to it; set **Is Trigger = true**.
> 4. With the palette open (Window → 2D → Tile Palette), select `Palette_SnowMountain`. Paint a small placeholder shape (~3 wide × 5 tall) of ice tiles into the `Tilemap` so the prefab is non-empty when previewed.
> 5. Resize the `ProximityTrigger`'s `BoxCollider2D` Size and Offset so the trigger volume covers the painted bounds plus ~1 unit buffer on each side (left, right, top, bottom).

- [ ] **Step 3: Add and wire the runtime components**

> **Unity Editor task (user):**
>
> 1. Select the `P_IceWall` root. Add Component → `MeltableObstacleController`. In the Inspector, wire:
>    - `_tilemap` → drag in the child `Tilemap` GameObject's `Tilemap` component.
>    - `_solidCollider` → drag in the child `Tilemap` GameObject's `TilemapCollider2D` component.
>    - `_meltSpells` → expand, set Size = 1, drag **`Assets/Data/Spells/SD_Combust.asset`** into Element 0. (The controller resolves to `SpellData.spellName` — `"combust"` — at call time, so changing the asset's `spellName` later updates this obstacle automatically.)
>    - `_fadeDuration` → leave at **`0.7`**.
> 2. Still on `P_IceWall` root: Add Component → `MeltableObstacleDebugCaster`. In the Inspector, wire:
>    - `_controller` → drag in the `MeltableObstacleController` from this same GameObject.
>    - `_debugMeltAction` → click the circle picker, search for and select **`DebugMeltCast`** (the InputActionReference auto-created when you saved the `.inputactions` asset in Task 7).
>    - `_debugSpellId` → leave at **`combust`**.
> 3. Select the child `ProximityTrigger`. Add Component → `MeltableObstacleProximityForwarder`. In the Inspector, wire:
>    - `_controller` → drag in the `MeltableObstacleController` on the parent `P_IceWall` root.

- [ ] **Step 4: Save as a prefab**

> **Unity Editor task (user):**
>
> 1. In the Project window, navigate to `Assets/Prefabs/Platformer/`. (Create the `Platformer/` subfolder under `Prefabs/` if it does not already exist.)
> 2. Drag the `P_IceWall` GameObject from the Hierarchy into the `Platformer/` folder. Unity creates `P_IceWall.prefab` (and its `.meta`).
> 3. Delete the `P_IceWall` instance still in the scratch scene Hierarchy — only the prefab asset should remain.

- [ ] **Step 5: Manual verification in the scratch scene**

> **Unity Editor task (user):**
>
> 1. Drag the `P_IceWall.prefab` back into the scratch scene next to a temporary placeholder GameObject tagged `Player` (Hierarchy → Create Empty, set Tag = Player, give it a `BoxCollider2D` not-trigger so it can collide with the wall).
> 2. Position the placeholder Player so it overlaps the prefab's `ProximityTrigger`.
> 3. Enter Play mode. Press **M**. Expected: tilemap pulses cyan, collider drops at ~0.15s, alpha fades to 0 over the next ~0.55s, tilemap GameObject deactivates at ~0.7s. Exit Play mode.
> 4. Move the placeholder Player outside the `ProximityTrigger`, then re-enter Play mode and press M. Expected: nothing happens (the controller's `_isPlayerInRange` gate blocks the melt).
> 5. Discard the scratch scene without saving.

---

## Task 9: Check in InputAction and prefab

- [ ] **Step 1: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-46): add P_IceWall prefab and DebugMeltCast input action`
- `Assets/InputSystem_Actions.inputactions`
- `Assets/Scripts/Platformer/InputSystem_Actions.cs`
- `Assets/Scripts/Platformer/InputSystem_Actions.cs.meta` (only if the file is newly tracked or its meta GUID changed; usually unchanged on edit)
- `Assets/Prefabs/Platformer/` (the folder, plus the folder's `.meta` sibling — only if the `Platformer/` subfolder was created in Task 8 Step 4)
- `Assets/Prefabs/Platformer/P_IceWall.prefab`
- `Assets/Prefabs/Platformer/P_IceWall.prefab.meta`

---

## Task 10: Place Ice Wall in Level_1-1

**Files:**
- Modify: `Assets/Scenes/Level_1-1.unity`

This implements **Step 5.5** of DEV-46 Task 12 — the new step inserted between Step 5 (Meltspawn placement) and Step 6 (Wire exit trigger).

- [ ] **Step 1: Drop the prefab into the scene**

> **Unity Editor task (user):**
>
> 1. Open `Assets/Scenes/Level_1-1.unity`.
> 2. Drag `Assets/Prefabs/Platformer/P_IceWall.prefab` into the scene Hierarchy at the scene root level.
> 3. Position the `P_IceWall` instance horizontally between `Meltspawn_01` (the first enemy encounter) and `LevelExit_To_1-2` (the right-edge exit trigger). The wall's painted tiles should span the full vertical playfield height — top of the wall above the player's max jump apex, bottom flush with the ground tiles.
> 4. If the placeholder paint shape from the prefab is not tall enough for the playfield: select the child `Tilemap` of the `P_IceWall` instance, open Window → 2D → Tile Palette, and paint additional ice tiles upward to span the playfield. (Editing a prefab instance creates an *instance override* — that is intentional; do not Apply All to the prefab asset, since other scenes should not inherit Level_1-1's specific wall height.)
> 5. After repainting, select the child `ProximityTrigger` and adjust the `BoxCollider2D` Size/Offset so the trigger volume covers the new painted bounds plus the ~1 unit buffer on all sides.
> 6. On the `MeltableObstacleDebugCaster` component on the `P_IceWall` instance, double-check `_debugMeltAction` is wired to the `DebugMeltCast` InputActionReference. (Drag-in references on the prefab asset survive instancing, but a brief sanity check is cheap.)

- [ ] **Step 2: Add the adjacent tutorial prompt**

> **Unity Editor task (user):**
>
> 1. In the scene Hierarchy, right-click at the scene root → Create Empty → name it **`Tutorial_IceWall`**.
> 2. Position `Tutorial_IceWall` immediately *before* the `P_IceWall` (i.e. between `Meltspawn_01` and the wall, on the path the player walks toward the wall).
> 3. Add Component → `BoxCollider2D` (Is Trigger = true). Size it to a comfortable trigger zone the player will walk through ~2–3 seconds before reaching the wall.
> 4. Add Component → `TutorialPromptTrigger`. Wire:
>    - `_message` → exact string: `An icy wall blocks your path. Press M to test-melt it. (Voice cast comes later.)`
>    - `_panel` → drag in the existing `TutorialPromptPanel` GameObject from `HUD_Canvas` (set up in DEV-46 Task 12 Step 1).

- [ ] **Step 3: End-to-end Play-mode verification**

> **Unity Editor task (user):** Enter Play mode from `Level_1-1`. Walk Kaelen left to right and confirm each beat:
>
> 1. Movement / Advantaged / Surprised tutorial prompts appear in sequence (existing DEV-46 Task 12 functionality — sanity check).
> 2. Reach `Meltspawn_01` and resolve the encounter (Advantaged or Surprised triggers Battle scene; defeat or flee).
> 3. After returning to Level_1-1, walk past `Tutorial_IceWall` — the **"An icy wall blocks your path…"** prompt appears.
> 4. Walk into the `P_IceWall` proximity trigger. The wall is solid — Kaelen cannot pass. Press **M**. Expected: cyan pulse, fade, scale.y sink, GameObject deactivates. Walk through.
> 5. Continue right; reach `LevelExit_To_1-2`. (Until DEV-46 Task 16 adds Level_1-2 to Build Settings, this prints a warning instead of loading the next scene — expected.)
>
> Edge cases to confirm in the same Play session:
>
> - Stand outside the proximity trigger (e.g. step back several units) and press **M**. Expected: nothing happens.
> - Stand inside the proximity trigger before any melt and press **M** with the Inspector showing `MeltableObstacleController._meltSpellIds` containing only `combust` and `_debugSpellId = "combust"`. Expected: melt fires.
> - After melt completes, re-enter the proximity zone and press **M** again. Expected: nothing happens (the `_isMelted` gate blocks the second call).

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-46): place Ice Wall and tutorial hint in Level_1-1`
- `Assets/Scenes/Level_1-1.unity`

---

## Task 11: Amend the DEV-46 plan to reference this work

**Files:**
- Modify: `docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md`

The DEV-46 plan tracks Level 1 implementation; the Ice Wall is a real Level_1-1 element, so the plan should point at this implementation plan. We do not duplicate the code — we insert pointers and a Step 5.5 reference.

- [ ] **Step 1: Insert Task 11A and Task 11B headings into the DEV-46 plan**

Open `docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md` and immediately after the existing Task 11 section (which ends with the `feat(DEV-46): add Level 1 EnemyData assets` UVCS check-in and the trailing `---` separator), insert:

```markdown
## Task 11A: Ice Wall scaffold — scripts, tests, prefab, input action

This task is implemented by a sub-plan: [`docs/superpowers/plans/2026-04-25-dev-46-ice-wall-puzzle-scaffold.md`](./2026-04-25-dev-46-ice-wall-puzzle-scaffold.md). Run that plan's Tasks 1–9 in full before continuing to Task 12. It adds:

- `MeltableObstacle` (pure C#) + `MeltableObstacleController`, `MeltableObstacleProximityForwarder`, `MeltableObstacleDebugCaster` MonoBehaviours
- `MeltableObstacleTests` (Edit Mode, NUnit)
- `DebugMeltCast` InputAction in `Assets/InputSystem_Actions.inputactions`, bound to `<Keyboard>/m`
- `Assets/Prefabs/Platformer/P_IceWall.prefab`

DEV-46 scope explicitly excludes voice casting in the platformer scene; voice replaces the debug caster in DEV-82.

---
```

- [ ] **Step 2: Insert Step 5.5 into the DEV-46 plan's Task 12**

In the same DEV-46 plan file, locate Task 12 (`## Task 12: Build Level_1-1 (tutorial stage)`). Between the existing Step 5 (`Place the single Meltspawn encounter`) and the existing Step 6 (`Wire the exit trigger`), insert:

```markdown
- [ ] **Step 5.5: Place the Ice Wall (Task 11A's P_IceWall prefab)**

Implemented by [`2026-04-25-dev-46-ice-wall-puzzle-scaffold.md`](./2026-04-25-dev-46-ice-wall-puzzle-scaffold.md) Task 10. In summary:

- Drop a `P_IceWall` instance into the scene horizontally between `Meltspawn_01` and `LevelExit_To_1-2`. Paint the wall to span the full playfield height; resize the child `ProximityTrigger`'s `BoxCollider2D` to match the painted bounds + ~1 unit buffer.
- Add a `Tutorial_IceWall` GameObject before the wall with a `BoxCollider2D` (Is Trigger) and `TutorialPromptTrigger` (`_message = "An icy wall blocks your path. Press M to test-melt it. (Voice cast comes later.)"`, `_panel` wired to the HUD's `TutorialPromptPanel`).
- Verify Play-mode melt end-to-end: tutorial → Meltspawn → Ice Wall hint → press M while in proximity → wall fades and collider drops → walk through to `LevelExit_To_1-2`.

```

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `docs(DEV-46): reference Ice Wall scaffold sub-plan in DEV-46 Tasks 11A and 12 Step 5.5`
- `docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md`

---

## Self-review checklist (run after the plan is complete)

This is the writing-unity-game-dev-plans Phase 1 review surface. Inline fixes only — no re-review needed if all green.

- **C# guard clause ordering:** `MeltableObstacle.CanMelt` checks `string.IsNullOrWhiteSpace(spellId)` *before* `meltSpellIds == null`. A null/empty spellId never needs the list, so it correctly exits before the list guard. The `for` loop never touches a null list because the second guard returns first. ✓
- **`TryMelt` guard ordering:** `_isMelted` first (cheapest, idempotent), then `_isPlayerInRange` (cheap, prevents long-range cheese), then `MeltableObstacle.CanMelt` (most work). ✓
- **Test coverage:** All 9 reachable branches of `CanMelt` have tests — null/empty/whitespace spellId, null/empty list, single-element match/no-match, multi-element match, case-sensitivity. The 3 MonoBehaviours and the coroutine are intentionally untested in Edit Mode (thin Unity glue, manual verification covers them). ✓
- **UVCS staged file audit:**
  - Task 6 stages all 5 new `.cs` files plus their `.cs.meta`. ✓
  - Task 9 stages `.inputactions` (no separate `.meta` change), `InputSystem_Actions.cs` (auto-regenerated; meta typically unchanged), the new prefab folder if created, the prefab, and the prefab's `.meta`. ✓
  - Task 10 Step 4 stages `Level_1-1.unity` (Unity scenes are checked in as a single file; their `.meta` rarely changes on content edits). ✓
  - Task 11 Step 3 stages the DEV-46 plan markdown. ✓
- **Method signature consistency:** `MeltableObstacle.CanMelt(string spellId, IReadOnlyList<string> meltSpellIds)` — same signature in tests, controller call, and implementation. `MeltableObstacleController.TryMelt(string spellId)` — same in caster call and method definition. `MeltableObstacleController.SetPlayerInRange(bool inRange)` — same in forwarder calls and method definition. ✓
- **Unity Editor task isolation:** Every `> **Unity Editor task (user):**` callout is its own checkbox step. No mixing of code and editor work in the same step. ✓
- **UVCS, never git:** Every check-in step uses `Unity Version Control → Pending Changes → Check in with message:`. No `git add` / `git commit` anywhere. ✓
