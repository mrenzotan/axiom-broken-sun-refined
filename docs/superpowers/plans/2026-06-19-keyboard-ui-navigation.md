# Keyboard UI Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-unity-game-dev-plans` together with `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every player-facing uGUI menu and modal operable with arrow keys plus Enter/Space, with deterministic initial focus and visible selection feedback.

**Architecture:** Keep Unity's existing `EventSystem`, `InputSystemUIInputModule`, `UI/Navigate`, and `UI/Submit` actions. Configure each `Selectable`'s navigation graph in serialized scenes/prefabs, and move focus inside the UI wrapper that already opens or enables that surface. Do not add a global focus singleton or a new input-routing system.

**Tech Stack:** Unity 6.0.4 LTS, uGUI, Unity Input System, C#, Unity Test Framework, UVCS.

---

## Scope and acceptance criteria

- Main menu, settings, confirmation dialog, pause/settings, battle action menu, battle item/spell panels, victory, defeat, level-up prompt, and exploration item/spell panels receive initial focus when shown.
- Arrow keys move only to active, interactable controls. Enter/Space invokes the same `Button.onClick` path as a mouse click.
- Closing a modal restores focus to its owning menu when one remains open; closing an exploration modal clears focus so arrow keys return exclusively to player movement.
- Persistent platformer HUD shortcuts are not selected while gameplay is unpaused. Existing `B` and `I` shortcuts remain unchanged.
- Every enabled build scene has one `EventSystem`, navigation events enabled, and an `InputSystemUIInputModule` wired to `UI/Navigate`, `UI/Submit`, and `UI/Cancel`.
- Keyboard focus has a visible `Selected` state. Pointer hover/click behavior remains unchanged.

## Existing behavior to preserve

- `PauseMenuUI.ApplyPanelState()` already selects `_firstSelectedOnPause` or `_firstSelectedOnSettings`.
- `ConfirmNewGameDialogUI.Show()` selects No and `Hide()` restores `_focusOnHide`.
- `Assets/InputSystem_Actions.inputactions` already contains the UI action map; do not create parallel actions or poll arrow keys in `Update()`.

## File structure

| File | Change |
|---|---|
| `Assets/Tests/Editor/UI/KeyboardNavigationConfigurationTests.cs` | Create serialized scene/prefab navigation audit |
| `Assets/Tests/Editor/UI/UITests.asmdef` | Add direct `UnityEngine.UI` and `Unity.InputSystem` references |
| `Assets/Scripts/Core/MainMenuUI.cs` | Select primary/settings defaults and restore focus |
| `Assets/Scripts/Core/UI/ItemSlotUI.cs` | Expose its serialized button read-only for its owning panel |
| `Assets/Scripts/Core/UI/ItemMenuUI.cs` | Select first usable item, otherwise Back |
| `Assets/Scripts/Core/UI/SpellListPanelUI.cs` | Select Close when shown |
| `Assets/Scripts/Platformer/UI/ExplorationMenuController.cs` | Clear focus when exploration modals close |
| `Assets/Scripts/Battle/UI/ActionMenuUI.cs` | Select first enabled action on player turn and expose focus restoration |
| `Assets/Scripts/Battle/BattleController.cs` | Restore action-menu focus after battle item/spell modals close |
| `Assets/Scripts/Battle/UI/VictoryScreenUI.cs` | Select confirm when shown |
| `Assets/Scripts/Battle/UI/DefeatScreenUI.cs` | Select continue when shown |
| `Assets/Scripts/Battle/UI/LevelUpPromptUI.cs` | Select confirm when shown |
| Player-facing `.unity` scenes and shared UI prefabs | Configure navigation links and Selected visuals in Unity Editor |

No new assembly definition is needed.

---

### Task 1: Add a failing configuration audit

**Files:**
- Create: `Assets/Tests/Editor/UI/KeyboardNavigationConfigurationTests.cs`
- Modify: `Assets/Tests/Editor/UI/UITests.asmdef`

- [ ] Add `UnityEngine.UI` and `Unity.InputSystem` to `UITests.asmdef.references`, then create the test file below. It iterates enabled `EditorBuildSettings.scenes`, opens each scene, and asserts:
  - exactly one active `EventSystem` exists;
  - `sendNavigationEvents` is true;
  - the EventSystem has an enabled `InputSystemUIInputModule`;
  - `move`, `submit`, and `cancel` action references are assigned;
  - every `Button`, `Slider`, and `Toggle` below an active Canvas has `navigation.mode != Navigation.Mode.None`.

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UITests
{
    public class KeyboardNavigationConfigurationTests
    {
        [Test]
        public void EnabledBuildScenes_HaveConfiguredKeyboardNavigation()
        {
            string originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            try
            {
                foreach (string path in EnabledScenePaths())
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                    EventSystem[] systems = Object.FindObjectsByType<EventSystem>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                    Assert.AreEqual(1, systems.Length, $"{path}: expected exactly one EventSystem.");
                    Assert.IsTrue(systems[0].sendNavigationEvents,
                        $"{path}: EventSystem must send navigation events.");

                    InputSystemUIInputModule module = systems[0].GetComponent<InputSystemUIInputModule>();
                    Assert.IsNotNull(module, $"{path}: missing InputSystemUIInputModule.");
                    Assert.IsTrue(module.enabled, $"{path}: InputSystemUIInputModule is disabled.");
                    Assert.IsNotNull(module.move, $"{path}: UI/Navigate is unassigned.");
                    Assert.IsNotNull(module.submit, $"{path}: UI/Submit is unassigned.");
                    Assert.IsNotNull(module.cancel, $"{path}: UI/Cancel is unassigned.");

                    foreach (Selectable selectable in Object.FindObjectsByType<Selectable>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                        AssertNavigationEnabled(selectable, path);
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalScene))
                    EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
            }
        }

        [TestCase("Assets/Prefabs/Core/GameManager.prefab")]
        [TestCase("Assets/Prefabs/Dialogue/DialogueCanvas.prefab")]
        public void SharedUiPrefabs_HaveNavigationEnabled(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
                    AssertNavigationEnabled(selectable, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static IEnumerable<string> EnabledScenePaths() =>
            EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path);

        private static void AssertNavigationEnabled(Selectable selectable, string assetPath)
        {
            Assert.AreNotEqual(
                Navigation.Mode.None,
                selectable.navigation.mode,
                $"{assetPath}: '{GetHierarchyPath(selectable.transform)}' cannot be reached by keyboard navigation.");
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }
            return path;
        }
    }
}
```

This helper makes failures identify the exact scene hierarchy:

```csharp
private static void AssertNavigationEnabled(Selectable selectable, string assetPath)
{
    Assert.AreNotEqual(
        Navigation.Mode.None,
        selectable.navigation.mode,
        $"{assetPath}: '{GetHierarchyPath(selectable.transform)}' cannot be reached by keyboard navigation.");
}
```

- [ ] Run Unity Editor → Test Runner → EditMode → `KeyboardNavigationConfigurationTests`.
  Expected: FAIL on the current controls whose Navigation is None/invalid; EventSystem module assertions should already pass.

### Task 2: Add deterministic focus to core menu panels

**Files:**
- Modify: `Assets/Scripts/Core/MainMenuUI.cs`
- Modify: `Assets/Scripts/Core/UI/ItemSlotUI.cs`
- Modify: `Assets/Scripts/Core/UI/ItemMenuUI.cs`
- Modify: `Assets/Scripts/Core/UI/SpellListPanelUI.cs`

- [ ] In `ItemSlotUI`, expose only the existing serialized button:

```csharp
public Button Button => _button;
```

- [ ] Add `using UnityEngine.EventSystems;` to the three panel wrappers that need it.
- [ ] In `MainMenuUI`, after Continue interactability is resolved, select the first active/interactable candidate in this order: Continue, New Game, Settings, Quit. When settings opens, select Master Volume, then Music, SFX, then Back. When settings closes, repeat the primary-menu selection order.
- [ ] In `ItemMenuUI.Show`, activate the panel before selecting; select the first active slot's `Button`, otherwise `_backButton` for an empty inventory.
- [ ] In `SpellListPanelUI.Show`, activate the panel and select `_closeButton`. The spell rows are informational and must not be added to the navigation graph.
- [ ] Keep all selection calls null-safe and use `Selectable.IsActive()` plus `IsInteractable()` before `EventSystem.current.SetSelectedGameObject(...)`.
- [ ] Run existing EditMode suites `MainMenuControllerTests`, `PauseMenuLogicTests`, and `UITests`; expect all PASS.

### Task 3: Add battle focus transitions

**Files:**
- Modify: `Assets/Scripts/Battle/UI/ActionMenuUI.cs`
- Modify: `Assets/Scripts/Battle/BattleController.cs`
- Modify: `Assets/Scripts/Battle/UI/VictoryScreenUI.cs`
- Modify: `Assets/Scripts/Battle/UI/DefeatScreenUI.cs`
- Modify: `Assets/Scripts/Battle/UI/LevelUpPromptUI.cs`

- [ ] Add `ActionMenuUI.FocusFirstInteractable()` that checks Attack, Spell, Item, Flee, then Spell List and selects the first active/interactable button. Call it after `SetInteractable(true)`. When disabling the menu, clear EventSystem selection only if the currently selected object belongs to this action menu.
- [ ] After `BattleController.HandleItemCancelled()` and `HandleSpellPanelClose()` hide their modal, call `ActionMenuUI.FocusFirstInteractable()`. Do the same after an item is rejected without consuming the turn.
- [ ] Immediately after showing each post-battle panel, select its sole actionable control:

```csharp
EventSystem.current?.SetSelectedGameObject(_confirmButton.gameObject); // Victory / Level Up
EventSystem.current?.SetSelectedGameObject(_continueButton.gameObject); // Defeat
```

- [ ] Verify tutorial locks: when Attack is disabled, focus skips it; when only Attack is enabled, focus lands on Attack.
- [ ] Run Unity Editor → Test Runner → EditMode → `BattleTests` and `UITests`; expect all PASS.

### Task 4: Keep exploration movement and UI focus mutually exclusive

**Files:**
- Modify: `Assets/Scripts/Platformer/UI/ExplorationMenuController.cs`

- [ ] Do not select the always-visible Spellbook/Items HUD buttons during normal gameplay.
- [ ] Let `ItemMenuUI.Show()` and `SpellListPanelUI.Show()` take focus when their modal opens.
- [ ] In `HideItems()` and `HideSpellbook()`, clear selection after hiding the panel:

```csharp
EventSystem.current?.SetSelectedGameObject(null);
```

- [ ] Confirm existing `B`/`I` shortcuts and player arrow-key movement remain unchanged when no modal is open.

### Task 5: Configure serialized navigation in Unity Editor

> **Unity Editor task (user):** For every player-facing `Button`, `Slider`, and `Toggle` in enabled build scenes plus `GameManager.prefab` and `DialogueCanvas.prefab`, set Navigation to Automatic for simple linear layouts and Explicit for the battle 2×2 grid or irregular layouts. Assign every Explicit Up/Down/Left/Right target; enable Wrap Around only for closed menu lists.

> **Unity Editor task (user):** Configure the battle grid explicitly: Attack ↔ Spell horizontally, Item ↔ Flee horizontally, Attack ↔ Item vertically, Spell ↔ Flee vertically; connect Spell List to the nearest intended grid control without creating a dead end.

> **Unity Editor task (user):** Ensure every navigable control has a visible Selected Color, Selected Sprite, or Animator state distinct from Normal and Disabled. Do not change the existing mouse-hover cursor system.

- [ ] Re-run `KeyboardNavigationConfigurationTests`; expect all PASS.

### Task 6: End-to-end verification and UVCS check-in

> **Unity Editor task (user):** In Play Mode, verify Main Menu → Settings → Back; confirmation Yes/No; Pause → Settings → Back; battle action grid; battle Item/Spell List → Back; Victory → Level Up; Defeat → Continue; exploration Item/Spell panels. For each surface, test arrows, Enter, Space, mouse click, disabled-control skipping, and focus restoration/clearing.

- [ ] Run all EditMode and PlayMode tests in Test Runner. Expected: zero failed and zero skipped tests related to this change.
- [ ] Assign a real Jira `DEV-#` before check-in; this plan intentionally does not invent a ticket ID.
- [ ] **Check in via UVCS:** Unity Version Control → Pending Changes → stage only the files listed in this plan plus Unity-generated metadata and the deliberately edited scenes/prefabs → Check in with message: `feat(DEV-#): add keyboard navigation to clickable UI`.

## Reference behavior

- Unity uGUI navigation is driven by `Selectable.navigation`; the EventSystem supplies move and submit events.
- `First Selected` only covers initial EventSystem startup. Panels opened later must explicitly select their default control, which is why focus stays inside the wrappers that show those panels.
