# Spell Vocabulary Manager (DEV-20) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `SpellVocabularyManager` — a plain C# service that builds a Vosk JSON grammar from unlocked `SpellData` ScriptableObjects and creates a new `VoskRecognizer` off the main thread when the spell set changes.

**Architecture:** `SpellVocabularyManager` is a stateless plain C# service (no MonoBehaviour, no ScriptableObject). `BuildGrammarJson` converts a `IReadOnlyList<SpellData>` into a Vosk-compatible JSON array string. `RebuildRecognizerAsync` wraps `VoskRecognizer` construction in `Task.Run` to keep the main thread unblocked during reload. `SpellData` is a minimal ScriptableObject living in a new `Axiom.Data` assembly; the Voice assembly gains a reference to it.

**Tech Stack:** Unity 6 LTS, C# (.NET Standard 2.1), Vosk C# bindings (`Vosk.dll`), Unity ScriptableObject, `System.Threading.Tasks`

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Data/Axiom.Data.asmdef` | Assembly definition for Data module |
| Create | `Assets/Scripts/Data/SpellData.cs` | ScriptableObject — holds `spellName` spoken trigger |
| Modify | `Assets/Scripts/Voice/Axiom.Voice.asmdef` | Add `Axiom.Data` to `references` array |
| Modify | `Assets/Tests/Editor/Voice/VoiceTests.asmdef` | Add `Axiom.Data` to `references` array |
| Create | `Assets/Scripts/Voice/SpellVocabularyManager.cs` | Plain C# service — grammar JSON builder + async recognizer factory |
| Create | `Assets/Tests/Editor/Voice/SpellVocabularyManagerTests.cs` | Edit Mode NUnit tests for `BuildGrammarJson` |

> **Out of scope for DEV-20:** Wiring the returned `VoskRecognizer` into a running `VoskRecognizerService` on spell unlock events. That integration belongs to `SpellCastController` (a separate Phase 3 ticket). `SpellVocabularyManager` only builds and provides the recognizer — it does not own or manage the service lifecycle.

---

## Task 1: Create `Axiom.Data` assembly and `SpellData` ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/Axiom.Data.asmdef`
- Create: `Assets/Scripts/Data/SpellData.cs`

> **Unity Editor task (user):** In the Project window, right-click `Assets/Scripts` → Create → Folder → name it `Data`. Unity needs this folder to exist before the `.asmdef` file can be added.

- [ ] **Step 1: Create `Assets/Scripts/Data/Axiom.Data.asmdef`**

```json
{
    "name": "Axiom.Data",
    "references": [],
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

- [ ] **Step 2: Create `Assets/Scripts/Data/SpellData.cs`**

```csharp
using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewSpellData", menuName = "Axiom/Data/Spell Data")]
    public class SpellData : ScriptableObject
    {
        [Tooltip("The spoken trigger word or phrase the player says to cast this spell.")]
        public string spellName;
    }
}
```

- [ ] **Step 3: Verify Unity compiles with no errors**

Switch to the Unity Editor — check the Console (Window → General → Console). No errors should appear. If you see `Axiom.Data` assembly errors, confirm the `.asmdef` file sits inside `Assets/Scripts/Data/`.

> **Unity Editor task (user):** Optionally create a sample asset to confirm the ScriptableObject works: right-click `Assets/Data/Spells/` (create that folder first if needed) → Create → Axiom → Data → Spell Data → name it `HydrogenBlast`. Set `spellName` to `"hydrogen blast"` in the Inspector. You can delete it after verifying it shows up correctly.

---

## Task 2: Wire `Axiom.Data` into Voice assembly references

**Files:**
- Modify: `Assets/Scripts/Voice/Axiom.Voice.asmdef`
- Modify: `Assets/Tests/Editor/Voice/VoiceTests.asmdef`

- [ ] **Step 1: Replace `Assets/Scripts/Voice/Axiom.Voice.asmdef` with the following**

(Adds `"Axiom.Data"` to `references`. All other fields are unchanged.)

```json
{
    "name": "Axiom.Voice",
    "references": [
        "Axiom.Data"
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

- [ ] **Step 2: Replace `Assets/Tests/Editor/Voice/VoiceTests.asmdef` with the following**

(Adds `"Axiom.Data"` to `references` so test code can directly reference `SpellData`.)

```json
{
    "name": "VoiceTests",
    "references": [
        "Axiom.Voice",
        "Axiom.Data"
    ],
    "testReferences": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: Verify Unity compiles with no errors**

Check the Unity Console — no assembly resolution errors. The `Axiom.Voice` scripts now have access to `SpellData` from `Axiom.Data`.

- [ ] **Step 4: Check in to UVCS**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Data.meta`              ← folder meta generated by Unity Editor
- `Assets/Scripts/Data/Axiom.Data.asmdef`
- `Assets/Scripts/Data/Axiom.Data.asmdef.meta`
- `Assets/Scripts/Data/SpellData.cs`
- `Assets/Scripts/Data/SpellData.cs.meta`
- `Assets/Scripts/Voice/Axiom.Voice.asmdef`
- `Assets/Tests/Editor/Voice/VoiceTests.asmdef`

Check in with message: `feat(DEV-20): add SpellData SO and wire Axiom.Data into Voice assembly`

---

## Task 3: Write failing Edit Mode tests for `BuildGrammarJson`

**Files:**
- Create: `Assets/Tests/Editor/Voice/SpellVocabularyManagerTests.cs`

- [ ] **Step 1: Create the test file**

```csharp
using System.Collections.Generic;
using Axiom.Data;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Voice
{
    public class SpellVocabularyManagerTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static SpellData MakeSpell(string name)
        {
            var so = ScriptableObject.CreateInstance<SpellData>();
            so.spellName = name;
            return so;
        }

        // ── BuildGrammarJson ───────────────────────────────────────────────────

        [Test]
        public void BuildGrammarJson_EmptyList_ReturnsNull()
        {
            string result = SpellVocabularyManager.BuildGrammarJson(
                new List<SpellData>());

            Assert.IsNull(result);
        }

        [Test]
        public void BuildGrammarJson_SingleSpell_ReturnsJsonArrayWithThatName()
        {
            var spell = MakeSpell("hydrogen blast");

            string result = SpellVocabularyManager.BuildGrammarJson(
                new List<SpellData> { spell });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.StartsWith("["), $"Grammar must start with '[', got: {result}");
            Assert.IsTrue(result.EndsWith("]"),   $"Grammar must end with ']', got: {result}");
            StringAssert.Contains("\"hydrogen blast\"", result);
        }

        [Test]
        public void BuildGrammarJson_MultipleSpells_ContainsAllNames()
        {
            var spells = new List<SpellData>
            {
                MakeSpell("hydrogen blast"),
                MakeSpell("acid rain"),
                MakeSpell("ember strike"),
            };

            string result = SpellVocabularyManager.BuildGrammarJson(spells);

            Assert.IsNotNull(result);
            StringAssert.Contains("\"hydrogen blast\"", result);
            StringAssert.Contains("\"acid rain\"",      result);
            StringAssert.Contains("\"ember strike\"",   result);
        }

        [Test]
        public void BuildGrammarJson_SpellNameWithEmbeddedQuote_EscapesCorrectly()
        {
            // Spell names with embedded quotes must be escaped so Vosk receives valid JSON.
            var spell = MakeSpell("alchemist\"s fire");

            string result = SpellVocabularyManager.BuildGrammarJson(
                new List<SpellData> { spell });

            Assert.IsNotNull(result);
            // The embedded " must appear as \" in the output
            StringAssert.Contains("\\\"", result,
                $"Embedded quote must be escaped in JSON output, got: {result}");
        }

        [Test]
        public void BuildGrammarJson_NullList_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => SpellVocabularyManager.BuildGrammarJson(null));
        }

        // ── RebuildRecognizerAsync ─────────────────────────────────────────────
        // Only the empty-list path is testable in Edit Mode — it exits before
        // touching the Vosk Model, so we can safely pass null for model here.

        [Test]
        public async System.Threading.Tasks.Task RebuildRecognizerAsync_EmptyList_ReturnsNull()
        {
            // model is irrelevant when the list is empty — the method returns before
            // ever touching it, so passing null here is intentional.
            Vosk.VoskRecognizer result = await SpellVocabularyManager.RebuildRecognizerAsync(
                model: null,
                sampleRate: 16000f,
                unlockedSpells: new List<SpellData>());

            Assert.IsNull(result);
        }
    }
}
```

- [ ] **Step 2: Run the tests in Unity Test Runner — expect compile failure**

Unity Editor → Window → General → Test Runner → Edit Mode tab.

Expected: compile error — `SpellVocabularyManager` does not exist yet. This confirms the tests are driving the implementation (TDD).

---

## Task 4: Implement `SpellVocabularyManager` to make tests pass

**Files:**
- Create: `Assets/Scripts/Voice/SpellVocabularyManager.cs`

- [ ] **Step 1: Create the implementation**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Axiom.Data;
using Vosk;

namespace Axiom.Voice
{
    /// <summary>
    /// Stateless service that converts the player's unlocked spell list into a
    /// Vosk-compatible JSON grammar string and creates <see cref="VoskRecognizer"/>
    /// instances off the Unity main thread.
    ///
    /// No MonoBehaviour. No Unity lifecycle. All methods are thread-safe.
    ///
    /// Usage pattern on spell unlock:
    ///   1. Stop the current <see cref="VoskRecognizerService"/> and dispose its recognizer.
    ///   2. Await <see cref="RebuildRecognizerAsync"/> with the new unlocked spell list.
    ///   3. If the result is non-null, construct a new <see cref="VoskRecognizerService"/>
    ///      with it and call Start().
    ///   4. If the result is null (empty spell set), do not start recognition.
    /// </summary>
    public class SpellVocabularyManager
    {
        /// <summary>
        /// Converts the unlocked spell list into a Vosk-compatible JSON grammar string.
        /// Returns <c>null</c> when the list is empty — the caller must skip recognition
        /// in this case (do not pass null to <see cref="VoskRecognizer"/>).
        /// </summary>
        /// <param name="unlockedSpells">The player's currently unlocked spells.</param>
        /// <returns>
        /// A JSON array string, e.g. <c>["hydrogen blast","acid rain"]</c>,
        /// or <c>null</c> if the list is empty.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="unlockedSpells"/> is null.</exception>
        public static string BuildGrammarJson(IReadOnlyList<SpellData> unlockedSpells)
        {
            if (unlockedSpells == null) throw new ArgumentNullException(nameof(unlockedSpells));
            if (unlockedSpells.Count == 0) return null;

            IEnumerable<string> escaped = unlockedSpells.Select(s =>
                "\"" + s.spellName
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                + "\"");

            return "[" + string.Join(",", escaped) + "]";
        }

        /// <summary>
        /// Creates a new <see cref="VoskRecognizer"/> on a background thread using the
        /// Vosk grammar built from <paramref name="unlockedSpells"/>.
        /// Returns <c>null</c> when <paramref name="unlockedSpells"/> is empty —
        /// the caller must not start recognition in this case.
        ///
        /// The caller is responsible for stopping and disposing the previous
        /// <see cref="VoskRecognizerService"/> before using the returned recognizer.
        /// </summary>
        /// <param name="model">The already-loaded Vosk <see cref="Model"/>.</param>
        /// <param name="sampleRate">Microphone sample rate in Hz (typically 16000).</param>
        /// <param name="unlockedSpells">The player's currently unlocked spells.</param>
        /// <returns>
        /// A <see cref="Task{VoskRecognizer}"/> that completes on a background thread.
        /// The result is <c>null</c> when the spell list is empty.
        /// </returns>
        public static Task<VoskRecognizer> RebuildRecognizerAsync(
            Model model,
            float sampleRate,
            IReadOnlyList<SpellData> unlockedSpells)
        {
            if (unlockedSpells == null) throw new ArgumentNullException(nameof(unlockedSpells));

            // Check empty BEFORE checking model — an empty spell set returns null
            // regardless of whether a model is available.
            string grammarJson = BuildGrammarJson(unlockedSpells);
            if (grammarJson == null) return Task.FromResult<VoskRecognizer>(null);

            if (model == null) throw new ArgumentNullException(nameof(model));

            // VoskRecognizer construction applies the grammar to the model — potentially
            // slow. Task.Run keeps the Unity main thread unblocked.
            return Task.Run(() => new VoskRecognizer(model, sampleRate, grammarJson));
        }
    }
}
```

- [ ] **Step 2: Run Edit Mode tests in Unity Test Runner**

Window → General → Test Runner → Edit Mode → select `SpellVocabularyManagerTests` → Run Selected (or Run All).

Expected output:
```
SpellVocabularyManagerTests (6 tests)
  ✓ BuildGrammarJson_EmptyList_ReturnsNull
  ✓ BuildGrammarJson_SingleSpell_ReturnsJsonArrayWithThatName
  ✓ BuildGrammarJson_MultipleSpells_ContainsAllNames
  ✓ BuildGrammarJson_SpellNameWithEmbeddedQuote_EscapesCorrectly
  ✓ BuildGrammarJson_NullList_ThrowsArgumentNullException
  ✓ RebuildRecognizerAsync_EmptyList_ReturnsNull
```

All 6 must pass before proceeding.

- [ ] **Step 3: Check in to UVCS**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Voice/SpellVocabularyManager.cs`
- `Assets/Scripts/Voice/SpellVocabularyManager.cs.meta`
- `Assets/Tests/Editor/Voice/SpellVocabularyManagerTests.cs`
- `Assets/Tests/Editor/Voice/SpellVocabularyManagerTests.cs.meta`

Check in with message: `feat(DEV-20): implement SpellVocabularyManager — grammar builder and async recognizer factory`

---

## Self-Review

| AC Requirement | Covered By |
|---|---|
| Reads from `SpellData` ScriptableObjects | `BuildGrammarJson` parameter is `IReadOnlyList<SpellData>` |
| Builds valid Vosk JSON grammar string | `BuildGrammarJson` — tested with single, multiple, escaped-quote cases |
| Grammar passed to `VoskRecognizerService` on init + unlock | `RebuildRecognizerAsync` returns ready-to-use `VoskRecognizer`; wiring is caller's responsibility (out of scope per note above) |
| Reloading does not cause frame drop | `Task.Run` in `RebuildRecognizerAsync` offloads recognizer construction |
| Empty unlocked set: no crash, no recognition | `BuildGrammarJson` returns `null`; `RebuildRecognizerAsync` returns `Task.FromResult(null)` — both paths tested |

### Bugs fixed during review

| # | Bug | Fix |
|---|-----|-----|
| 1 | `RebuildRecognizerAsync(null, 16000f, emptyList)` threw `ArgumentNullException` because model was checked before the empty-list early-exit | Reordered: check `unlockedSpells` null → build grammar → empty-list exit → then check `model` null |
| 2 | `RebuildRecognizerAsync` empty-list path had no test | Added `RebuildRecognizerAsync_EmptyList_ReturnsNull` — testable without a real Model because the null model guard never fires for an empty list |
| 3 | UVCS Task 2 check-in list omitted `Assets/Scripts/Data.meta` (Unity-generated folder meta) | Added to staged file list |
