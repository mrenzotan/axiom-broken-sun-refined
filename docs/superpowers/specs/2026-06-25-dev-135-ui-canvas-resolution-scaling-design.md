# DEV-135 — UI/Canvas Resolution Scaling Design

**Ticket:** [DEV-135](https://axiombrokensunrefined.atlassian.net/browse/DEV-135) — *UI/Canvas breaks at non-1920×1080 resolutions in builds*
**Date:** 2026-06-25
**Labels:** bug, phase-7-polish, unity
**Type:** Bug fix (primarily Unity Editor, one small C# change)

> Note: The ticket's "Related: DEV-57 (Settings Menu resolution dropdown)" coordination clause is **out of scope** — no Settings Menu exists yet. This design only needs to not *preclude* a future resolution dropdown, which Scale With Screen Size inherently satisfies.

---

## 1. Problem & Root Cause

The build lays out correctly at 1920×1080 but breaks (mispositioned / clipped / wrongly scaled UI) at any other resolution.

Root cause is **not** a project-wide failure. An audit of every Canvas + CanvasScaler in the project found that the **majority of UI is already configured correctly**, and only a small set of canvases are stuck on the legacy **Constant Pixel Size @ 800×600** default, which ignores resolution entirely.

### Audit results

**Already correct** — `Scale With Screen Size`, ref `1920×1080`, `Match = 0.5` (no change needed, verify only):
- `HUDCanvas.prefab` (platformer HUD)
- `MainMenuCanvas.prefab`
- `MainMenu.unity`
- `Disclaimer.unity`
- `Cutscene.unity`

**Broken** — `Constant Pixel Size @ 800×600` (the cause of the bug):
- Battle scene `Canvas` (BattleHUD) — `Assets/Scenes/Battle.unity`
- `DialogueCanvas.prefab` — `Assets/Prefabs/Dialogue/DialogueCanvas.prefab`
- `End_Credits` canvas — `Assets/Scenes/End_Credits.unity`

**Resolution-agnostic by design — intentionally left alone:**
- `CursorManager.prefab` — `StateBasedCursorUI` assigns `_cursorRect.position = screenPos + offset` using raw screen pixels (Input System / `Input.mousePosition`). With Constant Pixel Size, canvas units equal screen pixels, so this maps 1:1 at any resolution. A cursor *should* stay a fixed on-screen pixel size; switching it to Scale With Screen Size would make the cursor grow/shrink with resolution. The 800×600 reference field is dead (unused in Constant Pixel Size mode).

---

## 2. Chosen Approach

**Conform the broken canvases to the standard the rest of the project already uses** — do not introduce a new pattern.

**Project UI Canvas standard:**
- `CanvasScaler.UI Scale Mode` = **Scale With Screen Size**
- `Reference Resolution` = **1920 × 1080**
- `Screen Match Mode` = **Match Width Or Height**
- `Match` = **0.5**

**Rationale for the match value:** the game targets 16:9 and the reference is 16:9, so on any 16:9 display all match modes produce identical results. The match value only affects non-16:9 aspect ratios (e.g. 16:10 laptops, ultrawide). `0.5` (an even blend of width/height fitting) is the value every already-correct canvas in the project uses, so we keep it for consistency (CLAUDE.md Rule 11).

**Why scaling, not locking:** the ticket offers two documented options — lock the build to 1920×1080, or support a resolution range via scaling. Locking is rejected because the explicit requirement is that the game run on devices that **do not support** 1920×1080; locking would letterbox or fail on those displays. Scale With Screen Size is also the low-effort, standard Unity practice and is forward-compatible with a future resolution dropdown (DEV-57).

---

## 3. Scope of Changes

| # | Canvas | Change | Type |
|---|--------|--------|------|
| 1 | Battle scene `Canvas` (BattleHUD) | Scaler → standard + **full anchor/pivot pass** on children | Editor |
| 2 | `DialogueCanvas.prefab` | Scaler → standard + **full anchor/pivot pass** | Editor |
| 3 | `End_Credits` canvas | Scaler → standard + **fix `CreditsController`** + anchor pass | Editor + C# (TDD) |
| 4 | `CursorManager.prefab` | **No change** (documented exception) | — |
| 5 | Already-correct canvases | **No change** — verify in build only | Verify |
| 6 | UI standard doc | New short reference note in `docs/` | Docs |

### 3.1 Anchor / pivot pass (the real work — items 1–3)

The children of the broken canvases were positioned in an 800×600 *pixel* space. Merely flipping the scaler reinterprets those coordinates in a 1920×1080 reference space, shifting everything. So for each converted canvas we set **intentional anchors and pivots per element** instead of absolute offsets:

- **Corner elements** (HP/MP bars, action menu, turn indicator) → anchored to the screen corner they occupy, pivot to match.
- **Edge bars / banners** → edge-anchored (e.g. top-stretch, bottom-stretch).
- **Modal / dialogue panels** → center-anchored (0.5, 0.5) with centered pivot.
- **Full-screen backgrounds / dim overlays** → full stretch (anchors 0,0 → 1,1).

This is what makes the layout genuinely resolution-independent and satisfies the AC's anchor requirement.

### 3.2 End_Credits C# coupling (item 3)

`CreditsController.cs:173-184` drives the scroll using:
```csharp
pos.y = -Screen.height;                          // start offscreen
...
float target = _contentRoot.rect.height + Screen.height;  // scroll end
```
This works **only** under Constant Pixel Size, where canvas units equal screen pixels. Under Scale With Screen Size, `anchoredPosition` is in 1080-reference units while `Screen.height` is actual device pixels (e.g. 720) — the two no longer share a unit, so the start offset and scroll-end target become wrong.

**Fix:** replace `Screen.height` in the scroll math with the canvas's height **in canvas-reference units**, e.g. the height of the canvas `RectTransform` (or the content's parent viewport rect) obtained via `RectTransform.rect.height`. This keeps both terms in canvas units. Implemented test-first (see §5).

### 3.3 Runtime-positioned UI (verification, fix only if broken)

The Battle scene spawns floating damage numbers and status messages at runtime under the Battle canvas. After converting that canvas, these must be confirmed to use **canvas-aware positioning** (world-to-canvas via `RectTransformUtility`, not raw screen coords). The plan includes a verification task; a fix task is added **only if** they are found to break.

### 3.4 CursorManager (no change)

Left as Constant Pixel Size. Recorded in the UI standard doc as the deliberate exception so a future developer doesn't "fix" it into inconsistency.

---

## 4. Architecture / Boundaries

- **No new scripts, base classes, or abstractions** (CLAUDE.md Rule 4). This is configuration alignment plus one localized method edit.
- The only code touched is `CreditsController` scroll math — a self-contained change with a clear interface (input: canvas/content rect heights; output: scroll start/end offsets in canvas units).
- All Canvas/CanvasScaler edits are made **in the Unity Editor** (Inspector), per project workflow — not by hand-editing scene/prefab YAML.

---

## 5. Testing

- **CreditsController (EditMode, TDD):** Extract or expose the scroll start/end computation so it can be unit-tested without a live screen. Tests assert that start offset and scroll-end target are computed from canvas-reference height (constant across resolutions) rather than `Screen.height` — encoding *why* (CLAUDE.md Rule 9): the credits must start fully offscreen and fully clear the screen regardless of build resolution. A test that still passes with the old `Screen.height` coupling is insufficient.
- **Manual Play/Build verification matrix:** Build and verify each affected scene at **1280×720, 1920×1080, 2560×1440**, in both windowed and fullscreen:
  - Battle scene — HUD, action menu, health bars, floating numbers/status messages intact.
  - Platformer scene — HUDCanvas + DialogueCanvas intact (regression check on Dialogue).
  - End_Credits — credits begin fully offscreen, scroll smoothly, fully clear the top.
  - Spot-check already-correct scenes (MainMenu, Disclaimer, Cutscene) for regressions.

**Acceptance:** AC met when no clipping/mispositioning/wrong-scale occurs in any affected scene across all three resolutions.

---

## 6. Deliverables

1. Battle scene Canvas converted + anchored.
2. DialogueCanvas.prefab converted + anchored.
3. End_Credits canvas converted + anchored; `CreditsController` scroll math fixed (TDD).
4. EditMode tests for CreditsController scroll computation.
5. `docs/` UI canvas standard note (SWSS / 1920×1080 / 0.5 + CursorManager exception).
6. Verification across 1280×720 / 1920×1080 / 2560×1440 recorded on the ticket.
7. UVCS check-ins per `docs/VERSION_CONTROL.md` (`fix(DEV-135): …`).

## 7. Out of Scope

- DEV-57 Settings Menu / resolution dropdown (does not exist yet).
- CursorManager behavior change.
- Any canvas already on the correct standard (verify only).
- Aspect ratios other than 16:9 beyond graceful-degradation behavior already given by `Match = 0.5`.
