# DEV-127 Controls Guide Pause Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-unity-game-dev-plans` together with `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Controls Guide sub-panel to the persistent pause menu that lists the game's current keyboard/mouse and controller controls without resuming gameplay.

**Architecture:** Extend the existing plain-C# `PauseMenuLogic` state model with a `ControlsGuide` panel, then let the existing `PauseMenuUI` MonoBehaviour wire buttons, GameObjects, and EventSystem focus. Build the visual panel inside the existing `GameManager.prefab`; do not add another Canvas, EventSystem, input action map, controller, or assembly definition.

**Tech Stack:** Unity 6 LTS, uGUI, TextMeshPro, Unity Input System, NUnit/Edit Mode tests, UVCS.

---

## Scope and success criteria

- The pause menu has a button labeled **Controls Guide** between Settings and Quit.
- The guide lists only controls implemented today: movement, jump, sprint, crouch, interact, attack, previous/next selection, exploration menus, pause, UI navigation/confirm/back, push-to-talk, and cancel spell.
- Controls are shown in two readable columns/sections: Keyboard & Mouse and Controller. Where no controller binding currently exists (exploration menu shortcuts and push-to-talk), show `Not currently bound` rather than inventing a binding.
- Clicking Controls Guide hides the main pause buttons and shows the guide; Back, Escape, or controller Start returns to the main pause menu.
- `PauseMenuLogic.IsPaused` remains true and `Time.timeScale` remains `0f` while the guide is open.
- Opening and closing the guide assigns deterministic UI focus, while mouse clicks continue through the existing `GraphicRaycaster`.
- Resume, Settings, Quit, the corner Pause button, and their existing navigation remain functional.

## Existing behavior to preserve

- `PauseMenuUI` persists through `GameManager.prefab`, owns pause input, and is the only class that writes `Time.timeScale` for this menu.
- `PauseMenuLogic` is the testable source of truth for `IsPaused` and `ActivePanel`.
- `PauseMenuUI.ApplyPanelState()` already switches between Main and Settings and uses `EventSystem.current.SetSelectedGameObject` for first focus.
- `Assets/InputSystem_Actions.inputactions` already supplies `UI/Navigate`, `UI/Submit`, `UI/Cancel`, pointer position, and mouse click. No input asset changes are required.
- `KeyboardNavigationConfigurationTests.SharedUiPrefabs_HaveNavigationEnabled` already audits every `Selectable` in `Assets/Prefabs/Core/GameManager.prefab`.

## File map

| File | Responsibility |
|---|---|
| `Assets/Tests/Editor/Core/PauseMenuLogicTests.cs` | Specify guide open/close, guard, pause, and resume behavior |
| `Assets/Scripts/Core/PauseMenuLogic.cs` | Add the `ControlsGuide` state and transitions |
| `Assets/Scripts/Core/PauseMenuUI.cs` | Wire guide buttons, panel visibility, back input, and focus |
| `Assets/Prefabs/Core/GameManager.prefab` | Add the button, guide layout/content, Back button, serialized references, and navigation |

No new C# file, folder, `.asmdef`, or input action is needed.

---

### Task 1: Specify controls-guide state transitions

**Files:**
- Modify: `Assets/Tests/Editor/Core/PauseMenuLogicTests.cs`

- [ ] **Add failing tests for every new reachable branch:**

```csharp
[Test]
public void OpenControlsGuide_FromMain_SwitchesPanelWithoutResuming()
{
    _logic.Pause();
    _logic.OpenControlsGuide();

    Assert.IsTrue(_logic.IsPaused);
    Assert.AreEqual(PauseMenuPanel.ControlsGuide, _logic.ActivePanel);
}

[Test]
public void OpenControlsGuide_WhenNotPaused_IsNoOp()
{
    _logic.OpenControlsGuide();

    Assert.IsFalse(_logic.IsPaused);
    Assert.AreEqual(PauseMenuPanel.Closed, _logic.ActivePanel);
}

[Test]
public void CloseControlsGuide_ReturnsToMainWithoutResuming()
{
    _logic.Pause();
    _logic.OpenControlsGuide();
    _logic.CloseControlsGuide();

    Assert.IsTrue(_logic.IsPaused);
    Assert.AreEqual(PauseMenuPanel.Main, _logic.ActivePanel);
}

[Test]
public void CloseControlsGuide_WhenOnMain_IsNoOp()
{
    _logic.Pause();
    _logic.CloseControlsGuide();

    Assert.IsTrue(_logic.IsPaused);
    Assert.AreEqual(PauseMenuPanel.Main, _logic.ActivePanel);
}

[Test]
public void Resume_FromControlsGuide_ResumesDirectly()
{
    _logic.Pause();
    _logic.OpenControlsGuide();
    _logic.Resume();

    Assert.IsFalse(_logic.IsPaused);
    Assert.AreEqual(PauseMenuPanel.Closed, _logic.ActivePanel);
}
```

- [ ] **Run the focused Edit Mode suite:** Unity Editor → Window → General → Test Runner → EditMode → `CoreTests` → `PauseMenuLogicTests` → Run Selected.

  Expected: the five new tests fail to compile because `ControlsGuide`, `OpenControlsGuide`, and `CloseControlsGuide` do not exist yet; existing tests remain unchanged.

### Task 2: Implement the minimal plain-C# state

**Files:**
- Modify: `Assets/Scripts/Core/PauseMenuLogic.cs`

- [ ] **Add the panel enum member:**

```csharp
public enum PauseMenuPanel
{
    Closed,
    Main,
    Settings,
    ControlsGuide
}
```

- [ ] **Add transitions matching the existing Settings pattern:**

```csharp
public void OpenControlsGuide()
{
    if (!IsPaused) return;
    ActivePanel = PauseMenuPanel.ControlsGuide;
}

public void CloseControlsGuide()
{
    if (ActivePanel != PauseMenuPanel.ControlsGuide) return;
    ActivePanel = PauseMenuPanel.Main;
}
```

- [ ] **Re-run** `PauseMenuLogicTests` in Edit Mode.

  Expected: all existing and new tests pass. In particular, opening/closing the guide never changes `IsPaused`.

- [ ] **Check in via UVCS:** Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-127): add controls guide pause state`
  - `Assets/Scripts/Core/PauseMenuLogic.cs`
  - `Assets/Tests/Editor/Core/PauseMenuLogicTests.cs`

### Task 3: Wire the controls-guide panel through `PauseMenuUI`

**Files:**
- Modify: `Assets/Scripts/Core/PauseMenuUI.cs`

- [ ] **Add serialized references beside their matching pause/settings fields:**

```csharp
[SerializeField] private Button _controlsGuideButton;

[Header("Controls Guide Sub-panel")]
[SerializeField] private GameObject _controlsGuidePanel;
[SerializeField] private Button _controlsGuideBackButton;

[Header("First selected button on controls guide open")]
[SerializeField] private GameObject _firstSelectedOnControlsGuide;
```

- [ ] **Wire and unwire button listeners in `Start()` and `OnDestroy()`:**

```csharp
if (_controlsGuideButton != null) _controlsGuideButton.onClick.AddListener(OnControlsGuideClicked);
if (_controlsGuideBackButton != null) _controlsGuideBackButton.onClick.AddListener(OnControlsGuideBackClicked);
```

```csharp
if (_controlsGuideButton != null) _controlsGuideButton.onClick.RemoveListener(OnControlsGuideClicked);
if (_controlsGuideBackButton != null) _controlsGuideBackButton.onClick.RemoveListener(OnControlsGuideBackClicked);
```

- [ ] **Add button handlers that delegate state decisions:**

```csharp
private void OnControlsGuideClicked()
{
    _logic.OpenControlsGuide();
    ApplyPanelState();
}

private void OnControlsGuideBackClicked()
{
    _logic.CloseControlsGuide();
    ApplyPanelState();
}
```

- [ ] **Update the Escape/Start branch so either sub-panel returns to Main:**

```csharp
if (_logic.ActivePanel == PauseMenuPanel.Settings)
{
    _logic.CloseSettings();
}
else if (_logic.ActivePanel == PauseMenuPanel.ControlsGuide)
{
    _logic.CloseControlsGuide();
}
else
{
    _logic.TogglePause();
}
ApplyPanelState();
```

- [ ] **Replace the sub-panel portion of `ApplyPanelState()` with mutually exclusive visibility and focus:**

```csharp
bool showSettings = _logic.ActivePanel == PauseMenuPanel.Settings;
bool showControlsGuide = _logic.ActivePanel == PauseMenuPanel.ControlsGuide;

if (_settingsPanel != null)
    _settingsPanel.SetActive(showSettings);

if (_controlsGuidePanel != null)
    _controlsGuidePanel.SetActive(showControlsGuide);

if (_pausePanel != null && _pausePanel.activeSelf)
{
    GameObject mainButtons = _resumeButton?.transform.parent?.gameObject;
    if (mainButtons != null)
        mainButtons.SetActive(!showSettings && !showControlsGuide);
}

if (paused)
{
    GameObject select = showSettings
        ? _firstSelectedOnSettings
        : showControlsGuide
            ? _firstSelectedOnControlsGuide
            : _firstSelectedOnPause;

    if (select != null)
        EventSystem.current?.SetSelectedGameObject(select);
}
```

- [ ] **Verify compilation:** allow Unity to recompile, then run `PauseMenuLogicTests` and `KeyboardNavigationConfigurationTests` in Edit Mode.

  Expected: scripts compile; logic tests pass. The navigation test may remain green until the prefab adds the new button because no new `Selectable` exists yet.

### Task 4: Build and populate the guide in the persistent prefab

**Files:**
- Modify: `Assets/Prefabs/Core/GameManager.prefab`

> **Unity Editor task (user):** Open `Assets/Prefabs/Core/GameManager.prefab` in Prefab Mode. Under the pause menu's existing main button container, duplicate the Settings button, rename it `ControlsGuideButton`, change its TextMeshPro label to `Controls Guide`, and place it between Settings and Quit. Keep the same dimensions, colors, transition, font, and spacing as its siblings.

> **Unity Editor task (user):** Under the existing pause overlay, create a `ControlsGuidePanel` sibling of `SettingsPanel`. Match the Settings panel's background and safe margins. Add the title `Controls Guide`, two side-by-side TextMeshPro sections, and a `Back` button. Use a ScrollRect only if the content cannot remain readable at the project's smallest supported game view; do not shrink body text below the existing Settings label size.

> **Unity Editor task (user):** Populate the sections exactly from current bindings:
>
> | Action | Keyboard & Mouse | Controller |
> |---|---|---|
> | Move | W/A/S/D or Arrow Keys | Left Stick |
> | Jump | Space | South Button |
> | Sprint / Push-to-Talk | Left Shift | Left Stick Press / Not currently bound for PTT |
> | Crouch / Cancel | C / Escape | East Button |
> | Interact | E | North Button |
> | Attack | Enter | West Button |
> | Previous / Next | 1 / 2 | D-Pad Left / Right |
> | Spellbook / Items | B / I | Not currently bound |
> | Pause | Escape | Start |
> | Navigate Menus | W/A/S/D or Arrow Keys | Left Stick or D-Pad |
> | Confirm | Enter or Space | South Button |
>
> Add a short note: `Spellcasting uses voice input. Hold Push-to-Talk, speak the spell name, then release.` Do not claim mouse bindings for gameplay actions that are not bound.

> **Unity Editor task (user):** Configure explicit vertical navigation for Resume → Settings → Controls Guide → Quit, with reverse Up links and Wrap Around matching the existing menu convention. Set the guide Back button's Navigation to Automatic or Explicit, never None. Ensure Selected is visually distinct and mouse hover/click remains unchanged.

> **Unity Editor task (user):** Assign the new `PauseMenuUI` Inspector fields: `_controlsGuideButton`, `_controlsGuidePanel`, `_controlsGuideBackButton`, and `_firstSelectedOnControlsGuide` (the Back button). Save the prefab and exit Prefab Mode.

- [ ] **Run** `KeyboardNavigationConfigurationTests.SharedUiPrefabs_HaveNavigationEnabled`.

  Expected: PASS for `GameManager.prefab`, including Controls Guide and Back.

- [ ] **Check in via UVCS:** Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-127): add controls guide pause panel`
  - `Assets/Scripts/Core/PauseMenuUI.cs`
  - `Assets/Prefabs/Core/GameManager.prefab`

### Task 5: Verify every acceptance criterion

> **Unity Editor task (user):** Enter Play Mode in both a platformer scene and the Battle scene. Open Pause with Escape and controller Start. Open Controls Guide using keyboard/controller navigation and again with the mouse. Confirm the guide is legible, its full content is visible or scrollable, and `Time.timeScale` remains `0` in the Inspector/Debugger while it is open.

> **Unity Editor task (user):** Close the guide three ways: Back button with Submit, Back button with mouse click, and Escape/controller Start. Each must return to the main pause buttons, keep gameplay paused, and restore selection to the configured first pause button. Then verify Resume unpauses, Settings still opens/closes, Quit still returns to Main Menu, and the corner Pause button still opens the overlay.

- [ ] **Run all Edit Mode tests** in Unity Test Runner.

  Expected: zero failures and zero skipped tests related to DEV-127.

- [ ] **Run all Play Mode tests** in Unity Test Runner.

  Expected: zero failures and zero skipped tests related to DEV-127. Record any unrelated pre-existing failure rather than calling the story complete silently.

- [ ] **Final UVCS audit:** confirm Pending Changes contains only the planned script, test, prefab, and plan-document changes; do not add manually authored `.meta` files because no asset or folder was created outside Unity.

- [ ] **Check in the reviewed plan via UVCS:** Unity Version Control → Pending Changes → stage the file below → Check in with message: `docs(DEV-127): add controls guide implementation plan`
  - `docs/superpowers/plans/2026-06-22-dev-127-controls-guide-pause-menu.md`

## Requirement trace

| Acceptance criterion | Proof |
|---|---|
| Clearly labeled button | Prefab task and Play Mode visual check |
| All relevant current controls | Binding-derived table in Task 4 |
| Open and close from pause | Logic tests plus three close-path Play Mode checks |
| Does not unpause | Logic tests and `Time.timeScale == 0` Play Mode check |
| Keyboard/controller and mouse readable/navigation | Existing Input System UI map, prefab navigation audit, and Play Mode checks |
| Existing buttons/layout remain functional | Regression checklist in Task 5 |
