# DEV-76 — Victory Screen: XP Progress Toward Next Level

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Jira:** [DEV-76](https://axiombrokensunrefined.atlassian.net/browse/DEV-76)

**Goal:** Extend the existing Victory screen with a post-battle `{currentXp} / {xpForNextLevel}` readout plus a filled progress bar so players can see their progress toward the next level. When at the level cap, replace both with `MAX LEVEL`.

**Architecture:**
- New plain-C# readonly struct `Axiom.Core.XpProgress` carries the values the view needs (current XP into level, threshold, is-at-cap, ratio).
- `ProgressionService.GetXpProgress()` builds the snapshot — UI does no curve math or ratio division (AC: "keep `VictoryScreenUI` a thin view").
- `PostBattleFlowController` already awards XP *before* showing the Victory screen (see `BeginVictoryFlow`); it now reads the snapshot from `GameManager.Instance.ProgressionService` and passes it into `VictoryScreenUI.Show(result, xpProgress)`. UI never touches the singleton or `PlayerState` directly — stays aligned with CLAUDE.md's injection rule and AC ("No new singletons, no direct `PlayerState` mutation from UI").
- View changes in `VictoryScreenUI`: a `GameObject` root holding a `TextMeshProUGUI` label and a bar sub-root. At level cap, the label text becomes `MAX LEVEL` and the bar sub-root is hidden; at all other times, the label shows `{currentXp} / {xpForNextLevel}` and the bar sub-root renders a filled `Image` driven by `Progress01`. The pre-existing `XP +N` line is unchanged so XP continues to show even at cap.
- Sequence order from DEV-36 (Victory → dismiss → LevelUpPrompt) is unchanged.

**Tech Stack:** Unity 6 LTS · URP 2D · C# 9 · Unity UI (filled `Image`) · TextMeshPro · NUnit (Edit Mode tests via Unity Test Framework)

**Out of scope (per ticket Notes):** animated XP-bar fill, SFX, XP multipliers, per-fight breakdown.

---

## File Structure

| File | Responsibility | Status |
| --- | --- | --- |
| `Assets/Scripts/Core/XpProgress.cs` | Readonly struct snapshotting `{CurrentXp, XpForNextLevel, IsAtLevelCap, Progress01}` | Create |
| `Assets/Scripts/Core/ProgressionService.cs` | Add `GetXpProgress()` helper | Modify |
| `Assets/Scripts/Battle/UI/VictoryScreenUI.cs` | Accept `XpProgress` in `Show`, render ratio text + fill amount, toggle bar group at cap | Modify |
| `Assets/Scripts/Battle/PostBattleFlowController.cs` | Build `XpProgress` from `GameManager.Instance.ProgressionService` after `AwardXp`, pass into `VictoryScreenUI.Show` | Modify |
| `Assets/Tests/Editor/Core/ProgressionServiceTests.cs` | Add `GetXpProgress_*` tests | Modify |
| `Assets/Scenes/Battle.unity` | Wire up new UI: ratio text, fill Image, bar-group root under the Victory Panel | User (Unity Editor) |

No new asmdefs. `Axiom.Battle.asmdef` already references `Axiom.Core`; `CoreTests.asmdef` already references `Axiom.Core` + `Axiom.Data`.

---

## Task 1 — Add `XpProgress` readonly struct

**Files:**
- Create: `Assets/Scripts/Core/XpProgress.cs`

- [x] **Step 1.1 — Create the struct**

```csharp
namespace Axiom.Core
{
    /// <summary>
    /// Immutable snapshot of the player's XP progress within their current level.
    /// Produced by <see cref="ProgressionService.GetXpProgress"/> and consumed by
    /// the Victory screen so the UI avoids curve lookups and ratio math.
    /// </summary>
    public readonly struct XpProgress
    {
        /// <summary>XP accumulated toward the next level (i.e. <c>PlayerState.Xp</c>).</summary>
        public int CurrentXp { get; }

        /// <summary>XP required to cross into the next level, or 0 at the level cap.</summary>
        public int XpForNextLevel { get; }

        /// <summary>True when the player cannot level up further (<see cref="XpForNextLevel"/> is 0).</summary>
        public bool IsAtLevelCap { get; }

        /// <summary>
        /// Fractional progress into the current level, clamped to [0, 1].
        /// Always 0 when <see cref="IsAtLevelCap"/> is true.
        /// </summary>
        public float Progress01 { get; }

        public XpProgress(int currentXp, int xpForNextLevel, bool isAtLevelCap, float progress01)
        {
            CurrentXp      = currentXp;
            XpForNextLevel = xpForNextLevel;
            IsAtLevelCap   = isAtLevelCap;
            Progress01     = progress01;
        }
    }
}
```

- [x] **Step 1.2 — Compile check**

Unity Editor: let the compiler run. Expected: clean compile, no errors.

---

## Task 2 — Add `ProgressionService.GetXpProgress()` (TDD)

**Files:**
- Modify: `Assets/Scripts/Core/ProgressionService.cs`
- Modify: `Assets/Tests/Editor/Core/ProgressionServiceTests.cs`

- [x] **Step 2.1 — Write the failing tests**

Append the following tests at the bottom of `ProgressionServiceTests.cs`, inside the existing `ProgressionServiceTests` class, just before the final closing brace:

```csharp
        // ── GetXpProgress helper ──────────────────────────────────────────

        [Test]
        public void GetXpProgress_Fresh_ReturnsZeroCurrentAndThreshold()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100, 250));

            XpProgress progress = service.GetXpProgress();

            Assert.AreEqual(0,    progress.CurrentXp);
            Assert.AreEqual(100,  progress.XpForNextLevel);
            Assert.IsFalse(progress.IsAtLevelCap);
            Assert.AreEqual(0f,   progress.Progress01, 1e-6f);
        }

        [Test]
        public void GetXpProgress_MidLevel_ReturnsRatio()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100, 250));
            service.AwardXp(40); // below the L1→L2 threshold

            XpProgress progress = service.GetXpProgress();

            Assert.AreEqual(40,   progress.CurrentXp);
            Assert.AreEqual(100,  progress.XpForNextLevel);
            Assert.IsFalse(progress.IsAtLevelCap);
            Assert.AreEqual(0.40f, progress.Progress01, 1e-6f);
        }

        [Test]
        public void GetXpProgress_AfterLevelUpWithCarry_ReflectsNewLevelThreshold()
        {
            // AC: "Mid-battle level-up: the bar reflects the post-battle state
            //      (carried XP after any level crossings)."
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100, 250));

            service.AwardXp(150); // → L2 with 50 carried

            XpProgress progress = service.GetXpProgress();

            Assert.AreEqual(2,    state.Level);
            Assert.AreEqual(50,   progress.CurrentXp);
            Assert.AreEqual(250,  progress.XpForNextLevel);
            Assert.IsFalse(progress.IsAtLevelCap);
            Assert.AreEqual(50f / 250f, progress.Progress01, 1e-6f);
        }

        [Test]
        public void GetXpProgress_AtLevelCap_ReportsCapAndZeroProgress()
        {
            // AC: "Level cap: when XpForNextLevelUp == 0, hide the bar and
            //      numeric ratio, show MAX LEVEL instead."
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(100)); // cap = L2
            service.AwardXp(100);   // → L2, carry 0
            service.AwardXp(9999);  // post-cap XP still accumulates on state.Xp

            XpProgress progress = service.GetXpProgress();

            Assert.AreEqual(2,    state.Level);
            Assert.AreEqual(9999, progress.CurrentXp);
            Assert.AreEqual(0,    progress.XpForNextLevel);
            Assert.IsTrue(progress.IsAtLevelCap);
            Assert.AreEqual(0f,   progress.Progress01, 1e-6f);
        }

        [Test]
        public void GetXpProgress_EmptyCurve_ReportsCap()
        {
            PlayerState state = NewPlayerState();
            var service = new ProgressionService(state, MakeCharacterData(/*empty*/));

            XpProgress progress = service.GetXpProgress();

            Assert.IsTrue(progress.IsAtLevelCap);
            Assert.AreEqual(0,   progress.XpForNextLevel);
            Assert.AreEqual(0f,  progress.Progress01, 1e-6f);
        }

        [Test]
        public void GetXpProgress_ProgressIsClampedToOne_WhenXpExceedsThreshold()
        {
            // Guard: AwardXp cannot legally leave state.Xp >= threshold at a non-cap
            // level, but a manual ApplyProgression could. Clamp defensively.
            PlayerState state = NewPlayerState();
            state.ApplyProgression(level: 1, xp: 99999);
            var service = new ProgressionService(state, MakeCharacterData(100, 250));

            XpProgress progress = service.GetXpProgress();

            Assert.IsFalse(progress.IsAtLevelCap);
            Assert.AreEqual(1f, progress.Progress01, 1e-6f);
        }
```

- [x] **Step 2.2 — Run tests to confirm they fail**

Unity Editor → Window → General → Test Runner → Edit Mode tab → run `CoreTests.ProgressionServiceTests`. Expected: the 6 new tests fail with a compile error ("`ProgressionService` does not contain a definition for `GetXpProgress`"). All existing tests unaffected.

- [x] **Step 2.3 — Add the method to `ProgressionService`**

Insert the new method immediately after `XpForNextLevelUp` in `Assets/Scripts/Core/ProgressionService.cs` (i.e. between the closing brace of the property at line 47 and the doc comment for `AwardXp` at line 49):

```csharp
        /// <summary>
        /// Snapshots the player's XP progress into their current level. Designed
        /// for UI (Victory screen): the caller gets the ratio and level-cap flag
        /// without duplicating curve lookups. All reads are main-thread only.
        /// </summary>
        public XpProgress GetXpProgress()
        {
            int threshold = XpForNextLevelUp;
            int currentXp = _state.Xp;

            if (threshold <= 0)
                return new XpProgress(currentXp, xpForNextLevel: 0, isAtLevelCap: true, progress01: 0f);

            float ratio = (float)currentXp / threshold;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            return new XpProgress(currentXp, xpForNextLevel: threshold, isAtLevelCap: false, progress01: ratio);
        }
```

- [x] **Step 2.4 — Run tests to confirm they pass**

Unity Editor → Test Runner → Edit Mode → run `CoreTests.ProgressionServiceTests`. Expected: all tests pass (both new and existing).

- [x] **Step 2.5 — Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-76): add ProgressionService.GetXpProgress helper`

- `Assets/Scripts/Core/XpProgress.cs`
- `Assets/Scripts/Core/XpProgress.cs.meta`
- `Assets/Scripts/Core/ProgressionService.cs`
- `Assets/Tests/Editor/Core/ProgressionServiceTests.cs`

---

## Task 3 — Extend `VictoryScreenUI` with ratio text + fill bar

**Files:**
- Modify: `Assets/Scripts/Battle/UI/VictoryScreenUI.cs`

- [x] **Step 3.1 — Add the new serialized fields**

In `Assets/Scripts/Battle/UI/VictoryScreenUI.cs`, replace the existing serialized-field block (lines 18–27) with the block below. The new fields are additive — existing fields (`_panel`, `_titleText`, `_xpText`, `_lootText`, `_confirmButton`, `_itemCatalog`) must be preserved:

```csharp
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _xpText;
        [SerializeField] private TextMeshProUGUI _lootText;
        [SerializeField] private Button _confirmButton;

        [SerializeField]
        [Tooltip("Optional: ItemCatalog used to resolve itemId → displayName in the loot list. " +
                 "If unassigned, the raw itemId is shown.")]
        private ItemCatalog _itemCatalog;

        [Header("XP Progress (DEV-76)")]
        [SerializeField]
        [Tooltip("Root GameObject for the XP progress row (label + bar). " +
                 "Stays active in both normal and cap states so the MAX LEVEL label is visible.")]
        private GameObject _xpProgressRoot;

        [SerializeField]
        [Tooltip("TextMeshPro label under _xpProgressRoot. Normal state: '{currentXp} / {xpForNextLevel}'. " +
                 "Cap state: 'MAX LEVEL'.")]
        private TextMeshProUGUI _xpProgressText;

        [SerializeField]
        [Tooltip("Sub-root containing the bar background + fill. Hidden at level cap; " +
                 "visible otherwise so _xpProgressFill can render.")]
        private GameObject _xpProgressBarRoot;

        [SerializeField]
        [Tooltip("Image with Type=Filled (Horizontal) whose fillAmount is driven by XpProgress.Progress01.")]
        private Image _xpProgressFill;
```

- [x] **Step 3.2 — Update `Show` to take an `XpProgress` and render it**

Replace the `Show` method body (lines 53–82) with the version below:

```csharp
        /// <summary>
        /// Reveals the panel and renders <paramref name="result"/> plus the
        /// post-battle <paramref name="xpProgress"/> snapshot. Call once per battle.
        /// </summary>
        public void Show(PostBattleResult result, XpProgress xpProgress)
        {
            if (_titleText != null)
                _titleText.text = "VICTORY!";

            if (_xpText != null)
                _xpText.text = $"XP  +{result.Xp}";

            if (_lootText != null)
            {
                if (result.Items == null || result.Items.Count == 0)
                {
                    _lootText.text = "No items dropped.";
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Items:");
                    foreach (ItemGrant grant in result.Items)
                    {
                        string display = ResolveDisplayName(grant.ItemId);
                        sb.AppendLine($"  {display} x{grant.Quantity}");
                    }
                    _lootText.text = sb.ToString().TrimEnd();
                }
            }

            RenderXpProgress(xpProgress);

            ShowPanel();
        }

        private void RenderXpProgress(XpProgress xpProgress)
        {
            if (_xpProgressRoot != null) _xpProgressRoot.SetActive(true);

            if (xpProgress.IsAtLevelCap)
            {
                if (_xpProgressText != null) _xpProgressText.text = "MAX LEVEL";
                if (_xpProgressBarRoot != null) _xpProgressBarRoot.SetActive(false);
                return;
            }

            if (_xpProgressBarRoot != null) _xpProgressBarRoot.SetActive(true);
            if (_xpProgressText != null)
                _xpProgressText.text = $"{xpProgress.CurrentXp} / {xpProgress.XpForNextLevel}";
            if (_xpProgressFill != null)
                _xpProgressFill.fillAmount = xpProgress.Progress01;
        }
```

- [x] **Step 3.3 — Update the `using` directives**

Ensure the top of `VictoryScreenUI.cs` has the full set below (the file already contains `Axiom.Core`; leave it in place):

```csharp
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Core;
using Axiom.Data;
```

- [x] **Step 3.4 — Compile check**

Unity Editor: let the compiler run. Expected: `VictoryScreenUI.cs` compiles. `PostBattleFlowController.cs` will now fail to compile at the call site `_victoryScreenUI.Show(result)` because the signature changed — fix that in Task 4.

---

## Task 4 — Wire `PostBattleFlowController` to pass the snapshot

**Files:**
- Modify: `Assets/Scripts/Battle/PostBattleFlowController.cs`

- [x] **Step 4.1 — Replace the Victory UI dispatch**

In `Assets/Scripts/Battle/PostBattleFlowController.cs`, replace the block at lines 79–88 with the version below. The change: after `AwardXp` / inventory grants but before calling `Show`, snapshot `XpProgress` from the live `ProgressionService` — `AwardXp` has already updated `PlayerState.Xp`, so this is the post-battle state the AC asks for.

```csharp
            if (_victoryScreenUI == null)
            {
                Debug.LogWarning(
                    "[PostBattleFlow] _victoryScreenUI is not assigned — skipping Victory UI.", this);
                HandleVictoryScreenDismissed();
                return;
            }

            XpProgress xpProgress = gm?.ProgressionService != null
                ? gm.ProgressionService.GetXpProgress()
                : new XpProgress(currentXp: 0, xpForNextLevel: 0, isAtLevelCap: true, progress01: 0f);

            _victoryScreenUI.OnDismissed += HandleVictoryScreenDismissed;
            _victoryScreenUI.Show(result, xpProgress);
```

> Note: `default(XpProgress)` leaves `IsAtLevelCap = false` (the `bool` backing field zero-initializes to `false`) and would render `"0 / 0"` with an empty bar — wrong for the no-GameManager fallback. Construct the cap state explicitly instead so the UI falls back to `MAX LEVEL` with the bar hidden.

- [x] **Step 4.2 — Compile check**

Unity Editor: let the compiler run. Expected: both `VictoryScreenUI.cs` and `PostBattleFlowController.cs` compile cleanly. No test changes required — existing `ProgressionServiceTests` still pass.

- [x] **Step 4.3 — Run the full Edit Mode test suite**

Unity Editor → Test Runner → Edit Mode → Run All. Expected: all green, including the new `GetXpProgress_*` tests and the existing `BattleTests` / `CoreTests` suites.

- [x] **Step 4.4 — Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-76): render XP progress on Victory screen`

- `Assets/Scripts/Battle/UI/VictoryScreenUI.cs`
- `Assets/Scripts/Battle/PostBattleFlowController.cs`

---

## Task 5 — Wire the new UI in the Battle scene

> **Unity Editor task (user):** All steps below are performed in the Unity Editor. No code is touched in this task. UVCS check-in at the end captures the scene change.

- [x] **Step 5.1 — Open the Battle scene**

`Assets/Scenes/Battle.unity`.

- [x] **Step 5.2 — Locate the Victory panel**

In the scene Hierarchy, find the GameObject that currently holds `VictoryScreenUI` (the object whose `_panel` field points at the Victory panel root — usually named `VictoryPanel` under the Battle Canvas).

- [x] **Step 5.3 — Create the XP Progress row**

Under `VictoryPanel`, next to (and below) the existing XP text:

1. Create an empty child GameObject named `XpProgressRoot` with a `RectTransform`. This object stays active in both normal and cap states.
2. Under `XpProgressRoot`, create a `UI → Text - TextMeshPro` named `XpProgressText`. Placeholder text: `0 / 0`. Match font/size of the existing XP text. At cap this text becomes `MAX LEVEL`.
3. Under `XpProgressRoot`, create an empty child GameObject named `XpProgressBarRoot` with a `RectTransform` sized to the desired bar width/height. This object is hidden at level cap.
4. Under `XpProgressBarRoot`, create a `UI → Image` named `XpProgressBarBackground` (dark rounded sprite or solid color — match project UI style). Stretch to fill `XpProgressBarRoot`.
5. Under `XpProgressBarBackground`, create a `UI → Image` named `XpProgressFill`.
    - **Image Type:** `Filled`
    - **Fill Method:** `Horizontal`
    - **Fill Origin:** `Left`
    - **Fill Amount:** `0` (runtime will drive this)
    - Stretch to fill the parent rect.

- [x] **Step 5.4 — Assign the new fields on `VictoryScreenUI`**

Select the GameObject holding `VictoryScreenUI`. In the Inspector, under the new `XP Progress (DEV-76)` section, assign:

- `Xp Progress Root` → `XpProgressRoot`
- `Xp Progress Text` → `XpProgressText` (TextMeshPro component)
- `Xp Progress Bar Root` → `XpProgressBarRoot` (GameObject)
- `Xp Progress Fill` → `XpProgressFill` (Image component, NOT the background)

- [x] **Step 5.5 — Save the scene**

`File → Save` (or Ctrl/Cmd-S). Confirm `Assets/Scenes/Battle.unity` is now marked dirty in UVCS Pending Changes.

- [x] **Step 5.6 — Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-76): wire XP progress bar in Battle scene`

- `Assets/Scenes/Battle.unity`

---

## Task 6 — Manual verification in Play mode

> **Unity Editor task (user):** The progress bar is UI — Edit Mode tests cannot assert the rendered pixels. Exercise the golden paths manually before declaring the ticket done.

- [x] **Step 6.1 — Fresh-fight, below threshold**

1. Enter Play mode from the Platformer scene (new game or existing save well below level cap).
2. Trigger a battle that awards less XP than needed for the next level.
3. Win the battle.
4. **Expect:** Victory screen shows `XP +<gained>` (existing), `{currentXp} / {xpForNextLevel}` (new), and the fill bar partially filled matching `currentXp / xpForNextLevel`.
5. Click Confirm → Level-up prompt does **not** appear (correct — no level crossed); scene transitions back to Platformer.

- [x] **Step 6.2 — Single level-up with carry**

1. From a state where one fight pushes the player past the next-level threshold, win the battle.
2. **Expect:** Victory screen shows the *post*-level-up ratio — i.e. `{carried XP} / {threshold for the new level}`. The bar reflects the new-level ratio (*not* 100% from the old level).
3. Click Confirm → Level-up prompt appears (existing DEV-40 flow). Dismiss it. Scene transitions normally.

- [x] **Step 6.3 — Multi-level-up (if feasible to set up)**

1. Award enough XP in one fight to cross two thresholds.
2. **Expect:** Victory ratio reflects carry into the *highest* level reached; level-up prompt queues two entries in order.

- [x] **Step 6.4 — Level cap**

1. Use a debug path to push the player to cap (e.g. `_DevLevelUpTrigger` repeatedly, or edit `CharacterData.xpToNextLevelCurve` to a 1-entry curve for the test).
2. Win a battle.
3. **Expect:** The `XpProgressText` label now reads `MAX LEVEL` and the bar is hidden. The `XP +N` line still shows (post-cap XP still accumulates). Confirm advances normally.

- [x] **Step 6.5 — Regression check**

Verify: no layout shifts in the existing `XP +N` or items list; Confirm button still works; level-up prompt still appears after Victory dismissal when applicable; scene transition back to Platformer is unchanged.

- [x] **Step 6.6 — Transition the Jira ticket**

Move DEV-76 to `Done` in Jira once all checks above pass.

---

## Self-Review Notes

1. **Spec coverage** — every AC bullet maps to a task:
    - `{currentXp} / {xpForNextLevel}` line → Task 3.1/3.2 (_xpProgressText_), Task 5.3.
    - Visual progress bar → Task 3.1/3.2 (_xpProgressFill_), Task 5.3.
    - Sourced from `ProgressionService.XpForNextLevelUp` + `PlayerState.Xp` → Task 2 (`GetXpProgress`), Task 4.1 (controller passes snapshot).
    - Mid-battle level-up reflects post-battle carried state → Task 4.1 reads snapshot *after* `AwardXp`; asserted by `GetXpProgress_AfterLevelUpWithCarry_ReflectsNewLevelThreshold`.
    - Level cap shows `MAX LEVEL`, hides bar + ratio → Task 3.2 `RenderXpProgress` overwrites `_xpProgressText` with `"MAX LEVEL"` and toggles `_xpProgressBarRoot` off; Task 5.3 builds the matching hierarchy; asserted by `GetXpProgress_AtLevelCap_*` and `GetXpProgress_EmptyCurve_*`.
    - No change to DEV-36 sequence → Task 4.1 modifies only the Victory `Show` call site; `HandleVictoryScreenDismissed → LevelUpPromptUI.ShowIfPending` chain is untouched.
    - No new singletons, no `PlayerState` mutation from UI → UI gets `XpProgress` injected; `VictoryScreenUI` never references `GameManager` or `PlayerState`.
    - `VictoryScreenUI` thin, arithmetic in `ProgressionService` → curve lookup + division live in `GetXpProgress`; UI only reads fields.

2. **No placeholders** — every code block is literal. No "TODO" or "similar to".

3. **Signature consistency** — `Show(PostBattleResult result, XpProgress xpProgress)` is used identically in `VictoryScreenUI.cs` (Task 3.2) and `PostBattleFlowController.cs` (Task 4.1). `GetXpProgress()` return type matches every test call site.

4. **UVCS audit** — every created/modified file is staged in a UVCS check-in step:
    - Task 2.5 covers `XpProgress.cs`, `XpProgress.cs.meta`, `ProgressionService.cs`, `ProgressionServiceTests.cs`.
    - Task 4.4 covers `VictoryScreenUI.cs` and `PostBattleFlowController.cs`.
    - Task 5.6 covers `Battle.unity`.
    - No new `.asmdef` files, no new folders, no new `.meta` beyond `XpProgress.cs.meta`.

5. **MonoBehaviour rule** — `VictoryScreenUI` remains a view; arithmetic lives in plain-C# `ProgressionService`. `PostBattleFlowController` continues to be the thin orchestrator it already is.
