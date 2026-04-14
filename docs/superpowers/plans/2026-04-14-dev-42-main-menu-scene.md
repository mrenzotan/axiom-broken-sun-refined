# DEV-42: Main Menu Scene — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `MainMenu` scene that gives the player a "New Game" or "Continue" entry point; the Continue button is only interactive when a valid save file exists on disk (checked via `GameManager.HasSaveFile()`), and New Game resets all player state before loading the Platformer.

**Architecture:** `MainMenuController` (plain C#, `Axiom.Core`) owns the two button actions as `Func<bool>`/`Action` delegate injections — no Unity dependencies, so it is fully testable in Edit Mode. `MainMenuUI` (MonoBehaviour, `Axiom.Core`) handles Unity lifecycle only: creates `MainMenuController` in `Start()`, wires `Button.onClick` listeners, and drives `interactable` on the Continue button. `GameManager` gains a `StartNewGame()` method that resets `PlayerState` to defaults and loads the Platformer scene. No new `.asmdef` is needed — `Axiom.Core.asmdef` gains `"UnityEngine.UI"` to the references so it can refer to `Button`; `CoreTests.asmdef` already references everything needed.

**Tech Stack:** Unity 6 LTS, URP 2D, C# 9, Unity UI (Canvas + Button + TextMeshPro), NUnit via Unity Test Framework (Edit Mode in `Assets/Tests/Editor/Core/`), UVCS for check-ins.

---

## Project context (read before coding)

| Source | What applies to this ticket |
|--------|-----------------------------|
| `docs/GAME_PLAN.md` §Phase 5 | New Game / Continue flow; save file path via `Application.persistentDataPath` (owned by DEV-41 `SaveService`). |
| `docs/GAME_PLAN.md` §6 Key Architectural Decisions | `GameManager` DontDestroyOnLoad is the sole cross-scene state owner; no static singletons other than `GameManager`; MonoBehaviours = lifecycle only. |
| `CLAUDE.md` | MonoBehaviour rule; no `Assets/Scripts/UI/` top-level folder; namespace style `Axiom.Core`. |
| `Assets/Scripts/Core/GameManager.cs` | Already has `HasSaveFile()`, `TryContinueGame()`, `ApplySaveData()`, `PersistToDisk()` (DEV-41). Needs `StartNewGame()` added here. |
| `Assets/Scripts/Core/SaveService.cs` | `HasSave()`, `TryLoad()` — DEV-41 complete; no changes needed. |
| `Assets/Prefabs/Core/GameManager.prefab` | Place this prefab in the MainMenu scene so `GameManager` is available on first load. |
| `docs/VERSION_CONTROL.md` | UVCS is source of truth; scenes and prefabs must be checked in through UVCS, not git. |

---

## Current state (repository)

**Already implemented (DEV-41):**

- `SaveService` — `HasSave()`, `Save()`, `TryLoad()`, `DeleteSave()`; directory injectable for tests.
- `GameManager` — `HasSaveFile()`, `TryContinueGame()`, `BuildSaveData()`, `ApplySaveData()`, `PersistToDisk()`, `CaptureWorldSnapshot()`, `PlayerState` ownership, `DontDestroyOnLoad` singleton pattern.
- `PlayerState` — level, XP, HP/MP, spells, inventory, world position, checkpoint IDs with full mutators.
- `Assets/Prefabs/Core/GameManager.prefab` — exists; drop into MainMenu scene.

**Missing (scope of DEV-42):**

- `GameManager.StartNewGame()` — resets `PlayerState` and loads Platformer.
- `MainMenuController` — plain C# class with `CanContinue()`, `OnNewGameClicked()`, `OnContinueClicked()`.
- `MainMenuUI` — MonoBehaviour wrapper that wires the above to Unity `Button` components.
- `Assets/Scenes/MainMenu.unity` — the scene itself with Canvas, buttons, GameManager prefab.
- Build Settings: MainMenu set as scene index 0.

**Scene transition note:** The Acceptance Criteria reference the animated transition from DEV-34. That system is not yet in the repository. This plan uses `SceneManager.LoadScene` directly (same pattern as `BattleController`). Mark transition call sites with `// TODO(DEV-34): replace with SceneTransitionController when implemented.` and integrate in the DEV-34 ticket.

---

## File map

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `Assets/Scripts/Core/GameManager.cs` | Add `StartNewGame()` |
| Modify | `Assets/Scripts/Core/Axiom.Core.asmdef` | Add `"UnityEngine.UI"` to references |
| Create | `Assets/Scripts/Core/MainMenuController.cs` | Pure logic: `CanContinue`, `OnNewGameClicked`, `OnContinueClicked` |
| Create | `Assets/Scripts/Core/MainMenuUI.cs` | MonoBehaviour: wires buttons, sets interactable |
| Create | `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs` | Edit Mode tests for `StartNewGame()` |
| Create | `Assets/Tests/Editor/Core/MainMenuControllerTests.cs` | Edit Mode tests for `MainMenuController` |
| Unity Editor | `Assets/Scenes/MainMenu.unity` | Scene with Canvas, two buttons, GameManager prefab |

No new `.asmdef` files.

---

## Task 1: Add `GameManager.StartNewGame()` + tests

**Files:**

- Modify: `Assets/Scripts/Core/GameManager.cs`
- Create: `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs`

### Step 1.1 — Add `StartNewGame()` and `LoadScene()` helper to `GameManager`

Open `Assets/Scripts/Core/GameManager.cs`. Make **two additions**:

**A) Add the public `StartNewGame()` method alongside `TryContinueGame()`:**

```csharp
/// <summary>
/// Resets all player state to new-game defaults and loads the first scene.
/// Called by MainMenuUI when the player begins a fresh playthrough.
/// </summary>
public void StartNewGame()
{
    PlayerState = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
    ClearPendingBattle();
    LoadScene("Platformer");
}
```

**B) Add a private `LoadScene()` helper at the bottom of the private section (alongside `EnsurePlayerState` etc.):**

```csharp
/// <summary>
/// Single entry point for all scene loads originating from GameManager.
/// DEV-34: replace the body of this method with SceneTransitionController
/// when that system is implemented — no other call sites need to change.
/// </summary>
private void LoadScene(string sceneName)
{
    SceneManager.LoadScene(sceneName);
}
```

**C) Update the existing `TryContinueGame()` to call `LoadScene()` instead of `SceneManager.LoadScene()` directly:**

```csharp
// Change this line in TryContinueGame():
SceneManager.LoadScene(sceneToLoad);
// to:
LoadScene(sceneToLoad);
```

> **Why a private helper?** All scene-loading logic from `GameManager` now flows through one method. When DEV-34's `SceneTransitionController` is ready, the implementing dev changes **only `LoadScene()`** — `StartNewGame()`, `TryContinueGame()`, and any future callers are untouched. No scattered `TODO` comments to hunt down; one method, one change.

### Step 1.2 — Write Edit Mode tests

Create `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs`:

```csharp
using Axiom.Core;
using NUnit.Framework;
using UnityEngine;

namespace CoreTests
{
    public class GameManagerNewGameTests
    {
        private GameObject _gameManagerObject;
        private GameManager _gameManager;

        [SetUp]
        public void SetUp()
        {
            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameManagerObject != null)
                Object.DestroyImmediate(_gameManagerObject);
        }

        [Test]
        public void StartNewGame_ResetsPlayerLevelToOne()
        {
            _gameManager.PlayerState.ApplyProgression(level: 7, xp: 9000);

            _gameManager.StartNewGame(); // SceneManager.LoadScene is a no-op in Edit Mode

            Assert.AreEqual(1, _gameManager.PlayerState.Level);
        }

        [Test]
        public void StartNewGame_ResetsXpToZero()
        {
            _gameManager.PlayerState.ApplyProgression(level: 3, xp: 500);

            _gameManager.StartNewGame();

            Assert.AreEqual(0, _gameManager.PlayerState.Xp);
        }

        [Test]
        public void StartNewGame_ResetsCurrentHpToMaxHp()
        {
            _gameManager.PlayerState.SetCurrentHp(10);

            _gameManager.StartNewGame();

            Assert.AreEqual(_gameManager.PlayerState.MaxHp, _gameManager.PlayerState.CurrentHp);
        }

        [Test]
        public void StartNewGame_ResetsCurrentMpToMaxMp()
        {
            _gameManager.PlayerState.SetCurrentMp(0);

            _gameManager.StartNewGame();

            Assert.AreEqual(_gameManager.PlayerState.MaxMp, _gameManager.PlayerState.CurrentMp);
        }

        [Test]
        public void StartNewGame_ClearsUnlockedSpells()
        {
            _gameManager.PlayerState.SetUnlockedSpellIds(new[] { "spell_a", "spell_b" });

            _gameManager.StartNewGame();

            Assert.AreEqual(0, _gameManager.PlayerState.UnlockedSpellIds.Count);
        }

        [Test]
        public void StartNewGame_ClearsInventory()
        {
            _gameManager.PlayerState.SetInventoryItemIds(new[] { "potion_hp", "potion_hp" });

            _gameManager.StartNewGame();

            Assert.AreEqual(0, _gameManager.PlayerState.InventoryItemIds.Count);
        }

        [Test]
        public void StartNewGame_ReturnsMaxHpOf100()
        {
            _gameManager.StartNewGame();

            Assert.AreEqual(100, _gameManager.PlayerState.MaxHp);
        }
    }
}
```

### Step 1.3 — Run tests

> **Unity Editor task (user):** Window → General → Test Runner → **Edit Mode** → run all tests in `GameManagerNewGameTests`.
> **Expected:** All PASS. `SceneManager.LoadScene("Platformer")` is a no-op in Edit Mode — no errors.

### Step 1.4 — Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-42): add GameManager.StartNewGame and centralise scene loading`
  - `Assets/Scripts/Core/GameManager.cs` *(modified: StartNewGame() + private LoadScene() helper + TryContinueGame() updated to use helper — no new .meta)*
  - `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs`
  - `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs.meta`

---

## Task 2: `MainMenuController` (plain C#) + tests

**Files:**

- Create: `Assets/Scripts/Core/MainMenuController.cs`
- Create: `Assets/Tests/Editor/Core/MainMenuControllerTests.cs`

### Step 2.1 — Implement `MainMenuController`

Create `Assets/Scripts/Core/MainMenuController.cs`:

```csharp
using System;

namespace Axiom.Core
{
    /// <summary>
    /// Pure business logic for the main menu. No Unity dependencies — fully testable in Edit Mode.
    /// Instantiated by <see cref="MainMenuUI"/> with real GameManager delegates in Start().
    /// Test code injects stubs via the constructor.
    /// </summary>
    public sealed class MainMenuController
    {
        private readonly Func<bool> _hasSaveFile;
        private readonly Action _startNewGame;
        private readonly Action _continueGame;

        /// <param name="hasSaveFile">Returns true when a valid save file exists on disk.</param>
        /// <param name="startNewGame">Resets state and loads Platformer for a fresh playthrough.</param>
        /// <param name="continueGame">Loads save data then loads the saved scene.</param>
        public MainMenuController(Func<bool> hasSaveFile, Action startNewGame, Action continueGame)
        {
            _hasSaveFile  = hasSaveFile  ?? throw new ArgumentNullException(nameof(hasSaveFile));
            _startNewGame = startNewGame ?? throw new ArgumentNullException(nameof(startNewGame));
            _continueGame = continueGame ?? throw new ArgumentNullException(nameof(continueGame));
        }

        /// <summary>Returns true when a valid save file exists on disk.</summary>
        public bool CanContinue() => _hasSaveFile();

        /// <summary>Starts a fresh playthrough. Always callable regardless of save state.</summary>
        public void OnNewGameClicked() => _startNewGame();

        /// <summary>Resumes from save. No-op when <see cref="CanContinue"/> is false.</summary>
        public void OnContinueClicked()
        {
            if (!CanContinue()) return;
            _continueGame();
        }
    }
}
```

### Step 2.2 — Write Edit Mode tests

Create `Assets/Tests/Editor/Core/MainMenuControllerTests.cs`:

```csharp
using System;
using Axiom.Core;
using NUnit.Framework;

namespace CoreTests
{
    public class MainMenuControllerTests
    {
        // ── Constructor guard tests ──────────────────────────────────────────

        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenHasSaveFileIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MainMenuController(hasSaveFile: null, startNewGame: () => { }, continueGame: () => { }));
        }

        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenStartNewGameIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MainMenuController(hasSaveFile: () => false, startNewGame: null, continueGame: () => { }));
        }

        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenContinueGameIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MainMenuController(hasSaveFile: () => false, startNewGame: () => { }, continueGame: null));
        }

        // ── CanContinue tests ────────────────────────────────────────────────

        [Test]
        public void CanContinue_ReturnsFalse_WhenNoSaveFileExists()
        {
            var controller = new MainMenuController(() => false, () => { }, () => { });
            Assert.IsFalse(controller.CanContinue());
        }

        [Test]
        public void CanContinue_ReturnsTrue_WhenSaveFileExists()
        {
            var controller = new MainMenuController(() => true, () => { }, () => { });
            Assert.IsTrue(controller.CanContinue());
        }

        // ── OnNewGameClicked tests ───────────────────────────────────────────

        [Test]
        public void OnNewGameClicked_InvokesStartNewGame_WhenNoSaveExists()
        {
            bool called = false;
            var controller = new MainMenuController(
                hasSaveFile:  () => false,
                startNewGame: () => called = true,
                continueGame: () => { });

            controller.OnNewGameClicked();

            Assert.IsTrue(called);
        }

        [Test]
        public void OnNewGameClicked_InvokesStartNewGame_EvenWhenSaveFileExists()
        {
            bool called = false;
            var controller = new MainMenuController(
                hasSaveFile:  () => true,
                startNewGame: () => called = true,
                continueGame: () => { });

            controller.OnNewGameClicked();

            Assert.IsTrue(called);
        }

        // ── OnContinueClicked tests ──────────────────────────────────────────

        [Test]
        public void OnContinueClicked_DoesNotInvokeContinueGame_WhenNoSaveExists()
        {
            bool called = false;
            var controller = new MainMenuController(
                hasSaveFile:  () => false,
                startNewGame: () => { },
                continueGame: () => called = true);

            controller.OnContinueClicked();

            Assert.IsFalse(called);
        }

        [Test]
        public void OnContinueClicked_InvokesContinueGame_WhenSaveExists()
        {
            bool called = false;
            var controller = new MainMenuController(
                hasSaveFile:  () => true,
                startNewGame: () => { },
                continueGame: () => called = true);

            controller.OnContinueClicked();

            Assert.IsTrue(called);
        }

        [Test]
        public void OnContinueClicked_DoesNotInvokeStartNewGame()
        {
            bool newGameCalled = false;
            var controller = new MainMenuController(
                hasSaveFile:  () => true,
                startNewGame: () => newGameCalled = true,
                continueGame: () => { });

            controller.OnContinueClicked();

            Assert.IsFalse(newGameCalled);
        }
    }
}
```

### Step 2.3 — Run tests

> **Unity Editor task (user):** Test Runner → **Edit Mode** → run all tests in `MainMenuControllerTests`.
> **Expected:** All 10 tests PASS. No Unity scene or GameManager instance needed.

### Step 2.4 — Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-42): add MainMenuController with delegate injection and Edit Mode tests`
  - `Assets/Scripts/Core/MainMenuController.cs`
  - `Assets/Scripts/Core/MainMenuController.cs.meta`
  - `Assets/Tests/Editor/Core/MainMenuControllerTests.cs`
  - `Assets/Tests/Editor/Core/MainMenuControllerTests.cs.meta`

---

## Task 3: `MainMenuUI` MonoBehaviour + asmdef update

**Files:**

- Modify: `Assets/Scripts/Core/Axiom.Core.asmdef`
- Create: `Assets/Scripts/Core/MainMenuUI.cs`

### Step 3.1 — Add `UnityEngine.UI` to `Axiom.Core.asmdef`

Open `Assets/Scripts/Core/Axiom.Core.asmdef` and add `"UnityEngine.UI"` to the `references` array so `MainMenuUI` can use the `Button` type:

```json
{
    "name": "Axiom.Core",
    "references": [
        "Axiom.Data",
        "UnityEngine.UI"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

> After saving, Unity will recompile. Verify no compile errors appear in the Console.

### Step 3.2 — Implement `MainMenuUI`

Create `Assets/Scripts/Core/MainMenuUI.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// MonoBehaviour wrapper for <see cref="MainMenuController"/>.
    /// Handles Unity lifecycle only: creates the controller in Start(),
    /// wires button listeners, and drives Continue interactability.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The New Game button on the main menu Canvas.")]
        private Button _newGameButton;

        [SerializeField]
        [Tooltip("The Continue button on the main menu Canvas.")]
        private Button _continueButton;

        private MainMenuController _controller;

        private void Start()
        {
            _controller = new MainMenuController(
                hasSaveFile:  () => GameManager.Instance?.HasSaveFile() ?? false,
                startNewGame: () => GameManager.Instance?.StartNewGame(),
                continueGame: () => GameManager.Instance?.TryContinueGame());

            _continueButton.interactable = _controller.CanContinue();

            _newGameButton.onClick.AddListener(_controller.OnNewGameClicked);
            _continueButton.onClick.AddListener(_controller.OnContinueClicked);
        }

        private void OnDestroy()
        {
            _newGameButton?.onClick.RemoveAllListeners();
            _continueButton?.onClick.RemoveAllListeners();
        }
    }
}
```

> **Guard clause note:** `GameManager.Instance?.` null-conditional guards are intentional — they allow the Battle and Platformer scenes to open in isolation during development without a GameManager present, matching the pattern used throughout `BattleController`.

### Step 3.3 — Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-42): add MainMenuUI MonoBehaviour and update Axiom.Core asmdef`
  - `Assets/Scripts/Core/Axiom.Core.asmdef` *(modified — no new .meta)*
  - `Assets/Scripts/Core/MainMenuUI.cs`
  - `Assets/Scripts/Core/MainMenuUI.cs.meta`

---

## Task 4: Build the `MainMenu` scene

All steps below are **Unity Editor tasks (user)**. Claude does not author scene files.

### Step 4.1 — Create the scene

> **Unity Editor task (user):** File → New Scene (Basic 2D built-in) → Save As `Assets/Scenes/MainMenu.unity`.

### Step 4.2 — Add GameManager prefab

> **Unity Editor task (user):** Drag `Assets/Prefabs/Core/GameManager.prefab` into the MainMenu scene Hierarchy. This gives `GameManager.Instance` a value when the game starts from this scene.

### Step 4.3 — Set up the UI Canvas

> **Unity Editor task (user):** In the Hierarchy, right-click → UI → Canvas. Set Render Mode to **Screen Space – Overlay**. Add a child `Panel` (optional background), then add two child `Button - TextMeshPro` objects named `NewGameButton` and `ContinueButton`. Set their label text to **"New Game"** and **"Continue"** respectively.

### Step 4.4 — Attach `MainMenuUI`

> **Unity Editor task (user):** Select the Canvas (or a dedicated "MainMenuUI" empty child GameObject). In Inspector → Add Component → search for `MainMenuUI`. Drag `NewGameButton` into the **New Game Button** field, and `ContinueButton` into the **Continue Button** field.

### Step 4.5 — Set Continue button colors for disabled state

> **Unity Editor task (user):** Select `ContinueButton`. In the `Button` component, open the **Colors** block. Set **Disabled Color** to a clearly greyed-out value (e.g. RGBA `150, 150, 150, 128`). Unity will apply this automatically when `interactable` is false — no code change required.

### Step 4.6 — Set MainMenu as first scene in Build Settings

> **Unity Editor task (user):** File → Build Settings → click **Add Open Scenes** to add `MainMenu.unity`. Drag it to the top of the list (index 0). Confirm `Platformer.unity` and `Battle.unity` are also in the list at indices 1 and 2 (or whatever order the existing scenes have — just MainMenu must be index 0).

### Step 4.7 — Smoke test in Play Mode

> **Unity Editor task (user):** Open `MainMenu.unity`. Enter Play Mode.
>
> **Scenario A — no save file:**
> - Continue button appears greyed out and is not clickable.
> - New Game button is interactive. Clicking it loads the Platformer scene.
>
> **Scenario B — with save file:**
> - Delete any existing save, play the Platformer a moment so the autosave runs, then return to MainMenu.
> - Continue button is interactive. Clicking it loads Platformer at the saved world position.
>
> **Expected:** No console errors. GameManager persists across the scene transition (DontDestroyOnLoad — check that only one GameManager exists in the Platformer hierarchy after loading).

### Step 4.8 — Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-42): build MainMenu scene with New Game and Continue buttons`
  - `Assets/Scenes/MainMenu.unity`
  - `Assets/Scenes/MainMenu.unity.meta`

---

## Adding future menu buttons (Settings, Quit, Credits…)

The `MainMenuController` constructor uses fixed `Action` delegates. Adding a **new button** without touching existing tests follows this pattern:

**Step A — add the delegate field and method to `MainMenuController`:**

```csharp
// New field alongside the existing ones:
private readonly Action _openSettings;   // example

// New constructor parameter at the end (keeps existing call sites compiling):
public MainMenuController(Func<bool> hasSaveFile, Action startNewGame, Action continueGame,
                          Action openSettings = null)
{
    // existing guards …
    _openSettings = openSettings; // optional — null is valid for buttons not wired yet
}

// New method:
public void OnSettingsClicked() => _openSettings?.Invoke();
```

> Default `null` keeps all existing `new MainMenuController(…, …, …)` tests compiling without any changes.

**Step B — wire the button in `MainMenuUI`:**

```csharp
[SerializeField] private Button _settingsButton;

// In Start(), after existing wiring:
if (_settingsButton != null)
    _settingsButton.onClick.AddListener(_controller.OnSettingsClicked);

// In OnDestroy():
_settingsButton?.onClick.RemoveAllListeners();
```

**Step C — drag the new button into the Inspector field in the MainMenu scene.**

No existing tests need to change. New behaviour is covered by adding a test to `MainMenuControllerTests` that asserts `OnSettingsClicked` invokes the delegate when provided.

---

## Final state (checklist against Jira DEV-42)

- [ ] `MainMenu` scene exists at `Assets/Scenes/MainMenu.unity`.
- [ ] Menu shows **New Game** and **Continue** buttons.
- [ ] **New Game** resets `GameManager.PlayerState` to defaults and loads `Platformer`.
- [ ] **Continue** is only enabled when `GameManager.HasSaveFile()` returns true.
- [ ] **Continue** loads save state via `TryContinueGame()` (which calls `SaveService`, DEV-41) then loads the saved scene.
- [ ] **Continue** button is visually disabled (greyed out, non-interactive) when no save file is found.
- [ ] Scene transition uses `SceneManager.LoadScene` via a private `GameManager.LoadScene()` helper — DEV-34 integration requires changing only that one method body.
- [ ] `MainMenu` scene is index 0 in Build Settings.
- [ ] No hardcoded file paths — `Application.persistentDataPath` resolves via `SaveService` (DEV-41).

---

## Self-review

### C# guard clause ordering

**`MainMenuController.OnContinueClicked`:**
```
if (!CanContinue()) return;   ← guard exits before _continueGame() is called
_continueGame();
```
Order correct — no parameter is ever dereferenced before the guard.

**`GameManager.LoadScene()`:**
`StartNewGame()` and `TryContinueGame()` both call `LoadScene()` — no direct `SceneManager` calls remain in public methods. DEV-34 integration requires changing exactly one private method body.

**`MainMenuUI.Start`:**
Null-conditional on `GameManager.Instance?.` — these lambdas are safe to call even if `Instance` is null, matching the project-wide defensive pattern in `BattleController`.

### Test coverage gaps

| Method | Null arg | Empty/false case | Happy path |
|--------|----------|-----------------|------------|
| `MainMenuController(...)` | ✓ three null-arg tests | — | ✓ |
| `CanContinue()` | — | ✓ false case | ✓ true case |
| `OnNewGameClicked()` | — | ✓ no-save path | ✓ with-save path |
| `OnContinueClicked()` | — | ✓ no-save (no-op) | ✓ with-save path |
| `GameManager.StartNewGame()` | — | — | ✓ 7 state-reset assertions |

All non-trivial branches that are reachable without a real save file or Unity scene are tested.

### UVCS staged file audit

| Task | Files staged | Listed in check-in step? |
|------|-------------|--------------------------|
| 1 | `GameManager.cs` (modified, no new .meta), `GameManagerNewGameTests.cs` + `.meta` | ✓ Step 1.4 |
| 2 | `MainMenuController.cs` + `.meta`, `MainMenuControllerTests.cs` + `.meta` | ✓ Step 2.4 |
| 3 | `Axiom.Core.asmdef` (modified, no new .meta), `MainMenuUI.cs` + `.meta` | ✓ Step 3.3 |
| 4 | `MainMenu.unity` + `.meta` | ✓ Step 4.8 |

### Method signature consistency

| Implementation | Tests |
|----------------|-------|
| `MainMenuController(Func<bool>, Action, Action)` | Tests call `new MainMenuController(() => …, () => …, () => …)` ✓ |
| `CanContinue() → bool` | Tests assert `Assert.IsTrue/False(controller.CanContinue())` ✓ |
| `OnNewGameClicked()` | Tests call `controller.OnNewGameClicked()` ✓ |
| `OnContinueClicked()` | Tests call `controller.OnContinueClicked()` ✓ |
| `GameManager.StartNewGame()` | Tests call `_gameManager.StartNewGame()` ✓ |
| Namespace `Axiom.Core` | Tests use `using Axiom.Core;` ✓ |

### Unity Editor task isolation

Every Unity Editor action appears in its own `> **Unity Editor task (user):**` callout and is not mixed with code steps. ✓

---

**Plan complete and saved to** `docs/superpowers/plans/2026-04-14-dev-42-main-menu-scene.md`.

**Execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks, fast iteration. Use `superpowers:subagent-driven-development`.
2. **Parallel Session** — open a new session in the worktree and use `superpowers:executing-plans`.

Which approach do you want?
