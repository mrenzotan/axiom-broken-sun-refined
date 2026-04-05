# DEV-26: Vosk Pipeline Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create `BattleVoiceBootstrap` — a MonoBehaviour that loads the Vosk model off the main thread, constructs `VoskRecognizerService`, and injects shared queues into `MicrophoneInputHandler` and `SpellCastController` so voice recognition actually runs in the Battle scene.

**Architecture:** A single MonoBehaviour (`BattleVoiceBootstrap`) in the `Axiom.Voice` assembly owns the full startup sequence as an `IEnumerator Start()` coroutine. It uses `Task.Run` + `WaitUntil` to keep both model loading and recognizer construction off the Unity main thread. No new asmdef or plain C# logic class is needed — this is purely wiring with no logic of its own to test.

**Tech Stack:** Unity 6 LTS · Vosk C# bindings (`Vosk.dll`) · `System.Collections.Concurrent` · `System.Threading.Tasks` · UVCS for check-ins

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| **Create** | `Assets/Scripts/Voice/BattleVoiceBootstrap.cs` | MonoBehaviour: load model off main thread, build recognizer, create shared queues, inject into `MicrophoneInputHandler` and `SpellCastController`, dispose on destroy |

No asmdef changes needed. `BattleVoiceBootstrap` joins the existing `Axiom.Voice` assembly (`Assets/Scripts/Voice/Axiom.Voice.asmdef`), which already references `Axiom.Battle`, `Axiom.Data`, `Unity.InputSystem`, and `Vosk.dll`.

No Edit Mode tests are warranted: the bootstrap has no logic of its own — every collaborating class (`VoskRecognizerService`, `SpellVocabularyManager`, `SpellResultMatcher`) is already tested. The only verification is the Play Mode smoke test in Task 2.

---

## Task 1 — Implement BattleVoiceBootstrap

**Files:**
- Create: `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`

- [ ] **Create `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`:**

```csharp
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Axiom.Data;
using UnityEngine;
using Vosk;

namespace Axiom.Voice
{
    /// <summary>
    /// Scene-level bootstrap that loads the Vosk model on a background thread,
    /// constructs <see cref="VoskRecognizerService"/>, and injects the shared queues
    /// into <see cref="MicrophoneInputHandler"/> and <see cref="SpellCastController"/>.
    ///
    /// Place this component in the Battle scene on the same GameObject as
    /// <see cref="MicrophoneInputHandler"/> and <see cref="SpellCastController"/>.
    /// Assign all three SerializeFields in the Inspector before entering Play Mode.
    ///
    /// <para>
    /// <b>Sample rate:</b> <see cref="_sampleRate"/> must match the value set on
    /// <see cref="MicrophoneInputHandler"/> (both default to 16000 Hz).
    /// </para>
    /// </summary>
    public class BattleVoiceBootstrap : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("MicrophoneInputHandler component in this scene.")]
        private MicrophoneInputHandler _microphoneInputHandler;

        [SerializeField]
        [Tooltip("SpellCastController component in this scene.")]
        private SpellCastController _spellCastController;

        [SerializeField]
        [Tooltip("Spells the player has unlocked. For Phase 3 testing assign SpellData assets directly here.")]
        private SpellData[] _unlockedSpells;

        [SerializeField]
        [Tooltip("Sample rate in Hz passed to the Vosk recognizer. Must match MicrophoneInputHandler._sampleRate.")]
        private int _sampleRate = 16000;

        private static readonly string ModelRelativePath =
            Path.Combine("VoskModels", "vosk-model-en-us-0.22-lgraph");

        private VoskRecognizerService _recognizerService;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            string modelPath = Path.Combine(Application.streamingAssetsPath, ModelRelativePath);

            if (!Directory.Exists(modelPath))
            {
                Debug.LogError(
                    $"[BattleVoiceBootstrap] Vosk model not found at: {modelPath}\n" +
                    "Place vosk-model-en-us-0.22-lgraph inside StreamingAssets/VoskModels/.", this);
                yield break;
            }

            // Model construction is blocking and slow (~1-2s). Run on a background thread.
            Task<Model> modelTask = Task.Run(() => new Model(modelPath));
            yield return new WaitUntil(() => modelTask.IsCompleted);

            if (modelTask.IsFaulted)
            {
                Debug.LogError(
                    $"[BattleVoiceBootstrap] Failed to load Vosk model: " +
                    $"{modelTask.Exception?.InnerException?.Message}", this);
                yield break;
            }

            SpellData[] spells = _unlockedSpells ?? Array.Empty<SpellData>();

            // VoskRecognizer construction applies grammar to the model — also off main thread.
            Task<VoskRecognizer> recognizerTask =
                SpellVocabularyManager.RebuildRecognizerAsync(modelTask.Result, _sampleRate, spells);
            yield return new WaitUntil(() => recognizerTask.IsCompleted);

            if (recognizerTask.IsFaulted)
            {
                Debug.LogError(
                    $"[BattleVoiceBootstrap] Failed to build Vosk recognizer: " +
                    $"{recognizerTask.Exception?.InnerException?.Message}", this);
                yield break;
            }

            if (recognizerTask.Result == null)
            {
                Debug.LogWarning(
                    "[BattleVoiceBootstrap] Spell list is empty — voice recognition not started.\n" +
                    "Assign at least one SpellData asset to the Unlocked Spells field.", this);
                yield break;
            }

            // Create the shared queues.
            var inputQueue  = new ConcurrentQueue<short[]>();
            var resultQueue = new ConcurrentQueue<string>();

            // Wire the pipeline.
            _recognizerService = new VoskRecognizerService(recognizerTask.Result, inputQueue, resultQueue);
            _recognizerService.Start();

            _microphoneInputHandler.Inject(inputQueue, _recognizerService);
            _spellCastController.Inject(resultQueue, spells);

            Debug.Log("[BattleVoiceBootstrap] Vosk pipeline ready.");
        }

        private void OnDestroy()
        {
            _recognizerService?.Dispose();
            _recognizerService = null;
        }
    }
}
```

- [ ] **Verify Unity compiles without errors** — switch to the Unity Editor after saving; the console should show no errors.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-26): add BattleVoiceBootstrap to wire Vosk pipeline in Battle scene`
  - `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`
  - `Assets/Scripts/Voice/BattleVoiceBootstrap.cs.meta`

---

## Task 2 — Add to Battle Scene, Wire Inspector, Smoke Test

> **Unity Editor task (user):** All steps below are performed in the Unity Editor.

### 2a — Create SpellData assets (if none exist)

The `_unlockedSpells` field requires at least one `SpellData` asset. If you have none yet:

- [ ] **Create a SpellData asset** — Project window → right-click `Assets/Data/` → Create folder `Spells` → right-click `Assets/Data/Spells/` → Create → Axiom → Spell Data (or whatever the CreateAssetMenu path is for SpellData). Set the `spellName` field to a test spell name, e.g. `"hydrogen blast"`.

> If you already have SpellData assets, skip to 2b.

### 2b — Add the component to the scene

- [ ] **Open the Battle scene** — double-click `Assets/Scenes/Battle.unity` in the Project window.

- [ ] **Add `BattleVoiceBootstrap` to the scene** — in the Hierarchy, select the GameObject that holds `MicrophoneInputHandler` and `SpellCastController` (or create a new empty GameObject and name it `VoiceBootstrap`). In the Inspector → Add Component → search **Battle Voice Bootstrap** → add it.

### 2c — Wire Inspector references

- [ ] **Assign `Microphone Input Handler`** → drag the `MicrophoneInputHandler` component from the Hierarchy into the **Microphone Input Handler** slot on `BattleVoiceBootstrap`.

- [ ] **Assign `Spell Cast Controller`** → drag the `SpellCastController` component into the **Spell Cast Controller** slot.

- [ ] **Assign `Unlocked Spells`** → set the array size to the number of SpellData assets you want to test with, then drag each `SpellData` asset from the Project window into the slots.

- [ ] **Verify `Sample Rate`** is `16000` — confirm the value matches the `Sample Rate` field on `MicrophoneInputHandler` (both should be 16000). Change one to match the other if they differ.

### 2d — Smoke test

- [ ] **Enter Play Mode** (▶). Watch the Console:
  - After ~1–2 seconds you should see: `[BattleVoiceBootstrap] Vosk pipeline ready.`
  - No `NullReferenceException`, no model-not-found error, no recognizer build error.

- [ ] **Click Spell** in the Action Menu → the `PromptPanel` should appear ("Hold [Left Shift] and speak a spell name").

- [ ] **Hold Left Shift** → `ListeningPanel` should appear. Speak one of the spell names you assigned to `_unlockedSpells`.

- [ ] **Release Left Shift** → `PromptPanel` returns briefly while Vosk processes.
  - If the spell is recognized → `FeedbackPanel` shows the spell name and the enemy turn begins.
  - If not recognized → `FeedbackPanel` shows "Not recognized. Try again." and `PromptPanel` returns after 2s.

- [ ] **Exit Play Mode.**

- [ ] **Save the scene** — Ctrl+S.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-26): wire BattleVoiceBootstrap in Battle scene`
  - `Assets/Scenes/Battle.unity`
  - `Assets/Scenes/Battle.unity.meta`

---

## Self-Review

### Spec Coverage (DEV-26 Acceptance Criteria)

| AC | Covered by |
|----|-----------|
| `BattleVoiceBootstrap` MonoBehaviour in `Assets/Scripts/Voice/` | Task 1 |
| Model loaded from `StreamingAssets/.../vosk-model-en-us-0.22-lgraph` off main thread | Task 1: `Task.Run(() => new Model(modelPath))` + `WaitUntil` |
| `VoskRecognizerService` constructed with model + queues | Task 1: queues + `new VoskRecognizerService(...)` |
| `VoskRecognizerService.Start()` called | Task 1 |
| `MicrophoneInputHandler.Inject(inputQueue, recognizerService)` called | Task 1 |
| `SpellCastController.Inject(resultQueue, unlockedSpells)` called | Task 1 |
| `VoskRecognizerService.Stop()` / `Dispose()` in `OnDestroy()` | Task 1: `_recognizerService?.Dispose()` (`Dispose` calls `Stop` internally) |
| Inspector SerializeFields: handler, controller, spell list | Task 1 |
| Missing model path → clear error, no crash | Task 1: `Directory.Exists` guard + `Debug.LogError` + `yield break` |
| Component added to Battle scene + all references wired | Task 2 |

All AC items covered. ✓

### Placeholder Scan

No TBD, TODO, "implement later", or "similar to Task N" references. ✓

### Type Consistency

| Type / Method | Defined in | Used in |
|---|---|---|
| `MicrophoneInputHandler.Inject(ConcurrentQueue<short[]>, VoskRecognizerService)` | DEV-21 | Task 1 |
| `SpellCastController.Inject(ConcurrentQueue<string>, IReadOnlyList<SpellData>)` | DEV-22 | Task 1 (`SpellData[]` implements `IReadOnlyList<SpellData>`) |
| `SpellVocabularyManager.RebuildRecognizerAsync(Model, float, IReadOnlyList<SpellData>)` | DEV-21 | Task 1 |
| `VoskRecognizerService(VoskRecognizer, ConcurrentQueue<short[]>, ConcurrentQueue<string>)` | DEV-21 | Task 1 |
| `VoskRecognizerService.Start()` | DEV-21 | Task 1 |
| `VoskRecognizerService.Dispose()` | DEV-21 (calls `Stop()` then disposes recognizer) | Task 1 |

All consistent. ✓

### UVCS Staged File Audit

| Check-in | Files staged |
|---|---|
| Task 1 | `BattleVoiceBootstrap.cs` + `.meta` |
| Task 2 | `Battle.unity` + `.meta` |

All created/modified files accounted for. ✓

### Unity Editor Task Isolation

All Unity Editor steps are in Task 2 under explicit `> **Unity Editor task (user):**` callouts. No code steps are mixed with editor steps. ✓
