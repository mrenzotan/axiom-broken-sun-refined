# DEV-71: BattleVoiceBootstrap Can Throw Null Reference During Voice Pipeline Injection

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add early required-reference validation to `BattleVoiceBootstrap` so that missing `_microphoneInputHandler` or `_spellCastController` Inspector references do not throw `NullReferenceException` at runtime. When a required reference is missing, the component logs a clear actionable error, calls `DisableSpell()`, and disables itself — all *before* any Vosk model work begins.

**Architecture:** `BattleVoiceBootstrap` is a MonoBehaviour whose `IEnumerator Start()` coroutine loads the Vosk model on a background `Task`, constructs a `VoskRecognizerService`, then calls `_microphoneInputHandler.Inject(...)` and `_spellCastController.Inject(...)` (lines 141–142). Those two `Inject` calls dereference the serialized references with no null check — if either is unassigned in the Inspector, an NRE fires *after* the ~50 MB Vosk model has already loaded (wasted startup time + confusing error location). The fix extracts the guard into a pure helper `ValidateRequiredReferences(out string)`, calls it at the very top of `Start()`, and exits cleanly on failure.

The fix deliberately mirrors the style established by DEV-70 (`MicrophoneInputHandler` null guards): Edit Mode tests exercise pure-C# logic only; coroutines and Vosk are not driven in tests; private `[SerializeField]` fields are set from tests via reflection rather than adding test-only setters or `InternalsVisibleTo`.

**Tech Stack:** Unity 6 LTS, C#, NUnit (Edit Mode tests), Coroutines, Vosk

**Jira:** DEV-71 — Bug — Labels: `bug`, `unity`, `vosk`
**Parent:** DEV-44 (Phase 5: Data Layer & Progression)

---

## Task 1: Write Failing Tests for ValidateRequiredReferences

**Files:**
- Create: `Assets/Tests/Editor/Voice/BattleVoiceBootstrapTests.cs`
- Create: `Assets/Tests/Editor/Voice/BattleVoiceBootstrapTests.cs.meta` (Unity auto-generated)

**Step 1: Write the guard-path tests**

The tests target `ValidateRequiredReferences(out string)` — a pure helper method we will add to `BattleVoiceBootstrap` in Task 2. The helper has no coroutine, no Vosk, and no scene dependencies, so it is safely Edit-Mode testable.

Private `[SerializeField]` fields are populated via a small reflection helper. This avoids adding test-only setters to the production class and avoids `InternalsVisibleTo` wiring on the `Axiom.Voice` asmdef.

```csharp
using System.Reflection;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Voice.Tests
{
    public class BattleVoiceBootstrapTests
    {
        private GameObject _bootstrapGo;
        private BattleVoiceBootstrap _bootstrap;
        private GameObject _micGo;
        private GameObject _spellGo;

        [SetUp]
        public void SetUp()
        {
            _bootstrapGo = new GameObject("TestBootstrap");
            _bootstrap = _bootstrapGo.AddComponent<BattleVoiceBootstrap>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_bootstrapGo != null) Object.DestroyImmediate(_bootstrapGo);
            if (_micGo != null) Object.DestroyImmediate(_micGo);
            if (_spellGo != null) Object.DestroyImmediate(_spellGo);
        }

        // ── ValidateRequiredReferences ──────────────────────────────────────────────

        [Test]
        public void ValidateRequiredReferences_AllNull_ReturnsFalseAndReportsMissingField()
        {
            bool result = _bootstrap.ValidateRequiredReferences(out string missingRefName);

            Assert.IsFalse(result, "Should return false when required refs are null.");
            Assert.AreEqual("_microphoneInputHandler", missingRefName,
                "Should report the first missing field in declaration order.");
        }

        [Test]
        public void ValidateRequiredReferences_MicrophoneInputHandlerMissing_ReturnsFalse()
        {
            // Assign only SpellCastController. _microphoneInputHandler stays null.
            _spellGo = new GameObject("SpellController");
            SetPrivateField(_bootstrap, "_spellCastController",
                _spellGo.AddComponent<SpellCastController>());

            bool result = _bootstrap.ValidateRequiredReferences(out string missingRefName);

            Assert.IsFalse(result);
            Assert.AreEqual("_microphoneInputHandler", missingRefName);
        }

        [Test]
        public void ValidateRequiredReferences_SpellCastControllerMissing_ReturnsFalse()
        {
            // Assign only MicrophoneInputHandler. _spellCastController stays null.
            _micGo = new GameObject("MicHandler");
            SetPrivateField(_bootstrap, "_microphoneInputHandler",
                _micGo.AddComponent<MicrophoneInputHandler>());

            bool result = _bootstrap.ValidateRequiredReferences(out string missingRefName);

            Assert.IsFalse(result);
            Assert.AreEqual("_spellCastController", missingRefName);
        }

        [Test]
        public void ValidateRequiredReferences_AllPresent_ReturnsTrueAndNoMissingName()
        {
            _micGo = new GameObject("MicHandler");
            _spellGo = new GameObject("SpellController");
            SetPrivateField(_bootstrap, "_microphoneInputHandler",
                _micGo.AddComponent<MicrophoneInputHandler>());
            SetPrivateField(_bootstrap, "_spellCastController",
                _spellGo.AddComponent<SpellCastController>());

            bool result = _bootstrap.ValidateRequiredReferences(out string missingRefName);

            Assert.IsTrue(result, "Should return true when both required refs are assigned.");
            Assert.IsNull(missingRefName, "No missing ref name when all present.");
        }

        // ── Reflection helper ───────────────────────────────────────────────────────

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(field,
                $"Private field '{fieldName}' not found on {target.GetType().Name}. " +
                "Check field name and access modifier.");

            field.SetValue(target, value);
        }
    }
}
```

**Step 2: Run tests — they fail to compile because `ValidateRequiredReferences` does not yet exist**

> **Unity Editor task (user):** Open Window → General → Test Runner, select the **Edit Mode** tab. The assembly will fail to compile with a `CS1061` error: `'BattleVoiceBootstrap' does not contain a definition for 'ValidateRequiredReferences'`. This is the expected TDD red state — the test file defines the API contract we will implement in Task 2.

---

## Task 2: Implement ValidateRequiredReferences and Wire It Into Start()

**Files:**
- Modify: `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`

**Step 1: Add the validation helper**

Add this public method to `BattleVoiceBootstrap`, placed just above `private void OnDestroy()` (around the current line 205):

```csharp
/// <summary>
/// Validates that all required Inspector references are assigned.
/// Returns <c>true</c> when all required refs are present. On failure,
/// returns <c>false</c> and sets <paramref name="missingRefName"/> to
/// the name of the first missing field (in declaration order).
///
/// <para>
/// Called at the top of <see cref="Start"/> before any async Vosk work
/// so that missing refs fail fast with a clear diagnostic instead of
/// throwing <see cref="System.NullReferenceException"/> deep in the
/// coroutine after the 50MB Vosk model has been loaded.
/// </para>
///
/// <para>
/// Public to allow Edit Mode tests to exercise the guard paths without
/// running the coroutine. <c>_actionMenuUI</c> is intentionally excluded
/// from this check: <see cref="DisableSpell"/> already handles its own
/// null case with a logged error and is still useful to call even when
/// the UI reference is missing (nothing crashes).
/// </para>
/// </summary>
public bool ValidateRequiredReferences(out string missingRefName)
{
    if (_microphoneInputHandler == null)
    {
        missingRefName = nameof(_microphoneInputHandler);
        return false;
    }

    if (_spellCastController == null)
    {
        missingRefName = nameof(_spellCastController);
        return false;
    }

    missingRefName = null;
    return true;
}
```

**Step 2: Wire the guard into the Start() coroutine**

Add the validation block at the very top of `private IEnumerator Start()` — before the `Microphone.devices.Length == 0` check currently on line 63:

```csharp
private IEnumerator Start()
{
    if (!ValidateRequiredReferences(out string missingRef))
    {
        Debug.LogError(
            $"[BattleVoiceBootstrap] Required reference '{missingRef}' is not assigned " +
            "in the Inspector. Voice pipeline will not start; Spell action disabled.", this);
        DisableSpell();
        enabled = false;
        yield break;
    }

    if (Microphone.devices.Length == 0)
    // ... rest of Start() unchanged
```

**Rationale for ordering:**
- `ValidateRequiredReferences` must come **before** the microphone and model checks. Those downstream checks also call `DisableSpell()` and `yield break`, but they run *after* potentially expensive work (device enumeration, file existence check). Validating Inspector wiring is the cheapest check, so it goes first.
- `enabled = false` prevents `OnDestroy`'s cleanup path from touching partially-initialized Vosk state, since none of `_voskModel`, `_recognizerService`, or `_activeSpells` is assigned on this early-exit path. The existing null-conditional operators in `OnDestroy` already handle the uninitialized case, so no change to `OnDestroy` is needed.

**Step 3: Run tests — all four should now pass**

> **Unity Editor task (user):** Test Runner → Edit Mode tab → run `BattleVoiceBootstrapTests`. All four tests (`_AllNull_`, `_MicrophoneInputHandlerMissing_`, `_SpellCastControllerMissing_`, `_AllPresent_`) should pass.

---

## Task 3: Verify Full Voice Test Suite Still Passes

**Files:**
- No code changes. Verification step only.

> **Unity Editor task (user):** Test Runner → Edit Mode tab → run the entire **Voice** folder. All tests in `VoskRecognizerServiceTests`, `MicrophoneCaptureTests`, `SpellResultMatcherTests`, `SpellVocabularyManagerTests`, `MicrophoneInputHandlerTests`, `VoskSetupTests`, and the new `BattleVoiceBootstrapTests` should pass (green). Report back if any test is red — the new code should only add coverage, not regress existing behavior.

---

## Task 4: UVCS Check-in

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-71): add required-reference validation to BattleVoiceBootstrap`
  - `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`
  - `Assets/Scripts/Voice/BattleVoiceBootstrap.cs.meta`
  - `Assets/Tests/Editor/Voice/BattleVoiceBootstrapTests.cs`
  - `Assets/Tests/Editor/Voice/BattleVoiceBootstrapTests.cs.meta`

---

## Acceptance Criteria Verification

| Criterion | How Verified |
|-----------|-------------|
| Missing refs do not throw runtime exception | `ValidateRequiredReferences_*_ReturnsFalse` tests confirm the guard returns false on each missing-ref permutation. `Start()` calls the guard before touching the fields, so the NRE at the `Inject(...)` calls (old lines 141–142) is unreachable when validation fails. |
| System logs clear actionable error | `Debug.LogError` in `Start()` names the specific missing field (e.g. `_microphoneInputHandler`) and instructs the developer to assign it in the Inspector. |
| Spell action is safely disabled on setup failure | `Start()` calls `DisableSpell()` and sets `enabled = false` immediately after logging. `DisableSpell()` already handles its own null-UI case via the existing error log. |
| Tests verify guard paths | Four tests in `BattleVoiceBootstrapTests` cover every permutation of the two required refs: both null, mic-only missing, spell-only missing, all present. |
