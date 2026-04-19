# DEV-77 — Unified Post-Battle Panel Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Victory → Level-Up handoff feel like a single continuous panel by sharing layout + visual language and fading between them with a short CanvasGroup crossfade on `PostBattleFlowController`.

**Architecture:** Keep `VictoryScreenUI` and `LevelUpPromptUI` fully independent (no shared script, no `UIPanelStyle` ScriptableObject — CLAUDE.md §4 forbids premature abstraction). Unify their RectTransforms and visuals at authoring time. Add a `CanvasGroup` to each panel's root; `PostBattleFlowController` owns a single `IEnumerator` fade coroutine that drives alpha + interactability. The UIs expose `Hide()` / `Show()` as SetActive semantics only — the controller orchestrates the alpha. Both UIs are modified to stop auto-deactivating themselves mid-dismiss so the controller's fade is actually visible.

**Tech Stack:** Unity 6 LTS, URP 2D, Unity UI Canvas, TextMeshPro, `CanvasGroup`, plain `IEnumerator` coroutine (no DOTween).

**Jira:** DEV-77 — Phase 4 (Scene Bridge). Follow-up to DEV-36.

---

## File Structure

| File | Role | Action |
|---|---|---|
| `Assets/Scenes/Battle.unity` | Scene containing both panels + `PostBattleFlowController` | Modify (Inspector-only changes to panels, add `CanvasGroup` components, wire serialized refs) |
| `Assets/Scripts/Battle/UI/VictoryScreenUI.cs` | Victory panel view | Modify — defer panel hide; expose public `Hide()` / `Show()` |
| `Assets/Scripts/Battle/UI/LevelUpPromptUI.cs` | Level-up prompt view + queue driver | Modify — defer panel hide; expose public `Hide()` |
| `Assets/Scripts/Battle/PostBattleFlowController.cs` | Orchestrator | Modify — add two `CanvasGroup` serialized fields, fade duration field, fade coroutine, updated dismiss handler |

No new files, no new folders, no new asmdefs. No tests added — this is a visual/authoring + thin coroutine ticket (≤40 lines of logic) verified by the manual QA matrix in Task 8.

---

## Task 1: Mirror the LevelUpPromptPanel hierarchy + add CanvasGroups to both panel roots

**Files:**
- Modify: `Assets/Scenes/Battle.unity` (Inspector-only)

**Why this comes first:** `VictoryScreenPanel` already has a two-child structure (`DimOverlay` + `PanelFrame`). `LevelUpPromptPanel` currently has all of its content as direct children of the root and no dim overlay. We reshape Level-Up to match so that (a) Task 2's rect match and Task 3's visual unification become one-to-one comparisons, (b) the crossfade dims both states equally, and (c) the CanvasGroup on each panel root fades the dim + frame together.

### Part A — Restructure `LevelUpPromptPanel` to mirror `VictoryScreenPanel`

**Current scene layout (as of authoring):**

```
Canvas (BattleHUD)
├── LevelUpPromptPanel       ← wired to LevelUpPromptUI._panel
│   ├── Title
│   ├── Stats
│   ├── New Spells
│   └── ConfirmButton
├── LevelUpPromptRoot        ← sibling; hosts the LevelUpPromptUI script + field wiring. LEAVE AS-IS.
├── VictoryScreenPanel
│   ├── DimOverlay
│   └── PanelFrame           ← the visible card
└── DefeatScreenPanel
```

**Target end state for `LevelUpPromptPanel`:**

```
LevelUpPromptPanel            ← root (will get CanvasGroup in Part B)
├── DimOverlay                ← stretch-stretch, same sprite/color as Victory's
└── PanelFrame                ← middle-center, visible card
    ├── Title
    ├── Stats
    ├── New Spells
    └── ConfirmButton
```

**`LevelUpPromptRoot` is NOT touched** — it's a sibling GameObject that hosts the `LevelUpPromptUI` MonoBehaviour and owns the `Panel → LevelUpPromptPanel` wiring plus the `Title Text / Stats Text / Spells Text / Confirm Button` references. Leave it where it is and don't rename it. As long as the reparenting in Step 4 below doesn't break the serialized references (Unity preserves them across parent changes), no re-wiring is needed.

> **Unity Editor task (user):**
> 1. Open `Assets/Scenes/Battle.unity`.
> 2. **Duplicate `DimOverlay`:** Select `VictoryScreenPanel/DimOverlay`, press Ctrl/Cmd+D. Drag the duplicate to become a child of `LevelUpPromptPanel`. If Unity suffixed the name (e.g., `DimOverlay (1)`), rename it back to `DimOverlay`.
> 3. **Create `PanelFrame`:** Right-click `LevelUpPromptPanel` → UI → Panel (or Create Empty + Add Component → RectTransform). Name it `PanelFrame`. If Unity added a default `Image` component with a background sprite, leave it — you'll overwrite it to match Victory's in Task 3. (If you used Create Empty, you'll add the `Image` component in Task 3.)
> 4. **Reparent content:** Drag `Title`, `Stats`, `New Spells`, and `ConfirmButton` (in that order) under `LevelUpPromptPanel/PanelFrame`. Unity 6 preserves world positions on reparent by default — if you see a jump, undo and reparent holding Alt to preserve local transform.
> 5. **Verify references on `LevelUpPromptRoot`:** Select `LevelUpPromptRoot` in the hierarchy (the sibling GameObject that hosts the `LevelUpPromptUI` script). In the Inspector, confirm every field still resolves:
>    - `Panel` → `LevelUpPromptPanel` (unchanged — we didn't move or rename the panel itself)
>    - `Title Text` → `Title (Text Mesh Pro UGUI)` (now under `LevelUpPromptPanel/PanelFrame/Title`)
>    - `Stats Text` → `Stats (Text Mesh Pro UGUI)` (now under `.../PanelFrame/Stats`)
>    - `Spells Text` → `New Spells (Text Mesh Pro UGUI)` (now under `.../PanelFrame/New Spells`)
>    - `Confirm Button` → `ConfirmButton (Button)` (now under `.../PanelFrame/ConfirmButton`)
>    Reparenting preserves serialized references by GUID, so these should resolve automatically. If any shows "Missing", re-drag from the new PanelFrame subtree.
> 6. Save the scene (Ctrl/Cmd+S).

### Part B — Add CanvasGroup to both panel roots

> **Unity Editor task (user):**
> 1. Select `VictoryScreenPanel` (root). Add Component → `CanvasGroup`. Set: `Alpha = 1`, `Interactable = true`, `Blocks Raycasts = true`, `Ignore Parent Groups = false`.
> 2. Select `LevelUpPromptPanel` (root). Add Component → `CanvasGroup`. Same settings.
> 3. Save the scene.

Alpha is set to 1 at authoring time so the scene-serialized state is "visible". The controller will flip alpha to 0 immediately before showing, so there is no first-frame flash. Because the CanvasGroup lives on the root, it fades both `DimOverlay` and `PanelFrame` as one — matching the "continuous panel" feel DEV-77 is after.

---

## Task 2: Unify RectTransforms so panels occupy the same rect

**Files:**
- Modify: `Assets/Scenes/Battle.unity` (Inspector-only)

Now that both panels share the same two-child structure (`DimOverlay` + `PanelFrame`), the rect match is a direct comparison at each level.

> **Unity Editor task (user):**
>
> **Frame ↔ Frame (the visible card — this is what the player sees move):**
> 1. Select `VictoryScreenPanel/PanelFrame`'s RectTransform. Note these five fields: `Anchor Min`, `Anchor Max`, `Pivot`, `Anchored Position` (Pos X / Pos Y), `Size Delta` (Width / Height). The anchor preset should be **middle-center**.
> 2. Select `LevelUpPromptPanel/PanelFrame`'s RectTransform. First set the anchor preset to **middle-center** (click the anchor icon in the top-left of the RectTransform; pick middle-center from the grid). Then copy each of the five fields to exactly match Victory's PanelFrame. **Do not literally copy values if the preset differs** — presets change what `Size Delta` / `Anchored Position` mean. Reset the preset first, then copy values.
>
> **Dim ↔ Dim (the full-screen overlay — both should be stretch-stretch and identical):**
> 3. Select `VictoryScreenPanel/DimOverlay`'s RectTransform. Confirm it is **stretch-stretch** (anchors at (0,0)–(1,1)) with `Left/Right/Top/Bottom` at 0 so it covers the whole canvas.
> 4. Select `LevelUpPromptPanel/DimOverlay`'s RectTransform (the one you duplicated in Task 1). Confirm it has the same stretch-stretch anchors and 0 offsets. It should already match since it was duplicated — this step is a sanity check.
>
> **Verify in the scene:**
> 5. Temporarily set both `VictoryScreenPanel` and `LevelUpPromptPanel` active simultaneously (tick both top-level GameObjects in the hierarchy). Confirm their `PanelFrame`s overlap perfectly and both `DimOverlay`s cover the full canvas. Toggle the root panels back off when done.
> 6. Save the scene.

**Acceptance for this step:** With both panels active simultaneously, the two `PanelFrame`s occupy the exact same rect. No visible shift when swapping visibility.

---

## Task 3: Unify background, fonts, and button style between panels

**Files:**
- Modify: `Assets/Scenes/Battle.unity` (Inspector-only)

> **Unity Editor task (user):**
> 1. Pick whichever panel currently has the preferred visual treatment as the **reference**. Apply its values to the other panel for each of the following (the DEV-77 AC requires identical values):
>    - **DimOverlay `Image`:** `sprite`, `color` (including alpha — typically a low-alpha black), `type`. The Level-Up `DimOverlay` you duplicated in Task 1 should already match Victory's — verify and reapply if it drifted.
>    - **PanelFrame background:** `Image.sprite`, `Image.color`, `Image.type` on each `PanelFrame`. If you created `LevelUpPromptPanel/PanelFrame` as a Create Empty in Task 1 (no Image component yet), Add Component → `Image` now and copy Victory's values.
>    - **Title text:** `TextMeshProUGUI` font asset, font size, font style, vertex color, alignment. (`VictoryScreenUI._titleText` vs `LevelUpPromptUI._titleText`.)
>    - **Body text:** Same set for the body `TextMeshProUGUI`s — `_xpText` + `_lootText` + `_xpProgressText` on Victory; `_statsText` + `_spellsText` on Level-Up.
>    - **Confirm button:** `Button.transition`, target graphic sprite, colors block (normal/highlighted/pressed/selected/disabled), and button label font/size/color. (`VictoryScreenUI._confirmButton` vs `LevelUpPromptUI._confirmButton`.)
> 2. Save the scene.

No `UIPanelStyle` ScriptableObject. No shared prefab. Values are authored directly on each panel — duplication is acceptable per CLAUDE.md §4. (If visual drift becomes a real maintenance pain after DEV-77 ships and a third post-battle panel type appears, the right escape hatch is a Prefab Variant — not this ticket's problem.)

---

## Task 4: Defer Victory panel hide so the controller can fade it out

**Files:**
- Modify: `Assets/Scripts/Battle/UI/VictoryScreenUI.cs`

- [ ] **Step 1: Remove the auto-hide from `OnConfirmClicked` and expose public `Hide()` / `Show()` methods**

Replace the existing `OnConfirmClicked`, `ShowPanel`, and `HidePanel` members with the following. The rest of the class (all fields, `Awake`, `OnEnable`, `OnDisable`, `Show`, `RenderXpProgress`, `ResolveDisplayName`, `IsShowing`) is unchanged.

```csharp
/// <summary>
/// Deactivates the panel GameObject. The orchestrator
/// (<see cref="Axiom.Battle.PostBattleFlowController"/>) calls this after it
/// finishes fading the CanvasGroup out, so the fade stays visible.
/// </summary>
public void Hide()
{
    if (_panel != null) _panel.SetActive(false);
}

private void OnConfirmClicked()
{
    // Do NOT deactivate the panel here — the controller runs a fade on the
    // CanvasGroup first, then calls Hide() when the fade finishes. Hiding
    // here would make the fade invisible.
    OnDismissed?.Invoke();
}

private void ShowPanel()
{
    if (_panel != null) _panel.SetActive(true);
}
```

Leave `Awake`'s existing `HidePanel()` call — rename the target to `Hide()` so the class still has one source of truth for "deactivate the panel GameObject":

```csharp
private void Awake()
{
    Hide();
}
```

- [ ] **Step 2: Verify compile**

In Unity, let the editor recompile. Expected: no errors.

---

## Task 5: Defer Level-Up panel hide so the controller can fade it out

**Files:**
- Modify: `Assets/Scripts/Battle/UI/LevelUpPromptUI.cs`

- [ ] **Step 1: Replace `OnConfirmClicked`, `HandleQueueDrained`, and `HidePanel` with deferred variants**

All other members stay as-is. Replace the three methods below with these exact definitions, and add a new public `Hide()` method:

```csharp
/// <summary>
/// Deactivates the panel GameObject. The orchestrator
/// (<see cref="Axiom.Battle.PostBattleFlowController"/>) calls this after it
/// finishes fading the CanvasGroup out, so the fade stays visible.
/// </summary>
public void Hide()
{
    if (_panel != null) _panel.SetActive(false);
}

private void OnConfirmClicked()
{
    _controller.Dismiss();
    if (_controller.IsPending)
        RenderCurrent();
    // else: do NOT hide here; HandleQueueDrained will fire and the
    // orchestrator will fade out, then call Hide().
}

private void HandleQueueDrained()
{
    // Do NOT deactivate the panel here — the controller fades the CanvasGroup
    // first, then calls Hide() when the fade finishes.
    OnDismissed?.Invoke();
}

private void ShowPanel()
{
    if (_panel != null) _panel.SetActive(true);
}
```

- [ ] **Step 2: Update `OnEnable` to use `Hide()` instead of `HidePanel()`**

```csharp
private void OnEnable()
{
    Hide();

    GameManager manager = GameManager.Instance;
    if (manager == null) return;

    _progression  = manager.ProgressionService;
    _spellUnlocks = manager.SpellUnlockService;
    _lastSeenUnlockCount = _spellUnlocks?.UnlockedSpellNames.Count ?? 0;

    if (_progression != null)
        _progression.OnLevelUp += HandleLevelUp;

    _controller.OnDismissed += HandleQueueDrained;

    if (_confirmButton != null)
        _confirmButton.onClick.AddListener(OnConfirmClicked);
}
```

Delete the old private `HidePanel()` method entirely (now superseded by public `Hide()`).

- [ ] **Step 3: Verify compile**

In Unity, let the editor recompile. Expected: no errors. `IsShowing` still works because it reads `_panel.activeSelf`, which `Hide()` controls.

---

## Task 6: Add the CanvasGroup fade coroutine to `PostBattleFlowController`

**Files:**
- Modify: `Assets/Scripts/Battle/PostBattleFlowController.cs`

- [ ] **Step 1: Add new `using` and serialized fields**

At the top of the file, ensure `using System.Collections;` is present (add it if missing — the existing file does not use coroutines yet).

```csharp
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Axiom.Battle.UI;
using Axiom.Core;
using Axiom.Data;
```

Add three new serialized fields inside the class, directly under the existing `_levelUpPromptUI` field:

```csharp
[Header("Fade (DEV-77)")]
[SerializeField]
[Tooltip("CanvasGroup on the VictoryScreenPanel root. Drives the crossfade to Level-Up.")]
private CanvasGroup _victoryCanvasGroup;

[SerializeField]
[Tooltip("CanvasGroup on the LevelUpPromptPanel root. Drives the crossfade from Victory.")]
private CanvasGroup _levelUpCanvasGroup;

[SerializeField, Range(0f, 0.5f)]
[Tooltip("Fade duration for each leg of the crossfade, seconds. DEV-77 spec: ≤0.2s.")]
private float _fadeDuration = 0.2f;
```

- [ ] **Step 2: Replace `HandleVictoryScreenDismissed` with a fade-driven version**

Replace the existing `HandleVictoryScreenDismissed` method body with the version below, and add the three new helper methods (`CrossfadeVictoryToLevelUp`, `FadeCanvasGroup`, `SetCanvasGroupAlpha`) alongside it in the same class:

```csharp
private void HandleVictoryScreenDismissed()
{
    if (_victoryScreenUI != null)
        _victoryScreenUI.OnDismissed -= HandleVictoryScreenDismissed;

    StartCoroutine(CrossfadeVictoryToLevelUp());
}

private IEnumerator CrossfadeVictoryToLevelUp()
{
    // Leg 1: fade Victory out (if the panel is actually showing and we have a CanvasGroup).
    if (_victoryScreenUI != null && _victoryScreenUI.IsShowing && _victoryCanvasGroup != null)
        yield return FadeCanvasGroup(_victoryCanvasGroup, 1f, 0f, _fadeDuration);

    if (_victoryScreenUI != null)
        _victoryScreenUI.Hide();

    // No level-up UI wired → go straight to transition.
    if (_levelUpPromptUI == null)
    {
        HandleLevelUpPromptDismissed();
        yield break;
    }

    // Pre-set Level-Up alpha to 0 BEFORE ShowIfPending so it doesn't flash at
    // full alpha for one frame if the panel is about to activate.
    if (_levelUpCanvasGroup != null)
        SetCanvasGroupAlpha(_levelUpCanvasGroup, 0f, interactable: false);

    _levelUpPromptUI.OnDismissed += HandleLevelUpPromptDismissed;
    _levelUpPromptUI.ShowIfPending();

    // Empty-queue path: ShowIfPending fired OnDismissed synchronously without
    // activating the panel. HandleLevelUpPromptDismissed has already run. Skip
    // the fade-in on an invisible panel — scene transition is already queued.
    if (!_levelUpPromptUI.IsShowing)
        yield break;

    // Leg 2: fade Level-Up in.
    if (_levelUpCanvasGroup != null)
        yield return FadeCanvasGroup(_levelUpCanvasGroup, 0f, 1f, _fadeDuration);
}

private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
{
    if (group == null) yield break;

    // Disable input for the duration of the fade so mid-fade clicks can't
    // double-dismiss the panel.
    bool targetInteractable = to >= 1f;
    group.interactable = false;
    group.blocksRaycasts = targetInteractable; // keep raycasts blocked while fading in; release while fading out
    group.alpha = from;

    if (duration <= 0f)
    {
        SetCanvasGroupAlpha(group, to, interactable: targetInteractable);
        yield break;
    }

    float elapsed = 0f;
    while (elapsed < duration)
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        group.alpha = Mathf.Lerp(from, to, t);
        yield return null;
    }

    SetCanvasGroupAlpha(group, to, interactable: targetInteractable);
}

private static void SetCanvasGroupAlpha(CanvasGroup group, float alpha, bool interactable)
{
    if (group == null) return;
    group.alpha = alpha;
    group.interactable = interactable;
    group.blocksRaycasts = interactable;
}
```

- [ ] **Step 3: Pre-set Victory alpha to 1 before `Show()` in `BeginVictoryFlow`**

In `BeginVictoryFlow`, find the existing two-line block that subscribes `HandleVictoryScreenDismissed` and then calls `_victoryScreenUI.Show(result, xpProgress)`. Replace it with the three-statement block below so the CanvasGroup is known-visible + interactable when Victory appears:

```csharp
_victoryScreenUI.OnDismissed += HandleVictoryScreenDismissed;

if (_victoryCanvasGroup != null)
    SetCanvasGroupAlpha(_victoryCanvasGroup, 1f, interactable: true);

_victoryScreenUI.Show(result, xpProgress);
```

- [ ] **Step 4: Verify compile**

In Unity, let the editor recompile. Expected: no errors. `_victoryCanvasGroup` and `_levelUpCanvasGroup` show up as empty slots in the `PostBattleFlowController` Inspector.

---

## Task 7: Wire the two CanvasGroup references on `PostBattleFlowController`

**Files:**
- Modify: `Assets/Scenes/Battle.unity` (Inspector-only)

> **Unity Editor task (user):**
> 1. Open `Assets/Scenes/Battle.unity`.
> 2. Select the GameObject hosting `PostBattleFlowController`.
> 3. Under the new "Fade (DEV-77)" header in the Inspector:
>    - Drag `VictoryScreenPanel`'s GameObject (the one with the new CanvasGroup) into `Victory Canvas Group`.
>    - Drag `LevelUpPromptPanel`'s GameObject into `Level Up Canvas Group`.
>    - Leave `Fade Duration` at its default `0.2`.
> 4. Confirm both CanvasGroups have Alpha = 1 in the Inspector (scene-serialized state; the controller will flip them to 0 at runtime as needed).
> 5. Save the scene.

---

## Task 8: Manual QA in Play Mode

**Files:** None.

> **Unity Editor task (user):** Enter Play Mode in `Battle.unity` and walk through each of the following scenarios. All must pass.

- [ ] **Scenario A — Victory, no level-up queued:** Defeat an enemy whose XP grant does NOT cross a level threshold. Victory panel fades in (alpha 0 → 1). Click Confirm. Victory fades out (alpha 1 → 0) and the scene transitions to Platformer. **No level-up panel appears**, no visual flash.
- [ ] **Scenario B — Victory → single level-up:** Defeat an enemy whose XP grant crosses exactly one level threshold. Victory fades out, level-up fades in **at the same on-screen rect** (no positional jump), at no point does the level-up panel render at full alpha for a frame. Click Confirm on level-up → level-up fades out → scene transitions.
- [ ] **Scenario C — Victory → multi-level-up queue:** Award enough XP in a single battle to cross two level thresholds (e.g. temporarily bump XP reward on a test enemy). Victory fades out → level-up fades in with entry #1. Click Confirm. **Entry #2 renders immediately** — the panel does NOT fade out between entries. Click Confirm on entry #2 → level-up fades out → scene transitions.
- [ ] **Scenario D — Rapid double-click protection:** During either leg of a fade, attempt to click Confirm repeatedly. The button must be non-interactive for the full fade window (0.2s) — no double-dismiss, no skipped state.
- [ ] **Scenario E — Fade duration feel:** Each fade leg completes in approximately 0.2s of real time (DEV-77 AC ceiling). Adjust `_fadeDuration` on the Inspector if it feels too slow/fast, but keep it ≤0.2.

If any scenario fails, fix the relevant code task and re-run Play Mode QA. Do not check in until all five pass.

---

## Task 9: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-77): unify post-battle panel fade handoff`
  - `Assets/Scripts/Battle/UI/VictoryScreenUI.cs`
  - `Assets/Scripts/Battle/UI/VictoryScreenUI.cs.meta`
  - `Assets/Scripts/Battle/UI/LevelUpPromptUI.cs`
  - `Assets/Scripts/Battle/UI/LevelUpPromptUI.cs.meta`
  - `Assets/Scripts/Battle/PostBattleFlowController.cs`
  - `Assets/Scripts/Battle/PostBattleFlowController.cs.meta`
  - `Assets/Scenes/Battle.unity`
  - `Assets/Scenes/Battle.unity.meta`

(`.meta` files are only staged if UVCS shows them as changed — rename/id changes aside, editing a `.cs` body typically does not touch its `.meta`. Include them if Pending Changes lists them.)

---

## Out of Scope (per DEV-77 AC)

- **No `UIPanelStyle` ScriptableObject.** Duplicating font/color/sprite values across the two panels is the intended outcome — CLAUDE.md §4 forbids premature abstraction.
- **No DOTween or tween libraries.** A plain `IEnumerator` + `Time.unscaledDeltaTime` covers the ≤0.2s fade.
- **No script merge.** `VictoryScreenUI` and `LevelUpPromptUI` remain independent components.
- **No new tests.** The logic changes amount to ≤40 lines of fade-orchestration code on the controller and a small dismiss refactor on each UI; correctness is verified by the Task 8 manual QA matrix in Play Mode.
