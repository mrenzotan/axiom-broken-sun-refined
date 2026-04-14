# DEV-34: Animated Scene Transition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add full-screen white-flash (Platformer → Battle) and black-fade (Battle → Platformer) overlay transitions that hold scene activation until the overlay is fully opaque, then reveal the new scene and fire `GameManager.OnSceneReady` so scene controllers can gate initialization.

**Architecture:** `SceneTransitionService` (plain C#) owns `IsTransitioning` state and per-style timing/color config; `SceneTransitionController` (MonoBehaviour on the GameManager prefab) drives the Canvas/Image coroutine and `LoadSceneAsync`; `GameManager` exposes both via `SceneTransition` property and `OnSceneReady` event; call sites in `ExplorationEnemyCombatTrigger` and `BattleController` are updated; both `BattleController` and `PlayerController` gate initialization behind the event.

**Tech Stack:** Unity 6 LTS, URP 2D, Unity UI Canvas + Image, `AsyncOperation.allowSceneActivation`, `Color.Lerp` coroutines, NUnit Editor-mode + PlayMode tests.

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Core/TransitionStyle.cs` | Enum: `WhiteFlash`, `BlackFade` |
| Create | `Assets/Scripts/Core/SceneTransitionService.cs` | IsTransitioning flag; timing/color config per style |
| Create | `Assets/Scripts/Core/SceneTransitionController.cs` | MonoBehaviour: coroutine driver, overlay Image |
| Modify | `Assets/Scripts/Core/GameManager.cs` | Add `SceneTransition`, `OnSceneReady`, `RaiseSceneReady()` |
| Modify | `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs` | Replace `SceneManager.LoadScene("Battle")` |
| Modify | `Assets/Scripts/Battle/BattleController.cs` | Replace fled `SceneManager.LoadScene`, add Start gate |
| Modify | `Assets/Scripts/Platformer/PlayerController.cs` | Add Start gate; add `using Axiom.Core;` |
| Create | `Assets/Tests/Editor/Core/SceneTransitionServiceTests.cs` | Editor tests for plain-C# service |
| Create | `Assets/Tests/Editor/Core/GameManagerTransitionTests.cs` | Editor tests for new GameManager members |
| Create | `Assets/Tests/PlayMode/Core/SceneTransitionControllerTests.cs` | PlayMode tests for controller state |

---

## Task 1: TransitionStyle Enum

**Files:**
- Create: `Assets/Scripts/Core/TransitionStyle.cs`

- [ ] **Step 1: Create the enum**

```csharp
namespace Axiom.Core
{
    /// <summary>
    /// Identifies which overlay style to use for a scene transition.
    /// Color and timing are owned by SceneTransitionService.
    /// </summary>
    public enum TransitionStyle
    {
        /// <summary>Platformer → Battle: 0.2s flash to white, 0.8s reveal from white.</summary>
        WhiteFlash,
        /// <summary>Battle → Platformer: 0.5s fade to black, 0.5s reveal from black.</summary>
        BlackFade,
    }
}
```

Save as `Assets/Scripts/Core/TransitionStyle.cs`.

- [ ] **Step 2: Commit**

```
git add Assets/Scripts/Core/TransitionStyle.cs
git commit -m "feat(DEV-34): add TransitionStyle enum"
```

---

## Task 2: SceneTransitionService + Editor Tests

**Files:**
- Create: `Assets/Scripts/Core/SceneTransitionService.cs`
- Create: `Assets/Tests/Editor/Core/SceneTransitionServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Core/SceneTransitionServiceTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Core;
using UnityEngine;

namespace Axiom.Tests.Editor.Core
{
    public class SceneTransitionServiceTests
    {
        [Test]
        public void IsTransitioning_IsFalseByDefault()
        {
            var service = new SceneTransitionService();
            Assert.IsFalse(service.IsTransitioning);
        }

        [Test]
        public void SetTransitioning_True_MakesIsTransitioningTrue()
        {
            var service = new SceneTransitionService();
            service.SetTransitioning(true);
            Assert.IsTrue(service.IsTransitioning);
        }

        [Test]
        public void SetTransitioning_False_MakesIsTransitioningFalse()
        {
            var service = new SceneTransitionService();
            service.SetTransitioning(true);
            service.SetTransitioning(false);
            Assert.IsFalse(service.IsTransitioning);
        }

        [Test]
        public void GetColor_WhiteFlash_ReturnsWhite()
        {
            var service = new SceneTransitionService();
            Assert.AreEqual(Color.white, service.GetColor(TransitionStyle.WhiteFlash));
        }

        [Test]
        public void GetColor_BlackFade_ReturnsBlack()
        {
            var service = new SceneTransitionService();
            Assert.AreEqual(Color.black, service.GetColor(TransitionStyle.BlackFade));
        }

        [Test]
        public void GetFadeOutDuration_WhiteFlash_Returns0Point2()
        {
            var service = new SceneTransitionService();
            Assert.AreEqual(0.2f, service.GetFadeOutDuration(TransitionStyle.WhiteFlash), delta: 0.001f);
        }

        [Test]
        public void GetFadeInDuration_WhiteFlash_Returns0Point8()
        {
            var service = new SceneTransitionService();
            Assert.AreEqual(0.8f, service.GetFadeInDuration(TransitionStyle.WhiteFlash), delta: 0.001f);
        }

        [Test]
        public void GetFadeOutDuration_BlackFade_Returns0Point5()
        {
            var service = new SceneTransitionService();
            Assert.AreEqual(0.5f, service.GetFadeOutDuration(TransitionStyle.BlackFade), delta: 0.001f);
        }

        [Test]
        public void GetFadeInDuration_BlackFade_Returns0Point5()
        {
            var service = new SceneTransitionService();
            Assert.AreEqual(0.5f, service.GetFadeInDuration(TransitionStyle.BlackFade), delta: 0.001f);
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they fail with "type not found"**

In Unity Editor: Window → General → Test Runner → EditMode → run `SceneTransitionServiceTests`.
Expected: all 9 fail because `SceneTransitionService` doesn't exist yet.

- [ ] **Step 3: Implement SceneTransitionService**

Create `Assets/Scripts/Core/SceneTransitionService.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Core
{
    /// <summary>
    /// Plain C# — no Unity dependencies.
    /// Owns IsTransitioning state and per-style timing/color config.
    /// Injected into SceneTransitionController via constructor.
    /// </summary>
    public class SceneTransitionService
    {
        public bool IsTransitioning { get; private set; }

        private static readonly Dictionary<TransitionStyle, (Color color, float fadeOut, float fadeIn)> Config =
            new Dictionary<TransitionStyle, (Color, float, float)>
            {
                [TransitionStyle.WhiteFlash] = (Color.white, 0.2f, 0.8f),
                [TransitionStyle.BlackFade]  = (Color.black, 0.5f, 0.5f),
            };

        public Color GetColor(TransitionStyle style)          => Config[style].color;
        public float GetFadeOutDuration(TransitionStyle style) => Config[style].fadeOut;
        public float GetFadeInDuration(TransitionStyle style)  => Config[style].fadeIn;

        public void SetTransitioning(bool value) => IsTransitioning = value;
    }
}
```

- [ ] **Step 4: Run tests — verify all 9 pass**

In Unity Editor: Test Runner → EditMode → run `SceneTransitionServiceTests`.
Expected: 9 tests PASS.

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/Core/SceneTransitionService.cs Assets/Tests/Editor/Core/SceneTransitionServiceTests.cs
git commit -m "feat(DEV-34): add SceneTransitionService with timing/color config"
```

---

## Task 3: GameManager Additions + Editor Tests

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Create: `Assets/Tests/Editor/Core/GameManagerTransitionTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Core/GameManagerTransitionTests.cs`:

```csharp
using System;
using NUnit.Framework;
using UnityEngine;
using Axiom.Core;

namespace Axiom.Tests.Editor.Core
{
    public class GameManagerTransitionTests
    {
        private GameObject _go;
        private GameManager _gm;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GameManager");
            _gm = _go.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void SceneTransition_IsNull_WhenNoChildController()
        {
            // GameManager has no SceneTransitionController child — should be null.
            Assert.IsNull(_gm.SceneTransition);
        }

        [Test]
        public void OnSceneReady_IsRaised_ByRaiseSceneReady()
        {
            bool fired = false;
            _gm.OnSceneReady += () => fired = true;

            _gm.RaiseSceneReady();

            Assert.IsTrue(fired);
        }

        [Test]
        public void RaiseSceneReady_DoesNotThrow_WhenNoSubscribers()
        {
            Assert.DoesNotThrow(() => _gm.RaiseSceneReady());
        }

        [Test]
        public void SceneTransition_IsAssigned_WhenChildControllerExists()
        {
            var childGo = new GameObject("Child");
            childGo.transform.SetParent(_go.transform);
            var controller = childGo.AddComponent<SceneTransitionController>();

            // Destroy and recreate so Awake runs with the child present.
            Object.DestroyImmediate(_go);
            _go = new GameObject("GameManager");
            childGo = new GameObject("Child");
            childGo.transform.SetParent(_go.transform);
            childGo.AddComponent<SceneTransitionController>();
            _gm = _go.AddComponent<GameManager>();

            Assert.IsNotNull(_gm.SceneTransition);
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

In Unity Editor: Test Runner → EditMode → run `GameManagerTransitionTests`.
Expected: all 4 fail (`SceneTransition` and `RaiseSceneReady` don't exist yet).

- [ ] **Step 3: Update GameManager**

Open `Assets/Scripts/Core/GameManager.cs`. The full file after changes:

```csharp
using System;
using UnityEngine;
using Axiom.Data;

namespace Axiom.Core
{
    /// <summary>
    /// Persistent singleton that survives scene loads and owns the cross-scene PlayerState.
    ///
    /// Access pattern for other systems — store a local reference, never a new static field:
    ///
    ///   private PlayerState _playerState;
    ///
    ///   void Start()
    ///   {
    ///       _playerState = GameManager.Instance.PlayerState;
    ///   }
    ///
    /// Do NOT write: public static GameManager instance; in any other class.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerState PlayerState { get; private set; }

        /// <summary>The scene transition controller on this prefab's child hierarchy.</summary>
        public SceneTransitionController SceneTransition { get; private set; }

        /// <summary>
        /// Fires after the scene transition fade-in completes.
        /// Subscribers must unsubscribe immediately in their callback to prevent phantom calls
        /// on subsequent transitions.
        /// Raised by RaiseSceneReady(), called by SceneTransitionController.
        /// </summary>
        public event Action OnSceneReady;

        /// <summary>
        /// Set by ExplorationEnemyCombatTrigger before loading the Battle scene.
        /// Consumed and cleared by BattleController.Start() on Battle scene load.
        /// Null when no battle transition is pending (normal state).
        /// </summary>
        public BattleEntry PendingBattle { get; private set; }

        /// <summary>Sets the pending battle context before transitioning to the Battle scene.</summary>
        public void SetPendingBattle(BattleEntry entry) => PendingBattle = entry;

        /// <summary>
        /// Clears the pending battle context after BattleController has consumed it.
        /// Safe to call when PendingBattle is already null.
        /// </summary>
        public void ClearPendingBattle() => PendingBattle = null;

        /// <summary>
        /// Called by SceneTransitionController after the fade-in completes.
        /// Notifies all subscribers that the scene is ready for initialization.
        /// </summary>
        public void RaiseSceneReady() => OnSceneReady?.Invoke();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            PlayerState = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            SceneTransition = GetComponentInChildren<SceneTransitionController>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
```

- [ ] **Step 4: Run tests — verify all 4 pass**

In Unity Editor: Test Runner → EditMode → run `GameManagerTransitionTests`.
Expected: 4 tests PASS.

Also run `GameManagerPendingBattleTests` to confirm no regression — all should still PASS.

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/Core/GameManager.cs Assets/Tests/Editor/Core/GameManagerTransitionTests.cs
git commit -m "feat(DEV-34): add SceneTransition property and OnSceneReady event to GameManager"
```

---

## Task 4: SceneTransitionController + PlayMode Tests

**Files:**
- Create: `Assets/Scripts/Core/SceneTransitionController.cs`
- Create: `Assets/Tests/PlayMode/Core/SceneTransitionControllerTests.cs`

- [ ] **Step 1: Write the failing PlayMode tests**

Create `Assets/Tests/PlayMode/Core/SceneTransitionControllerTests.cs`:

```csharp
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Axiom.Core;

namespace Axiom.Tests.PlayMode.Core
{
    public class SceneTransitionControllerTests
    {
        private GameObject _go;
        private SceneTransitionController _controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _go = new GameObject("Controller");
            var imageGo = new GameObject("Image");
            imageGo.transform.SetParent(_go.transform);
            var image = imageGo.AddComponent<Image>();

            _controller = _go.AddComponent<SceneTransitionController>();
            yield return null; // Allow Awake to run

            // Wire the overlay image via reflection (it's a [SerializeField] meant for Inspector use).
            typeof(SceneTransitionController)
                .GetField("_overlayImage", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_controller, image);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_go != null)
                Object.Destroy(_go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IsTransitioning_DefaultsFalse()
        {
            Assert.IsFalse(_controller.IsTransitioning);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BeginTransition_SetsIsTransitioning_True_Immediately()
        {
            // NOTE: Passing a non-existent scene name causes LoadSceneAsync to log a warning
            // and stall at progress < 0.9 — IsTransitioning stays true while we observe it.
            _controller.BeginTransition("__nonexistent_test_scene__", TransitionStyle.BlackFade);

            // IsTransitioning should be true before the coroutine's first yield returns.
            // We observe after one frame — coroutine has started but fade is still running.
            yield return null;

            Assert.IsTrue(_controller.IsTransitioning);
        }

        [UnityTest]
        public IEnumerator BeginTransition_IsNoOp_WhenAlreadyTransitioning()
        {
            _controller.BeginTransition("__nonexistent_test_scene__", TransitionStyle.BlackFade);
            yield return null;

            // Second call while transitioning — should not reset or throw.
            Assert.DoesNotThrow(() =>
                _controller.BeginTransition("__nonexistent_test_scene__", TransitionStyle.WhiteFlash));

            Assert.IsTrue(_controller.IsTransitioning);
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they fail with "type not found"**

In Unity Editor: Test Runner → PlayMode → run `SceneTransitionControllerTests`.
Expected: all 3 fail because `SceneTransitionController` doesn't exist yet.

- [ ] **Step 3: Implement SceneTransitionController**

Create `Assets/Scripts/Core/SceneTransitionController.cs`:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// MonoBehaviour — Unity lifecycle wrapper for scene transition animation.
    /// Add to the GameManager prefab. Owns the child Canvas + Image overlay.
    ///
    /// Public API:
    ///   void BeginTransition(string sceneName, TransitionStyle style)
    ///   bool IsTransitioning  — delegates to SceneTransitionService
    ///
    /// BeginTransition is a no-op when IsTransitioning is already true.
    /// </summary>
    public class SceneTransitionController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The full-screen Image on the TransitionOverlay Canvas child. Assign in Inspector.")]
        private Image _overlayImage;

        private SceneTransitionService _service;

        public bool IsTransitioning => _service.IsTransitioning;

        private void Awake()
        {
            _service = new SceneTransitionService();
        }

        /// <summary>
        /// Begins the three-phase transition: fade out → async load → fade in.
        /// No-op if a transition is already in progress.
        /// </summary>
        public void BeginTransition(string sceneName, TransitionStyle style)
        {
            if (_service.IsTransitioning) return;
            StartCoroutine(RunTransition(sceneName, style));
        }

        private IEnumerator RunTransition(string sceneName, TransitionStyle style)
        {
            _service.SetTransitioning(true);

            Color baseColor       = _service.GetColor(style);
            float fadeOutDuration = _service.GetFadeOutDuration(style);
            float fadeInDuration  = _service.GetFadeInDuration(style);

            // Phase 1: Fade out — alpha 0 → 1
            _overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeOutDuration);
                _overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
            _overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);

            // Phase 2: Async load — held until overlay is fully opaque, then activated
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
                yield return null;

            op.allowSceneActivation = true;
            yield return op;

            // Phase 3: Fade in — alpha 1 → 0
            elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - elapsed / fadeInDuration);
                _overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
            _overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

            // Fire OnSceneReady first (subscribers may check IsTransitioning),
            // then clear the flag.
            if (GameManager.Instance != null)
                GameManager.Instance.RaiseSceneReady();

            _service.SetTransitioning(false);
        }
    }
}
```

- [ ] **Step 4: Run tests — verify all 3 pass**

In Unity Editor: Test Runner → PlayMode → run `SceneTransitionControllerTests`.
Expected: 3 tests PASS. (Unity will log a warning about the nonexistent scene name — that is expected and harmless.)

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/Core/SceneTransitionController.cs Assets/Tests/PlayMode/Core/SceneTransitionControllerTests.cs
git commit -m "feat(DEV-34): add SceneTransitionController coroutine driver"
```

---

## Task 5: ExplorationEnemyCombatTrigger Call Site

**Files:**
- Modify: `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`

- [ ] **Step 1: Replace the SceneManager.LoadScene call**

Open `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`.

Find `TriggerBattle` (lines 58–75). Replace the body so the full method reads:

```csharp
private void TriggerBattle(CombatStartState startState)
{
    _triggered = true;

    if (GameManager.Instance != null)
    {
        GameManager.Instance.SetPendingBattle(new BattleEntry(startState, _enemyData));
        GameManager.Instance.SceneTransition.BeginTransition("Battle", TransitionStyle.WhiteFlash);
    }
    else
    {
        Debug.LogWarning(
            "[ExplorationEnemyCombatTrigger] GameManager not found — cannot start transition. " +
            "Add the GameManager prefab to the Platformer scene.",
            this);
    }
}
```

Also add `using Axiom.Core;` to the top of the file if not already present. The existing `using Axiom.Core;` is already there — confirm and leave it.

Remove the `using UnityEngine.SceneManagement;` line **only if** `SceneManager` is no longer referenced anywhere else in this file. (It isn't — this was the only call site.)

- [ ] **Step 2: Verify the file compiles in Unity Editor**

Switch to the Unity Editor and confirm no compile errors in the Console.

- [ ] **Step 3: Commit**

```
git add Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs
git commit -m "feat(DEV-34): replace direct LoadScene with BeginTransition in ExplorationEnemyCombatTrigger"
```

---

## Task 6: BattleController Updates (Call Site + Start Gate)

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

### 6a — Replace the fled LoadScene call

- [ ] **Step 1: Update HandleStateChanged**

In `HandleStateChanged` (line 484), find:

```csharp
else if (state == BattleState.Fled)
    SceneManager.LoadScene("Platformer");
```

Replace with:

```csharp
else if (state == BattleState.Fled)
{
    if (GameManager.Instance?.SceneTransition != null)
        GameManager.Instance.SceneTransition.BeginTransition("Platformer", TransitionStyle.BlackFade);
    else
        SceneManager.LoadScene("Platformer"); // Standalone Battle scene testing fallback
}
```

### 6b — Add the Start gate

- [ ] **Step 2: Refactor Start() to use InitializeFromTransition**

Find `Start()` (line 194). Replace the full method:

```csharp
private void Start()
{
    var pending = GameManager.Instance?.PendingBattle;
    if (pending != null)
    {
        _startState = pending.StartState;
        _enemyData  = pending.EnemyData;
        GameManager.Instance.ClearPendingBattle();
    }

    if (GameManager.Instance?.SceneTransition?.IsTransitioning == true)
        GameManager.Instance.OnSceneReady += InitializeFromTransition;
    else
        InitializeFromTransition();
}

private void InitializeFromTransition()
{
    if (GameManager.Instance != null)
        GameManager.Instance.OnSceneReady -= InitializeFromTransition;
    Initialize(_startState);
}
```

### 6c — Add OnDestroy cleanup for OnSceneReady

- [ ] **Step 3: Update OnDestroy to unsubscribe from OnSceneReady**

Find `OnDestroy()` (line 611). Add one line at the top of the method body, before the `_battleManager` null check:

```csharp
private void OnDestroy()
{
    if (GameManager.Instance != null)
        GameManager.Instance.OnSceneReady -= InitializeFromTransition;

    if (_battleManager != null)
        _battleManager.OnStateChanged -= HandleStateChanged;

    // ... rest of existing OnDestroy unchanged ...
```

### 6d — Remove SceneManager using if no longer needed

- [ ] **Step 4: Check if `using UnityEngine.SceneManagement;` is still needed**

Search the file for any remaining `SceneManager.` references. The fallback in `HandleStateChanged` still uses `SceneManager.LoadScene`, so **keep** the using statement.

- [ ] **Step 5: Verify no compile errors in Unity Editor**

Switch to Unity Editor and confirm the Console has no errors.

- [ ] **Step 6: Commit**

```
git add Assets/Scripts/Battle/BattleController.cs
git commit -m "feat(DEV-34): add transition call site and Start gate to BattleController"
```

---

## Task 7: PlayerController Start Gate

**Files:**
- Modify: `Assets/Scripts/Platformer/PlayerController.cs`

- [ ] **Step 1: Add `using Axiom.Core;` to PlayerController**

Open `Assets/Scripts/Platformer/PlayerController.cs`. The top of the file currently has no namespace imports. Add `using Axiom.Core;` after the existing `using` statements:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using Axiom.Core;
```

- [ ] **Step 2: Add Start() and InitializeFromTransition()**

`PlayerController` has no `Start()` method currently. Add it after `OnDisable()` (after line 67):

```csharp
private void Start()
{
    if (GameManager.Instance?.SceneTransition?.IsTransitioning == true)
    {
        // Disable input and lock movement until the transition reveal completes.
        // (OnEnable already ran and enabled input — we override that here.)
        _input.Player.Disable();
        _movement.SetMovementLocked(true);
        GameManager.Instance.OnSceneReady += InitializeFromTransition;
    }
    else
    {
        InitializeFromTransition();
    }
}

private void InitializeFromTransition()
{
    if (GameManager.Instance != null)
        GameManager.Instance.OnSceneReady -= InitializeFromTransition;
    _input.Player.Enable();
    _movement.SetMovementLocked(false);
}
```

- [ ] **Step 3: Add OnDestroy to clean up OnSceneReady subscription**

`PlayerController` has no `OnDestroy()`. Add it after `InitializeFromTransition()`:

```csharp
private void OnDestroy()
{
    if (GameManager.Instance != null)
        GameManager.Instance.OnSceneReady -= InitializeFromTransition;
}
```

- [ ] **Step 4: Verify no compile errors in Unity Editor**

Switch to Unity Editor and confirm the Console has no errors. Also verify the existing PlayMode/Editor tests for PlayerController (if any) still pass in the Test Runner.

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/Platformer/PlayerController.cs
git commit -m "feat(DEV-34): add Start gate to PlayerController — blocks input until OnSceneReady"
```

---

## Task 8: Unity Editor Manual Setup

> These steps cannot be scripted. They are performed manually in the Unity Editor after all code compiles without errors.

- [ ] **Step 1: Add SceneTransitionController to the GameManager prefab**

1. In the Project window, locate the **GameManager prefab** (look in `Assets/Prefabs/` or search for "GameManager" in the asset search bar).
2. Open the prefab in Prefab Edit mode (double-click).
3. Select the root GameObject.
4. In the Inspector, click **Add Component** → search for `SceneTransitionController` → add it.

- [ ] **Step 2: Create the TransitionOverlay child hierarchy**

Still in Prefab Edit mode on the GameManager root:

1. Right-click the root → **UI → Canvas**. Rename it `TransitionOverlay`.
2. In the `TransitionOverlay` Canvas Inspector:
   - **Render Mode:** Screen Space — Overlay
   - **Sort Order:** 999
3. Select `TransitionOverlay`, then **Add Component → UI → Canvas Scaler**:
   - **UI Scale Mode:** Scale With Screen Size
4. Right-click `TransitionOverlay` → **UI → Image**. Rename it `OverlayImage`.
5. In the `OverlayImage` RectTransform:
   - Set **Anchor Presets** to stretch/stretch (Alt+click the bottom-right corner preset to stretch both axes and reset position to zero).
6. In the `OverlayImage` Image component:
   - **Color:** white, Alpha = 0

- [ ] **Step 3: Wire the Image reference**

1. Select the **GameManager root GameObject** in the prefab hierarchy.
2. In the Inspector, find the `SceneTransitionController` component.
3. Drag the `OverlayImage` GameObject from the prefab hierarchy into the **Overlay Image** field.

- [ ] **Step 4: Save the prefab**

Click **Save** in the Prefab Edit mode toolbar (or Ctrl/Cmd+S).

- [ ] **Step 5: Verify in Play Mode**

1. Open the Platformer scene.
2. Enter Play Mode.
3. Walk into an enemy (or attack one) to trigger `ExplorationEnemyCombatTrigger`.
4. Verify a white flash transition plays before the Battle scene loads.
5. In the Battle scene, use Flee.
6. Verify a black fade transition plays before the Platformer scene returns.
7. Confirm the player cannot move during the fade-in (input is gated until `OnSceneReady` fires).

---

## Spec Coverage Self-Check

| Spec requirement | Covered by |
|---|---|
| WhiteFlash: 0.2s fade out, 0.8s fade in | Task 2 (`SceneTransitionService` config) |
| BlackFade: 0.5s fade out, 0.5s fade in | Task 2 (`SceneTransitionService` config) |
| Canvas + Image overlay, no external sprites | Task 4 (`SceneTransitionController`) |
| Canvas sort order 999 | Task 8 (Editor setup) |
| `allowSceneActivation = false` until fully opaque | Task 4 (`RunTransition` coroutine) |
| `IsTransitioning` true from fade-out start until after fire | Task 4 (flag set at top, cleared after `RaiseSceneReady`) |
| `BeginTransition` no-op when already transitioning | Task 4 (guard in `BeginTransition`) |
| `OnSceneReady` fires after fade-in, then `IsTransitioning` clears | Task 4 (order in `RunTransition`) |
| ExplorationEnemyCombatTrigger call site | Task 5 |
| BattleController fled call site | Task 6a |
| BattleController.Start gate | Task 6b |
| PlayerController.Start gate | Task 7 |
| Subscribers unsubscribe in callback | Tasks 6b, 7 (`InitializeFromTransition` unsubscribes itself) |
| Standalone scene testing fallback (no GameManager) | Tasks 6b, 7 (`else` path) |
| OnDestroy cleanup for dangling delegates | Tasks 6c, 7 |
| `GameManager.SceneTransition` assigned in Awake | Task 3 |
| `GameManager.OnSceneReady` event | Task 3 |
