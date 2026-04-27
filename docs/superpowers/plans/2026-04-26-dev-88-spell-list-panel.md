# Spell List Panel — DEV-88 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the current Spell button placeholder with a centered read-only panel that lists the player's unlocked spells during battle, then proceeds to the voice spell phase.

**Architecture:** Plain C# `SpellListPanelLogic` owns all state and data fetching; `SpellListPanelUI` (MonoBehaviour) handles Unity lifecycle and UI GameObject binding. `BattleController.PlayerSpell()` becomes a three-state toggle: (1) first press shows the panel, (2) second press dismisses it and starts the voice spell phase, (3) close button cancels back to the action menu.

**Tech Stack:** Unity 6 LTS, URP 2D, TMPro, existing `Axiom.Battle` asmdef (no new asmdef needed).

---

### Task 1: SpellListPanelLogic (plain C# service + Edit Mode tests)

**Files:**
- Create: `Assets/Scripts/Battle/UI/SpellListPanelLogic.cs`
- Create: `Assets/Scripts/Battle/UI/SpellListPanelLogic.cs.meta`
- Create: `Assets/Tests/Editor/Battle/SpellListPanelLogicTests.cs`
- Create: `Assets/Tests/Editor/Battle/SpellListPanelLogicTests.cs.meta`

**Step 1: Write the Edit Mode test file**

```csharp
using System.Collections.Generic;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Battle.Tests
{
    [TestFixture]
    public class SpellListPanelLogicTests
    {
        // Dev-only stub SpellData for tests that don't need actual ScriptableObjects.
        private SpellData StubSpell(string name) =>
            ScriptableObject.CreateInstance<SpellData>() is { } s
                ? (s.spellName = name.ToLower(), s)
                : null;

        // ── Constructor ──────────────────────────────────────────────────

        [Test]
        public void Constructor_NullSpells_ReturnsEmptyList()
        {
            var logic = new SpellListPanelLogic(null);
            Assert.That(logic.Spells, Is.Empty);
        }

        [Test]
        public void Constructor_EmptySpells_ReturnsEmptyList()
        {
            var logic = new SpellListPanelLogic(new List<SpellData>());
            Assert.That(logic.Spells, Is.Empty);
        }

        [Test]
        public void Constructor_PopulatedSpells_StoresCopy()
        {
            var spells = new List<SpellData> { StubSpell("fireball"), StubSpell("iceshard") };
            var logic = new SpellListPanelLogic(spells);
            Assert.That(logic.Spells.Count, Is.EqualTo(2));
            Assert.That(logic.Spells[0].spellName, Is.EqualTo("fireball"));
            Assert.That(logic.Spells[1].spellName, Is.EqualTo("iceshard"));
        }

        // ── IsEmpty ──────────────────────────────────────────────────────

        [Test]
        public void IsEmpty_NullSpells_ReturnsTrue()
        {
            var logic = new SpellListPanelLogic(null);
            Assert.That(logic.IsEmpty, Is.True);
        }

        [Test]
        public void IsEmpty_EmptySpells_ReturnsTrue()
        {
            var logic = new SpellListPanelLogic(new List<SpellData>());
            Assert.That(logic.IsEmpty, Is.True);
        }

        [Test]
        public void IsEmpty_PopulatedSpells_ReturnsFalse()
        {
            var logic = new SpellListPanelLogic(new List<SpellData> { StubSpell("fireball") });
            Assert.That(logic.IsEmpty, Is.False);
        }

        // ── SpellNames ───────────────────────────────────────────────────

        [Test]
        public void SpellNames_ReturnsDisplayCaseNames()
        {
            var spells = new List<SpellData> { StubSpell("fireball"), StubSpell("ice shard") };
            var logic = new SpellListPanelLogic(spells);
            var names = logic.SpellNames;
            Assert.That(names.Count, Is.EqualTo(2));
            Assert.That(names[0], Is.EqualTo("Fireball"));     // Capitalized for display
            Assert.That(names[1], Is.EqualTo("Ice Shard"));    // Title-case
        }

        [Test]
        public void SpellNames_EmptyList_ReturnsEmpty()
        {
            var logic = new SpellListPanelLogic(new List<SpellData>());
            Assert.That(logic.SpellNames, Is.Empty);
        }

        // ── EmptyMessage ─────────────────────────────────────────────────

        [Test]
        public void EmptyMessage_ReturnsConstant()
        {
            var logic = new SpellListPanelLogic(null);
            Assert.That(logic.EmptyMessage, Is.EqualTo("No spells available"));
        }

        // ── BuildFromSpellUnlockService ──────────────────────────────────

        [Test]
        public void BuildFromSpellUnlockService_NullService_ReturnsNull()
        {
            var result = SpellListPanelLogic.BuildFromSpellUnlockService(null);
            Assert.That(result, Is.Null);
        }

        // ── Cleanup ──────────────────────────────────────────────────────

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in Object.FindObjectsOfType<SpellData>())
                Object.DestroyImmediate(obj);
        }
    }
}
```

**Step 2: Run the test to verify it fails**

> **Unity Editor task (user):** Open Window → General → Test Runner → EditMode tab → run `SpellListPanelLogicTests`. All tests should fail because the class doesn't exist yet.

**Step 3: Write the SpellListPanelLogic implementation**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# service that holds the unlocked spell list for the read-only
    /// spell info panel shown during the Spell action in battle.
    ///
    /// No Unity types — fully testable in Edit Mode.
    /// Owned by <see cref="SpellListPanelUI"/> at runtime.
    ///
    /// <see cref="BuildFromSpellUnlockService"/> is the recommended factory —
    /// it reads the player's current unlocked spells from <see cref="GameManager"/>.
    /// </summary>
    public class SpellListPanelLogic
    {
        private readonly List<SpellData> _spells;

        public IReadOnlyList<SpellData> Spells => _spells;

        public bool IsEmpty => _spells.Count == 0;

        public IReadOnlyList<string> SpellNames
        {
            get
            {
                var names = new string[_spells.Count];
                for (int i = 0; i < _spells.Count; i++)
                    names[i] = ToDisplayCase(_spells[i].spellName);
                return names;
            }
        }

        public string EmptyMessage => "No spells available";

        public SpellListPanelLogic(List<SpellData> spells)
        {
            _spells = spells != null && spells.Count > 0
                ? new List<SpellData>(spells)
                : new List<SpellData>();
        }

        /// <summary>
        /// Factory that reads the player's unlocked spells from GameManager.
        /// Returns null when GameManager or SpellUnlockService is unavailable
        /// (standalone Battle scene testing, no catalog assigned, etc.).
        /// Returns an empty-logic instance when the service exists but the
        /// player has no unlocked spells.
        /// </summary>
        public static SpellListPanelLogic BuildFromSpellUnlockService(SpellUnlockService service)
        {
            if (service == null)
                return null;

            var unlocked = service.UnlockedSpells;
            var list = unlocked != null
                ? new List<SpellData>(unlocked)
                : new List<SpellData>();
            return new SpellListPanelLogic(list);
        }

        /// <summary>
        /// Converts a lowercase spellName to Title Case for display.
        /// "fireball" → "Fireball", "ice shard" → "Ice Shard".
        /// </summary>
        private static string ToDisplayCase(string spellName)
        {
            if (string.IsNullOrWhiteSpace(spellName))
                return string.Empty;

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spellName);
        }
    }
}
```

**Step 4: Run the tests to verify they pass**

> **Unity Editor task (user):** In Test Runner → EditMode tab → run `SpellListPanelLogicTests`. All tests should pass.

**Step 5: Check in via UVCS**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-88): add SpellListPanelLogic plain C# service with Edit Mode tests`
  - `Assets/Scripts/Battle/UI/SpellListPanelLogic.cs`
  - `Assets/Scripts/Battle/UI/SpellListPanelLogic.cs.meta`
  - `Assets/Tests/Editor/Battle/SpellListPanelLogicTests.cs`
  - `Assets/Tests/Editor/Battle/SpellListPanelLogicTests.cs.meta`

---

### Task 2: SpellListPanelUI (MonoBehaviour wrapper)

**Files:**
- Create: `Assets/Scripts/Battle/UI/SpellListPanelUI.cs`
- Create: `Assets/Scripts/Battle/UI/SpellListPanelUI.cs.meta`

**Step 1: Write the SpellListPanelUI MonoBehaviour**

```csharp
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour that drives the read-only spell list panel in the Battle scene.
    ///
    /// Assigned fields:
    ///   _panel           — root GameObject toggled by Show/Hide
    ///   _contentParent   — Transform where spell row GameObjects are instantiated
    ///   _spellRowPrefab  — prefab with a TMP_Text child (spell name label)
    ///   _closeButton     — dismisses the panel without entering voice phase
    ///   _emptyMessageText — TMP_Text shown when no spells are unlocked
    ///
    /// All logic lives in <see cref="SpellListPanelLogic"/>.
    /// </summary>
    public class SpellListPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _contentParent;
        [SerializeField] private GameObject _spellRowPrefab;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _emptyMessageText;

        /// <summary>Fires when the player clicks the close (X) button.</summary>
        public event Action OnCloseClicked;

        private SpellListPanelLogic _logic;
        private readonly List<GameObject> _activeRows = new List<GameObject>();

        public bool IsVisible => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(HandleClose);
            if (_panel != null)
                _panel.SetActive(false);
        }

        /// <summary>
        /// Populates and shows the panel with the given spell list.
        /// Call from BattleController when the player first presses Spell.
        /// Pass null or empty-logic to show the "No spells available" message.
        /// </summary>
        public void Show(SpellListPanelLogic logic)
        {
            _logic = logic ?? throw new ArgumentNullException(nameof(logic));
            ClearRows();

            if (_logic.IsEmpty)
            {
                if (_emptyMessageText != null)
                {
                    _emptyMessageText.gameObject.SetActive(true);
                    _emptyMessageText.text = _logic.EmptyMessage;
                }
            }
            else
            {
                if (_emptyMessageText != null)
                    _emptyMessageText.gameObject.SetActive(false);

                IReadOnlyList<string> names = _logic.SpellNames;
                for (int i = 0; i < names.Count; i++)
                {
                    GameObject row = Instantiate(_spellRowPrefab, _contentParent);
                    TMP_Text label = row.GetComponentInChildren<TMP_Text>();
                    if (label != null)
                        label.text = names[i];
                    _activeRows.Add(row);
                }
            }

            if (_panel != null)
                _panel.SetActive(true);
        }

        /// <summary>Hides the panel and cleans up instantiated rows.</summary>
        public void Hide()
        {
            if (_panel != null)
                _panel.SetActive(false);
            ClearRows();
            _logic = null;
        }

        private void HandleClose()
        {
            OnCloseClicked?.Invoke();
        }

        private void ClearRows()
        {
            foreach (GameObject row in _activeRows)
                if (row != null) Destroy(row);
            _activeRows.Clear();
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveAllListeners();
            ClearRows();
        }
    }
}
```

**Step 2: Check in via UVCS**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-88): add SpellListPanelUI MonoBehaviour wrapper`
  - `Assets/Scripts/Battle/UI/SpellListPanelUI.cs`
  - `Assets/Scripts/Battle/UI/SpellListPanelUI.cs.meta`

---

### Task 3: Modify BattleController for the two-step Spell flow

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

**Step 1: Add the SpellListPanelUI field and modify PlayerSpell()**

In `BattleController.cs`, add a serialized field after the existing `_itemMenuUI` field (around line 72):

```csharp
[SerializeField]
[Tooltip("Assign the SpellListPanelUI component from the Battle Canvas.")]
private SpellListPanelUI _spellListPanelUI;
```

Replace the existing `PlayerSpell()` method (currently lines 437–445) with:

```csharp
/// <summary>
/// Three-state toggle:
///   1. First press (panel hidden, no action processing) → shows the spell list panel.
///   2. Second press (panel visible) → dismisses panel and starts the voice spell phase.
///   3. Close button press (panel visible) → dismisses panel, cancels the action.
///
/// No-op outside PlayerTurn or when an unrelated action is already processing.
/// </summary>
public void PlayerSpell()
{
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;

    // Second press: panel is visible, dismiss it and start voice phase.
    if (_spellListPanelUI != null && _spellListPanelUI.IsVisible)
    {
        _spellListPanelUI.OnCloseClicked -= HandleSpellPanelClose;
        _spellListPanelUI.Hide();
        StartVoiceSpellPhase();
        return;
    }

    if (_isProcessingAction) return;

    // First press: show the informational spell list panel.
    _isProcessingAction = true;

    var gm = Axiom.Core.GameManager.Instance;
    SpellListPanelLogic logic = null;
    if (gm != null)
        logic = SpellListPanelLogic.BuildFromSpellUnlockService(gm.SpellUnlockService);

    if (_spellListPanelUI != null)
    {
        // Wire callback before showing so it's ready.
        _spellListPanelUI.OnCloseClicked -= HandleSpellPanelClose;
        _spellListPanelUI.OnCloseClicked += HandleSpellPanelClose;

        // Fallback: if logic is null (no GameManager / no service), show empty panel.
        if (logic == null)
            logic = new SpellListPanelLogic(null);

        _spellListPanelUI.Show(logic);
    }
    else
    {
        // No panel UI assigned — fall back to immediate voice phase (standalone testing).
        StartVoiceSpellPhase();
    }
}

private void StartVoiceSpellPhase()
{
    _isAwaitingVoiceSpell = true;
    OnSpellChargeStarted?.Invoke();
    OnSpellPhaseStarted?.Invoke();
}

private void HandleSpellPanelClose()
{
    if (_spellListPanelUI != null)
    {
        _spellListPanelUI.Hide();
        _spellListPanelUI.OnCloseClicked -= HandleSpellPanelClose;
    }
    _isProcessingAction = false;
    _isAwaitingVoiceSpell = false;
}
```

**Step 2: Add cleanup in Initialize() and OnDestroy()**

In `BattleController.Initialize()`, add to the existing cleanup block (after the `_itemMenuUI` cleanup, around line 285):

```csharp
if (_spellListPanelUI != null)
{
    _spellListPanelUI.OnCloseClicked -= HandleSpellPanelClose;
    _spellListPanelUI.Hide();
}
```

In `BattleController.OnDestroy()`, add after the `_itemMenuUI` cleanup (around line 867):

```csharp
if (_spellListPanelUI != null)
{
    _spellListPanelUI.OnCloseClicked -= HandleSpellPanelClose;
}
```

**Step 3: Verify compilation**

> **Unity Editor task (user):** In the Unity Editor, verify the project compiles without errors. Check the Console window.

**Step 4: Check in via UVCS**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-88): modify BattleController for two-step Spell panel flow`
  - `Assets/Scripts/Battle/BattleController.cs`
  - `Assets/Scripts/Battle/BattleController.cs.meta`

---

### Task 4: Unity Editor — create the SpellListPanel prefab and wire it

> **All steps in this task are Unity Editor actions performed by the user.**

**Step 1: Create the Spell List Panel UI prefab**

> **Unity Editor task (user):**
> 1. In the Project window, navigate to `Assets/Scripts/Battle/UI/`.
> 2. Right-click → Create → Prefab. Name it `SpellListPanel`.
> 3. Open the prefab in Prefab Mode.
> 4. Add a root `GameObject` named `Panel` as the first child:
>    - Add an `Image` component (use the panel background sprite from ItemMenuUI's panel for consistency).
>    - Set the RectTransform anchors to center, size to ~500x400.
> 5. Under `Panel`, add:
>    - **Header Text** (TMP_Text): "Available Spells" at the top, centered.
>    - **Scroll View** (Unity UI ScrollRect) for the spell list content:
>      - Content GameObject with `Vertical Layout Group` + `Content Size Fitter` (Vertical: Preferred).
>      - Viewport → Content → (rows will be instantiated here at runtime).
>    - **Empty Message** (TMP_Text): "No spells available" — centered, hidden by default.
>    - **Close Button** (Button with X or "Close" text) — top-right corner.
> 6. Create a spell row template prefab:
>    - In `Assets/Scripts/Battle/UI/`, create a Prefab named `SpellRow`.
>    - It should be a single GameObject with a `TMP_Text` child, font size ~24, centered.
>    - Add a `Layout Element` with Min Height = 40 for consistent row sizing.
>    - No Button component — rows are read-only.
> 7. **Save the prefab.**

**Step 2: Add SpellListPanelUI to the Battle Canvas**

> **Unity Editor task (user):**
> 1. Open `Assets/Scenes/Battle.unity`.
> 2. Find the Battle Canvas in the Hierarchy.
> 3. Drag the `SpellListPanel` prefab as a child of the Battle Canvas.
> 4. With the SpellListPanel selected, add the `SpellListPanelUI` component:
>    - Drag the root `Panel` GameObject into `_panel`.
>    - Drag the Scroll View's Content into `_contentParent`.
>    - Drag the `SpellRow` prefab into `_spellRowPrefab`.
>    - Drag the Close Button into `_closeButton`.
>    - Drag the Empty Message TMP_Text into `_emptyMessageText`.
> 5. Ensure the panel starts disabled (uncheck the GameObject in the Inspector so it's off by default).
> 6. **Save the scene.**

**Step 3: Wire the BattleController's SpellListPanelUI reference**

> **Unity Editor task (user):**
> 1. Select the `BattleController` GameObject in the Battle scene.
> 2. In the Inspector, find the new `_spellListPanelUI` field.
> 3. Drag the `SpellListPanel` GameObject (with the `SpellListPanelUI` component) into the field.
> 4. **Save the scene.**

**Step 4: Smoke test in Play Mode**

> **Unity Editor task (user):**
> 1. Open the Battle scene and enter Play Mode.
> 2. Press the **Spell** button — the spell list panel should appear showing unlocked spells (or "No spells available").
> 3. Verify rows are read-only (no highlight, no click response).
> 4. Press the **Close** (X) button — panel should dismiss, action menu should re-enable.
> 5. Press **Spell** again → panel opens → press **Spell** again → panel dismisses and SpellInputUI prompt appears ("Hold [Space] and speak...").
> 6. Verify voice spell recognition still works after the panel dismisses.

**Step 5: Check in via UVCS**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-88): add SpellListPanel prefab and wire Battle scene references`
  - `Assets/Scripts/Battle/UI/SpellListPanel.prefab`
  - `Assets/Scripts/Battle/UI/SpellListPanel.prefab.meta`
  - `Assets/Scripts/Battle/UI/SpellRow.prefab`
  - `Assets/Scripts/Battle/UI/SpellRow.prefab.meta`
  - `Assets/Scenes/Battle.unity`
  - `Assets/Scenes/Battle.unity.meta`

---

## Acceptance Criteria Verification

| AC | How Verified |
|----|-------------|
| Spell button opens centered panel with unlocked spells | Smoke test Step 4-2; SpellListPanelLogicTests verify data flow |
| List is read-only, no interaction | Smoke test Step 4-3; SpellRow has no Button component |
| Each row shows the spell name | SpellListPanelLogicTests.SpellNames verify Title Case conversion |
| "No spells available" when empty | SpellListPanelLogic.EmptyMessage; SpellListPanelUI.Show() with empty logic |
| Spell button again or close dismisses the panel | Smoke test Steps 4-4 and 4-5 |
| Panel in `Assets/Scripts/Battle/UI/` | Prefab created in correct folder |
| Reads from GameManager/SpellData | SpellListPanelLogic.BuildFromSpellUnlockService reads from GameManager.Instance.SpellUnlockService |
| No new MonoBehaviour business logic | All logic in SpellListPanelLogic (plain C#); SpellListPanelUI only wires Unity objects |
