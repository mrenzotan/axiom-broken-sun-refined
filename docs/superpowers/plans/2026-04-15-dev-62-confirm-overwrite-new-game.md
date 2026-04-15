# DEV-62: Confirm overwrite dialog before starting a new game — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent accidental progress loss when the player clicks **New Game** on the main menu while a save file exists. Show a modal Yes/No confirmation dialog. Yes → existing `GameManager.StartNewGame()` path (deletes save, loads Platformer). No → dismiss dialog, leave state untouched. When no save exists, New Game proceeds immediately without the dialog.

**Architecture:**
- `MainMenuController` (plain C#, `Axiom.Core`) gains an optional `requestNewGameConfirmation` `Action` delegate injected in the constructor. `OnNewGameClicked()` branches: if `hasSaveFile()` returns true **and** the delegate is supplied, it invokes the delegate (show dialog); otherwise it invokes `startNewGame()` directly. Still fully testable in Edit Mode, no Unity dependencies.
- `ConfirmDialogController` (plain C#, `Axiom.Core`) — new generic pure-logic class holding `_onConfirm` and `_onCancel` `Action`s with `OnYesClicked()` / `OnNoClicked()` methods. Generic so the same class can serve future confirm dialogs (e.g. delete save mid-game).
- `ConfirmNewGameDialogUI` (MonoBehaviour, `Axiom.Core`) — lifecycle only. Owns the two `Button` references, the `TextMeshProUGUI` label, wires `ConfirmDialogController`, handles Submit/Cancel input via `EventSystem` for keyboard/gamepad. Activates/deactivates the dialog root GameObject on Show()/Hide(). Restores EventSystem focus to the New Game button on dismiss.
- `MainMenuUI` — extended to hold a `[SerializeField] ConfirmNewGameDialogUI _confirmDialog` reference and pass a lambda `() => _confirmDialog.Show(onConfirm: GameManager.Instance.StartNewGame)` as the new controller delegate.

**Tech Stack:** Unity 6 LTS, URP 2D, C# 9, Unity UI Canvas + TextMeshPro + EventSystem, New Input System (UI Input Module — `Submit` / `Cancel` already map to Enter/Esc and gamepad A/B), NUnit via Unity Test Framework (Edit Mode in `Assets/Tests/Editor/Core/`), UVCS for check-ins.

---

## Project context (read before coding)

| Source | What applies to this ticket |
|--------|-----------------------------|
| `docs/GAME_PLAN.md` §10 | MonoBehaviour = lifecycle only; logic lives in plain C#. No premature abstraction — `ConfirmDialogController` is generic because a second use case (mid-game overwrite dialog) is explicitly called out as out of scope but architecturally imminent; a one-off would be fine too, but a two-field generic routing class is not over-abstraction. |
| `CLAUDE.md` | Scene-scoped UI lives under the owning scene's folder. `MainMenu` lives in `Assets/Scripts/Core/`, so the dialog lives there too (not a new top-level UI folder). |
| `Assets/Scripts/Core/MainMenuController.cs` | Already has delegate-injection pattern with optional `quit`. Adding an optional `requestNewGameConfirmation` parameter preserves all existing `MainMenuControllerTests` call sites. |
| `Assets/Scripts/Core/GameManager.cs` | `StartNewGame()` (DEV-61) already deletes the save and loads Platformer — do not change. `HasSaveFile()` is the dialog gate. |
| `Assets/Prefabs/UI/` | Existing UI prefab location. New prefab: `ConfirmNewGameDialog.prefab`. |
| `Assets/InputSystem_Actions.inputactions` | UI action map provides `Submit` / `Cancel` / `Navigate` bound to Enter / Esc / gamepad A / B / dpad + left stick. No new bindings required. |
| `docs/VERSION_CONTROL.md` | UVCS is source of truth. Prefabs and scenes check in via UVCS, not git. |

---

## Current state (repository)

**Already implemented (DEV-42 + DEV-61):**
- `MainMenuController` with `hasSaveFile` / `startNewGame` / `continueGame` / optional `quit` delegates.
- `MainMenuUI` wiring GameManager delegates through the controller and driving `_continueButton.interactable`.
- `GameManager.StartNewGame()` resets in-memory state, deletes the save via `SaveService.DeleteSave()`, and loads `Platformer`.
- `MainMenu.unity` scene with New Game / Continue / Quit buttons.
- EventSystem present in MainMenu scene (added with Canvas in DEV-42).

**Missing (scope of DEV-62):**
- `MainMenuController` does not branch on save existence — New Game always runs immediately.
- No confirmation dialog prefab, script, or wiring.
- No modal blocking behaviour on the main menu.

---

## File map

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `Assets/Scripts/Core/MainMenuController.cs` | Add optional `requestNewGameConfirmation` ctor param + branching in `OnNewGameClicked` |
| Modify | `Assets/Tests/Editor/Core/MainMenuControllerTests.cs` | Add tests for the new branching behaviour |
| Create | `Assets/Scripts/Core/ConfirmDialogController.cs` | Generic plain-C# dialog routing (`OnYesClicked` / `OnNoClicked`) |
| Create | `Assets/Tests/Editor/Core/ConfirmDialogControllerTests.cs` | Edit Mode tests for the routing class |
| Create | `Assets/Scripts/Core/ConfirmNewGameDialogUI.cs` | MonoBehaviour wrapper — button wiring, Show/Hide, focus restoration |
| Modify | `Assets/Scripts/Core/MainMenuUI.cs` | Serialize dialog ref, pass `requestNewGameConfirmation` lambda to controller |
| Unity Editor | `Assets/Prefabs/UI/ConfirmNewGameDialog.prefab` | Modal panel with label + Yes / No buttons |
| Unity Editor | `Assets/Scenes/MainMenu.unity` | Instantiate dialog prefab, assign ref on `MainMenuUI` |

No new `.asmdef` files. `Axiom.Core.asmdef` already references `UnityEngine.UI`; TextMeshPro is auto-referenced.

---

## Task 1: Extend `MainMenuController` with confirmation branching

**Files:**
- Modify: `Assets/Scripts/Core/MainMenuController.cs`
- Modify: `Assets/Tests/Editor/Core/MainMenuControllerTests.cs`

### Step 1.1 — Add optional `requestNewGameConfirmation` delegate

Edit `Assets/Scripts/Core/MainMenuController.cs`. Add a new field and ctor parameter, and update `OnNewGameClicked` to branch.

```csharp
using System;

namespace Axiom.Core
{
    public sealed class MainMenuController
    {
        private readonly Func<bool> _hasSaveFile;
        private readonly Action _startNewGame;
        private readonly Action _continueGame;
        private readonly Action _requestNewGameConfirmation;
        private readonly Action _quit;

        /// <param name="hasSaveFile">Returns true when a valid save file exists on disk.</param>
        /// <param name="startNewGame">Resets state and loads Platformer for a fresh playthrough.</param>
        /// <param name="continueGame">Loads save data then loads the saved scene.</param>
        /// <param name="requestNewGameConfirmation">
        /// Optional — invoked instead of <paramref name="startNewGame"/> when a save exists,
        /// so the UI can present a modal confirmation dialog. Null means skip confirmation
        /// (legacy/test behaviour: New Game always runs immediately).
        /// </param>
        /// <param name="quit">Optional — exits the application when the Quit button is used.</param>
        public MainMenuController(
            Func<bool> hasSaveFile,
            Action startNewGame,
            Action continueGame,
            Action requestNewGameConfirmation = null,
            Action quit = null)
        {
            _hasSaveFile  = hasSaveFile  ?? throw new ArgumentNullException(nameof(hasSaveFile));
            _startNewGame = startNewGame ?? throw new ArgumentNullException(nameof(startNewGame));
            _continueGame = continueGame ?? throw new ArgumentNullException(nameof(continueGame));
            _requestNewGameConfirmation = requestNewGameConfirmation;
            _quit = quit;
        }

        public bool CanContinue() => _hasSaveFile();

        /// <summary>
        /// Starts a new playthrough. When a save file exists and a confirmation delegate was
        /// supplied, the confirmation is requested instead — the UI is responsible for
        /// invoking <see cref="_startNewGame"/> if the player confirms.
        /// </summary>
        public void OnNewGameClicked()
        {
            if (_requestNewGameConfirmation != null && _hasSaveFile())
            {
                _requestNewGameConfirmation();
                return;
            }

            _startNewGame();
        }

        public void OnContinueClicked()
        {
            if (!CanContinue()) return;
            _continueGame();
        }

        public void OnQuitClicked() => _quit?.Invoke();
    }
}
```

> **Why optional?** Existing `MainMenuControllerTests` constructs the controller with the 3-arg and 4-arg (quit) positional signatures. Adding `requestNewGameConfirmation` between `continueGame` and `quit` would break those tests. Keeping it optional with a default `null` preserves every existing call site and is consistent with the `quit` pattern.

### Step 1.2 — Extend `MainMenuControllerTests`

Edit `Assets/Tests/Editor/Core/MainMenuControllerTests.cs`. **Do not change existing tests** — the `null` default for `requestNewGameConfirmation` keeps their `new MainMenuController(…, …, …)` and `new MainMenuController(…, …, …, quit: …)` call sites valid. Add the four new tests below at the bottom of the class.

Because `MainMenuController` ctor now has an additional optional parameter between `continueGame` and `quit`, the existing `quit: …` named-arg tests continue to compile unchanged. Verify by running all existing tests after Step 1.1 before adding new ones.

New tests to append:

```csharp
// ── OnNewGameClicked confirmation branching tests (DEV-62) ───────────────

[Test]
public void OnNewGameClicked_RequestsConfirmation_WhenSaveExistsAndDelegateProvided()
{
    bool confirmationRequested = false;
    bool startNewGameCalled = false;

    var controller = new MainMenuController(
        hasSaveFile:  () => true,
        startNewGame: () => startNewGameCalled = true,
        continueGame: () => { },
        requestNewGameConfirmation: () => confirmationRequested = true);

    controller.OnNewGameClicked();

    Assert.IsTrue(confirmationRequested, "Confirmation delegate should be invoked when a save exists.");
    Assert.IsFalse(startNewGameCalled, "StartNewGame must not fire directly — wait for the UI to confirm.");
}

[Test]
public void OnNewGameClicked_StartsImmediately_WhenNoSaveExists_EvenWithConfirmationDelegate()
{
    bool confirmationRequested = false;
    bool startNewGameCalled = false;

    var controller = new MainMenuController(
        hasSaveFile:  () => false,
        startNewGame: () => startNewGameCalled = true,
        continueGame: () => { },
        requestNewGameConfirmation: () => confirmationRequested = true);

    controller.OnNewGameClicked();

    Assert.IsFalse(confirmationRequested, "No save means no confirmation needed.");
    Assert.IsTrue(startNewGameCalled, "StartNewGame should run immediately when no save exists.");
}

[Test]
public void OnNewGameClicked_StartsImmediately_WhenSaveExistsButNoConfirmationDelegate()
{
    bool startNewGameCalled = false;

    var controller = new MainMenuController(
        hasSaveFile:  () => true,
        startNewGame: () => startNewGameCalled = true,
        continueGame: () => { },
        requestNewGameConfirmation: null);

    controller.OnNewGameClicked();

    Assert.IsTrue(startNewGameCalled, "With no confirmation delegate, controller preserves legacy direct-start behaviour.");
}

[Test]
public void OnNewGameClicked_EachCallReEvaluatesSaveExistence()
{
    bool hasSave = true;
    int confirmationCount = 0;
    int startCount = 0;

    var controller = new MainMenuController(
        hasSaveFile:  () => hasSave,
        startNewGame: () => startCount++,
        continueGame: () => { },
        requestNewGameConfirmation: () => confirmationCount++);

    controller.OnNewGameClicked();    // save exists → confirmation
    hasSave = false;                  // e.g. user confirmed elsewhere and save was deleted
    controller.OnNewGameClicked();    // no save → direct start

    Assert.AreEqual(1, confirmationCount);
    Assert.AreEqual(1, startCount);
}
```

### Step 1.3 — Run tests

> **Unity Editor task (user):** Window → General → Test Runner → **Edit Mode** → run all tests in `MainMenuControllerTests`.
> **Expected:** All existing tests still PASS (proves the new optional parameter is backwards-compatible) plus the 4 new tests PASS.

### Step 1.4 — Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-62): add confirmation branching to MainMenuController.OnNewGameClicked`
  - `Assets/Scripts/Core/MainMenuController.cs`
  - `Assets/Tests/Editor/Core/MainMenuControllerTests.cs`

---

## Task 2: `ConfirmDialogController` (plain C#) + tests

**Files:**
- Create: `Assets/Scripts/Core/ConfirmDialogController.cs`
- Create: `Assets/Tests/Editor/Core/ConfirmDialogControllerTests.cs`

### Step 2.1 — Implement `ConfirmDialogController`

Create `Assets/Scripts/Core/ConfirmDialogController.cs`:

```csharp
using System;

namespace Axiom.Core
{
    /// <summary>
    /// Pure business logic for a generic Yes/No confirmation dialog. No Unity dependencies.
    /// Instantiated per-open by <see cref="ConfirmNewGameDialogUI"/> with the callbacks
    /// that should fire on confirm and cancel.
    /// </summary>
    public sealed class ConfirmDialogController
    {
        private readonly Action _onConfirm;
        private readonly Action _onCancel;

        /// <param name="onConfirm">Invoked on Yes. Required.</param>
        /// <param name="onCancel">Invoked on No / Esc / B-button / background-click. Required.</param>
        public ConfirmDialogController(Action onConfirm, Action onCancel)
        {
            _onConfirm = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));
            _onCancel  = onCancel  ?? throw new ArgumentNullException(nameof(onCancel));
        }

        public void OnYesClicked() => _onConfirm();
        public void OnNoClicked()  => _onCancel();
    }
}
```

### Step 2.2 — Edit Mode tests

Create `Assets/Tests/Editor/Core/ConfirmDialogControllerTests.cs`:

```csharp
using System;
using Axiom.Core;
using NUnit.Framework;

namespace Axiom.Tests.Editor.Core
{
    public class ConfirmDialogControllerTests
    {
        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenOnConfirmIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ConfirmDialogController(onConfirm: null, onCancel: () => { }));
        }

        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenOnCancelIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ConfirmDialogController(onConfirm: () => { }, onCancel: null));
        }

        [Test]
        public void OnYesClicked_InvokesOnConfirm_NotOnCancel()
        {
            bool confirmCalled = false;
            bool cancelCalled = false;

            var controller = new ConfirmDialogController(
                onConfirm: () => confirmCalled = true,
                onCancel:  () => cancelCalled = true);

            controller.OnYesClicked();

            Assert.IsTrue(confirmCalled);
            Assert.IsFalse(cancelCalled);
        }

        [Test]
        public void OnNoClicked_InvokesOnCancel_NotOnConfirm()
        {
            bool confirmCalled = false;
            bool cancelCalled = false;

            var controller = new ConfirmDialogController(
                onConfirm: () => confirmCalled = true,
                onCancel:  () => cancelCalled = true);

            controller.OnNoClicked();

            Assert.IsTrue(cancelCalled);
            Assert.IsFalse(confirmCalled);
        }
    }
}
```

### Step 2.3 — Run tests

> **Unity Editor task (user):** Test Runner → Edit Mode → run all tests in `ConfirmDialogControllerTests`. **Expected:** 4 PASS.

### Step 2.4 — Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-62): add generic ConfirmDialogController with Edit Mode tests`
  - `Assets/Scripts/Core/ConfirmDialogController.cs`
  - `Assets/Scripts/Core/ConfirmDialogController.cs.meta`
  - `Assets/Tests/Editor/Core/ConfirmDialogControllerTests.cs`
  - `Assets/Tests/Editor/Core/ConfirmDialogControllerTests.cs.meta`

---

## Task 3: `ConfirmNewGameDialogUI` MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Core/ConfirmNewGameDialogUI.cs`

### Step 3.1 — Implement the MonoBehaviour

Create `Assets/Scripts/Core/ConfirmNewGameDialogUI.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// Modal confirmation dialog shown before <see cref="GameManager.StartNewGame"/> runs while
    /// a save file exists. MonoBehaviour handles lifecycle only: activates/deactivates the dialog
    /// root, wires Yes/No buttons through <see cref="ConfirmDialogController"/>, and restores
    /// EventSystem focus to the caller-supplied focus target on dismiss.
    ///
    /// Keyboard / gamepad: the UI Input Module's Submit binding (Enter / gamepad A) activates the
    /// currently-selected Unity Button; Cancel (Esc / gamepad B) is forwarded through
    /// <see cref="Update"/> via EventSystem.current.currentInputModule.
    /// </summary>
    public class ConfirmNewGameDialogUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Yes, start new game — confirms destruction of existing save.")]
        private Button _yesButton;

        [SerializeField]
        [Tooltip("No, go back — dismisses the dialog without touching state.")]
        private Button _noButton;

        [SerializeField]
        [Tooltip("Optional — GameObject to receive EventSystem focus when the dialog hides. " +
                 "Typically the New Game button in the main menu.")]
        private GameObject _focusOnHide;

        private ConfirmDialogController _controller;
        private Action _pendingConfirm;

        /// <summary>True while the dialog is active on screen.</summary>
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>
        /// Opens the dialog. <paramref name="onConfirm"/> fires when the player clicks Yes;
        /// No / Esc / B-button dismiss silently. The dialog hides itself after either path.
        /// </summary>
        public void Show(Action onConfirm)
        {
            if (onConfirm == null) throw new ArgumentNullException(nameof(onConfirm));

            _pendingConfirm = onConfirm;
            _controller = new ConfirmDialogController(
                onConfirm: HandleConfirm,
                onCancel:  HandleCancel);

            gameObject.SetActive(true);

            // Default focus on No — safer default for a destructive prompt.
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && _noButton != null)
                eventSystem.SetSelectedGameObject(_noButton.gameObject);
        }

        /// <summary>Explicit hide used by integration wiring. Does not invoke either callback.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && _focusOnHide != null)
                eventSystem.SetSelectedGameObject(_focusOnHide);
        }

        private void Awake()
        {
            // Dialog starts hidden; MainMenuUI calls Show() on demand.
            gameObject.SetActive(false);

            if (_yesButton != null) _yesButton.onClick.AddListener(OnYesButton);
            if (_noButton  != null) _noButton .onClick.AddListener(OnNoButton);
        }

        private void OnDestroy()
        {
            _yesButton?.onClick.RemoveAllListeners();
            _noButton ?.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            // Esc / gamepad B — UI Input Module routes Cancel via BaseInputModule; if a
            // selected element is present, Unity will fire OnCancel there. For a dialog-level
            // cancel (nothing selected, or player pressed Cancel while a non-cancel element is
            // selected) we poll the UI module's Cancel state.
            if (!IsOpen) return;
            if (_controller == null) return;

            BaseInputModule module = EventSystem.current != null ? EventSystem.current.currentInputModule : null;
            if (module == null) return;

            // UI Input Module's cancel action fires once per press; this path is intentionally
            // defensive — the Button's own cancel handler covers the common case.
            if (module.input != null && module.input.GetButtonDown("Cancel"))
                _controller.OnNoClicked();
        }

        private void OnYesButton() => _controller?.OnYesClicked();
        private void OnNoButton()  => _controller?.OnNoClicked();

        private void HandleConfirm()
        {
            Action confirmed = _pendingConfirm;
            _pendingConfirm = null;
            _controller = null;

            Hide();
            confirmed?.Invoke();
        }

        private void HandleCancel()
        {
            _pendingConfirm = null;
            _controller = null;
            Hide();
        }
    }
}
```

> **Why the `input.GetButtonDown("Cancel")` polling in Update?** The dialog must cancel even when the currently-selected element does not have its own `IsCancelHandler`. Buttons inherit cancel only via the navigation module; for belt-and-braces behaviour on both Standalone Input Module and the New Input System UI module, a single-frame poll is the simplest reliable fallback. When the Yes button is focused and the player presses B/Esc, this path triggers cancel.
>
> **Modal input blocking:** the dialog's root panel uses a full-screen `Image` with raycast target enabled (see Task 4 prefab setup). Any click outside Yes/No hits the panel, not the main-menu buttons behind — that's how the AC "dialog blocks background input" is satisfied. We intentionally do not add a CanvasGroup.interactable = false toggle on the main menu because the panel raycast is sufficient and keeps scene complexity low.

### Step 3.2 — Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-62): add ConfirmNewGameDialogUI MonoBehaviour`
  - `Assets/Scripts/Core/ConfirmNewGameDialogUI.cs`
  - `Assets/Scripts/Core/ConfirmNewGameDialogUI.cs.meta`

---

## Task 4: Wire `MainMenuUI` to the dialog

**Files:**
- Modify: `Assets/Scripts/Core/MainMenuUI.cs`

### Step 4.1 — Serialize dialog reference and pass confirmation lambda

Edit `Assets/Scripts/Core/MainMenuUI.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Core
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button _newGameButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _quitButton;

        [SerializeField]
        [Tooltip("Modal dialog shown before StartNewGame when a save file exists. " +
                 "Optional — when null, New Game runs immediately with no confirmation (legacy behaviour).")]
        private ConfirmNewGameDialogUI _confirmNewGameDialog;

        private MainMenuController _controller;

        private void Start()
        {
            _controller = new MainMenuController(
                hasSaveFile:  () => GameManager.Instance?.HasSaveFile() ?? false,
                startNewGame: () => GameManager.Instance?.StartNewGame(),
                continueGame: () => GameManager.Instance?.TryContinueGame(),
                requestNewGameConfirmation: BuildConfirmationDelegate(),
                quit: QuitApplication);

            _continueButton.interactable = _controller.CanContinue();

            _newGameButton.onClick.AddListener(_controller.OnNewGameClicked);
            _continueButton.onClick.AddListener(_controller.OnContinueClicked);
            if (_quitButton != null)
                _quitButton.onClick.AddListener(_controller.OnQuitClicked);
        }

        private System.Action BuildConfirmationDelegate()
        {
            if (_confirmNewGameDialog == null)
                return null;   // No dialog assigned → controller falls back to direct start.

            return () => _confirmNewGameDialog.Show(
                onConfirm: () => GameManager.Instance?.StartNewGame());
        }

        private void OnDestroy()
        {
            _newGameButton?.onClick.RemoveAllListeners();
            _continueButton?.onClick.RemoveAllListeners();
            _quitButton?.onClick.RemoveAllListeners();
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
```

> **Order of early-exit vs. null-guards:** `BuildConfirmationDelegate` checks `_confirmNewGameDialog == null` first and returns before touching `GameManager.Instance` — the returned lambda will only run later when the button is clicked, by which point `Instance` is either set or the existing `?.` guard covers it. No parameter is dereferenced before its guard.

### Step 4.2 — Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-62): wire MainMenuUI to ConfirmNewGameDialogUI`
  - `Assets/Scripts/Core/MainMenuUI.cs`

---

## Task 5: Build the `ConfirmNewGameDialog` prefab + scene wiring

All steps below are **Unity Editor tasks (user)**.

### Step 5.1 — Build the prefab

> **Unity Editor task (user):** Open `Assets/Scenes/MainMenu.unity`. In the existing Canvas, right-click → Create Empty child named `ConfirmNewGameDialog`.
>
> Add children:
> 1. **Background** — UI → Image. Anchors: stretch-stretch, offsets all zero. Color RGBA `0, 0, 0, 180` (dimmed backdrop). Raycast Target **enabled** — this is what blocks clicks on the main-menu buttons.
> 2. **Panel** — UI → Image. Centered, size `560×220`. Any panel background sprite or a flat color the game already uses.
>    - Child **MessageLabel** — UI → Text - TextMeshPro. Text: `Are you sure? Starting a new game now will delete your progress.` Wrap enabled, center-aligned, positioned in the top half of the panel.
>    - Child **YesButton** — UI → Button - TextMeshPro. Label: `Yes, start new game`. Positioned bottom-left of panel.
>    - Child **NoButton** — UI → Button - TextMeshPro. Label: `No, go back`. Positioned bottom-right of panel.

### Step 5.2 — Configure Button navigation

> **Unity Editor task (user):** On **YesButton**, Inspector → Button component → Navigation → **Explicit**. Set `Select On Right` to NoButton. Leave other directions at None.
>
> On **NoButton**, Navigation → Explicit → `Select On Left` to YesButton. Leave other directions at None.
>
> This ensures gamepad / arrow-key focus moves cleanly between the two options with no escape to the main menu while the dialog is open.

### Step 5.3 — Attach `ConfirmNewGameDialogUI`

> **Unity Editor task (user):** Select the `ConfirmNewGameDialog` root GameObject. Add Component → `ConfirmNewGameDialogUI`.
>
> Assign fields in the Inspector:
> - **Yes Button** → the YesButton child
> - **No Button** → the NoButton child
> - **Focus On Hide** → the MainMenu scene's `NewGameButton` GameObject (so focus returns cleanly after dismiss)

### Step 5.4 — Save as prefab

> **Unity Editor task (user):** Drag `ConfirmNewGameDialog` from the Hierarchy into `Assets/Prefabs/UI/` to create the prefab. Keep the instance in the MainMenu scene as an override-free prefab instance.

### Step 5.5 — Wire `MainMenuUI`

> **Unity Editor task (user):** Select the MainMenuUI GameObject in the scene. Drag the `ConfirmNewGameDialog` scene instance into the new **Confirm New Game Dialog** field on `MainMenuUI`.

### Step 5.6 — Smoke test in Play Mode

> **Unity Editor task (user):** Enter Play Mode on `MainMenu.unity`.
>
> **Scenario A — no save:**
> - Delete any existing save (`%AppData%/LocalLow/<Company>/<Product>/savegame.json` on Windows, `~/Library/Application Support/<Company>/<Product>/savegame.json` on macOS).
> - Return to MainMenu. Click **New Game**. Expected: loads Platformer immediately, **no dialog**.
>
> **Scenario B — save exists, confirm:**
> - Start a game and save (let autosave run or trigger a checkpoint). Return to MainMenu.
> - Click **New Game**. Expected: dialog appears, dim backdrop covers screen, **No** has focus.
> - Press Enter or click **Yes, start new game**. Expected: dialog dismisses, save file is deleted, Platformer loads fresh.
>
> **Scenario C — save exists, cancel via mouse:**
> - With a save present, click **New Game** → click **No, go back**. Expected: dialog hides, focus returns to New Game button, save file untouched (verify on disk), player can still use Continue normally.
>
> **Scenario D — keyboard cancel:**
> - With a save present, click **New Game** → press **Esc**. Expected: same as Scenario C.
>
> **Scenario E — gamepad navigation (if a gamepad is connected):**
> - With a save present, click **New Game** → press **B** on gamepad. Expected: dialog cancels.
> - Re-open dialog → press **A**. Expected: Yes-path confirms and starts new game.
>
> **Scenario F — modal blocking:**
> - With the dialog open, click on the New Game / Continue / Quit buttons behind it. Expected: they do **not** fire — the Background image absorbs the raycast.

### Step 5.7 — Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-62): add ConfirmNewGameDialog prefab and wire into MainMenu scene`
  - `Assets/Prefabs/UI/ConfirmNewGameDialog.prefab`
  - `Assets/Prefabs/UI/ConfirmNewGameDialog.prefab.meta`
  - `Assets/Scenes/MainMenu.unity`

---

## Final state (checklist against Jira DEV-62 Acceptance Criteria)

- [ ] Clicking **New Game** with a save present shows a modal dialog with the exact message: `Are you sure? Starting a new game now will delete your progress.`
- [ ] Dialog has **Yes, start new game** and **No, go back** buttons.
- [ ] **Yes** calls `GameManager.StartNewGame()` (save deleted, fresh state, loads Platformer).
- [ ] **No** dismisses without touching save file or state.
- [ ] With no save file, **New Game** starts immediately (no dialog).
- [ ] Dialog background image blocks clicks to main-menu buttons behind it.
- [ ] Esc / gamepad B cancels the dialog.
- [ ] Enter / gamepad A confirms the focused button (default focus is No).
- [ ] Focus returns to the New Game button on dismiss.
- [ ] All Edit Mode tests pass (`MainMenuControllerTests`, `ConfirmDialogControllerTests`).

---

## Self-review

### C# guard clause ordering

**`MainMenuController.OnNewGameClicked`:**
```
if (_requestNewGameConfirmation != null && _hasSaveFile()) { _requestNewGameConfirmation(); return; }
_startNewGame();
```
The null-delegate check is evaluated first (short-circuit), so `_hasSaveFile()` is only called when a delegate is present — which is always the case in production wiring. No parameter is dereferenced before its guard.

**`ConfirmDialogController` constructor:**
Both null guards throw before any field assignment. Correct order.

**`ConfirmNewGameDialogUI.Show`:**
`if (onConfirm == null) throw …` fires before any side-effect (field assignment, GameObject.SetActive). Correct order.

**`MainMenuUI.BuildConfirmationDelegate`:**
Early-return when `_confirmNewGameDialog` is null, before the lambda would capture `GameManager.Instance`. Correct.

### Test coverage gaps

| Method | Null arg | Empty/false case | Happy path |
|--------|----------|-----------------|------------|
| `MainMenuController` ctor (new `requestNewGameConfirmation` param) | Covered implicitly by existing 3 null-arg tests; new param is optional so no separate null-guard test needed (null is valid) | — | — |
| `MainMenuController.OnNewGameClicked` (confirmation branch) | — | ✓ no-save with delegate → direct start; ✓ save exists but no delegate → direct start (legacy) | ✓ save exists + delegate → request confirmation; ✓ re-evaluates save existence per call |
| `ConfirmDialogController` ctor | ✓ two null-arg tests | — | — |
| `ConfirmDialogController.OnYesClicked` | — | — | ✓ invokes confirm, not cancel |
| `ConfirmDialogController.OnNoClicked` | — | — | ✓ invokes cancel, not confirm |
| `ConfirmNewGameDialogUI` | Not tested in Edit Mode — requires Unity scene + EventSystem. Smoke-tested in Play Mode Scenarios A–F. | — | — |

`ConfirmNewGameDialogUI` is MonoBehaviour glue with no branching logic beyond null checks against Inspector fields (Awake sets inactive, button listeners, Update cancel poll). Following the project standard ("test the plain C# class in Edit Mode; test the MonoBehaviour wrapper in Play Mode if at all"), the Play Mode smoke-test scenarios are the coverage path — a PlayMode test is not worth the scaffolding for two button clicks.

### UVCS staged file audit

| Task | Files created/modified | In check-in step? |
|------|-----------------------|-------------------|
| 1 | `MainMenuController.cs` (mod), `MainMenuControllerTests.cs` (mod) | ✓ Step 1.4 — no new .meta |
| 2 | `ConfirmDialogController.cs` + `.meta`, `ConfirmDialogControllerTests.cs` + `.meta` | ✓ Step 2.4 |
| 3 | `ConfirmNewGameDialogUI.cs` + `.meta` | ✓ Step 3.2 |
| 4 | `MainMenuUI.cs` (mod) | ✓ Step 4.2 — no new .meta |
| 5 | `ConfirmNewGameDialog.prefab` + `.meta`, `MainMenu.unity` (mod) | ✓ Step 5.7 — no new .meta for the scene |

### Method signature consistency

| Implementation | Tests / callers |
|----------------|-----------------|
| `MainMenuController(Func<bool>, Action, Action, Action = null, Action = null)` | Existing tests (3-arg and 4-arg `quit:`) compile unchanged; new tests use `requestNewGameConfirmation:` named arg ✓ |
| `MainMenuController.OnNewGameClicked()` | Tests call `controller.OnNewGameClicked()` ✓ |
| `ConfirmDialogController(Action onConfirm, Action onCancel)` | Tests use `new ConfirmDialogController(onConfirm: …, onCancel: …)` ✓ |
| `ConfirmNewGameDialogUI.Show(Action onConfirm)` | `MainMenuUI.BuildConfirmationDelegate` calls `_confirmNewGameDialog.Show(onConfirm: …)` ✓ |
| Namespace `Axiom.Core` | All new files declare `namespace Axiom.Core` ✓; tests use `using Axiom.Core;` ✓ |

### Unity Editor task isolation

Every Unity Editor action is wrapped in its own `> **Unity Editor task (user):**` callout. No code-and-editor steps share a checkbox. ✓

---

**Plan complete and saved to** `docs/superpowers/plans/2026-04-15-dev-62-confirm-overwrite-new-game.md`.

**Execution options:**

1. **Subagent-Driven (recommended)** — one subagent per task, review between tasks. Use `superpowers:subagent-driven-development`.
2. **Executing-Plans (interactive)** — you drive task-by-task with review checkpoints. Use `superpowers:executing-plans`.

Which approach do you want?