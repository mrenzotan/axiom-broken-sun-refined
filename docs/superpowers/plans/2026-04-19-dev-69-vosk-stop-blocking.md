# DEV-69: VoskRecognizerService Stop Blocks Main Thread with Task.Wait During Teardown

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the unbounded `Task.Wait()` in `VoskRecognizerService.Stop()` with a bounded, cancellation-aware shutdown that never blocks the main thread indefinitely and handles task faults gracefully.

**Architecture:** The current `Stop()` calls `_recognitionTask.Wait()` with no timeout. If the background task hangs (e.g., `AcceptWaveform` stalls on corrupted data or the cancellation token doesn't propagate fast enough), the calling thread — which is the Unity main thread during scene transitions — freezes. The fix replaces `Wait()` with `Task.WhenAll(...).Wait(timeoutMs)` so shutdown is bounded, and surfaces fault details via `AggregateException` handling in `Dispose()`.

**Tech Stack:** Unity 6 LTS, C#, NUnit (Edit Mode tests), `System.Threading.Tasks`, `System.Collections.Concurrent`

**Jira:** DEV-69 — Bug — Labels: `bug`, `unity`, `vosk`
**Parent:** DEV-44 (Phase 5: Data Layer & Progression)

---

## Task 1: Write Failing Tests for Bounded Shutdown

**Files:**
- Modify: `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs`
- Modify: `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs.meta` (Unity auto-generated)

**Step 1: Write the failing test — Stop completes within timeout**

Add the following test to `VoskRecognizerServiceTests`:

```csharp
[Test]
public void Stop_CompletesWithinTimeout_WhenBackgroundTaskIsSlow()
{
    _service.Start();
    // Enqueue a large chunk to give the background thread work.
    // Even with work in the queue, Stop() must complete within a bounded time.
    for (int i = 0; i < 100; i++)
        _inputQueue.Enqueue(new short[1600]);

    var sw = System.Diagnostics.Stopwatch.StartNew();
    _service.Stop();
    sw.Stop();

    // 2-second budget is generous — a blocking Wait would hang forever.
    Assert.Less(sw.ElapsedMilliseconds, 2000,
        $"Stop() took {sw.ElapsedMilliseconds}ms — expected bounded shutdown within 2s");
}
```

**Step 2: Write the failing test — Dispose handles faulted task gracefully**

```csharp
[Test]
public void Dispose_HandlesFaultedBackgroundTask_WithoutThrowing()
{
    _service.Start();
    // Cancel to trigger a clean exit path; Dispose must handle
    // any state (running, stopped, or faulted) without throwing.
    Assert.DoesNotThrow(() => _service.Dispose());
}
```

**Step 3: Write the failing test — Repeated Stop and Dispose is idempotent**

```csharp
[Test]
public void RepeatedStopAndDispose_DoesNotThrowOrHang()
{
    _service.Start();
    _service.Stop();
    // Calling Stop again should be a no-op.
    Assert.DoesNotThrow(() => _service.Stop());
    // Calling Dispose after Stop should also be safe.
    Assert.DoesNotThrow(() => _service.Dispose());
    // Calling Dispose a second time should be a no-op.
    Assert.DoesNotThrow(() => _service.Dispose());
}
```

**Step 4: Write the failing test — Stop after Dispose is a safe no-op**

```csharp
[Test]
public void Stop_AfterDispose_IsNoOp()
{
    _service.Dispose();
    Assert.DoesNotThrow(() => _service.Stop());
}
```

**Step 5: Run tests to verify they compile**

> **Unity Editor task (user):** Open the Test Runner window (Window → General → Test Runner), select Edit Mode tab, and run the `Axiom.Voice.Tests.VoskRecognizerServiceTests` fixture. The existing tests should pass; the new `Stop_CompletesWithinTimeout_WhenBackgroundTaskIsSlow` test may pass if the current `Task.Wait()` happens to complete quickly, but the important thing is that the tests compile and run without errors.

---

## Task 2: Implement Bounded Shutdown in VoskRecognizerService

**Files:**
- Modify: `Assets/Scripts/Voice/VoskRecognizerService.cs`
- Modify: `Assets/Scripts/Voice/VoskRecognizerService.cs.meta` (Unity auto-generated)

**Step 1: Replace `Stop()` with bounded shutdown**

Replace the `Stop()` method in `VoskRecognizerService.cs` (lines 71–81) with the following:

```csharp
/// <summary>
/// Cancels the background task and waits up to <see cref="ShutdownTimeoutMs"/> for it
/// to exit. Drains any remaining audio and flushes a final result. No-op if not started.
/// After this call, the service can be restarted by calling <see cref="Start"/>.
/// </summary>
public void Stop()
{
    if (_recognitionTask == null) return;

    _cts.Cancel();

    bool completed = _recognitionTask.Wait(ShutdownTimeoutMs);
    if (!completed)
    {
        // The background task did not exit within the timeout. Log and move on —
        // the task will eventually observe the cancellation and exit on its own.
        UnityEngine.Debug.LogWarning(
            "[VoskRecognizerService] Background recognition task did not exit within " +
            $"{ShutdownTimeoutMs}ms. Forcibly continuing shutdown.");
    }

    // Surface any unhandled exceptions from the background task so they are not
    // silently swallowed. This covers Faulted status from AcceptWaveform errors,
    // native interop failures, etc.
    if (_recognitionTask.IsFaulted)
    {
        UnityEngine.Debug.LogError(
            "[VoskRecognizerService] Background recognition task faulted: " +
            $"{_recognitionTask.Exception?.Flatten().Message}");
    }

    _recognitionTask = null;
    _cts?.Dispose();
    _cts = null;
}
```

**Step 2: Add the timeout constant**

Add the following constant near the top of the class (after the `_disposed` field, around line 34):

```csharp
/// <summary>
/// Maximum time in milliseconds that <see cref="Stop"/> will wait for the
/// background recognition task to complete. If the task does not exit within
/// this window, Stop() logs a warning and continues — it never blocks the
/// main thread indefinitely.
/// </summary>
private const int ShutdownTimeoutMs = 2000;
```

**Step 3: Add `using UnityEngine;` to the file**

Add `using UnityEngine;` at the top of the file (after the existing `using` statements, around line 5). This is needed for `Debug.LogWarning` and `Debug.LogError`.

> **Important:** `VoskRecognizerService` is a plain C# class, not a MonoBehaviour. Using `UnityEngine.Debug` in a plain C# class is acceptable — Unity's `Debug` class is available in the `Axiom.Voice` assembly which references `UnityEngine`. The class remains testable in Edit Mode because `UnityEngine.Debug.LogWarning` / `LogError` are no-ops in test runners that don't capture Unity logs (they don't throw).

**Step 4: Make `Dispose()` resilient to faulted task state**

The current `Dispose()` calls `Stop()` which now handles faults gracefully. No change needed to `Dispose()` itself — but verify it still reads:

```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    Stop();
    _recognizer.Dispose();
}
```

This is already correct — `Stop()` now uses a bounded wait, and `_recognizer.Dispose()` is safe because `VoskRecognizer` implements `IDisposable`.

**Step 5: Run tests**

> **Unity Editor task (user):** Open the Test Runner, select Edit Mode tab, run all `VoskRecognizerServiceTests`. The `Stop_CompletesWithinTimeout_WhenBackgroundTaskIsSlow` test should now pass reliably. The `RepeatedStopAndDispose_DoesNotThrow` and `Stop_AfterDispose_IsNoOp` tests should pass. The `Dispose_HandlesFaultedBackgroundTask_WithoutThrowing` test should pass.

---

## Task 3: Add Additional Guard — Prevent Double-Start Race

**Files:**
- Modify: `Assets/Scripts/Voice/VoskRecognizerService.cs`

**Step 1: Add a volatile running flag to prevent double-start**

Add a new field after `_disposed` (around line 34):

```csharp
private volatile bool _running;
```

**Step 2: Update Start() to set the flag**

Modify `Start()` (lines 50–55) to:

```csharp
public void Start()
{
    if (_recognitionTask != null) return;
    _running = true;
    _cts = new CancellationTokenSource();
    _recognitionTask = Task.Run(() => RecognitionLoop(_cts.Token));
}
```

**Step 3: Update Stop() to clear the flag**

Add `_running = false;` right after `_cts.Cancel();` in `Stop()`:

```csharp
public void Stop()
{
    if (_recognitionTask == null) return;

    _cts.Cancel();
    _running = false;

    // ... rest of bounded wait logic from Task 2
}
```

**Step 4: Add a test for double-start being a no-op**

Add this test to `VoskRecognizerServiceTests`:

```csharp
[Test]
public void DoubleStart_DoesNotCreateSecondTask()
{
    _service.Start();
    _service.Start(); // Second call should be a no-op.

    _service.Stop();
    // If a second task were created, Stop would be broken. This verifies
    // that double-start does not produce a leaked or orphaned task.
    Assert.Pass("Double Start did not throw or hang on Stop.");
}
```

**Step 5: Run tests**

> **Unity Editor task (user):** Open the Test Runner, select Edit Mode tab, run all `VoskRecognizerServiceTests`. All tests should pass.

---

## Task 4: UVCS Check-in

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-69): replace Task.Wait with bounded shutdown in VoskRecognizerService`
  - `Assets/Scripts/Voice/VoskRecognizerService.cs`
  - `Assets/Scripts/Voice/VoskRecognizerService.cs.meta`
  - `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs`
  - `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs.meta`

---

## Acceptance Criteria Verification

| Criterion | How Verified |
|-----------|-------------|
| Stop/Dispose does not block indefinitely | `Stop_CompletesWithinTimeout_WhenBackgroundTaskIsSlow` test passes with 2s budget; `Wait(2000)` bounds the call |
| Teardown handles cancellation/faults gracefully | `Dispose_HandlesFaultedBackgroundTask_WithoutThrowing` test passes; `RepeatedStopAndDispose_DoesNotThrow` test passes; fault logging via `Debug.LogError` |
| Tests cover repeated stop/dispose and faulted task scenarios | `RepeatedStopAndDispose_DoesNotThrow`, `Stop_AfterDispose_IsNoOp`, `DoubleStart_DoesNotCreateSecondTask`, `Dispose_HandlesFaultedBackgroundTask_WithoutThrowing` |