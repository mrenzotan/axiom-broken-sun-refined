# DEV-46 Freezable Water Platform Scaffold Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Pair with the `executing-unity-game-dev-plans` skill for Unity Editor handoffs and UVCS check-in cadence.

**Goal:** Add a reusable `P_WaterPlatform.prefab` that the player passes through while liquid and can stand on only when frozen, with a debug-key input layer (DEV-82 will later replace it with voice casting) — without touching the platform logic.

**Architecture:** Pure C# decision class (`FreezablePlatform.CanFreeze`) Edit-Mode tested. A `FreezablePlatformController` MonoBehaviour owns frozen state, the re-melt countdown, the end-of-timer warning blink, the sprite swap (water ↔ ice), and the `BoxCollider2D` toggle, exposing `TryFreeze(spellId)` as the stable seam shared by today's debug caster and tomorrow's voice caster. A child proximity trigger forwards player enter/exit to the controller via a thin forwarder. The prefab is self-contained (a single `SpriteRenderer` + `BoxCollider2D` root with a child `ProximityTrigger`) so it drops into any platformer scene.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, New Input System, Unity Test Framework (Edit Mode, NUnit).

**Related tickets:**
- **DEV-46** — Level 1 (Snow Mountain) implementation. This scaffold is consumed by DEV-46 Task 13 (Build Level_1-2) when that task is scheduled — that is a separate ticket-of-work, not part of this plan.
- **DEV-82** — Voice cast in the platformer scene (future). DEV-82 will delete `FreezablePlatformDebugCaster.cs` and the `DebugFreezeCast` action and replace them with a voice caster that calls the same `TryFreeze(spellId)` seam.

**Source spec:** `docs/superpowers/specs/2026-04-28-dev-46-freezable-water-platform-scaffold-design.md`

**Prerequisites:**
- Existing Ice Wall scaffold (Tasks 11A of the DEV-46 plan) has shipped — `MeltableObstacleProximityForwarder` is the pattern this plan's forwarder mirrors, and the `Player` GameObject in the platformer scene is already tagged `Player`.
- `Assets/Data/Spells/SD_Freeze.asset` exists with `spellName = "freeze"` (already true in this repo).
- `Axiom.Platformer.asmdef` already exists at `Assets/Scripts/Platformer/Platformer.asmdef`; `PlatformerTests.asmdef` already exists at `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef`. No new asmdefs are required.

---

## File Structure

### New runtime scripts (`Assets/Scripts/Platformer/`, covered by existing `Axiom.Platformer.asmdef`)

| File | Type | Responsibility |
|---|---|---|
| `FreezablePlatform.cs` | Plain C# (static) | Pure decision: `CanFreeze(string spellId, IReadOnlyList<string> freezeSpellIds)`. Edit-Mode tested. |
| `FreezablePlatformController.cs` | MonoBehaviour | Holds frozen state, runs the re-melt countdown, runs the end-of-timer flash blink, swaps the SpriteRenderer's sprite (water ↔ ice), toggles the `BoxCollider2D`, exposes `TryFreeze(string spellId)`. |
| `FreezablePlatformProximityForwarder.cs` | MonoBehaviour | Sits on the child proximity trigger; forwards `OnTriggerEnter2D` / `OnTriggerExit2D` to the parent controller's `_isPlayerInRange` flag. Sibling-but-separate from `MeltableObstacleProximityForwarder` (no shared interface — see spec "Risks and decisions"). |
| `FreezablePlatformDebugCaster.cs` | MonoBehaviour | DEV-46 stub. Listens for the `DebugFreezeCast` `InputAction`; calls `TryFreeze(_debugSpellId)` on the controller only while `_isPlayerInRange` is true on the controller. Replaced by DEV-82's voice caster. |

### New tests (`Assets/Tests/Editor/Platformer/`, covered by existing `PlatformerTests.asmdef`)

| File | Tests |
|---|---|
| `FreezablePlatformTests.cs` | 4 cases covering the `CanFreeze` decision logic — null spellId, empty spellId, single-element match, single-element no-match. (Mirrors `MeltableObstacleTests.cs`.) |

### New Unity Editor assets

| Asset | Location |
|---|---|
| `water_platform_placeholder.png` | `Assets/Art/Sprites/Platformer/` |
| `ice_platform_placeholder.png` | `Assets/Art/Sprites/Platformer/` |
| `P_WaterPlatform.prefab` | `Assets/Prefabs/Platformer/` |

### Modified

| File | Change |
|---|---|
| `Assets/InputSystem_Actions.inputactions` | Add `DebugFreezeCast` button action under the `Player` map, bound to `<Keyboard>/f`. The auto-generated `Assets/Scripts/Platformer/InputSystem_Actions.cs` regenerates on save. |

### Out of scope (not modified by this plan)

- `docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md` — the umbrella plan is **not** modified by this scaffold. Per the source spec ("Plan integration"), the umbrella amendment is its own ticket-of-work that fires when Task 13 (Build Level_1-2) is actually scheduled.
- `Assets/Scenes/Level_1-2.unity` — does not exist yet, and is not authored here. The final manual verification uses a throwaway scratch scene (mirroring how Ice Wall verified before Level_1-1 placement).

### Deferred deletion (DEV-82, not now)

| File | Reason |
|---|---|
| `Assets/Scripts/Platformer/FreezablePlatformDebugCaster.cs` | Replaced by voice caster |
| `DebugFreezeCast` action in `InputSystem_Actions.inputactions` | Replaced by voice caster |

---

## Task 1: Write failing tests for `FreezablePlatform.CanFreeze`

**Files:**
- Create: `Assets/Tests/Editor/Platformer/FreezablePlatformTests.cs`

This is the TDD step: write the tests first against a class that does not yet exist, then verify they fail to compile.

- [ ] **Step 1: Create `FreezablePlatformTests.cs`**

Create `Assets/Tests/Editor/Platformer/FreezablePlatformTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class FreezablePlatformTests
    {
        [Test]
        public void CanFreeze_NullSpellId_ReturnsFalse()
        {
            var freezeSpellIds = new List<string> { "freeze" };

            bool result = FreezablePlatform.CanFreeze(null, freezeSpellIds);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanFreeze_EmptySpellId_ReturnsFalse()
        {
            var freezeSpellIds = new List<string> { "freeze" };

            bool result = FreezablePlatform.CanFreeze(string.Empty, freezeSpellIds);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanFreeze_SpellInList_ReturnsTrue()
        {
            var freezeSpellIds = new List<string> { "freeze" };

            bool result = FreezablePlatform.CanFreeze("freeze", freezeSpellIds);

            Assert.IsTrue(result);
        }

        [Test]
        public void CanFreeze_SpellNotInList_ReturnsFalse()
        {
            var freezeSpellIds = new List<string> { "freeze" };

            bool result = FreezablePlatform.CanFreeze("combust", freezeSpellIds);

            Assert.IsFalse(result);
        }
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail to compile**

> **Unity Editor task (user):** Window → General → Test Runner → EditMode tab → click **Run All**.
> Expected: compile error in `PlatformerTests.asmdef` because `FreezablePlatform` does not exist yet. The Console will show `error CS0103: The name 'FreezablePlatform' does not exist...`. This is the desired failing state — it confirms the test file is wired into the `PlatformerTests` asmdef and will exercise the type once it exists.

---

## Task 2: Implement `FreezablePlatform.CanFreeze`

**Files:**
- Create: `Assets/Scripts/Platformer/FreezablePlatform.cs`

Pure decision logic — no Unity dependencies. Mirrors `MeltableObstacle.CanMelt` exactly: null/empty spell ID guard first, then null collection guard, then iterate.

- [ ] **Step 1: Create `FreezablePlatform.cs`**

Create `Assets/Scripts/Platformer/FreezablePlatform.cs`:

```csharp
using System.Collections.Generic;

namespace Axiom.Platformer
{
    public static class FreezablePlatform
    {
        public static bool CanFreeze(string spellId, IReadOnlyList<string> freezeSpellIds)
        {
            if (string.IsNullOrEmpty(spellId)) return false;
            if (freezeSpellIds == null) return false;

            for (int i = 0; i < freezeSpellIds.Count; i++)
            {
                if (freezeSpellIds[i] == spellId) return true;
            }

            return false;
        }
    }
}
```

- [ ] **Step 2: Run the tests and verify they pass**

> **Unity Editor task (user):** Window → General → Test Runner → EditMode tab → click **Run All**.
> Expected: all 4 tests in `FreezablePlatformTests` pass (green check). All previously-passing `PlatformerTests` (e.g. `MeltableObstacleTests`) remain green.

---

## Task 3: Implement `FreezablePlatformController`

**Files:**
- Create: `Assets/Scripts/Platformer/FreezablePlatformController.cs`

MonoBehaviour wrapper. Owns frozen state, the proximity flag, and the freeze coroutine (sprite swap → solid window → accelerating warning blink → sprite swap back → collider drop). The `TryFreeze` method is the stable seam shared by the debug caster and (future) voice caster.

- [ ] **Step 1: Create `FreezablePlatformController.cs`**

Create `Assets/Scripts/Platformer/FreezablePlatformController.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Platformer
{
    public class FreezablePlatformController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private BoxCollider2D _solidCollider;
        [SerializeField] private Sprite _waterSprite;
        [SerializeField] private Sprite _iceSprite;
        [SerializeField] private List<SpellData> _freezeSpells = new();
        [SerializeField, Min(1f)] private float _freezeDuration = 5f;
        [SerializeField, Min(0.1f)] private float _warningWindow = 1.5f;
        [SerializeField] private float _warningFlashStartHz = 4f;
        [SerializeField] private float _warningFlashEndHz = 12f;

        private bool _isFrozen;
        private bool _isPlayerInRange;

        public bool IsFrozen => _isFrozen;
        public bool IsPlayerInRange => _isPlayerInRange;

        public void SetPlayerInRange(bool inRange)
        {
            _isPlayerInRange = inRange;
        }

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

        private IEnumerator FreezeCoroutine()
        {
            SetVisualState(frozen: true);

            float solidWindow = Mathf.Max(0f, _freezeDuration - _warningWindow);
            yield return new WaitForSeconds(solidWindow);

            float elapsed = 0f;
            Color color = _spriteRenderer != null ? _spriteRenderer.color : Color.white;
            while (elapsed < _warningWindow)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _warningWindow);
                float hz = Mathf.Lerp(_warningFlashStartHz, _warningFlashEndHz, progress);
                float wave = Mathf.Sin(elapsed * hz * 2f * Mathf.PI);
                color.a = wave > 0f ? 1f : 0.5f;
                if (_spriteRenderer != null) _spriteRenderer.color = color;
                yield return null;
            }

            color.a = 1f;
            if (_spriteRenderer != null) _spriteRenderer.color = color;
            SetVisualState(frozen: false);
            _isFrozen = false;
        }

        private void SetVisualState(bool frozen)
        {
            if (_spriteRenderer != null)
                _spriteRenderer.sprite = frozen ? _iceSprite : _waterSprite;
            if (_solidCollider != null)
                _solidCollider.enabled = frozen;
        }
    }
}
```

---

## Task 4: Implement `FreezablePlatformProximityForwarder`

**Files:**
- Create: `Assets/Scripts/Platformer/FreezablePlatformProximityForwarder.cs`

Thin trigger glue. The root collider on the prefab is the *solid* `BoxCollider2D` — but it is disabled while the platform is liquid, and the player needs proximity detection regardless of frozen state — so a child trigger volume sized to sprite + ~1 unit buffer is needed. This is a sibling-but-separate script from `MeltableObstacleProximityForwarder` (no shared interface — see source spec "Risks and decisions").

- [ ] **Step 1: Create `FreezablePlatformProximityForwarder.cs`**

Create `Assets/Scripts/Platformer/FreezablePlatformProximityForwarder.cs`:

```csharp
using UnityEngine;

namespace Axiom.Platformer
{
    [RequireComponent(typeof(Collider2D))]
    public class FreezablePlatformProximityForwarder : MonoBehaviour
    {
        [SerializeField] private FreezablePlatformController _controller;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
            if (_controller == null)
                _controller = GetComponentInParent<FreezablePlatformController>();
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

## Task 5: Implement `FreezablePlatformDebugCaster`

**Files:**
- Create: `Assets/Scripts/Platformer/FreezablePlatformDebugCaster.cs`

DEV-46 stub. Drives the controller via the `DebugFreezeCast` `InputAction`. The caster has no privileged access — it goes through the public `TryFreeze` method exactly like the future voice caster will. The proximity gate lives inside `TryFreeze`, so the caster does not need to check `_isPlayerInRange` itself.

- [ ] **Step 1: Create `FreezablePlatformDebugCaster.cs`**

Create `Assets/Scripts/Platformer/FreezablePlatformDebugCaster.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Axiom.Platformer
{
    [RequireComponent(typeof(FreezablePlatformController))]
    public class FreezablePlatformDebugCaster : MonoBehaviour
    {
        [SerializeField] private InputActionReference _debugFreezeAction;
        [SerializeField] private string _debugSpellId = "freeze";

        private FreezablePlatformController _controller;

        private void Awake()
        {
            _controller = GetComponent<FreezablePlatformController>();
        }

        private void OnEnable()
        {
            if (_debugFreezeAction == null || _debugFreezeAction.action == null)
            {
                Debug.LogError(
                    "[FreezablePlatformDebugCaster] _debugFreezeAction is not assigned or its action is null. " +
                    "Disabling component.", this);
                enabled = false;
                return;
            }

            _debugFreezeAction.action.performed += OnDebugFreezePerformed;
            _debugFreezeAction.action.Enable();
        }

        private void OnDisable()
        {
            if (_debugFreezeAction == null || _debugFreezeAction.action == null) return;
            _debugFreezeAction.action.performed -= OnDebugFreezePerformed;
            _debugFreezeAction.action.Disable();
        }

        private void OnDebugFreezePerformed(InputAction.CallbackContext _)
        {
            if (_controller == null) return;
            _controller.TryFreeze(_debugSpellId);
        }
    }
}
```

---

## Task 6: Check in scripts and tests

- [ ] **Step 1: Re-run all PlatformerTests in Test Runner**

> **Unity Editor task (user):** Window → General → Test Runner → EditMode tab → click **Run All**.
> Expected: all `PlatformerTests` pass, including the 4 new `FreezablePlatformTests`. No compile errors anywhere in the project.

- [ ] **Step 2: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-46): add FreezablePlatform scripts and tests`
- `Assets/Scripts/Platformer/FreezablePlatform.cs`
- `Assets/Scripts/Platformer/FreezablePlatform.cs.meta`
- `Assets/Scripts/Platformer/FreezablePlatformController.cs`
- `Assets/Scripts/Platformer/FreezablePlatformController.cs.meta`
- `Assets/Scripts/Platformer/FreezablePlatformProximityForwarder.cs`
- `Assets/Scripts/Platformer/FreezablePlatformProximityForwarder.cs.meta`
- `Assets/Scripts/Platformer/FreezablePlatformDebugCaster.cs`
- `Assets/Scripts/Platformer/FreezablePlatformDebugCaster.cs.meta`
- `Assets/Tests/Editor/Platformer/FreezablePlatformTests.cs`
- `Assets/Tests/Editor/Platformer/FreezablePlatformTests.cs.meta`

---

## Task 7: Add `DebugFreezeCast` InputAction

**Files:**
- Modify: `Assets/InputSystem_Actions.inputactions`
- Modify (auto-regenerated): `Assets/Scripts/Platformer/InputSystem_Actions.cs`

- [ ] **Step 1: Add the action and binding**

> **Unity Editor task (user):**
> 1. Project window → double-click `Assets/InputSystem_Actions.inputactions` to open the Input Actions editor.
> 2. In the **Action Maps** column on the left, select **Player**.
> 3. In the **Actions** column, click the **+** button → name the new action `DebugFreezeCast`.
> 4. With `DebugFreezeCast` selected, set **Action Type = Button** in the right-hand properties panel.
> 5. Expand `DebugFreezeCast`, click **<No Binding>**, then in **Path** enter `<Keyboard>/f` (or use Listen, then press F).
> 6. Click **Save Asset** at the top of the Input Actions editor.
> 7. Return to the Project window. Confirm `Assets/Scripts/Platformer/InputSystem_Actions.cs` has been regenerated with a `@DebugFreezeCast` property on the `Player` partial. If it has not, open the `.inputactions` asset's Inspector and verify **Generate C# Class** is enabled at the path `Assets/Scripts/Platformer/InputSystem_Actions.cs`.

- [ ] **Step 2: Sanity-check by entering Play mode in any scene that already has a Player**

> **Unity Editor task (user):** Open `Assets/Scenes/Platformer.unity` (or another platformer scene). Enter Play mode. The action does not yet drive anything — this is just verifying the project still compiles cleanly with the regenerated `InputSystem_Actions.cs`. Exit Play mode.

---

## Task 8: Create placeholder water and ice sprite assets

**Files:**
- Create: `Assets/Art/Sprites/Platformer/water_platform_placeholder.png`
- Create: `Assets/Art/Sprites/Platformer/ice_platform_placeholder.png`

Two flat-color 32×8 PNGs — cyan for water, pale-white for ice. The `Platformer/` subfolder under `Assets/Art/Sprites/` does not yet exist; the script below creates it. Real sprite art is a Phase 7 polish swap (`_waterSprite` / `_iceSprite` fields on the prefab) and does not block this scaffold.

- [ ] **Step 1: Create the folder and generate the two PNGs**

Run the following from the project root (i.e. `/Users/markrenzotan/Unity Projects/Axiom of the Broken Sun Refined/axiom-broken-sun-refined`). The script uses Python 3 stdlib only (`struct`, `zlib`) — no Pillow needed. macOS ships with `python3`.

```bash
mkdir -p "Assets/Art/Sprites/Platformer"
python3 - <<'PY'
import struct, zlib

def write_png(path, w, h, r, g, b):
    sig = bytes([137, 80, 78, 71, 13, 10, 26, 10])

    ihdr_data = struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)
    ihdr = struct.pack('>I', len(ihdr_data)) + b'IHDR' + ihdr_data \
         + struct.pack('>I', zlib.crc32(b'IHDR' + ihdr_data) & 0xFFFFFFFF)

    raw = b''
    for _ in range(h):
        raw += b'\x00' + bytes([r, g, b, 255]) * w
    compressed = zlib.compress(raw, 9)
    idat = struct.pack('>I', len(compressed)) + b'IDAT' + compressed \
         + struct.pack('>I', zlib.crc32(b'IDAT' + compressed) & 0xFFFFFFFF)

    iend = struct.pack('>I', 0) + b'IEND' \
         + struct.pack('>I', zlib.crc32(b'IEND') & 0xFFFFFFFF)

    with open(path, 'wb') as f:
        f.write(sig + ihdr + idat + iend)

write_png('Assets/Art/Sprites/Platformer/water_platform_placeholder.png', 32, 8, 0x3C, 0xC4, 0xFF)
write_png('Assets/Art/Sprites/Platformer/ice_platform_placeholder.png',  32, 8, 0xE6, 0xF8, 0xFF)
print('done')
PY
ls -la "Assets/Art/Sprites/Platformer/"
```

Expected: both `.png` files exist, each ~100–200 bytes, owned by the current user. `done` and a 2-file directory listing print to the terminal.

- [ ] **Step 2: Configure import settings in Unity**

> **Unity Editor task (user):** Return to Unity (it auto-imports the new files). For each of the two new PNGs:
>
> 1. Select the PNG in the Project window. The Inspector shows the Texture Importer.
> 2. Set:
>    - **Texture Type** = **Sprite (2D and UI)**
>    - **Sprite Mode** = **Single**
>    - **Pixels Per Unit** = **16** (codebase convention — matches all Player, VFX, and Snow Mountain `16x16/` tile imports; the source design spec's "32" is incorrect)
>    - **Filter Mode** = **Point (no filter)**
>    - **Compression** = **None**
> 3. Click **Apply** at the bottom of the Inspector.
> 4. Repeat for the second PNG.
>
> Expected: each sprite previews as a solid-color rectangle in the Inspector preview pane. At PPU 16 a 32×8-pixel placeholder is 2.0 × 0.5 world units — about two ground-tile-widths wide.

---

## Task 9: Build `P_WaterPlatform.prefab`

**Files:**
- Create: `Assets/Prefabs/Platformer/P_WaterPlatform.prefab`

A self-contained prefab that drops into any platformer scene without scene-level setup. Hierarchy: a single root `SpriteRenderer` + `BoxCollider2D` GameObject (no Grid/Tilemap — the platform is a discrete sprite, not a painted region) with a child `ProximityTrigger`.

- [ ] **Step 1: Open a scratch scene for prefab authoring**

> **Unity Editor task (user):** File → New Scene → choose **Basic 2D** template → **don't save**. This is a scratch context for building the prefab; nothing in this scene gets committed.

- [ ] **Step 2: Build the prefab hierarchy**

> **Unity Editor task (user):** In the Hierarchy window:
>
> 1. Right-click → **Create Empty** → name it **`P_WaterPlatform`**. With the new GameObject selected, set Position = (0, 0, 0). **Set the GameObject's Layer (top-right of the Inspector) to `Ground`.** This is required — `PlayerMovement.CheckGrounded()` only treats colliders on the `Ground` layer as ground, so without this the player would stand on the frozen platform but `_isGrounded` would stay false (no jumping, airborne animator state). Do NOT use `OneWayPlatform` — that layer is for drop-through platforms; the spec wants this platform solid both top and bottom when frozen.
> 2. Add Component → **Sprite Renderer**. Drag `Assets/Art/Sprites/Platformer/water_platform_placeholder.png` into the **Sprite** field. Confirm **Draw Mode** = **Simple**, and the platform renders as a flat cyan rectangle in the Scene view.
> 3. Add Component → **Box Collider 2D**. Confirm **Is Trigger = false**. Click the small **Edit Collider** button on the BoxCollider2D and confirm the collider exactly matches the sprite bounds (Unity auto-sizes on add). Then **uncheck the BoxCollider2D's enabled checkbox at the top of the component header** — the platform is liquid by default, so the solid collider must start disabled.
> 4. Right-click `P_WaterPlatform` in the Hierarchy → **Create Empty** → name the child **`ProximityTrigger`**. With it selected, set Local Position = (0, 0, 0). **Leave the child's Layer at `Default`** (or anything that is NOT `Ground`) — the trigger volume is always present even while the platform is liquid, so putting it on `Ground` would risk false-positive ground detection depending on the project's `Queries Hit Triggers` physics setting. Add Component → **Box Collider 2D**. Set **Is Trigger = true**. Resize **Size** so the trigger volume covers the sprite bounds plus ~1 unit buffer on each side (left, right, top, bottom). For a 32×8-pixel sprite at 16 PPU (2 × 0.5 world units), Size ≈ (4, 2.5) is a reasonable starting buffer.

- [ ] **Step 3: Add and wire the runtime components**

> **Unity Editor task (user):**
>
> 1. Select the `P_WaterPlatform` root. Add Component → **`FreezablePlatformController`**. In the Inspector, wire:
>    - `_spriteRenderer` → drag in the `SpriteRenderer` from this same GameObject.
>    - `_solidCollider` → drag in the `BoxCollider2D` from this same GameObject.
>    - `_waterSprite` → drag in `Assets/Art/Sprites/Platformer/water_platform_placeholder.png`.
>    - `_iceSprite` → drag in `Assets/Art/Sprites/Platformer/ice_platform_placeholder.png`.
>    - `_freezeSpells` → expand, set Size = 1, drag **`Assets/Data/Spells/SD_Freeze.asset`** into Element 0. (The controller resolves to `SpellData.spellName` — `"freeze"` — at call time.)
>    - `_freezeDuration` → leave at **`5`**.
>    - `_warningWindow` → leave at **`1.5`**.
>    - `_warningFlashStartHz` → leave at **`4`**.
>    - `_warningFlashEndHz` → leave at **`12`**.
> 2. Still on `P_WaterPlatform` root: Add Component → **`FreezablePlatformDebugCaster`**. In the Inspector, wire:
>    - `_debugFreezeAction` → click the circle picker, search for and select **`DebugFreezeCast`** (the InputActionReference auto-created when you saved the `.inputactions` asset in Task 7).
>    - `_debugSpellId` → leave at **`freeze`**.
>    - (No `_controller` field — the caster uses `RequireComponent(FreezablePlatformController)` and resolves it in `Awake`.)
> 3. Select the child `ProximityTrigger`. Add Component → **`FreezablePlatformProximityForwarder`**. In the Inspector, wire:
>    - `_controller` → drag in the `FreezablePlatformController` on the parent `P_WaterPlatform` root.

- [ ] **Step 4: Save as a prefab**

> **Unity Editor task (user):**
>
> 1. In the Project window, navigate to `Assets/Prefabs/Platformer/`.
> 2. Drag the `P_WaterPlatform` GameObject from the Hierarchy into the `Platformer/` folder. Unity creates `P_WaterPlatform.prefab` (and its `.meta`).
> 3. Delete the `P_WaterPlatform` instance still in the scratch scene Hierarchy — only the prefab asset should remain.

---

## Task 10: Manual scratch-scene verification

**Files:**
- (Throwaway) `Assets/Scenes/_ScratchFreezablePlatform.unity` — never committed to Build Settings, never checked in.

Mirrors the in-spec manual verification from `2026-04-28-dev-46-freezable-water-platform-scaffold-design.md` ("Manual verification (in-spec)"). This is a scratch playtest — discard the scene afterward.

- [ ] **Step 1: Build the scratch scene**

> **Unity Editor task (user):**
>
> 1. File → New Scene → choose **Basic 2D** template. File → Save As → `Assets/Scenes/_ScratchFreezablePlatform.unity`. Do **not** add it to Build Settings.
> 2. Copy the Player into the scratch scene:
>    - In a second Hierarchy window (or by reopening `Platformer.unity` in a tab), open `Assets/Scenes/Platformer.unity`.
>    - Locate the root `Player` GameObject (it is tagged `Player` and carries `Rigidbody2D`, `Collider2D`, and `PlayerMovement`).
>    - Right-click → **Copy**.
>    - Switch back to `_ScratchFreezablePlatform.unity` (it must remain the **active** scene).
>    - Right-click in its Hierarchy → **Paste**. Confirm the pasted `Player` carries the `Player` tag (Inspector → top-of-component Tag dropdown).
>    - Also drag `Assets/Prefabs/Platformer/P_PlatformerCamera.prefab` into the scratch scene so the camera follows the Player. Disable / delete any duplicate `Main Camera` GameObject that the Basic 2D template added.
> 3. Add a flat ground stripe under the camera so the Player can stand and walk: Hierarchy → Create → 2D Object → Sprites → Square; scale it to e.g. (40, 1, 1) and position below where the platform will go. Add a `BoxCollider2D` (not trigger).
> 4. Drag `Assets/Prefabs/Platformer/P_WaterPlatform.prefab` from the Project window into the scene Hierarchy. Position it above the ground stripe at a height the Player can fall onto when it freezes. Below the platform, leave a clearly-labelled "death pit" gap in the ground stripe — drag a temporary 2D Sprite (Square, scaled to ~3×3, tinted red, named `DeathMarker`) into the gap so you can see the player fall through.
> 5. Confirm no `Tutorial_*` Canvas / pause UI is present (so input does not get blocked by a focused UI element).
> 6. Save the scratch scene.

- [ ] **Step 2: Run the verification checklist in Play mode**

> **Unity Editor task (user):** Press Play. Verify, in order, with the Inspector visible on the `P_WaterPlatform` instance to watch `_isFrozen` and `_isPlayerInRange` flip:
>
> 1. **Liquid pass-through** — Walk under/onto the platform without casting. The Player passes straight through (no collider). They land on whatever is below — ground stripe or `DeathMarker`. ✓
> 2. **Out-of-range gate** — Walk well outside the proximity trigger (Inspector: `_isPlayerInRange = false`). Press **F**. Nothing happens — no freeze. ✓
> 3. **In-range freeze** — Walk into the proximity trigger (Inspector: `_isPlayerInRange = true`). Press **F**. The sprite swaps to ice (pale-white), the BoxCollider2D enables, and the Player can stand on the platform and jump from it. Inspector: `_isFrozen = true`. ✓
> 4. **Re-cast while frozen** — Stay in proximity. Press **F** again immediately. Nothing happens — no timer refresh, no double-freeze. Inspector: `_isFrozen` stays `true`, the original timer keeps counting. ✓
> 5. **Warning blink** — Wait ~3.5 s after the initial freeze. The sprite begins alpha-blinking between 1.0 and 0.5, and the blink frequency visibly accelerates from ~4 Hz to ~12 Hz over the next ~1.5 s. ✓
> 6. **Melt back** — At ~5 s after the initial freeze, the sprite swaps back to water, the BoxCollider2D disables, and the Player (if standing on it) falls. Inspector: `_isFrozen = false`. ✓
> 7. **Re-freeze post-melt** — Still in proximity, press **F**. The platform freezes again for another full 5 s. ✓
> 8. **Decision-list gate** — Exit Play mode. On the `P_WaterPlatform` instance Inspector, swap `_freezeSpells[0]` from `SD_Freeze` to `SD_Combust`. Re-enter Play mode. Walk into the proximity trigger and press **F**. Nothing happens — `CanFreeze` rejects `"freeze"` because `_freezeSpells[0].spellName == "combust"`. ✓
>
> If any step fails, capture the failing step and any Console errors, exit Play mode, and revisit the relevant Task before proceeding to check-in.

- [ ] **Step 3: Discard the scratch scene**

> **Unity Editor task (user):**
>
> 1. Exit Play mode.
> 2. Restore `_freezeSpells[0]` on the `P_WaterPlatform` instance back to `SD_Freeze` (the value the prefab asset itself ships with — the change in Step 2.8 was an instance-only override on the scratch scene, so this restoration is only relevant if you accidentally clicked **Apply All** to the prefab asset).
> 3. In the Project window, **delete** `Assets/Scenes/_ScratchFreezablePlatform.unity` (and its `.meta`). The scratch scene is throwaway — never check it in.
> 4. Confirm `Assets/Scenes/_ScratchFreezablePlatform.unity` does **not** appear in the next UVCS Pending Changes list.

---

## Task 11: Check in InputAction, sprites, and prefab

- [ ] **Step 1: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-46): add P_WaterPlatform prefab and DebugFreezeCast input action`
- `Assets/InputSystem_Actions.inputactions`
- `Assets/Scripts/Platformer/InputSystem_Actions.cs`
- `Assets/Scripts/Platformer/InputSystem_Actions.cs.meta` (only if the file is newly tracked or its meta GUID changed; usually unchanged on edit)
- `Assets/Art/Sprites/Platformer/` (the new folder, plus the folder's `.meta` sibling)
- `Assets/Art/Sprites/Platformer/water_platform_placeholder.png`
- `Assets/Art/Sprites/Platformer/water_platform_placeholder.png.meta`
- `Assets/Art/Sprites/Platformer/ice_platform_placeholder.png`
- `Assets/Art/Sprites/Platformer/ice_platform_placeholder.png.meta`
- `Assets/Prefabs/Platformer/P_WaterPlatform.prefab`
- `Assets/Prefabs/Platformer/P_WaterPlatform.prefab.meta`

> **Note:** The throwaway `Assets/Scenes/_ScratchFreezablePlatform.unity` from Task 10 must **not** appear in this check-in. If it does, exclude it (and its `.meta`) from the staged set, and delete the scene file from the Project window before staging.

---

## Self-review checklist (run after the plan is complete)

This is the writing-unity-game-dev-plans Phase 1 review surface. Inline fixes only — no re-review needed if all green.

- **Spec coverage:**
  - In-scope item "reusable `P_WaterPlatform.prefab`" → Task 9. ✓
  - In-scope item "Plain-C# decision logic `FreezablePlatform.CanFreeze`" + Edit-Mode tests → Tasks 1, 2. ✓
  - In-scope item "MonoBehaviour controller … exposes `TryFreeze(spellId)`" → Task 3. ✓
  - In-scope item "debug-key input caster (DEV-46 stub)" → Task 5. ✓
  - In-scope item "InputSystem action `DebugFreezeCast` bound to keyboard `F`" → Task 7. ✓
  - In-scope item "two placeholder sprite assets" → Task 8. ✓
  - In-scope item "proximity forwarder" → Task 4. ✓
  - In-spec "Manual verification" → Task 10. ✓
  - Out-of-scope items (`Level_1-2.unity` authoring, voice cast, platform motion, animated water, persistence, chemistry-condition matching, audio) → no tasks added. ✓
- **C# guard clause ordering:** `FreezablePlatform.CanFreeze` checks `string.IsNullOrEmpty(spellId)` first, then `freezeSpellIds == null`. A null/empty spellId never needs the list, so it correctly exits before the list guard. The `for` loop never touches a null list because the second guard returns first. ✓
- **`TryFreeze` guard ordering:** `_isFrozen` first (cheapest, idempotent), then `_isPlayerInRange` (cheap, prevents long-range cheese), then build the local `freezeSpellIds` list and call `FreezablePlatform.CanFreeze` (most work). ✓
- **Test coverage:** The 4 cases listed by the spec — null spellId, empty spellId, single-element match, single-element no-match — are covered. This deliberately mirrors `MeltableObstacleTests.cs`'s 4-case surface for codebase consistency rather than expanding to every reachable branch in `CanFreeze`. The null-list branch in `CanFreeze` is technically reachable (it is a public static method), but the controller's `TryFreeze` always builds a non-null list before calling it, and the codebase convention is to pin only what `MeltableObstacleTests.cs` pins. The empty-list branch is structurally identical to the no-match branch (both fall off the `for` loop and return `false`), so the no-match test exercises it. The 3 MonoBehaviours and the freeze coroutine are intentionally untested in Edit Mode (thin Unity glue with non-deterministic timing); manual verification in Task 10 covers them. ✓
- **UVCS staged file audit:**
  - Task 6 stages all 5 new `.cs` files plus their `.cs.meta`. ✓
  - Task 11 stages `.inputactions` (no separate `.meta` change), `InputSystem_Actions.cs` (auto-regenerated; meta typically unchanged), the new `Assets/Art/Sprites/Platformer/` folder + folder `.meta`, both `.png` files + their `.png.meta`, and the prefab + prefab `.meta`. ✓
  - The throwaway scratch scene from Task 10 is explicitly excluded from Task 11. ✓
- **Method signature consistency:** `FreezablePlatform.CanFreeze(string spellId, IReadOnlyList<string> freezeSpellIds)` — same signature in tests, controller call, and implementation. `FreezablePlatformController.TryFreeze(string spellId)` — same in caster call and method definition. `FreezablePlatformController.SetPlayerInRange(bool inRange)` — same in forwarder calls and method definition. `_debugFreezeAction` field name on the caster matches the `DebugFreezeCast` InputAction; `_freezeSpells` list matches the controller wiring instructions in Task 9. ✓
- **Unity Editor task isolation:** Every `> **Unity Editor task (user):**` callout is its own checkbox step. Only Task 8 Step 1 mixes a Bash command with a follow-up Editor step — and they are explicitly separated into Step 1 (Bash, Claude/CLI) and Step 2 (Editor, user). ✓
- **UVCS, never git:** Every check-in step uses `Unity Version Control → Pending Changes → Check in with message:`. No `git add` / `git commit` anywhere. ✓
- **Prefab vs. spec layout:** Spec says single `SpriteRenderer` (not stacked), `BoxCollider2D` on root next to SpriteRenderer (default `enabled = false`), child `ProximityTrigger` with its own `BoxCollider2D` (isTrigger = true), no Grid/Tilemap — Task 9 Steps 2–3 follow exactly. ✓
- **Behavior tunable defaults match spec:** `_freezeDuration = 5`, `_warningWindow = 1.5`, `_warningFlashStartHz = 4`, `_warningFlashEndHz = 12` — controller code (Task 3) and prefab wiring (Task 9 Step 3) both set these. ✓
- **Umbrella-plan non-modification:** Per spec "Plan integration", the umbrella `2026-04-20-dev-46-level-1-snow-mountain.md` is **not** modified — no Task 11-style amendment as the Ice Wall plan had. The integration with umbrella Task 13 is explicitly deferred. ✓
