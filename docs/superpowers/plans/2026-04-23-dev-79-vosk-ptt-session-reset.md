# VoskRecognizerService PTT Session Reset Fix — DEV-79

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix cross-session recognizer state contamination by replacing the `_finalResultRequested` flag with a `null` sentinel in the input queue.

**Architecture:** The `RecognitionLoop()` will dequeue a `null` sentinel (instead of checking a flag) and call `FinalResult()` at that exact point — guaranteed reset between PTT sessions regardless of queue fill rate. `MicrophoneInputHandler.StopCapture()` enqueues a `null` instead of setting a flag.

**Tech Stack:** C#, NUnit (Edit Mode), Vosk, Unity Test Framework

---

## Affected Files

| File | Change |
|---|---|
| `Assets/Scripts/Voice/VoskRecognizerService.cs` | Remove `_finalResultRequested`; handle `null` sentinel in queue |
| `Assets/Scripts/Voice/MicrophoneInputHandler.cs` | Enqueue `null` instead of calling `RequestFinalResult()` |
| `Assets/Scripts/Voice/MicrophoneCapture.cs` | Add `EnqueueSentinel()` method |
| `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs` | Add sentinel test; update `RequestFinalResult_EnqueuesAtLeastOneResult` to use sentinel pattern |

---

## Task 1: Write the sentinel test

**Files:**
- Modify: `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs`

Add a new test after the existing `RequestFinalResult_EnqueuesAtLeastOneResult` test:

```csharp
[Test]
public void SentinelInInputQueue_TriggersFinalResult_AndResetsRecognizerState()
{
    // Arrange: recognizer with grammar ["ignis", "aqua"]
    _recognizer = new VoskRecognizer(s_model, 16000f, "[\"ignis\", \"aqua\"]");
    _service = new VoskRecognizerService(_recognizer, _inputQueue, _resultQueue);
    _service.Start();

    // Act: enqueue audio samples followed by a null sentinel, then more audio
    short[] samples1 = new short[800]; // ~50 ms of audio
    short[] samples2 = new short[800];
    _inputQueue.Enqueue(samples1);
    _inputQueue.Enqueue(null); // sentinel
    _inputQueue.Enqueue(samples2);

    // Wait for the background thread to process sentinel + samples2
    Thread.Sleep(300);
    _service.Stop();

    // Assert:
    // 1. At least one result was produced (sentinel triggered FinalResult)
    Assert.GreaterOrEqual(_resultQueue.Count, 1,
        "Expected at least one result after sentinel was processed");

    // 2. samples2 audio was accepted after the sentinel reset
    Assert.GreaterOrEqual(_resultQueue.Count, 2,
        "Expected a second result for samples2 audio processed after reset");
}
```

**Step 2: Run the test**

Run: `Unity Test Runner → Edit Mode → VoiceTests → VoskRecognizerServiceTests.SentinelInInputQueue_TriggersFinalResult_AndResetsRecognizerState`
Expected: FAIL — VoskRecognizerService doesn't handle `null` sentinel yet

---

## Task 2: Update VoskRecognizerService to handle sentinel

**Files:**
- Modify: `Assets/Scripts/Voice/VoskRecognizerService.cs`

**Step 1: Remove the `_finalResultRequested` field**

Delete from field declarations (line 34):
```csharp
private volatile bool _finalResultRequested;
```

**Step 2: Remove the `RequestFinalResult()` method**

Delete the entire method (lines 65-68):
```csharp
public void RequestFinalResult()
{
    _finalResultRequested = true;
}
```

**Step 3: Replace the `else if (_finalResultRequested)` branch in RecognitionLoop()**

Replace:
```csharp
else if (_finalResultRequested)
{
    _finalResultRequested = false;
    _resultQueue.Enqueue(_recognizer.FinalResult());
}
```

With:
```csharp
else if (_inputQueue.TryPeek(out short[] peek) && peek == null)
{
    _inputQueue.TryDequeue(); // consume the sentinel
    _resultQueue.Enqueue(_recognizer.FinalResult());
}
```

**Step 4: Run the test**

Run: `Unity Test Runner → Edit Mode → VoiceTests → VoskRecognizerServiceTests.SentinelInInputQueue_TriggersFinalResult_AndResetsRecognizerState`
Expected: PASS

---

## Task 3: Update MicrophoneInputHandler to use sentinel

**Files:**
- Modify: `Assets/Scripts/Voice/MicrophoneInputHandler.cs`

**Step 1: Replace `StopCapture()` body**

Replace:
```csharp
private void StopCapture()
{
    if (!_isCapturing) return;
    _isCapturing = false;
    Microphone.End(_deviceName);
    _clip = null;
    _recognizerService?.RequestFinalResult();
}
```

With:
```csharp
private void StopCapture()
{
    if (!_isCapturing) return;
    _isCapturing = false;
    Microphone.End(_deviceName);
    _clip = null;
    _capture?.EnqueueSentinel();
}
```

---

## Task 3b: Add `EnqueueSentinel()` to MicrophoneCapture

**Files:**
- Modify: `Assets/Scripts/Voice/MicrophoneCapture.cs`

**Step 1: Add `EnqueueSentinel()` method**

Add after the existing `ProcessSamples()` method:
```csharp
public void EnqueueSentinel()
{
    _inputQueue.Enqueue(null);
}
```

---

## Task 4: Update existing test to use sentinel pattern

**Files:**
- Modify: `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs`

Rename and update `RequestFinalResult_EnqueuesAtLeastOneResult` to use the sentinel pattern:

Replace:
```csharp
[Test]
public void RequestFinalResult_EnqueuesAtLeastOneResult()
{
    _service.Start();

    _service.RequestFinalResult();
    // Give the background thread up to 200 ms to process the flag.
    Thread.Sleep(200);

    _service.Stop();

    Assert.GreaterOrEqual(_resultQueue.Count, 1,
        "Expected at least one result in queue after RequestFinalResult()");
}
```

With:
```csharp
[Test]
public void Sentinel_EnqueuesAtLeastOneResult()
{
    _service.Start();

    _inputQueue.Enqueue(null);
    // Give the background thread up to 200 ms to process the sentinel.
    Thread.Sleep(200);

    _service.Stop();

    Assert.GreaterOrEqual(_resultQueue.Count, 1,
        "Expected at least one result in queue after null sentinel is processed");
}
```

**Step 2: Run all VoskRecognizerServiceTests**

Run: `Unity Test Runner → Edit Mode → VoiceTests → VoskRecognizerServiceTests`
Expected: ALL PASS

---

## Task 5: Verify no regression in MicrophoneInputHandlerTests

**Files:**
- Verify: `Assets/Tests/Editor/Voice/MicrophoneInputHandlerTests.cs` — no code changes needed

**Step 1: Run MicrophoneInputHandlerTests**

Run: `Unity Test Runner → Edit Mode → VoiceTests → MicrophoneInputHandlerTests`
Expected: ALL PASS

---

## Task 6: UVCS Check-in

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-79): reset Vosk state between PTT sessions via sentinel queue`

  - `Assets/Scripts/Voice/VoskRecognizerService.cs`
  - `Assets/Scripts/Voice/VoskRecognizerService.cs.meta`
  - `Assets/Scripts/Voice/MicrophoneInputHandler.cs`
  - `Assets/Scripts/Voice/MicrophoneInputHandler.cs.meta`
  - `Assets/Scripts/Voice/MicrophoneCapture.cs`
  - `Assets/Scripts/Voice/MicrophoneCapture.cs.meta`
  - `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs`
  - `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs.meta`

---

## Execution Options

**1. Subagent-Driven (this session)** - I dispatch fresh subagent per task, review between tasks, fast iteration

**2. Parallel Session (separate)** - Open new session with executing-plans, batch execution with checkpoints

Which approach?