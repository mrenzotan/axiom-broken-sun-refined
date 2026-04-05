# Vosk Service Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Install the Vosk C# bindings and the `vosk-model-en-us-0.22-lgraph` speech model so Unity resolves both correctly on Windows and macOS, and scaffold the `Axiom.Voice` assembly definition for all future Voice scripts.

**Architecture:** Vosk ships as two layers — a managed C# wrapper (`Vosk.dll`) and platform-specific native runtimes (`libvosk.dll` + 3 GCC runtime DLLs on Windows, `libvosk.dylib` universal binary on macOS). All DLLs land in `Assets/ThirdParty/Vosk/` as Unity plugins with platform settings configured in the Inspector, mirroring the folder structure of the official `vosk-unity-asr` sample project. The lgraph model folder lands verbatim in `Assets/StreamingAssets/VoskModels/` where `Application.streamingAssetsPath` resolves it at runtime. A new `Axiom.Voice` asmdef references `Vosk.dll` via `precompiledReferences`, matching the `Axiom.Battle` pattern already in the project. An Edit Mode smoke test verifies the model directory exists at the expected path — written before the model is placed (TDD red), passing after (TDD green).

**Tech Stack:** Unity 6 LTS, Vosk C# bindings (NuGet package `Vosk` by alphacep), NUnit Edit Mode tests, Unity Plugin Inspector

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `Assets/Scripts/Voice/Axiom.Voice.asmdef` | Create | Scopes Voice scripts; references `Vosk.dll` |
| `Assets/Tests/Editor/Voice/VoiceTests.asmdef` | Create | Edit Mode test assembly for Voice module |
| `Assets/Tests/Editor/Voice/VoskSetupTests.cs` | Create | Smoke test — model directory exists at StreamingAssets path |
| `Assets/ThirdParty/Vosk/Vosk.dll` | User places | Managed C# wrapper (Any Platform) |
| `Assets/ThirdParty/Vosk/Plugins/Windows/libvosk.dll` | User places | Native runtime — Windows x86_64 |
| `Assets/ThirdParty/Vosk/Plugins/Windows/libgcc_s_seh-1.dll` | User places | GCC runtime — Windows (required by libvosk) |
| `Assets/ThirdParty/Vosk/Plugins/Windows/libstdc++-6.dll` | User places | GCC runtime — Windows (required by libvosk) |
| `Assets/ThirdParty/Vosk/Plugins/Windows/libwinpthread-1.dll` | User places | GCC runtime — Windows (required by libvosk) |
| `Assets/ThirdParty/Vosk/Plugins/OSX/libvosk.dylib` | User places | Native runtime — macOS universal (Intel + Apple Silicon) |
| `Assets/StreamingAssets/VoskModels/vosk-model-en-us-0.22-lgraph/` | User places | Lgraph speech model folder |

---

## Task 1: Install Vosk C# bindings

> **Unity Editor task (user)**

The Vosk C# package is distributed via NuGet. The easiest way to get the DLLs without NuGetForUnity is to download the `.nupkg` manually — a `.nupkg` is a zip file.

- [ ] **Step 1: Download the Vosk NuGet package**

  Go to: https://www.nuget.org/packages/Vosk  
  Click **Download package** (bottom-right). Save the file — it will be named something like `Vosk.0.3.45.nupkg`.

- [ ] **Step 2: Extract the package**

  Rename `Vosk.0.3.38.nupkg` → `Vosk.0.3.38.zip`, then extract it.  
  Inside you'll find:

  ```
  lib/
  └── netstandard2.0/
      └── Vosk.dll                  ← managed C# wrapper
  build/
  └── lib/
      ├── win-x64/
      │   ├── libvosk.dll           ← Windows native
      │   ├── libgcc_s_seh-1.dll    ← GCC runtime (required)
      │   ├── libstdc++-6.dll       ← GCC runtime (required)
      │   └── libwinpthread-1.dll   ← GCC runtime (required)
      └── osx-universal/
          └── libvosk.dylib         ← macOS universal (Intel + Apple Silicon)
  ```

- [ ] **Step 3: Create the destination folder in your Unity project**

  In Finder, create this folder structure (the `ThirdParty/Vosk` folder does not exist yet):

  ```
  Assets/ThirdParty/Vosk/
  └── Plugins/
      ├── Windows/
      └── OSX/
  ```

- [ ] **Step 4: Place the DLLs**

  Copy files to their destinations:
  - `lib/netstandard2.0/Vosk.dll` → `Assets/ThirdParty/Vosk/Vosk.dll`
  - `build/lib/win-x64/libvosk.dll` → `Assets/ThirdParty/Vosk/Plugins/Windows/libvosk.dll`
  - `build/lib/win-x64/libgcc_s_seh-1.dll` → `Assets/ThirdParty/Vosk/Plugins/Windows/libgcc_s_seh-1.dll`
  - `build/lib/win-x64/libstdc++-6.dll` → `Assets/ThirdParty/Vosk/Plugins/Windows/libstdc++-6.dll`
  - `build/lib/win-x64/libwinpthread-1.dll` → `Assets/ThirdParty/Vosk/Plugins/Windows/libwinpthread-1.dll`
  - `build/lib/osx-universal/libvosk.dylib` → `Assets/ThirdParty/Vosk/Plugins/OSX/libvosk.dylib`

- [ ] **Step 5: Configure plugin platform settings in Unity**

  Switch to the Unity Editor. In the Project window, navigate to `Assets/ThirdParty/Vosk/`.

  Select **`Vosk.dll`**, open the Inspector, and set:
  - Select platforms for plugin: **Any Platform** ✓
  - CPU: **Any CPU**
  - Click **Apply**

  Select each of the **4 DLLs in `Plugins/Windows/`** (`libvosk.dll`, `libgcc_s_seh-1.dll`, `libstdc++-6.dll`, `libwinpthread-1.dll`) one at a time, open the Inspector, and set:
  - Select platforms for plugin: **Editor** ✓, **Standalone** ✓ (uncheck all others)
  - **Editor tab** (Unity cube icon): under Windows → **x86** ✓, CPU = **x64**
  - **Standalone tab** (monitor icon): OS = **Windows**, CPU = **x64**
  - Click **Apply**

  Select **`Plugins/OSX/libvosk.dylib`**, open the Inspector, and set:
  - Select platforms for plugin: **Editor** ✓, **Standalone** ✓ (uncheck all others)
  - Under Standalone: OS = **macOS**, CPU = **Any CPU**
  - Under Editor: OS = **macOS**, CPU = **Any CPU**
  - Click **Apply**

- [ ] **Step 6: Verify Unity compiles with no errors**

  Check the Unity Console — there should be no compile errors after Unity reimports.

---

## Task 2: Scaffold Voice assembly definition

**Files:**
- Create: `Assets/Scripts/Voice/Axiom.Voice.asmdef`

- [ ] **Step 1: Create the Voice scripts folder**

  In the Unity Project window, right-click `Assets/Scripts/` → **Create → Folder** → name it `Voice`.

  > **Unity Editor task (user)**

- [ ] **Step 2: Create `Axiom.Voice.asmdef`**

  Claude writes this file directly. Create `Assets/Scripts/Voice/Axiom.Voice.asmdef`:

  ```json
  {
      "name": "Axiom.Voice",
      "references": [],
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

  > `overrideReferences: true` is required for `precompiledReferences` to take effect.  
  > This mirrors the `Axiom.Battle` pattern already in the project.

- [ ] **Step 3: Verify Unity still compiles with no errors**

  Check the Unity Console — the new asmdef should register cleanly.

---

## Task 3: Write failing model-path smoke test

**Files:**
- Create: `Assets/Tests/Editor/Voice/VoiceTests.asmdef`
- Create: `Assets/Tests/Editor/Voice/VoskSetupTests.cs`

- [ ] **Step 1: Create the test folder**

  In the Unity Project window, right-click `Assets/Tests/Editor/` → **Create → Folder** → name it `Voice`.

  > **Unity Editor task (user)**

- [ ] **Step 2: Create `VoiceTests.asmdef`**

  Create `Assets/Tests/Editor/Voice/VoiceTests.asmdef` (mirrors `BattleTests.asmdef` pattern):

  ```json
  {
      "name": "VoiceTests",
      "references": [
          "Axiom.Voice"
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

- [ ] **Step 3: Create `VoskSetupTests.cs`**

  Create `Assets/Tests/Editor/Voice/VoskSetupTests.cs`:

  ```csharp
  using NUnit.Framework;
  using System.IO;
  using UnityEngine;

  public class VoskSetupTests
  {
      [Test]
      public void ModelDirectory_ExistsInStreamingAssets()
      {
          string modelPath = Path.Combine(
              Application.streamingAssetsPath,
              "VoskModels",
              "vosk-model-en-us-0.22-lgraph");

          Assert.IsTrue(
              Directory.Exists(modelPath),
              $"Vosk model not found at: {modelPath}\n" +
              "Download vosk-model-en-us-0.22-lgraph from https://alphacephei.com/vosk/models " +
              "and place it under Assets/StreamingAssets/VoskModels/");
      }
  }
  ```

- [ ] **Step 4: Run the test — confirm it FAILS**

  In Unity: **Window → General → Test Runner → EditMode tab**  
  Find `VoskSetupTests.ModelDirectory_ExistsInStreamingAssets` and run it.

  Expected result: **FAIL** — `Vosk model not found at: .../StreamingAssets/VoskModels/vosk-model-en-us-0.22-lgraph`  
  This is correct — the model hasn't been placed yet.

---

## Task 4: Place the Vosk lgraph model

> **Unity Editor task (user)**

- [ ] **Step 1: Download the lgraph model**

  Go to: https://alphacephei.com/vosk/models  
  Download **`vosk-model-en-us-0.22-lgraph`** (the small ~50MB model — NOT `vosk-model-en-us-0.22`, the full model).

- [ ] **Step 2: Create the StreamingAssets destination folder**

  In Finder (not Unity Editor — Unity does not need to be running for this), create:

  ```
  Assets/StreamingAssets/VoskModels/
  ```

  > `StreamingAssets` may already exist. If not, create it too.

- [ ] **Step 3: Extract and place the model**

  Extract the downloaded archive. Place the entire `vosk-model-en-us-0.22-lgraph` folder inside `Assets/StreamingAssets/VoskModels/`. Final path should be:

  ```
  Assets/StreamingAssets/VoskModels/vosk-model-en-us-0.22-lgraph/
  ├── am/
  ├── conf/
  ├── graph/
  └── ivector/
  ```

- [ ] **Step 4: Let Unity reimport**

  Switch back to the Unity Editor. Unity will detect the new files in `StreamingAssets` and reimport. Wait for the progress bar to complete.

---

## Task 5: Run smoke test — confirm green

- [ ] **Step 1: Run the test**

  In Unity: **Window → General → Test Runner → EditMode tab**  
  Run `VoskSetupTests.ModelDirectory_ExistsInStreamingAssets`.

  Expected result: **PASS**

- [ ] **Step 2: Verify no Profiler spikes (manual)**

  Open **Window → Analysis → Profiler**. Enter Play Mode. Confirm the main thread shows no unexpected spikes during startup (model directory check is a filesystem stat — it completes in microseconds).

  > Note: Full model initialization (`new Vosk.Model(path)`) is deferred to the `VoskRecognizerService` ticket (DEV-19). The Profiler check for that heavier operation belongs there.

---

## Task 6: UVCS check-in

> **Unity Editor task (user)**

- [ ] **Step 1: Stage and check in**

  Unity Version Control → Pending Changes → stage:
  - `Assets/Scripts/Voice/Axiom.Voice.asmdef`
  - `Assets/Scripts/Voice/Axiom.Voice.asmdef.meta`
  - `Assets/Tests/Editor/Voice/VoiceTests.asmdef`
  - `Assets/Tests/Editor/Voice/VoiceTests.asmdef.meta`
  - `Assets/Tests/Editor/Voice/VoskSetupTests.cs`
  - `Assets/Tests/Editor/Voice/VoskSetupTests.cs.meta`
  - `Assets/ThirdParty/Vosk/` (entire folder + metas)
  - `Assets/StreamingAssets/VoskModels/vosk-model-en-us-0.22-lgraph/` (entire folder + metas)

  Check in with message:
  ```
  feat(DEV-18): install Vosk C# bindings and lgraph model; scaffold Axiom.Voice asmdef
  ```
