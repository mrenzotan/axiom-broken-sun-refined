# DEV-22: SpellCastController Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create `SpellCastController` — a MonoBehaviour that polls the Vosk result queue each `Update()`, matches recognized text against the player's unlocked `SpellData` assets, and dispatches confirmed spells to `BattleController`.

**Architecture:** A plain C# class `SpellResultMatcher` handles all parsing and matching logic (fully testable in Edit Mode). `SpellCastController` (MonoBehaviour) handles only Unity lifecycle: it polls a `ConcurrentQueue<string>`, delegates to `SpellResultMatcher`, and calls `BattleController.OnSpellCast(spell)` on a match. `Axiom.Voice.asmdef` gains an `Axiom.Battle` reference so `SpellCastController` can hold a direct `BattleController` reference — voice dispatches into battle, not the other way around.

**Tech Stack:** Unity 6 LTS, C#, Vosk (result JSON already in queue), NUnit (Edit Mode tests via Unity Test Runner), `System.Collections.Concurrent.ConcurrentQueue<string>`

---

## File Map

| Action   | Path                                                               | Responsibility                                           |
|----------|--------------------------------------------------------------------|----------------------------------------------------------|
| Create   | `Assets/Scripts/Voice/SpellResultMatcher.cs`                       | Parse Vosk JSON → extract text; match text → `SpellData` |
| Create   | `Assets/Scripts/Voice/SpellCastController.cs`                      | MonoBehaviour: poll queue, dispatch matched spell         |
| Create   | `Assets/Tests/Editor/Voice/SpellResultMatcherTests.cs`             | Edit Mode unit tests for `SpellResultMatcher`            |
| Modify   | `Assets/Scripts/Voice/Axiom.Voice.asmdef`                          | Add `"Axiom.Battle"` to `references`                     |
| Modify   | `Assets/Scripts/Battle/BattleController.cs`                        | Add `OnSpellCast(SpellData spell)` public method         |

> `PlayerSpell()` in `BattleController` is the **placeholder** wired to the Action Menu button — it is left unchanged. `OnSpellCast(SpellData)` is the new entry point for voice-dispatched spells.

---

## Task 1: Add `Axiom.Battle` to `Axiom.Voice.asmdef`

**Files:**
- Modify: `Assets/Scripts/Voice/Axiom.Voice.asmdef`

`SpellCastController` (in `Axiom.Voice`) needs to hold a `BattleController` reference (in `Axiom.Battle`). Add the reference now so subsequent tasks compile.

- [ ] **Open** `Assets/Scripts/Voice/Axiom.Voice.asmdef` and replace the `references` array:

```json
{
    "name": "Axiom.Voice",
    "references": [
        "Axiom.Data",
        "Axiom.Battle",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "Vosk.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Check compile errors** — Unity Editor should recompile with no errors after saving.

---

## Task 2: Create `SpellResultMatcher` (plain C# class)

**Files:**
- Create: `Assets/Scripts/Voice/SpellResultMatcher.cs`

This class has two responsibilities:
1. `ExtractTextField(string json)` — parse the `"text"` field from a Vosk JSON result string
2. `Match(string voskJson, IReadOnlyList<SpellData> unlockedSpells)` — return the first `SpellData` whose `spellName` matches the extracted text (case-insensitive, trimmed), or `null`

Vosk final results always have the shape `{"text": "hydrogen blast"}`. Partial results (`{"partial": "hydrogen"}`) have no `"text"` key, so `ExtractTextField` returns `""` for them — which causes `Match` to return `null`. No external JSON library is needed.

- [ ] **Create** `Assets/Scripts/Voice/SpellResultMatcher.cs`:

```csharp
using System;
using System.Collections.Generic;
using Axiom.Data;

namespace Axiom.Voice
{
    /// <summary>
    /// Stateless utility for matching a Vosk JSON result string against the player's
    /// unlocked spell list. Contains no Unity types — fully testable in Edit Mode.
    ///
    /// Vosk final results: {"text": "hydrogen blast"}
    /// Vosk partial results: {"partial": "hydrogen"}  ← no "text" key → ExtractTextField returns ""
    /// </summary>
    public static class SpellResultMatcher
    {
        /// <summary>
        /// Parses the <c>"text"</c> field from a Vosk result JSON string.
        /// Returns <see cref="string.Empty"/> when no <c>"text"</c> key is present
        /// (e.g. partial results) or when the value is empty/whitespace.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="voskJson"/> is null.</exception>
        public static string ExtractTextField(string voskJson)
        {
            if (voskJson == null) throw new ArgumentNullException(nameof(voskJson));

            const string key = "\"text\"";
            int keyIdx = voskJson.IndexOf(key, StringComparison.Ordinal);
            if (keyIdx < 0) return string.Empty;

            int colonIdx = voskJson.IndexOf(':', keyIdx + key.Length);
            if (colonIdx < 0) return string.Empty;

            int openQuote = voskJson.IndexOf('"', colonIdx + 1);
            if (openQuote < 0) return string.Empty;

            int closeQuote = voskJson.IndexOf('"', openQuote + 1);
            if (closeQuote < 0) return string.Empty;

            return voskJson.Substring(openQuote + 1, closeQuote - openQuote - 1).Trim();
        }

        /// <summary>
        /// Returns the first <see cref="SpellData"/> in <paramref name="unlockedSpells"/> whose
        /// <c>spellName</c> matches the <c>"text"</c> field in <paramref name="voskJson"/>
        /// (case-insensitive, trimmed). Returns <c>null</c> when:
        /// <list type="bullet">
        ///   <item>the spell list is empty</item>
        ///   <item>the JSON contains no <c>"text"</c> key (e.g. partial result)</item>
        ///   <item>the recognized text is empty or whitespace</item>
        ///   <item>no spell name matches</item>
        /// </list>
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="voskJson"/> or <paramref name="unlockedSpells"/> is null.
        /// </exception>
        public static SpellData Match(string voskJson, IReadOnlyList<SpellData> unlockedSpells)
        {
            if (voskJson == null)        throw new ArgumentNullException(nameof(voskJson));
            if (unlockedSpells == null)  throw new ArgumentNullException(nameof(unlockedSpells));
            if (unlockedSpells.Count == 0) return null;

            string recognized = ExtractTextField(voskJson);
            if (string.IsNullOrWhiteSpace(recognized)) return null;

            foreach (SpellData spell in unlockedSpells)
            {
                if (string.Equals(spell.spellName, recognized, StringComparison.OrdinalIgnoreCase))
                    return spell;
            }

            return null;
        }
    }
}
```

- [ ] **Check compile errors** — Unity Editor should recompile with no errors.

---

## Task 3: Write `SpellResultMatcherTests` (Edit Mode)

**Files:**
- Create: `Assets/Tests/Editor/Voice/SpellResultMatcherTests.cs`

All tests run in Edit Mode — no scene, no Vosk model, no microphone required.

- [ ] **Create** `Assets/Tests/Editor/Voice/SpellResultMatcherTests.cs`:

```csharp
using System.Collections.Generic;
using Axiom.Data;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Voice
{
    public class SpellResultMatcherTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────────

        private static SpellData MakeSpell(string name)
        {
            var so = ScriptableObject.CreateInstance<SpellData>();
            so.spellName = name;
            return so;
        }

        // ── ExtractTextField ───────────────────────────────────────────────────────

        [Test]
        public void ExtractTextField_NullJson_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => SpellResultMatcher.ExtractTextField(null));
        }

        [Test]
        public void ExtractTextField_FinalResultJson_ReturnsSpellText()
        {
            string result = SpellResultMatcher.ExtractTextField("{\"text\": \"hydrogen blast\"}");
            Assert.AreEqual("hydrogen blast", result);
        }

        [Test]
        public void ExtractTextField_PartialResultJson_ReturnsEmpty()
        {
            // Partial results have no "text" key — must not be treated as a match.
            string result = SpellResultMatcher.ExtractTextField("{\"partial\": \"hydrogen\"}");
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void ExtractTextField_EmptyTextField_ReturnsEmpty()
        {
            string result = SpellResultMatcher.ExtractTextField("{\"text\": \"\"}");
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void ExtractTextField_WhitespaceTextField_ReturnsEmpty()
        {
            // Vosk can produce "  " — trimmed result is empty.
            string result = SpellResultMatcher.ExtractTextField("{\"text\": \"   \"}");
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void ExtractTextField_EmptyJsonString_ReturnsEmpty()
        {
            string result = SpellResultMatcher.ExtractTextField("{}");
            Assert.AreEqual(string.Empty, result);
        }

        // ── Match ──────────────────────────────────────────────────────────────────

        [Test]
        public void Match_NullJson_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => SpellResultMatcher.Match(null, new List<SpellData>()));
        }

        [Test]
        public void Match_NullSpellList_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => SpellResultMatcher.Match("{\"text\": \"hydrogen blast\"}", null));
        }

        [Test]
        public void Match_EmptySpellList_ReturnsNull()
        {
            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"hydrogen blast\"}",
                new List<SpellData>());

            Assert.IsNull(result);
        }

        [Test]
        public void Match_KnownSpell_ExactCase_ReturnsSpellData()
        {
            var spell = MakeSpell("hydrogen blast");

            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"hydrogen blast\"}",
                new List<SpellData> { spell });

            Assert.AreSame(spell, result);
        }

        [Test]
        public void Match_KnownSpell_UppercaseRecognized_ReturnsSpellData()
        {
            // Vosk occasionally returns uppercase — matching must be case-insensitive.
            var spell = MakeSpell("hydrogen blast");

            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"HYDROGEN BLAST\"}",
                new List<SpellData> { spell });

            Assert.AreSame(spell, result);
        }

        [Test]
        public void Match_KnownSpell_MixedCaseRecognized_ReturnsSpellData()
        {
            var spell = MakeSpell("acid rain");

            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"Acid Rain\"}",
                new List<SpellData> { spell });

            Assert.AreSame(spell, result);
        }

        [Test]
        public void Match_UnknownWord_ReturnsNull()
        {
            var spell = MakeSpell("hydrogen blast");

            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"fire ball\"}",
                new List<SpellData> { spell });

            Assert.IsNull(result);
        }

        [Test]
        public void Match_EmptyTextField_ReturnsNull()
        {
            var spell = MakeSpell("hydrogen blast");

            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"\"}",
                new List<SpellData> { spell });

            Assert.IsNull(result);
        }

        [Test]
        public void Match_PartialResultJson_ReturnsNull()
        {
            // Partial results must never dispatch a spell.
            var spell = MakeSpell("hydrogen blast");

            SpellData result = SpellResultMatcher.Match(
                "{\"partial\": \"hydrogen blast\"}",
                new List<SpellData> { spell });

            Assert.IsNull(result);
        }

        [Test]
        public void Match_MultipleSpells_ReturnsCorrectOne()
        {
            var spellA = MakeSpell("hydrogen blast");
            var spellB = MakeSpell("acid rain");
            var spellC = MakeSpell("ember strike");

            SpellData result = SpellResultMatcher.Match(
                "{\"text\": \"acid rain\"}",
                new List<SpellData> { spellA, spellB, spellC });

            Assert.AreSame(spellB, result);
        }
    }
}
```

- [ ] **Run** Unity Editor → Window → General → Test Runner → Edit Mode → filter `SpellResultMatcherTests` → Run Selected.

Expected: **all 13 tests FAIL** (class `SpellResultMatcher` doesn't exist yet).

> Wait — `SpellResultMatcher.cs` was created in Task 2. If you followed task order, these tests should **pass** on first run. This is the correct TDD outcome: write the class alongside the tests in the same session. Verify all 13 pass before continuing.

Expected: **13 tests PASS**.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-22): SpellResultMatcher — parse Vosk JSON and match to SpellData`
  - `Assets/Scripts/Voice/Axiom.Voice.asmdef`
  - `Assets/Scripts/Voice/SpellResultMatcher.cs`
  - `Assets/Scripts/Voice/SpellResultMatcher.cs.meta`
  - `Assets/Tests/Editor/Voice/SpellResultMatcherTests.cs`
  - `Assets/Tests/Editor/Voice/SpellResultMatcherTests.cs.meta`

---

## Task 4: Add `OnSpellCast(SpellData)` to `BattleController`

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

`PlayerSpell()` (the Action Menu button placeholder) remains untouched. This new method is the voice-specific entry point — `SpellCastController` calls it when a spell is recognized.

- [ ] **Open** `Assets/Scripts/Battle/BattleController.cs`. Locate the `PlayerSpell()` method (around line 176). Add the new method immediately after it:

```csharp
/// <summary>
/// Called by SpellCastController when a recognized spell name matches an unlocked spell.
/// Guards against calls outside PlayerTurn or while an action is already processing.
/// In Phase 3 the spell executes without damage — Phase 6 will add per-spell effects.
/// </summary>
public void OnSpellCast(SpellData spell)
{
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
    if (_isProcessingAction) return;
    _isProcessingAction = true;
    _playerDamageVisualsFired = true; // No damage visuals for spells in Phase 3
    Debug.Log($"[Battle] Voice spell cast: {spell.spellName}");
    StartCoroutine(CompletePlayerAction(targetDefeated: false));
}
```

- [ ] **Add** the `using Axiom.Data;` directive at the top of `BattleController.cs` if it isn't already present. The file currently uses `using UnityEngine.SceneManagement;` — add the Data using below it:

```csharp
using Axiom.Data;
```

- [ ] **Check compile errors** — Unity Editor recompiles with no errors.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-22): add OnSpellCast(SpellData) voice-spell entry point`
  - `Assets/Scripts/Battle/BattleController.cs`

---

## Task 5: Create `SpellCastController` (MonoBehaviour)

**Files:**
- Create: `Assets/Scripts/Voice/SpellCastController.cs`

**Dependency direction:** `Axiom.Voice` references `Axiom.Battle` (Task 1). `Axiom.Battle` must NOT reference `Axiom.Voice` — that would create a circular dependency. Therefore `SpellCastController` holds the `BattleController` reference (Inspector-assigned via `[SerializeField]`), and `BattleController` has no field pointing back at `SpellCastController`.

This MonoBehaviour:
- Has `[SerializeField] BattleController _battleController` — assigned in the Inspector
- Holds the result queue and spell list via `Inject()` — called by the Vosk bootstrap (wired in a later ticket; `Start()` creates a stub queue for Battle scene isolation in the meantime)
- Polls the queue in `Update()` — dequeues every pending result each frame
- Delegates matching to `SpellResultMatcher.Match()` and calls `_battleController.OnSpellCast(spell)` on a hit
- Logs a one-time warning if `_battleController` is not assigned

The MonoBehaviour contains **no parsing or matching logic** — that lives in `SpellResultMatcher`.

- [ ] **Create** `Assets/Scripts/Voice/SpellCastController.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Axiom.Battle;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Voice
{
    /// <summary>
    /// MonoBehaviour that polls the Vosk result queue each frame, matches recognized text
    /// against the player's unlocked spell list, and dispatches confirmed spells to
    /// <see cref="BattleController"/>. Contains no parsing or matching logic — delegates
    /// entirely to <see cref="SpellResultMatcher"/>.
    ///
    /// Assign <see cref="_battleController"/> in the Inspector.
    /// Call <see cref="Inject"/> with the shared result queue and unlocked spell list
    /// before or during the voice-spell phase. If Inject is never called, Start() creates
    /// a stub empty queue so the Battle scene runs without a Vosk service attached.
    /// Typical Inject caller: a scene-level Vosk bootstrap MonoBehaviour (Phase 3).
    /// </summary>
    public class SpellCastController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Assign the BattleController component from this scene.")]
        private BattleController _battleController;

        private ConcurrentQueue<string>  _resultQueue;
        private IReadOnlyList<SpellData> _unlockedSpells;
        private bool                     _battleControllerWarningLogged;

        // ── Injection ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Injects the shared Vosk result queue and the player's current unlocked spell list.
        /// Call this before or after Start() — the queue is replaced if already stubbed.
        /// </summary>
        public void Inject(
            ConcurrentQueue<string>  resultQueue,
            IReadOnlyList<SpellData> unlockedSpells)
        {
            _resultQueue    = resultQueue;
            _unlockedSpells = unlockedSpells;
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Start()
        {
            // Stub queue and empty spell list keep Update() safe when Inject() has not been
            // called yet — allows the Battle scene to run in isolation without a Vosk service.
            _resultQueue    = _resultQueue    ?? new ConcurrentQueue<string>();
            _unlockedSpells = _unlockedSpells ?? Array.Empty<SpellData>();
        }

        private void Update()
        {
            while (_resultQueue.TryDequeue(out string voskJson))
            {
                SpellData matched = SpellResultMatcher.Match(voskJson, _unlockedSpells);
                if (matched == null) continue;

                if (_battleController == null)
                {
                    if (!_battleControllerWarningLogged)
                    {
                        Debug.LogError("[SpellCastController] BattleController is not assigned in the Inspector.", this);
                        _battleControllerWarningLogged = true;
                    }
                    continue;
                }

                _battleController.OnSpellCast(matched);
            }
        }
    }
}
```

- [ ] **Check compile errors** — Unity Editor recompiles with no errors.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-22): SpellCastController polls result queue and dispatches matched spells`
  - `Assets/Scripts/Voice/SpellCastController.cs`
  - `Assets/Scripts/Voice/SpellCastController.cs.meta`

---

## Task 6: Wire SpellCastController in the Battle Scene

**Files:**
- No new scripts. Unity Editor scene wiring only.

`SpellCastController` must live as a component in the Battle scene with its `BattleController` reference assigned in the Inspector. `BattleController` does **not** hold a reference to `SpellCastController` — that would require `Axiom.Battle` to reference `Axiom.Voice`, creating a circular assembly dependency.

### 6a — Add SpellCastController to the scene

> **Unity Editor task (user):** Open `Assets/Scenes/Battle.unity`. Select the GameObject that holds `BattleController`. Add a `SpellCastController` component to the same GameObject via Add Component → Scripts → Axiom.Voice → Spell Cast Controller.

### 6b — Assign BattleController in the Inspector

> **Unity Editor task (user):** With the same GameObject selected, find the **Spell Cast Controller** component in the Inspector. Drag the `BattleController` component (on the same GameObject) into the **Battle Controller** slot.

- [ ] **Check** the Unity Console — no errors after entering Play Mode.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-22): add SpellCastController to Battle scene and assign BattleController`
  - `Assets/Scenes/Battle.unity`

---

## Task 7: Smoke Test in Play Mode

There is no automated Play Mode test for `SpellCastController` at this stage — the full pipeline requires a live Vosk model and microphone. This task verifies the wiring doesn't produce errors.

- [ ] **Enter Play Mode** in the Unity Editor (press the Play button). Verify:
  - No `NullReferenceException` in the console.
  - No `[SpellCastController] Inject() was not called` error in the console.
  - The battle scene initializes normally (BattleManager starts, turn indicator appears).
  - The existing Attack / Item / Flee buttons still work (regression check — `PlayerSpell()` placeholder is untouched).

- [ ] **Exit Play Mode.**

---

## Self-Review

### 1. Spec coverage

| DEV-22 requirement | Task that covers it |
|---|---|
| MonoBehaviour that polls result queue in `Update()` | Task 5 (`SpellCastController.Update()`) |
| Match recognized text against `SpellData` ScriptableObjects | Task 2 (`SpellResultMatcher.Match`) |
| Dispatch matched spell to BattleManager | Task 4 + Task 5 (`OnSpellCast` → `BattleManager` via `BattleController`) |
| `Inject()` pattern (consistent with `MicrophoneInputHandler`) | Task 5 |
| `Axiom.Battle` reference in `Axiom.Voice.asmdef` | Task 1 |
| No business logic in MonoBehaviour | Enforced: all logic in `SpellResultMatcher`; `SpellCastController` only wires |
| Tests for matching logic | Task 3 (13 Edit Mode tests) |

### 2. Guard clause ordering

**`SpellResultMatcher.Match`:**
1. `voskJson == null` → throw (can't extract text without json)
2. `unlockedSpells == null` → throw (can't match without list)
3. `unlockedSpells.Count == 0` → return null (no spells to match — exits before iterating)
4. `ExtractTextField` → if empty/whitespace → return null (no recognized text → no match)
5. Loop → return match or null

**`ExtractTextField`:**
1. `voskJson == null` → throw
2. String parsing guards (key not found → return empty, quote not found → return empty)

Order is correct — early exits skip work that's irrelevant.

### 3. Test coverage gaps

| Branch | Test |
|---|---|
| `ExtractTextField(null)` | `ExtractTextField_NullJson_ThrowsArgumentNullException` ✓ |
| `ExtractTextField` — final result with text | `ExtractTextField_FinalResultJson_ReturnsSpellText` ✓ |
| `ExtractTextField` — partial (no "text" key) | `ExtractTextField_PartialResultJson_ReturnsEmpty` ✓ |
| `ExtractTextField` — empty text value | `ExtractTextField_EmptyTextField_ReturnsEmpty` ✓ |
| `ExtractTextField` — whitespace-only text value | `ExtractTextField_WhitespaceTextField_ReturnsEmpty` ✓ |
| `ExtractTextField` — empty JSON object | `ExtractTextField_EmptyJsonString_ReturnsEmpty` ✓ |
| `Match(null, list)` | `Match_NullJson_ThrowsArgumentNullException` ✓ |
| `Match(json, null)` | `Match_NullSpellList_ThrowsArgumentNullException` ✓ |
| `Match(json, emptyList)` | `Match_EmptySpellList_ReturnsNull` ✓ |
| `Match` — happy path exact case | `Match_KnownSpell_ExactCase_ReturnsSpellData` ✓ |
| `Match` — case-insensitive (uppercase input) | `Match_KnownSpell_UppercaseRecognized_ReturnsSpellData` ✓ |
| `Match` — case-insensitive (mixed case) | `Match_KnownSpell_MixedCaseRecognized_ReturnsSpellData` ✓ |
| `Match` — no match in list | `Match_UnknownWord_ReturnsNull` ✓ |
| `Match` — empty text field | `Match_EmptyTextField_ReturnsNull` ✓ |
| `Match` — partial JSON (no "text" key) | `Match_PartialResultJson_ReturnsNull` ✓ |
| `Match` — multi-spell list, correct one selected | `Match_MultipleSpells_ReturnsCorrectOne` ✓ |

All reachable branches without external dependencies are covered.

### 4. UVCS check-in file audit

**Task 3 check-in:** `Axiom.Voice.asmdef`, `SpellResultMatcher.cs`, `SpellResultMatcher.cs.meta`, `SpellResultMatcherTests.cs`, `SpellResultMatcherTests.cs.meta`. ✓  
**Task 4 check-in:** `BattleController.cs`. ✓  
**Task 5 check-in:** `SpellCastController.cs`, `SpellCastController.cs.meta`. ✓  
**Task 6 check-in:** `Battle.unity`. ✓  

> Note: `.meta` files for new scripts are auto-generated by Unity on compile. Stage them alongside the `.cs` files in UVCS Pending Changes.

> **No circular dependency:** `Axiom.Voice.asmdef` references `Axiom.Battle`. `Axiom.Battle.asmdef` does NOT reference `Axiom.Voice`. `SpellCastController` (Voice) holds `BattleController` (Battle) via `[SerializeField]`. `BattleController` (Battle) has no field pointing at `SpellCastController`.

### 5. Method signature consistency

| Defined in | Used in | Match? |
|---|---|---|
| `SpellResultMatcher.ExtractTextField(string)` (Task 2) | Tests (Task 3) | ✓ |
| `SpellResultMatcher.Match(string, IReadOnlyList<SpellData>)` (Task 2) | Tests (Task 3), `SpellCastController.Update()` (Task 5) | ✓ |
| `BattleController.OnSpellCast(SpellData)` (Task 4) | `SpellCastController.Update()` (Task 5) | ✓ |
| `SpellCastController.Inject(ConcurrentQueue<string>, IReadOnlyList<SpellData>)` (Task 5) | Future: Vosk bootstrap (Phase 3 follow-up ticket) | ✓ |

### 6. Unity Editor task isolation

- Task 6a: dedicated `> **Unity Editor task (user):**` callout ✓
- Task 6b: dedicated `> **Unity Editor task (user):**` callout ✓
- No code steps mixed into editor callouts ✓
