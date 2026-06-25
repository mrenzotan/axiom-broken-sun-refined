# DEV-135 UI/Canvas Resolution Scaling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Hybrid plan:** Task 1 is a C# (TDD) task **Claude performs**. Tasks 2–4 are **Unity Editor tasks the user performs** (marked with `> **Unity Editor task (user):**`). Task 5 is user-run verification. Task 6 is docs.

**Goal:** Make every game-UI canvas render correctly at any 16:9 resolution by conforming the three broken canvases to the project's existing CanvasScaler standard, and fix the one C# coupling that breaks under scaling.

**Architecture:** Three canvases are stuck on legacy *Constant Pixel Size @ 800×600* and break off-1080p; the rest of the project already uses *Scale With Screen Size @ 1920×1080, Match 0.5*. We standardize the three onto that, do a proper anchor/pivot pass per canvas, and fix `CreditsController`'s `Screen.height` scroll math (which only worked because Constant Pixel Size made canvas units equal screen pixels) by routing it through a pure, unit-tested geometry helper that uses canvas-reference height.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, uGUI Canvas + CanvasScaler, TextMeshPro, NUnit (Unity Test Framework, EditMode), UVCS.

**Spec:** [docs/superpowers/specs/2026-06-25-dev-135-ui-canvas-resolution-scaling-design.md](../specs/2026-06-25-dev-135-ui-canvas-resolution-scaling-design.md)

## Global Constraints

- **Canvas standard (apply verbatim to every converted canvas):** `UI Scale Mode = Scale With Screen Size`, `Reference Resolution = 1920 × 1080`, `Screen Match Mode = Match Width Or Height`, `Match = 0.5`.
- **MonoBehaviour separation (CLAUDE.md):** logic in plain C# classes; MonoBehaviour does lifecycle/wiring only. New scroll geometry lives in a pure static helper, tested in EditMode.
- **No new abstractions** beyond the single small helper. Surgical changes only.
- **Do NOT touch** `CursorManager.prefab` or any already-correct canvas (HUDCanvas, MainMenuCanvas, MainMenu, Disclaimer, Cutscene) except to verify.
- **Version control = UVCS only.** Never `git add`/`git commit`. Check in via Unity Version Control → Pending Changes. Commit message format: `<type>(DEV-135): <desc>`.
- **Test reference resolutions:** 1280×720, 1920×1080, 2560×1440 (all 16:9).

---

## File Map

| File | Responsibility | Action |
|------|----------------|--------|
| `Assets/Scripts/Core/CreditsScrollMath.cs` | Pure geometry: start offset + scroll-end threshold in **canvas units** | Create |
| `Assets/Tests/Editor/Core/CreditsScrollMathTests.cs` | EditMode tests encoding resolution-independence intent | Create |
| `Assets/Scripts/Core/CreditsController.cs` | Replace `Screen.height` scroll math with helper fed canvas-reference height | Modify (`:172-174`, `:184`) |
| `Assets/Scenes/Battle.unity` | Battle HUD Canvas → standard scaler + anchor pass | Modify (Editor) |
| `Assets/Prefabs/Dialogue/DialogueCanvas.prefab` | Dialogue Canvas → standard scaler + anchor pass | Modify (Editor) |
| `Assets/Scenes/End_Credits.unity` | Credits Canvas → standard scaler + anchor pass | Modify (Editor) |
| `docs/ui-canvas-scaling-standard.md` | Records the standard + CursorManager exception | Create |

---

## Task 1: CreditsController scroll math → resolution-independent helper (C#, TDD — Claude)

**Files:**
- Create: `Assets/Scripts/Core/CreditsScrollMath.cs`
- Test: `Assets/Tests/Editor/Core/CreditsScrollMathTests.cs`
- Modify: `Assets/Scripts/Core/CreditsController.cs` (lines 172–174 and 184, plus a new private helper + field)

**Interfaces:**
- Produces: `Axiom.Core.CreditsScrollMath.StartOffsetY(float viewportHeight) → float` and `Axiom.Core.CreditsScrollMath.EndThresholdY(float contentHeight, float viewportHeight) → float`. Both pure/static; all arguments and results are in canvas-local units (the units of `RectTransform.anchoredPosition`), never device pixels.
- Consumes (in `CreditsController`): the root `Canvas` `RectTransform.rect.height` as the viewport height.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/Editor/Core/CreditsScrollMathTests.cs`:

```csharp
using Axiom.Core;
using NUnit.Framework;

namespace Axiom.Tests.Core
{
    public class CreditsScrollMathTests
    {
        // WHY: credits must begin fully below the visible viewport so they scroll up
        // into view. The offset is exactly one viewport-height below origin, expressed
        // in canvas units — the units of anchoredPosition.
        [Test]
        public void StartOffsetY_IsOneViewportBelowOrigin()
        {
            Assert.AreEqual(-1080f, CreditsScrollMath.StartOffsetY(1080f));
            Assert.AreEqual(-720f, CreditsScrollMath.StartOffsetY(720f));
        }

        // WHY: the scroll must run until the entire content column has cleared the top
        // of the viewport — content height plus one full viewport height.
        [Test]
        public void EndThresholdY_IsContentHeightPlusViewport()
        {
            Assert.AreEqual(3000f + 1080f, CreditsScrollMath.EndThresholdY(3000f, 1080f));
        }

        // WHY (the bug this fixes): geometry depends ONLY on the canvas viewport height
        // passed in, not on device pixels. Under Scale With Screen Size the canvas height
        // is a constant 1080 reference units at every build resolution, so the start/end
        // values are identical at 720p, 1080p and 1440p. Different viewport → different
        // value proves the helper actually consumes its argument.
        [Test]
        public void Geometry_DependsOnlyOnViewportArgument()
        {
            Assert.AreNotEqual(CreditsScrollMath.StartOffsetY(1080f),
                               CreditsScrollMath.StartOffsetY(600f));
            Assert.AreNotEqual(CreditsScrollMath.EndThresholdY(3000f, 1080f),
                               CreditsScrollMath.EndThresholdY(3000f, 600f));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Unity Editor → Window → General → Test Runner → EditMode → run `CreditsScrollMathTests`.
Expected: **compile failure / FAIL** — `CreditsScrollMath` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `Assets/Scripts/Core/CreditsScrollMath.cs`:

```csharp
namespace Axiom.Core
{
    /// <summary>
    /// Pure geometry for the End Credits vertical scroll.
    /// All values are in CANVAS-LOCAL units (the units of RectTransform.anchoredPosition),
    /// never device pixels — so the scroll behaves identically at every build resolution
    /// once the canvas uses Scale With Screen Size.
    /// </summary>
    public static class CreditsScrollMath
    {
        /// <summary>
        /// Y offset that places the content fully below the visible viewport so it
        /// scrolls up into view. Negative = below the screen.
        /// </summary>
        public static float StartOffsetY(float viewportHeight) => -viewportHeight;

        /// <summary>
        /// Y position at which the content has fully scrolled off the top of the
        /// viewport. Content travels from StartOffsetY up to this threshold.
        /// </summary>
        public static float EndThresholdY(float contentHeight, float viewportHeight)
            => contentHeight + viewportHeight;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Test Runner → EditMode → run `CreditsScrollMathTests`.
Expected: **PASS** (3 tests green).

- [ ] **Step 5: Wire `CreditsController` to the helper (canvas units, not `Screen.height`)**

In `Assets/Scripts/Core/CreditsController.cs`:

Add a cached field next to the other Runtime fields (after line 93, `private bool _skipTriggered;`):

```csharp
        private RectTransform _canvasRect;
```

Add this helper method (place it just above `BuildCredits`, after the `Update` method ends at line 123):

```csharp
        // Viewport height in CANVAS units — constant across resolutions under
        // Scale With Screen Size. Falls back to Screen.height only if no Canvas is found.
        private float ViewportHeight()
        {
            if (_canvasRect == null)
            {
                Canvas canvas = _contentRoot.GetComponentInParent<Canvas>();
                if (canvas != null)
                    _canvasRect = canvas.rootCanvas.GetComponent<RectTransform>();
            }
            return _canvasRect != null ? _canvasRect.rect.height : Screen.height;
        }
```

Replace the start-offset block (current lines 171–174):

```csharp
            // Position content below the screen so it scrolls into view
            Vector2 pos = _contentRoot.anchoredPosition;
            pos.y = -Screen.height;
            _contentRoot.anchoredPosition = pos;
```

with:

```csharp
            // Position content below the viewport (canvas units) so it scrolls into view
            Vector2 pos = _contentRoot.anchoredPosition;
            pos.y = CreditsScrollMath.StartOffsetY(ViewportHeight());
            _contentRoot.anchoredPosition = pos;
```

Replace the scroll-target line (current line 184):

```csharp
            float target = _contentRoot.rect.height + Screen.height;
```

with:

```csharp
            float target = CreditsScrollMath.EndThresholdY(_contentRoot.rect.height, ViewportHeight());
```

- [ ] **Step 6: Recompile and re-run EditMode tests**

Return to Unity to recompile. Test Runner → EditMode → run all Core tests.
Expected: **PASS**, no new compile errors. (`CreditsScrollMathTests` green; existing Core tests unaffected.)

- [ ] **Step 7: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `fix(DEV-135): drive credits scroll by canvas height not Screen.height`
  - `Assets/Scripts/Core/CreditsScrollMath.cs`
  - `Assets/Scripts/Core/CreditsScrollMath.cs.meta`
  - `Assets/Tests/Editor/Core/CreditsScrollMathTests.cs`
  - `Assets/Tests/Editor/Core/CreditsScrollMathTests.cs.meta`
  - `Assets/Scripts/Core/CreditsController.cs`

---

## Task 2: Battle scene Canvas → standard scaler + anchor pass (Unity Editor — user)

**Files:** Modify `Assets/Scenes/Battle.unity`

This canvas is named in the AC ("Both the Battle and Platformer scene Canvases are verified") and is the primary cause of the bug.

- [ ] **Step 1: Convert the CanvasScaler**

> **Unity Editor task (user):** Open `Assets/Scenes/Battle.unity`. Select the `Canvas` GameObject (the BattleHUD root). On its **Canvas Scaler** component set:
> - UI Scale Mode → **Scale With Screen Size**
> - Reference Resolution → **X 1920, Y 1080**
> - Screen Match Mode → **Match Width Or Height**
> - Match → **0.5**

- [ ] **Step 2: Set the Game view to the reference resolution**

> **Unity Editor task (user):** In the Game view aspect dropdown, select (or add via "+") a **Fixed Resolution 1920×1080**. This is your design reference — lay elements out to look correct here first.

- [ ] **Step 3: Anchor/pivot pass on every child**

> **Unity Editor task (user):** The children were laid out in 800×600 pixel space and will have shifted. For each direct child of `Canvas`, set anchors+pivot per the table below, then reposition to look correct at 1920×1080. Tip: open the **Anchor Presets** popup (top-left of the RectTransform inspector) and **hold Alt+Shift while clicking a preset** to snap anchors, pivot, and position to that region in one click; then nudge offsets.

| Element role (typical Battle HUD) | Anchor preset | Pivot |
|---|---|---|
| Player stats cluster (HP/MP, top-left) | Top-Left (0,1) | (0,1) |
| Enemy info / turn indicator (top-right) | Top-Right (1,1) | (1,1) |
| Action / spell menu bar (bottom) | Bottom-Stretch (0,0)–(1,0) | (0.5,0) |
| Centered popups / message banners | Middle-Center (0.5,0.5) | (0.5,0.5) |
| Full-screen background / dim overlay | Stretch (0,0)–(1,1) | (0.5,0.5) |

Apply the closest role to each element. Avoid leaving any gameplay element on a center anchor with large absolute offsets — that is exactly what broke off-1080p.

- [ ] **Step 4: Verify across resolutions in the Editor**

> **Unity Editor task (user):** Enter Play mode in `Battle.unity`. Cycle the Game view through **1280×720, 1920×1080, 2560×1440**. Confirm HP/MP bars, action menu, turn indicator, and any message panels stay anchored to their corners/edges with no clipping or overlap. Trigger at least one attack so **floating damage numbers / status messages** appear, and confirm they spawn at the correct on-screen position at each resolution. (If floating numbers are mispositioned, STOP and note it — see Task 5 Step 3; that indicates raw-screen-coord spawning that needs a separate fix.)

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message: `fix(DEV-135): scale Battle HUD canvas with screen size + anchor pass`
  - `Assets/Scenes/Battle.unity`

---

## Task 3: DialogueCanvas prefab → standard scaler + anchor pass (Unity Editor — user)

**Files:** Modify `Assets/Prefabs/Dialogue/DialogueCanvas.prefab`

Dialogue appears during gameplay (platformer), so although not named in the AC it is a user-facing broken canvas.

- [ ] **Step 1: Convert the CanvasScaler in Prefab Mode**

> **Unity Editor task (user):** Open `Assets/Prefabs/Dialogue/DialogueCanvas.prefab` (double-click → Prefab Mode). Select the `DialogueCanvas` root. On its **Canvas Scaler** set the standard values: Scale With Screen Size · 1920×1080 · Match Width Or Height · 0.5.

- [ ] **Step 2: Anchor/pivot pass**

> **Unity Editor task (user):** With Game/Scene view referencing 1920×1080, set anchors per role:
> - Dialogue panel (typically bottom of screen) → **Bottom-Stretch (0,0)–(1,0)**, pivot (0.5,0), with a fixed height and left/right margins as offsets.
> - Speaker name plate → anchored to the panel's top-left/top edge.
> - Continue/advance arrow → anchored to the panel's bottom-right corner.
> - Any full-screen letterbox/dim → **Stretch (0,0)–(1,1)**.
> Reposition so the panel reads correctly at 1920×1080.

- [ ] **Step 3: Verify across resolutions**

> **Unity Editor task (user):** Open the Platformer scene (or any scene that shows dialogue), enter Play mode, trigger a dialogue, and cycle 1280×720 / 1920×1080 / 2560×1440. Confirm the panel stays bottom-anchored full-width, text doesn't clip, and the name plate + arrow stay attached to their corners.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message: `fix(DEV-135): scale DialogueCanvas with screen size + anchor pass`
  - `Assets/Prefabs/Dialogue/DialogueCanvas.prefab`

---

## Task 4: End_Credits Canvas → standard scaler + anchor pass (Unity Editor — user; depends on Task 1)

**Files:** Modify `Assets/Scenes/End_Credits.unity`

> Depends on Task 1: the `CreditsController` code fix must be checked in first, otherwise switching this canvas to Scale With Screen Size breaks the scroll start/end math.

- [ ] **Step 1: Convert the CanvasScaler**

> **Unity Editor task (user):** Open `Assets/Scenes/End_Credits.unity`. Select the credits Canvas. On its **Canvas Scaler** set the standard values: Scale With Screen Size · 1920×1080 · Match Width Or Height · 0.5.

- [ ] **Step 2: Anchor the content root for centered scrolling**

> **Unity Editor task (user):** Select the `_contentRoot` RectTransform (the object `CreditsController._contentRoot` points to — credit entries are spawned as its children). Set its anchor to **horizontally centered** (anchor X 0.5) so credit text stays centered at any width. Leave it free to move vertically (the controller drives `anchoredPosition.y`). Set pivot to (0.5, 0). Confirm any background image behind the credits uses **Stretch (0,0)–(1,1)**.

- [ ] **Step 3: Play-test the scroll at each resolution**

> **Unity Editor task (user):** Enter Play mode in `End_Credits.unity` at **1280×720, 1920×1080, 2560×1440**. At each resolution confirm: credits **begin fully off the bottom** (no text visible at frame 0), scroll smoothly upward, and the final line **fully clears the top** before the scene loads MainMenu. The hold-to-skip and tap-to-fast-forward still work. (This validates the Task 1 canvas-unit fix end-to-end.)

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message: `fix(DEV-135): scale End_Credits canvas with screen size + anchor content`
  - `Assets/Scenes/End_Credits.unity`

---

## Task 5: Build verification matrix + regression check (user)

**Files:** none (verification only)

- [ ] **Step 1: Make a build**

> **Unity Editor task (user):** Build the game (File → Build Settings / active Build Profile → Build) for your platform (Windows or macOS).

- [ ] **Step 2: Run the resolution matrix on the build**

> **Unity Editor task (user):** Launch the build and exercise each affected scene at **1280×720, 1920×1080, 2560×1440** — windowed *and* fullscreen where possible (e.g. launch args `-screen-width 1280 -screen-height 720 -screen-fullscreen 0`). Verify, per scene:
> - **Battle** — HUD, action menu, health bars, floating numbers/status messages intact.
> - **Platformer** — HUDCanvas + DialogueCanvas intact (regression check on the already-correct HUD and the newly-fixed dialogue).
> - **End_Credits** — credits start offscreen, scroll, fully clear the top.
> - **Spot-check** MainMenu, Disclaimer, Cutscene for regressions (no changes expected).
>
> **AC met when** no clipping/mispositioning/wrong-scale occurs in any affected scene across all three resolutions.

- [ ] **Step 3: Resolve the floating-numbers question (only if Task 2 Step 4 flagged it)**

If Battle floating damage numbers / status messages were mispositioned at non-1080p, they are spawning at raw screen coordinates instead of canvas-aware coordinates. File this as a **separate follow-up bug** (it is a runtime-positioning issue, not a CanvasScaler issue) rather than expanding this ticket — note it on DEV-135 with the spawning script/line. If they were correct, record "runtime battle UI verified" and move on.

- [ ] **Step 4: Record results on the ticket**

> **Unity Editor task (user):** Add a comment to DEV-135 listing the three resolutions tested, pass/fail per scene, and platform. (Claude can post this comment via the Atlassian MCP on request.)

---

## Task 6: Document the canvas standard (docs)

**Files:** Create `docs/ui-canvas-scaling-standard.md`

Satisfies the AC: "Document the chosen approach … with an appropriate match (width/height) value documented."

- [ ] **Step 1: Write the standard doc**

Create `docs/ui-canvas-scaling-standard.md`:

```markdown
# UI Canvas Scaling Standard

**Status:** Adopted 2026-06-25 (DEV-135). Applies to all Screen Space UI canvases.

## The standard

Every Screen Space (Overlay or Camera) UI Canvas uses a `CanvasScaler` configured as:

| Field | Value |
|-------|-------|
| UI Scale Mode | **Scale With Screen Size** |
| Reference Resolution | **1920 × 1080** |
| Screen Match Mode | **Match Width Or Height** |
| Match | **0.5** |

The game targets 16:9. Because the reference is 16:9, all match modes are equivalent on
a 16:9 display; the `Match = 0.5` value only affects non-16:9 aspect ratios, where it
blends width- and height-based fitting for graceful degradation. 0.5 is the project-wide
value — keep it for consistency.

## Anchoring

Lay elements out against the 1920×1080 reference, then anchor each to the screen region it
belongs to (corner, edge, center, or full-stretch) — never leave gameplay UI on a center
anchor with large absolute pixel offsets. That is what breaks at other resolutions.

## Exception: CursorManager

`CursorManager.prefab` (`StateBasedCursorUI`) intentionally stays on **Constant Pixel Size**.
It assigns `_cursorRect.position = screenPos` in raw screen pixels, which maps 1:1 to canvas
units only under Constant Pixel Size. A hardware-style cursor should keep a fixed on-screen
pixel size at every resolution, so do **not** convert it. (Its 800×600 reference field is
dead/unused in that mode.)

## Verification

Verify UI at 1280×720, 1920×1080, and 2560×1440 (windowed + fullscreen) in a build before
shipping UI changes.

## Forward compatibility

This setup is compatible with a future in-game resolution dropdown (DEV-57): Scale With
Screen Size adapts to whatever resolution the player selects with no further canvas changes.
```

- [ ] **Step 2: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message: `docs(DEV-135): document UI canvas scaling standard`
  - `docs/ui-canvas-scaling-standard.md`

---

## Done When

- [ ] Task 1 EditMode tests green; `CreditsController` uses canvas-unit scroll math (UVCS checked in).
- [ ] Battle, Dialogue, and End_Credits canvases all on the standard scaler with a real anchor pass (UVCS checked in).
- [ ] CursorManager and the already-correct canvases left untouched and verified for regressions.
- [ ] Build verified at 1280×720 / 1920×1080 / 2560×1440 across all affected scenes; results recorded on DEV-135.
- [ ] `docs/ui-canvas-scaling-standard.md` written and checked in.
```