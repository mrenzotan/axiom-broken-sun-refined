# DEV-133 — Pause-on-Prompt Tutorial Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. For this Unity project also follow `executing-unity-game-dev-plans` (UVCS check-ins, Editor handoffs, Test Runner). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let designers mark individual tutorial prompts so that, when shown, the game freezes (`Time.timeScale = 0`) and the player must press a **Continue** button (click or Enter) before play resumes — for both platformer and battle tutorial prompts.

**Architecture:** Per-prompt, Inspector-driven opt-in (no player setting, no persistence, no settings UI — see Scope Reconciliation). The platformer `TutorialPromptTrigger` gains two Inspector bools and delegates the "should I pause now?" decision to a new plain-C# `TutorialPauseGate` (unit-tested). The battle `BattleTutorialController` gains one Inspector bool. Both tutorial panels (`TutorialPromptPanelUI`, `BattleTutorialPromptUI`) gain a `ShowAndPause(text, onContinue)` method that owns the freeze + Continue button + EventSystem focus, mirroring the existing `PauseMenuUI` timeScale pattern and `StatusMessageUI` Continue-button pattern. Enter-vs-attack conflict is solved per-context: platformer locks `PlayerExplorationAttack` input through the Continue frame; battle reuses `ActionMenuUI.SetMessageBlocked(true)` to disable action buttons while the prompt is up.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, C#, New Input System, Unity UI + TextMeshPro, NUnit (Unity Test Framework EditMode), UVCS for version control.

## Global Constraints

- **MonoBehaviour separation:** MonoBehaviours handle Unity lifecycle + wiring only; testable decision logic lives in plain C# classes (`TutorialPauseGate`, `BattleTutorialAction.ShowsPrompt`). (GAME_PLAN architecture rule.)
- **No new static singletons:** Reuse `GameManager.Instance` only. Do not introduce a static pause-runtime holder. timeScale/`SuppressPauseToggle` toggling lives inside each panel.
- **No premature abstraction:** The two prompt-panel classes are deliberate mirrored twins. Add the parallel `ShowAndPause` method to each; do **not** introduce a shared base class or interface.
- **Surgical changes:** Touch only the files listed per task. The non-paused code paths must behave exactly as they do today.
- **Commit messages:** `<type>(DEV-133): <short description>` — UVCS only, never git. No `Co-Authored-By`.
- **Tests:** Pure C# decision logic → EditMode (NUnit) in existing `Assets/Tests/Editor/<Module>/` folders (asmdefs already exist — do **not** create new ones). MonoBehaviour/timeScale/EventSystem behavior → Play Mode verification by the user.

## Scope Reconciliation (read before starting)

The Jira ticket's written Acceptance Criteria describe a **player-facing settings toggle** that persists across sessions (SaveData / PlayerPrefs) and lives in a settings menu. The reporter clarified during planning (2026-06-24) that the real intent is a **per-prompt Inspector boolean set by developers** — not a player setting. Confirmed decisions:

- **Per-prompt only.** No player toggle, no `SaveData`/`PlayerState`/`AudioSettingsStore` change, no settings-UI wiring. Those ticket ACs are intentionally **superseded** and out of scope here.
- **Battle prompts:** a single `_pauseOnPrompts` bool on `BattleTutorialController` — when true, every prompt it shows pauses.
- **Platformer:** `_pauseOnPrompt` bool plus a `_pauseOnlyOnce` bool (once-vs-every-entry) on `TutorialPromptTrigger`.

**Assumption flagged for the reporter:** when `_pauseOnlyOnce` is true, the *first* entry pauses; *subsequent* entries show the prompt normally (no pause), i.e. they revert to today's show/hide behavior. If you instead want later entries to show *nothing*, say so and Task 5 changes by one line.

---

## File Structure

**New files:**
- `Assets/Scripts/Platformer/TutorialPauseGate.cs` — plain C#; once-vs-every pause decision + state. Tested.
- `Assets/Tests/Editor/Platformer/TutorialPauseGateTests.cs` — EditMode tests for the gate.
- `Assets/Tests/Editor/Battle/BattleTutorialActionShowsPromptTests.cs` — EditMode tests for the new `ShowsPrompt` seam.

**Modified files:**
- `Assets/Scripts/Battle/BattleTutorialAction.cs` — add `ShowsPrompt` computed property.
- `Assets/Scripts/Platformer/UI/TutorialPromptPanelUI.cs` — add Continue button + `ShowAndPause`.
- `Assets/Scripts/Battle/UI/BattleTutorialPromptUI.cs` — mirror of the above.
- `Assets/Scripts/Platformer/TutorialPromptTrigger.cs` — pause fields, gate, pause input-lock, Continue handling.
- `Assets/Scripts/Battle/BattleTutorialController.cs` — `_pauseOnPrompts` bool; split `Apply` into prompt + deferred effects.

**Editor-only (Task 7):** add a bottom-center Continue button to each prompt panel prefab/scene object and assign the new serialized fields; set the Inspector bools on the relevant triggers/controller.

---

### Task 1: `TutorialPauseGate` — once-vs-every pause decision (plain C#)

**Files:**
- Create: `Assets/Scripts/Platformer/TutorialPauseGate.cs`
- Test: `Assets/Tests/Editor/Platformer/TutorialPauseGateTests.cs`

**Interfaces:**
- Produces: `Axiom.Platformer.TutorialPauseGate` with `bool ShouldPause(bool pauseOnlyOnce)` and `bool HasPaused { get; }`. Consumed by `TutorialPromptTrigger` (Task 5).

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/Platformer/TutorialPauseGateTests.cs`:
```csharp
using Axiom.Platformer;
using NUnit.Framework;

namespace Axiom.Tests.Editor.Platformer
{
    public class TutorialPauseGateTests
    {
        [Test]
        public void PauseEveryEntry_AlwaysReturnsTrue()
        {
            var gate = new TutorialPauseGate();
            Assert.IsTrue(gate.ShouldPause(pauseOnlyOnce: false));
            Assert.IsTrue(gate.ShouldPause(pauseOnlyOnce: false));
            Assert.IsTrue(gate.ShouldPause(pauseOnlyOnce: false));
        }

        [Test]
        public void PauseOnlyOnce_PausesFirstEntryThenNeverAgain()
        {
            var gate = new TutorialPauseGate();
            Assert.IsTrue(gate.ShouldPause(pauseOnlyOnce: true), "first entry must pause");
            Assert.IsFalse(gate.ShouldPause(pauseOnlyOnce: true), "second entry must not pause");
            Assert.IsFalse(gate.ShouldPause(pauseOnlyOnce: true), "third entry must not pause");
        }

        [Test]
        public void HasPaused_IsFalseUntilFirstPause()
        {
            var gate = new TutorialPauseGate();
            Assert.IsFalse(gate.HasPaused);
            gate.ShouldPause(pauseOnlyOnce: true);
            Assert.IsTrue(gate.HasPaused);
        }
    }
}
```

- [ ] **Step 2: Run the tests, verify they fail**

Unity Editor → Window → General → Test Runner → EditMode → run `TutorialPauseGateTests`.
Expected: FAIL/compile error — `TutorialPauseGate` does not exist.

- [ ] **Step 3: Write the minimal implementation**

`Assets/Scripts/Platformer/TutorialPauseGate.cs`:
```csharp
namespace Axiom.Platformer
{
    /// <summary>
    /// Per-trigger pause-on-prompt gating. Decides whether entering a tutorial zone should
    /// pause the game right now, honoring the designer's "only once" vs "every entry" choice.
    /// One instance per TutorialPromptTrigger; state lives for that trigger's lifetime
    /// (resets on scene reload, which spawns a fresh trigger + gate).
    /// </summary>
    public class TutorialPauseGate
    {
        public bool HasPaused { get; private set; }

        /// <summary>
        /// Returns true if entering should pause now.
        /// <paramref name="pauseOnlyOnce"/>: when true, only the first entry pauses; later
        /// entries return false. When false, every entry returns true.
        /// </summary>
        public bool ShouldPause(bool pauseOnlyOnce)
        {
            if (pauseOnlyOnce && HasPaused) return false;
            HasPaused = true;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run the tests, verify they pass**

Test Runner → EditMode → `TutorialPauseGateTests`. Expected: 3 PASS.

- [ ] **Step 5: Check in via UVCS**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-133): add TutorialPauseGate once-vs-every pause decision`
  - `Assets/Scripts/Platformer/TutorialPauseGate.cs`
  - `Assets/Scripts/Platformer/TutorialPauseGate.cs.meta`
  - `Assets/Tests/Editor/Platformer/TutorialPauseGateTests.cs`
  - `Assets/Tests/Editor/Platformer/TutorialPauseGateTests.cs.meta`

---

### Task 2: `BattleTutorialAction.ShowsPrompt` seam (plain C#)

**Files:**
- Modify: `Assets/Scripts/Battle/BattleTutorialAction.cs`
- Test: `Assets/Tests/Editor/Battle/BattleTutorialActionShowsPromptTests.cs`

**Interfaces:**
- Produces: `bool BattleTutorialAction.ShowsPrompt` — true iff `PromptText` is non-null and non-empty. Consumed by `BattleTutorialController.Apply` (Task 6).

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/Battle/BattleTutorialActionShowsPromptTests.cs`:
```csharp
using Axiom.Battle;
using NUnit.Framework;

namespace Axiom.Tests.Editor.Battle
{
    public class BattleTutorialActionShowsPromptTests
    {
        [Test]
        public void ShowsPrompt_FalseForNullPromptText()
        {
            var action = new BattleTutorialAction(promptText: null);
            Assert.IsFalse(action.ShowsPrompt);
        }

        [Test]
        public void ShowsPrompt_FalseForEmptyPromptText()
        {
            var action = new BattleTutorialAction(promptText: string.Empty);
            Assert.IsFalse(action.ShowsPrompt);
        }

        [Test]
        public void ShowsPrompt_TrueForNonEmptyPromptText()
        {
            var action = new BattleTutorialAction(promptText: "Press Attack to strike.");
            Assert.IsTrue(action.ShowsPrompt);
        }

        [Test]
        public void NoChange_DoesNotShowPrompt()
        {
            Assert.IsFalse(BattleTutorialAction.NoChange.ShowsPrompt);
        }
    }
}
```

- [ ] **Step 2: Run the tests, verify they fail**

Test Runner → EditMode → `BattleTutorialActionShowsPromptTests`.
Expected: FAIL/compile error — `ShowsPrompt` does not exist.

- [ ] **Step 3: Add the computed property**

In `Assets/Scripts/Battle/BattleTutorialAction.cs`, add this property to the struct, immediately after the `SpellGate` property (after line 18):
```csharp
        /// <summary>True when this action shows a prompt (PromptText is non-null and non-empty).
        /// "" (hide) and null (no change) both return false.</summary>
        public bool ShowsPrompt => !string.IsNullOrEmpty(PromptText);
```

- [ ] **Step 4: Run the tests, verify they pass**

Test Runner → EditMode → `BattleTutorialActionShowsPromptTests`. Expected: 4 PASS.

- [ ] **Step 5: Check in via UVCS**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-133): add BattleTutorialAction.ShowsPrompt`
  - `Assets/Scripts/Battle/BattleTutorialAction.cs`
  - `Assets/Tests/Editor/Battle/BattleTutorialActionShowsPromptTests.cs`
  - `Assets/Tests/Editor/Battle/BattleTutorialActionShowsPromptTests.cs.meta`

---

### Task 3: Platformer panel — `ShowAndPause` + Continue button

**Files:**
- Modify: `Assets/Scripts/Platformer/UI/TutorialPromptPanelUI.cs`

**Interfaces:**
- Consumes: `Axiom.Core.GameManager.Instance.SuppressPauseToggle` (existing settable bool, `GameManager.cs:184`).
- Produces: `void TutorialPromptPanelUI.ShowAndPause(string body, System.Action onContinue)`. Consumed by `TutorialPromptTrigger` (Task 5). Existing `Show`/`Hide` signatures unchanged.

> No EditMode test: this is MonoBehaviour + `Time.timeScale` + EventSystem behavior, verified in Play Mode (Task 8). The decision logic it relies on is tested in Tasks 1–2.

- [ ] **Step 1: Replace the file contents**

Full new `Assets/Scripts/Platformer/UI/TutorialPromptPanelUI.cs`:
```csharp
using System;
using Axiom.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Axiom.Platformer.UI
{
    /// <summary>
    /// Simple prompt panel anchored to the platformer HUD. Shown when the player enters a
    /// TutorialPromptTrigger zone; hidden when they leave it.
    ///
    /// Pause-on-prompt (DEV-133): ShowAndPause freezes the game (Time.timeScale = 0), shows a
    /// bottom-center Continue button, and resumes only when the player clicks Continue or
    /// presses Enter (Enter resolves to the focused Continue button via the UI/Submit action).
    /// </summary>
    public class TutorialPromptPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _bodyLabel;
        [SerializeField]
        [Tooltip("Bottom-center button shown only in pause-on-prompt mode. Required for ShowAndPause.")]
        private Button _continueButton;

        private Action _onContinue;
        private bool _pausedByThisPanel;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);
        }

        public void Show(string body)
        {
            if (_bodyLabel != null) _bodyLabel.text = body;
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);
            if (_root != null) _root.SetActive(true);
        }

        /// <summary>
        /// Shows the prompt, freezes the game, and shows the Continue button. The game stays
        /// paused until the player presses Continue, at which point timeScale is restored,
        /// the panel hides, and <paramref name="onContinue"/> is invoked.
        /// </summary>
        public void ShowAndPause(string body, Action onContinue)
        {
            if (_continueButton == null)
            {
                Debug.LogError($"{name}: ShowAndPause requested but no Continue button is wired. " +
                               "Falling back to non-paused Show.", this);
                Show(body);
                onContinue?.Invoke();
                return;
            }

            _onContinue = onContinue;
            if (_bodyLabel != null) _bodyLabel.text = body;
            if (_root != null) _root.SetActive(true);

            _continueButton.gameObject.SetActive(true);
            _continueButton.onClick.RemoveListener(HandleContinue);
            _continueButton.onClick.AddListener(HandleContinue);

            Time.timeScale = 0f;
            _pausedByThisPanel = true;
            if (GameManager.Instance != null) GameManager.Instance.SuppressPauseToggle = true;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
        }

        public void Hide()
        {
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);
            if (_root != null) _root.SetActive(false);
        }

        private void HandleContinue()
        {
            _continueButton.onClick.RemoveListener(HandleContinue);
            Action callback = _onContinue;
            _onContinue = null;
            ResumeFromPause();
            Hide();
            callback?.Invoke();
        }

        private void ResumeFromPause()
        {
            if (!_pausedByThisPanel) return;
            _pausedByThisPanel = false;
            Time.timeScale = 1f;
            if (GameManager.Instance != null) GameManager.Instance.SuppressPauseToggle = false;
        }

        private void OnDisable()
        {
            // Safety: never leave the game frozen if this panel is torn down mid-pause
            // (e.g. a scene transition while a prompt is up).
            if (_pausedByThisPanel) ResumeFromPause();
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Return to Unity, let it recompile. Expected: no console errors. (`Axiom.Platformer` already references `Axiom.Core`, `UnityEngine.UI`, `Unity.TextMeshPro`.)

- [ ] **Step 3: Check in via UVCS**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-133): add ShowAndPause to platformer tutorial panel`
  - `Assets/Scripts/Platformer/UI/TutorialPromptPanelUI.cs`

---

### Task 4: Battle panel — `ShowAndPause` + Continue button (mirror of Task 3)

**Files:**
- Modify: `Assets/Scripts/Battle/UI/BattleTutorialPromptUI.cs`

**Interfaces:**
- Consumes: `Axiom.Core.GameManager.Instance.SuppressPauseToggle`.
- Produces: `void BattleTutorialPromptUI.ShowAndPause(string body, System.Action onContinue)`. Consumed by `BattleTutorialController` (Task 6).

> Mirror of Task 3 by design (these two panels are intentional twins). Repeated in full because the engineer may read tasks out of order.

- [ ] **Step 1: Replace the file contents**

Full new `Assets/Scripts/Battle/UI/BattleTutorialPromptUI.cs`:
```csharp
using System;
using Axiom.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Axiom.Battle.UI
{
    /// <summary>
    /// Battle-scene tutorial prompt panel. Mirrors the platformer's TutorialPromptPanelUI but
    /// lives in the Battle Canvas. BattleTutorialController calls Show/Hide/ShowAndPause as the
    /// state machine emits prompts.
    ///
    /// Pause-on-prompt (DEV-133): ShowAndPause freezes the battle (Time.timeScale = 0), shows a
    /// bottom-center Continue button, and resumes only when the player clicks Continue or presses
    /// Enter. The controller separately disables the action menu while paused so Enter resolves
    /// to Continue rather than the focused Attack/Spell button.
    /// </summary>
    public class BattleTutorialPromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _bodyLabel;
        [SerializeField]
        [Tooltip("Bottom-center button shown only in pause-on-prompt mode. Required for ShowAndPause.")]
        private Button _continueButton;

        private Action _onContinue;
        private bool _pausedByThisPanel;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);
        }

        public void Show(string body)
        {
            if (_bodyLabel != null) _bodyLabel.text = body;
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);
            if (_root != null) _root.SetActive(true);
        }

        /// <summary>
        /// Shows the prompt, freezes the game, and shows the Continue button. The game stays
        /// paused until the player presses Continue, at which point timeScale is restored,
        /// the panel hides, and <paramref name="onContinue"/> is invoked.
        /// </summary>
        public void ShowAndPause(string body, Action onContinue)
        {
            if (_continueButton == null)
            {
                Debug.LogError($"{name}: ShowAndPause requested but no Continue button is wired. " +
                               "Falling back to non-paused Show.", this);
                Show(body);
                onContinue?.Invoke();
                return;
            }

            _onContinue = onContinue;
            if (_bodyLabel != null) _bodyLabel.text = body;
            if (_root != null) _root.SetActive(true);

            _continueButton.gameObject.SetActive(true);
            _continueButton.onClick.RemoveListener(HandleContinue);
            _continueButton.onClick.AddListener(HandleContinue);

            Time.timeScale = 0f;
            _pausedByThisPanel = true;
            if (GameManager.Instance != null) GameManager.Instance.SuppressPauseToggle = true;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
        }

        public void Hide()
        {
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);
            if (_root != null) _root.SetActive(false);
        }

        private void HandleContinue()
        {
            _continueButton.onClick.RemoveListener(HandleContinue);
            Action callback = _onContinue;
            _onContinue = null;
            ResumeFromPause();
            Hide();
            callback?.Invoke();
        }

        private void ResumeFromPause()
        {
            if (!_pausedByThisPanel) return;
            _pausedByThisPanel = false;
            Time.timeScale = 1f;
            if (GameManager.Instance != null) GameManager.Instance.SuppressPauseToggle = false;
        }

        private void OnDisable()
        {
            // Safety: never leave the battle frozen if this panel is torn down mid-pause.
            if (_pausedByThisPanel) ResumeFromPause();
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Return to Unity, let it recompile. Expected: no console errors. (`Axiom.Battle` already references `Axiom.Core`, `UnityEngine.UI`, `Unity.TextMeshPro`.)

- [ ] **Step 3: Check in via UVCS**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-133): add ShowAndPause to battle tutorial panel`
  - `Assets/Scripts/Battle/UI/BattleTutorialPromptUI.cs`

---

### Task 5: `TutorialPromptTrigger` — pause fields, gate, input lock

**Files:**
- Modify: `Assets/Scripts/Platformer/TutorialPromptTrigger.cs`

**Interfaces:**
- Consumes: `TutorialPauseGate.ShouldPause(bool)` (Task 1); `TutorialPromptPanelUI.ShowAndPause(string, Action)` (Task 3); existing `PlayerController.SetTutorialMovementLocked(bool)` and `PlayerExplorationAttack.SetInputLocked(bool)`.
- Produces: two new serialized bools (`_pauseOnPrompt`, `_pauseOnlyOnce`); no new public API.

> No EditMode test: trigger logic is MonoBehaviour + physics callbacks; the testable decision is `TutorialPauseGate` (Task 1). Behavior verified in Play Mode (Task 8).

- [ ] **Step 1: Add the `using` and serialized fields**

In `Assets/Scripts/Platformer/TutorialPromptTrigger.cs`, change the `using` block at the top to add `System.Collections`:
```csharp
using System.Collections;
using Axiom.Core;
using Axiom.Platformer.UI;
using UnityEngine;
```

Then add these fields immediately after the `_playerAttack` field (after line 40):
```csharp
        [SerializeField]
        [Tooltip("When true, entering this zone pauses the game (Time.timeScale = 0) and shows a " +
                 "Continue button; the player must click Continue or press Enter to resume. " +
                 "Requires Player Controller and Player Attack refs assigned. Do NOT also set the " +
                 "lock flags above — the pause supersedes them.")]
        private bool _pauseOnPrompt;
        [SerializeField]
        [Tooltip("Only meaningful when Pause On Prompt is true. When true, only the FIRST entry " +
                 "pauses; later entries show the prompt without pausing. When false, every entry pauses.")]
        private bool _pauseOnlyOnce = true;

        private readonly TutorialPauseGate _pauseGate = new TutorialPauseGate();
```

- [ ] **Step 2: Add fail-loud validation in Awake**

Replace the existing `Awake` method (lines 51–57) with:
```csharp
        private void Awake()
        {
            if (_pauseOnPrompt && (_playerController == null || _playerAttack == null))
                Debug.LogError($"{name}: Pause On Prompt is enabled but Player Controller and/or " +
                               "Player Attack refs are not assigned. Gameplay input will not be " +
                               "locked while paused, risking an attack firing on Continue.", this);

            if (_oneShotFlag == OneShotTutorialFlag.None) return;
            if (GameManager.Instance == null) return;
            if (TutorialOneShotFlagResolver.IsFlagSet(GameManager.Instance.PlayerState, _oneShotFlag))
                gameObject.SetActive(false);
        }
```

- [ ] **Step 3: Route the pause path in OnTriggerEnter2D**

Replace `OnTriggerEnter2D` (lines 59–64) with:
```csharp
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            if (_pauseOnPrompt && _panel != null && _pauseGate.ShouldPause(_pauseOnlyOnce))
            {
                SetPauseInputLock(true);
                _panel.ShowAndPause(_message, OnPauseContinue);
                return;
            }

            if (_panel != null) _panel.Show(_message);
            SetPlayerLock(true);
        }
```

- [ ] **Step 4: Add the Continue + pause-lock helpers**

Add these methods immediately after `SetPlayerLock` (after line 93):
```csharp
        private void OnPauseContinue()
        {
            // The panel has already restored Time.timeScale and hidden the prompt. Defer the
            // input unlock by one frame so the Enter press that activated Continue does not leak
            // into PlayerExplorationAttack's same-frame WasPerformedThisFrame() read.
            if (isActiveAndEnabled) StartCoroutine(UnlockPauseInputNextFrame());
            else SetPauseInputLock(false);
        }

        private IEnumerator UnlockPauseInputNextFrame()
        {
            yield return null;
            SetPauseInputLock(false);
        }

        private void SetPauseInputLock(bool locked)
        {
            if (_playerController != null) _playerController.SetTutorialMovementLocked(locked);
            if (_playerAttack != null) _playerAttack.SetInputLocked(locked);
        }
```

- [ ] **Step 5: Verify it compiles**

Return to Unity, let it recompile. Expected: no console errors.

- [ ] **Step 6: Check in via UVCS**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-133): add pause-on-prompt to TutorialPromptTrigger`
  - `Assets/Scripts/Platformer/TutorialPromptTrigger.cs`

---

### Task 6: `BattleTutorialController` — `_pauseOnPrompts` + deferred-effects split

**Files:**
- Modify: `Assets/Scripts/Battle/BattleTutorialController.cs`

**Interfaces:**
- Consumes: `BattleTutorialAction.ShowsPrompt` (Task 2); `BattleTutorialPromptUI.ShowAndPause(string, Action)` (Task 4); existing `ActionMenuUI.SetMessageBlocked(bool)`.
- Produces: one new serialized bool (`_pauseOnPrompts`); internal `Apply` split into `ApplyPromptText` + `ApplyActionEffects`. No public API change.

> No EditMode test: the controller is a MonoBehaviour wired to the live battle. The pause decision (`ShowsPrompt`) is unit-tested in Task 2; end-to-end flows verified in Play Mode (Task 8).

- [ ] **Step 1: Add the serialized field**

In `Assets/Scripts/Battle/BattleTutorialController.cs`, add after the `_promptUI` field (after line 22):
```csharp
        [SerializeField]
        [Tooltip("When true, every tutorial prompt this controller shows pauses the battle " +
                 "(Time.timeScale = 0) and requires the player to press Continue before play resumes.")]
        private bool _pauseOnPrompts;
```

- [ ] **Step 2: Replace `Apply` with the split + pause path**

Replace the entire `Apply` method (lines 156–198) with:
```csharp
        private void Apply(BattleTutorialAction action)
        {
            if (_pauseOnPrompts && action.ShowsPrompt && _promptUI != null)
            {
                // Show + freeze. Disable the action menu so Enter resolves to the Continue
                // button rather than the focused Attack/Spell button (SetMessageBlocked snapshots
                // and restores the buttons). Defer the action's button/gate/completion effects
                // until the player presses Continue.
                if (_actionMenu != null) _actionMenu.SetMessageBlocked(true);
                _promptUI.ShowAndPause(action.PromptText, () => OnPromptContinue(action));
                return;
            }

            ApplyPromptText(action);
            ApplyActionEffects(action);
        }

        private void OnPromptContinue(BattleTutorialAction action)
        {
            // The panel has already restored Time.timeScale and hidden the prompt.
            // Restore the action menu, then apply this action's intended button/gate states.
            if (_actionMenu != null) _actionMenu.SetMessageBlocked(false);
            ApplyActionEffects(action);
        }

        private void ApplyPromptText(BattleTutorialAction action)
        {
            if (_promptUI == null) return;
            if (action.PromptText == string.Empty)      _promptUI.Hide();
            else if (action.PromptText != null)         _promptUI.Show(action.PromptText);
        }

        private void ApplyActionEffects(BattleTutorialAction action)
        {
            if (_actionMenu != null)
            {
                bool buttonsChanged =
                    action.AttackInteractable.HasValue || action.SpellInteractable.HasValue ||
                    action.ItemInteractable.HasValue   || action.FleeInteractable.HasValue;

                if (action.AttackInteractable.HasValue) _actionMenu.SetAttackInteractable(action.AttackInteractable.Value);
                if (action.SpellInteractable.HasValue)  _actionMenu.SetSpellInteractable(action.SpellInteractable.Value);
                if (action.ItemInteractable.HasValue)   _actionMenu.SetItemInteractable(action.ItemInteractable.Value);
                if (action.FleeInteractable.HasValue)   _actionMenu.SetFleeInteractable(action.FleeInteractable.Value);

                // After locking a button, the EventSystem may still be on a now-disabled button,
                // leaving the player no highlighted/keyboard target. Move focus to the first
                // still-interactable button. Skip while a message is displaying — the MessageLog's
                // Continue button owns focus then.
                if (buttonsChanged && _currentBattleState == BattleState.PlayerTurn &&
                    !_actionMenu.IsMessageBlocked)
                    _actionMenu.FocusFirstInteractable();
            }

            if (action.SpellGate != null && _battleController != null)
                _battleController.SetTutorialSpellGate(action.SpellGate);

            if (action.MarkComplete && GameManager.Instance != null && _flow != null)
            {
                PlayerState ps = GameManager.Instance.PlayerState;
                switch (_flow.Mode)
                {
                    case BattleTutorialMode.FirstBattle:   ps.MarkFirstBattleTutorialCompleted(); break;
                    case BattleTutorialMode.SpellTutorial: ps.MarkSpellTutorialBattleCompleted(); break;
                }
                GameManager.Instance.PersistToDisk();
            }
        }
```

> Behavior note: the non-paused path (`_pauseOnPrompts == false`) calls `ApplyPromptText` + `ApplyActionEffects`, which together do exactly what the original `Apply` did. `OnBattleEnded` emits `PromptText == ""` (hide), so `ShowsPrompt` is false there — victory/defeat completion never pauses and `MarkComplete`/persist always runs immediately.

- [ ] **Step 3: Verify it compiles**

Return to Unity, let it recompile. Expected: no console errors.

- [ ] **Step 4: Run the existing battle tutorial tests, verify still green**

Test Runner → EditMode → `BattleTutorialFlowTests` (flow logic is unchanged; this guards against accidental regressions). Expected: all PASS.

- [ ] **Step 5: Check in via UVCS**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-133): pause battle tutorial prompts via _pauseOnPrompts`
  - `Assets/Scripts/Battle/BattleTutorialController.cs`

---

### Task 7: Unity Editor wiring (user) — Continue buttons + Inspector flags

> **Unity Editor task (user):** All steps in this task are performed in the Unity Editor. Claude does not edit scenes/prefabs.

- [ ] **Step 1 — Platformer panel Continue button.**
  In the Platformer scene (or the prompt-panel prefab), find the GameObject with `TutorialPromptPanelUI`. Under its panel `_root`, add a UI **Button** named `ContinueButton`, anchored **bottom-center** of the panel, with a TMP child label reading **"Continue"**. Assign it to the new **Continue Button** field on the `TutorialPromptPanelUI` component.

- [ ] **Step 2 — Battle panel Continue button.**
  In `Assets/Scenes/Battle.unity`, find the GameObject with `BattleTutorialPromptUI`. Under its `_root`, add a UI **Button** named `ContinueButton`, anchored **bottom-center**, TMP label **"Continue"**. Assign it to the new **Continue Button** field on `BattleTutorialPromptUI`.

- [ ] **Step 3 — Confirm an EventSystem exists in both scenes** (needed so Enter/UI-Submit can activate the focused Continue button). The platformer scene already has one for the pause menu; the Battle scene already has one for the action menu. No change expected — just verify.

- [ ] **Step 4 — Set platformer pause flags.**
  On the `TutorialPromptTrigger`(s) you want to pause, tick **Pause On Prompt**, set **Pause Only Once** as desired, and assign the **Player Controller** and **Player Attack** references (the player GameObject's components). Leave the legacy **Lock Movement / Lock Attack** flags **off** on these triggers.

- [ ] **Step 5 — Set battle pause flag.**
  On the `BattleTutorialController` in `Assets/Scenes/Battle.unity`, tick **Pause On Prompts** (only if you want battle tutorials to pause).

- [ ] **Step 6: Check in via UVCS**
  Unity Version Control → Pending Changes → stage the changed scene/prefab assets (and any new `.meta` for new button GameObjects if they are prefab assets) → Check in with message: `feat(DEV-133): wire Continue buttons and pause-on-prompt flags`
  - `Assets/Scenes/Battle.unity`
  - the platformer scene or prompt-panel prefab asset you edited (+ its `.meta` if new)

---

### Task 8: Play Mode verification (user)

> **Unity Editor task (user):** Enter Play Mode and verify each acceptance criterion. These are the checks the EditMode tests cannot cover (timeScale, EventSystem focus, input routing, battle state machine).

- [ ] **Platformer — pause + Continue (click).** Walk into a pause-on-prompt zone. Verify: game freezes (animations/physics stop), prompt + Continue visible, Continue focused. Click Continue → play resumes, prompt hidden.
- [ ] **Platformer — Continue via Enter.** Re-enter (use a `_pauseOnlyOnce = false` trigger). While paused, press **Enter** → resumes. **Critically verify no attack/battle is triggered by that Enter press** (no `BeginAttack`, no battle transition). Repeat several times.
- [ ] **Platformer — input locked while paused.** While paused, hold movement keys / press Jump / press Attack → nothing happens until Continue.
- [ ] **Platformer — pause-only-once.** On a `_pauseOnlyOnce = true` trigger: first entry pauses; exit and re-enter → prompt shows **without** pausing.
- [ ] **Platformer — Esc during pause.** While tutorial-paused, press **Esc** → the pause menu does **not** open over the prompt (`SuppressPauseToggle` is set). After Continue, Esc opens the pause menu normally.
- [ ] **Battle — FirstBattle end-to-end.** Start the FirstBattle tutorial with `_pauseOnPrompts` on. Verify: each prompt pauses the battle, action buttons are disabled while paused, Continue (click and Enter) resumes, and pressing Enter does **not** activate Attack/Spell. Confirm the battle completes and `HasCompletedFirstBattleTutorial` persists (no replay on re-entry).
- [ ] **Battle — SpellTutorial end-to-end.** Run the SpellTutorial flow. Verify the mid-resolution prompts ("Liquid blocks", "Frozen Solid", closing line) each pause and resume cleanly, the spell gate still restricts/allows correctly after Continue, the turn/state machine is not corrupted, and the tutorial completes + persists.
- [ ] **Battle — no double SetMessageBlocked.** Confirm a tutorial prompt never overlaps a status-message Continue in the same moment (would double-toggle `SetMessageBlocked`). If it ever does in these flows, note it — current flows are not expected to overlap.
- [ ] **Regression — toggles off.** With every pause flag off, confirm both platformer and battle tutorials behave exactly as before (show/hide on enter/exit and battle state changes, no pause, no Continue gate).

---

## Self-Review

**Spec coverage** (against the reconciled scope):
- Per-prompt pause toggle (platformer) → Tasks 1, 5. ✓
- Once-vs-every entry → Tasks 1, 5. ✓
- Pause via `Time.timeScale = 0` reusing the existing mechanism → Tasks 3, 4. ✓
- Continue button, bottom-center, both panels → Tasks 3, 4, 7. ✓
- Continue via click **or** Enter → Tasks 3, 4 (EventSystem focus) + 8 (verify). ✓
- Enter must not trigger attack: platformer (input lock + deferred unlock, Tasks 5/8); battle (`SetMessageBlocked`, Tasks 6/8). ✓
- Battle prompts pause via single controller bool → Tasks 2, 6, 7. ✓
- Battle state machine not corrupted; FirstBattle + SpellTutorial complete → Task 8. ✓
- Per-prompt locks superseded by pause → documented in Task 5 tooltip + Editor step 4. ✓
- Player settings toggle / persistence / settings UI → **intentionally out of scope** per Scope Reconciliation. ✓
- Esc-during-pause corruption → handled via `SuppressPauseToggle` (Tasks 3/4) + verified (Task 8). ✓

**Placeholder scan:** No TBD/TODO; every code step shows full code; mirrored Task 4 is repeated in full, not referenced.

**Type consistency:** `ShouldPause(bool pauseOnlyOnce)`, `HasPaused`, `ShowAndPause(string, Action)`, `ShowsPrompt`, `SetMessageBlocked(bool)`, `SetTutorialMovementLocked(bool)`, `SetInputLocked(bool)`, `SuppressPauseToggle` — all match their definitions in the read source files and earlier tasks. `_pauseOnPrompt`/`_pauseOnlyOnce` (platformer) vs `_pauseOnPrompts` (battle) are deliberately distinct names for distinct components.

---

## Open question for the reporter

- Confirm the flagged assumption (Scope Reconciliation): with **Pause Only Once** on, later entries show the prompt **without** pausing. If you'd rather they show nothing at all, Task 5 Step 3's fall-through changes to skip `_panel.Show`.
