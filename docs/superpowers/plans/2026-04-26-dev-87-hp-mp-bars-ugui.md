# DEV-87: HP/MP Bars (Player Fixed, Enemy Floating) Using UGUI — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Move the enemy HP bar from a fixed screen-space HUD panel to a world-space Canvas floating above the enemy sprite, add HP gradient color to both bars, and position player HP/MP bars in a fixed screen-space area (bottom-left corner). All via UGUI — no UI Toolkit.

**Architecture:** Extend `HealthBarUI` with a static color-gradient calculator (testable in Edit Mode) and visibility controls. Create a thin `EnemyWorldSpaceHealthBar` MonoBehaviour that owns a world-space Canvas + HealthBarUI child and follows the enemy transform in `Update()`. Modify `BattleHUD` to wire the new world-space enemy bar in place of the old fixed-panel enemy bar. Player bars stay on screen-space overlay Canvas — just repositioned in the scene.

**Tech Stack:** UGUI (Canvas, Image.fillAmount), TextMeshPro, URP 2D, Unity 6 LTS

**Jira:** [DEV-87](https://axiombrokensunrefined.atlassian.net/browse/DEV-87) | Parent: DEV-45 (Phase 6: World & Content) | Labels: `phase-2-combat`, `ui`

---

### Task 1: Add HP gradient color and visibility controls to HealthBarUI

**Files:**
- Modify: `Assets/Scripts/Battle/UI/HealthBarUI.cs`
- Test: `Assets/Tests/Editor/Battle/HealthBarUITests.cs` (new)

**Step 1: Write the failing Edit Mode tests**

```csharp
// Assets/Tests/Editor/Battle/HealthBarUITests.cs
using NUnit.Framework;
using UnityEngine;
using Axiom.Battle;

public class HealthBarUITests
{
    [Test]
    public void GetHealthColor_FullHP_ReturnsGreen()
    {
        Color result = HealthBarUI.GetHealthColor(1.0f);
        Assert.Greater(result.g, 0.8f);
        Assert.Less(result.r, 0.5f);
    }

    [Test]
    public void GetHealthColor_MidHP_ReturnsYellow()
    {
        Color result = HealthBarUI.GetHealthColor(0.5f);
        Assert.Greater(result.r, 0.7f);
        Assert.Greater(result.g, 0.7f);
        Assert.Less(result.b, 0.3f);
    }

    [Test]
    public void GetHealthColor_LowHP_ReturnsRed()
    {
        Color result = HealthBarUI.GetHealthColor(0.2f);
        Assert.Greater(result.r, 0.7f);
        Assert.Less(result.g, 0.3f);
    }

    [Test]
    public void GetHealthColor_ZeroHP_ReturnsRed()
    {
        Color result = HealthBarUI.GetHealthColor(0f);
        Assert.Greater(result.r, 0.7f);
        Assert.Less(result.g, 0.3f);
    }

    [Test]
    public void GetHealthColor_ReturnsOpaqueColor()
    {
        Color result = HealthBarUI.GetHealthColor(0.75f);
        Assert.AreEqual(1f, result.a, 0.001f);
    }

    [Test]
    public void GetHealthColor_ClampedAboveOne_TreatedAsOne()
    {
        Color above = HealthBarUI.GetHealthColor(1.5f);
        Color atOne = HealthBarUI.GetHealthColor(1.0f);
        Assert.AreEqual(atOne.r, above.r, 0.001f);
        Assert.AreEqual(atOne.g, above.g, 0.001f);
        Assert.AreEqual(atOne.b, above.b, 0.001f);
    }

    [Test]
    public void GetHealthColor_ClampedBelowZero_TreatedAsZero()
    {
        Color below = HealthBarUI.GetHealthColor(-0.5f);
        Color atZero = HealthBarUI.GetHealthColor(0f);
        Assert.AreEqual(atZero.r, below.r, 0.001f);
        Assert.AreEqual(atZero.g, below.g, 0.001f);
        Assert.AreEqual(atZero.b, below.b, 0.001f);
    }
}
```

**Step 2: Run test to verify it fails**

Run: Unity Editor → Window → General → Test Runner → EditMode → BattleTests → HealthBarUITests
Expected: Compile error — `HealthBarUI.GetHealthColor` does not exist.

**Step 3: Implement the static color method + visibility controls in HealthBarUI**

```csharp
// Assets/Scripts/Battle/UI/HealthBarUI.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Battle
{
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Image component used as the HP bar fill. Set Image Type to Filled.")]
        private Image _hpBarImage;

        [SerializeField]
        [Tooltip("TMP label showing 'current / max' HP. Optional.")]
        private TMP_Text _hpText;

        [SerializeField]
        [Tooltip("Image component used as the MP bar fill. Null for enemy slots (no MP bar).")]
        private Image _mpBarImage;

        [SerializeField]
        [Tooltip("TMP label showing 'current / max' MP. Null for enemy slots.")]
        private TMP_Text _mpText;

        [SerializeField]
        [Tooltip("Speed at which the bar fill lerps toward its target value (units per second).")]
        private float _lerpSpeed = 5f;

        [Header("HP Bar Color Gradient")]
        [SerializeField]
        [Tooltip("Color when HP is full (>= 50%).")]
        private Color _hpFullColor = new Color(0.2f, 0.85f, 0.2f, 1f);

        [SerializeField]
        [Tooltip("Color when HP is at 50%.")]
        private Color _hpMidColor = new Color(1f, 0.92f, 0.016f, 1f);

        [SerializeField]
        [Tooltip("Color when HP is at 25% or lower.")]
        private Color _hpLowColor = new Color(1f, 0.1f, 0.1f, 1f);

        [SerializeField]
        [Tooltip("Whether to apply the HP color gradient each frame. Disable for MP-only bars.")]
        private bool _applyHpColorGradient = true;

        private float _targetHPFill;
        private float _targetMPFill;

        /// <summary>
        /// Returns a Color interpolated from the HP gradient based on a fill fraction
        /// (0.0 = empty, 1.0 = full). Clamped to [0, 1].
        /// Green (>= 50%) → Yellow (at 50%) → Red (<= 25%).
        /// Static so it can be tested in Edit Mode without a scene.
        /// </summary>
        public static Color GetHealthColor(float fillAmount)
        {
            float t = Mathf.Clamp01(fillAmount);

            Color fullColor = new Color(0.2f, 0.85f, 0.2f, 1f);
            Color midColor  = new Color(1f, 0.92f, 0.016f, 1f);
            Color lowColor  = new Color(1f, 0.1f, 0.1f, 1f);

            if (t >= 0.5f)
                return Color.Lerp(midColor, fullColor, (t - 0.5f) * 2f);
            else if (t >= 0.25f)
                return Color.Lerp(lowColor, midColor, (t - 0.25f) * 4f);
            else
                return Color.Lerp(lowColor, Color.red, 0f); // stays red below 25%
        }

        private void Update()
        {
            if (_hpBarImage != null)
            {
                _hpBarImage.fillAmount = Mathf.Lerp(
                    _hpBarImage.fillAmount, _targetHPFill, Time.deltaTime * _lerpSpeed);

                if (_applyHpColorGradient)
                    _hpBarImage.color = GetHealthColor(_hpBarImage.fillAmount);
            }

            if (_mpBarImage != null)
                _mpBarImage.fillAmount = Mathf.Lerp(
                    _mpBarImage.fillAmount, _targetMPFill, Time.deltaTime * _lerpSpeed);
        }

        /// <summary>Shows this health bar GameObject.</summary>
        public void Show() => gameObject.SetActive(true);

        /// <summary>Hides this health bar GameObject.</summary>
        public void Hide() => gameObject.SetActive(false);

        /// <summary>Sets visibility of this health bar. Convenience wrapper.</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        /// <summary>Updates the HP bar fill target and numeric text label.</summary>
        public void SetHP(int current, int max)
        {
            _targetHPFill = max > 0 ? (float)current / max : 0f;
            if (_hpText != null)
                _hpText.text = $"{current} / {max}";
        }

        /// <summary>
        /// Updates the MP bar fill target and numeric text label.
        /// No-op if this slot has no MP bar (e.g. enemy panel).
        /// </summary>
        public void SetMP(int current, int max)
        {
            if (_mpBarImage == null) return;
            _targetMPFill = max > 0 ? (float)current / max : 0f;
            if (_mpText != null)
                _mpText.text = $"{current} / {max}";
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: Unity Editor → Window → General → Test Runner → EditMode → BattleTests → HealthBarUITests
Expected: All 7 tests PASS.

---

### Task 2: Create EnemyWorldSpaceHealthBar MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Battle/UI/EnemyWorldSpaceHealthBar.cs`
- Create: `Assets/Scripts/Battle/UI/EnemyWorldSpaceHealthBar.cs.meta` (auto-generated by Unity)

**Step 1: Write the MonoBehaviour (no separate plain-C# logic class — this is pure lifecycle: following a transform)**

```csharp
// Assets/Scripts/Battle/UI/EnemyWorldSpaceHealthBar.cs
using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// Places a world-space Canvas + HealthBarUI above an enemy transform.
    /// Follows the enemy position each frame with a configurable vertical offset.
    /// MonoBehaviour handles only Unity lifecycle (Update). All bar logic lives in HealthBarUI.
    /// </summary>
    public class EnemyWorldSpaceHealthBar : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The transform this health bar follows (the enemy sprite GameObject).")]
        private Transform _targetTransform;

        [SerializeField]
        [Tooltip("World-space vertical offset above the target.")]
        private float _yOffset = 2.5f;

        [SerializeField]
        [Tooltip("The HealthBarUI child on this world-space Canvas.")]
        private HealthBarUI _healthBar;

        private void Update()
        {
            if (_targetTransform != null)
            {
                Vector3 pos = _targetTransform.position;
                pos.y += _yOffset;
                transform.position = pos;
            }
        }

        /// <summary>Forwards HP update to the internal HealthBarUI.</summary>
        public void SetHP(int current, int max) => _healthBar?.SetHP(current, max);

        /// <summary>Shows or hides the entire world-space Canvas.</summary>
        public void SetVisible(bool visible)
        {
            if (_healthBar != null)
                _healthBar.SetVisible(visible);
        }

        /// <summary>Assigns the target transform to follow. Called by BattleHUD.Setup().</summary>
        public void SetTarget(Transform target) => _targetTransform = target;
    }
}
```

**Step 2: Verify compilation**

Run: In Unity Editor, confirm `EnemyWorldSpaceHealthBar.cs` compiles without errors (no Test Runner needed — this is lifecycle-only, no logic to unit test).

> **Note:** No Edit Mode test for this class — it is pure lifecycle (Update follows transform). A Play Mode test would be needed to verify transform following, but per the project's MonoBehaviour separation rule, Play Mode tests for thin MonoBehaviour wrappers are low-value compared to Edit Mode tests for plain C# logic. The HealthBarUI logic it delegates to is already tested in Task 1.

---

### Task 3: Update BattleHUD to wire the world-space enemy bar

**Files:**
- Modify: `Assets/Scripts/Battle/UI/BattleHUD.cs`

**Step 1: Replace fixed enemy health bar reference with world-space bar**

In `BattleHUD.cs`, replace the `_enemyHealthBar` field with `_enemyWorldBar` and update all references:

```csharp
// Replace:
// [Header("Enemy Panel")]
// [SerializeField] private HealthBarUI _enemyHealthBar;
// [SerializeField] private TMP_Text    _enemyNameText;

// With:
[Header("Enemy Panel")]
[SerializeField] private EnemyWorldSpaceHealthBar _enemyWorldBar;
[SerializeField] private TMP_Text                 _enemyNameText;
```

Then update every usage of `_enemyHealthBar` to `_enemyWorldBar`:

In `Setup()`:
```csharp
// Replace:
// _enemyHealthBar.SetHP(enemyStats.CurrentHP, enemyStats.MaxHP);

// With:
_enemyWorldBar.SetHP(enemyStats.CurrentHP, enemyStats.MaxHP);
_enemyWorldBar.SetTarget(_enemySpriteTransform);
_enemyWorldBar.SetVisible(true);
```

In `HandleDamageDealt()`:
```csharp
// Replace:
// _enemyHealthBar.SetHP(target.CurrentHP, target.MaxHP);

// With:
_enemyWorldBar.SetHP(target.CurrentHP, target.MaxHP);
```

In `HandleCharacterDefeated()`:
```csharp
// Replace:
// _enemyHealthBar.SetHP(0, character.MaxHP);

// With:
_enemyWorldBar.SetHP(0, character.MaxHP);
```

In `HandleSpellHealed()`:
```csharp
// Replace:
// _enemyHealthBar.SetHP(target.CurrentHP, target.MaxHP);

// With:
_enemyWorldBar.SetHP(target.CurrentHP, target.MaxHP);
```

In `HandleConditionDamageTick()`:
```csharp
// Replace:
// _enemyHealthBar.SetHP(target.CurrentHP, target.MaxHP);

// With:
_enemyWorldBar.SetHP(target.CurrentHP, target.MaxHP);
```

In `HandleItemUsed()`:
```csharp
// Replace:
// _enemyHealthBar.SetHP(target.CurrentHP, target.MaxHP);

// With:
_enemyWorldBar.SetHP(target.CurrentHP, target.MaxHP);
```

Also update `HandleStateChanged()` to hide the enemy bar on Victory/Defeat/Fled:
```csharp
// In HandleStateChanged, add visibility toggles for the enemy world bar:
if (state == BattleState.PlayerTurn || state == BattleState.EnemyTurn)
{
    _enemyWorldBar?.SetVisible(true);
}
else if (state == BattleState.Victory || state == BattleState.Defeat || state == BattleState.Fled)
{
    _enemyWorldBar?.SetVisible(false);
}
```

**Step 2: The complete modified BattleHUD.cs**

The full file after all edits (showing only changed sections for brevity):

Field declarations change:
```csharp
[Header("Enemy Panel")]
[SerializeField] private EnemyWorldSpaceHealthBar _enemyWorldBar;
[SerializeField] private TMP_Text                 _enemyNameText;
```

Setup() changes — add after existing enemy name / health bar init lines:
```csharp
_enemyNameText.text  = enemyStats.Name;
_enemyWorldBar.SetHP(enemyStats.CurrentHP, enemyStats.MaxHP);
_enemyWorldBar.SetTarget(_enemySpriteTransform);
_enemyWorldBar.SetVisible(true);
```

HandleStateChanged additions — insert before `_actionMenuUI.SetInteractable(isPlayerTurn);`:
```csharp
bool inBattle = state == BattleState.PlayerTurn || state == BattleState.EnemyTurn;
_enemyWorldBar?.SetVisible(inBattle);
```

All `_enemyHealthBar.` calls replaced with `_enemyWorldBar.` as described above.

---

### Task 4: Unity Editor tasks (user performs these)

> **Unity Editor task (user):** Create the Enemy World-Space Canvas in Battle.unity

1. Open `Assets/Scenes/Battle.unity`
2. In the Hierarchy, create a new GameObject named `EnemyWorldBar`
3. Add a **Canvas** component:
   - Render Mode: `World Space`
   - Set width/height to 200×40 (or whatever fits your enemy sprite size)
   - Set scale small enough to fit above the enemy (e.g., `0.01, 0.01, 0.01`)
4. Add the `EnemyWorldSpaceHealthBar` script to this GameObject
5. Add a child **Image** GameObject named `HPBarFill`:
   - Image Type: `Filled`, Fill Method: `Horizontal`, Fill Origin: `Left`
   - Add the `HealthBarUI` script
   - Color: assign a default red (gradient will override at runtime)
6. Add a child **TMP_Text** GameObject named `HPText`
7. Wire the `HealthBarUI` fields:
   - `_hpBarImage` → HPBarFill Image
   - `_hpText` → HPText TMP_Text
   - `_mpBarImage` / `_mpText` → leave null (enemies have no MP)
8. Wire the `EnemyWorldSpaceHealthBar` fields:
   - `_targetTransform` → leave unassigned (set at runtime by BattleHUD)
   - `_yOffset` → 2.5 (adjust based on enemy sprite height)
   - `_healthBar` → the HealthBarUI on HPBarFill

> **Unity Editor task (user):** Update BattleHUD references in Battle.unity

1. In the Battle scene Hierarchy, select the BattleHUD GameObject
2. In the Inspector, locate the `_enemyWorldBar` field (was `_enemyHealthBar`)
3. Drag the `EnemyWorldBar` GameObject from Hierarchy into this field
4. Remove or disable the old fixed-position enemy HP bar GameObject from the screen-space Canvas (if it existed)

> **Unity Editor task (user):** Reposition player HP/MP bars to bottom-left

1. In the Battle scene, select the player HP/MP bar panel (child of the screen-space Canvas)
2. Set its RectTransform anchor to bottom-left
3. Position at e.g. `(20, 20, 0)` offset from bottom-left corner

> **Unity Editor task (user):** Configure HP gradient colors (optional — defaults are set in code)

1. Select the player HP bar's HealthBarUI component
2. In the Inspector, verify `_applyHpColorGradient` is checked
3. Adjust `_hpFullColor`, `_hpMidColor`, `_hpLowColor` if desired (defaults: green → yellow → red)
4. For the enemy HealthBarUI, `_applyHpColorGradient` should also be checked (red gradient bar)

> **Unity Editor task (user):** Enter Play Mode and verify

1. Start the Battle scene
2. Verify player HP/MP bars appear in bottom-left corner
3. Verify enemy HP bar floats above the enemy sprite in world space
4. Attack / take damage — verify bars lerp smoothly
5. Verify HP bar changes color (green → yellow → red) as HP drops
6. Verify enemy HP bar hides on Victory/Defeat/Fled
7. Verify TMP labels show correct "HP 45/50" format
8. Verify no console errors

---

### Task 5: UVCS Check-in

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-87): add HP gradient color, world-space enemy bar, reposition player bars`
  - `Assets/Scripts/Battle/UI/HealthBarUI.cs`
  - `Assets/Scripts/Battle/UI/HealthBarUI.cs.meta`
  - `Assets/Scripts/Battle/UI/EnemyWorldSpaceHealthBar.cs`
  - `Assets/Scripts/Battle/UI/EnemyWorldSpaceHealthBar.cs.meta`
  - `Assets/Scripts/Battle/UI/BattleHUD.cs`
  - `Assets/Scripts/Battle/UI/BattleHUD.cs.meta`
  - `Assets/Tests/Editor/Battle/HealthBarUITests.cs`
  - `Assets/Tests/Editor/Battle/HealthBarUITests.cs.meta`
  - `Assets/Scenes/Battle.unity`

---

### Post-Plan Review Checklist

- [x] C# guard clause ordering — HealthBarUI.SetMP exits early on `_mpBarImage == null` before checking text; SetHP checks `_hpText != null` after setting fill target (correct — text is optional)
- [x] Test coverage — `GetHealthColor` tested for full, mid, low, zero, clamped-above, clamped-below, and alpha opacity (7 tests covering all branches)
- [x] UVCS staged file audit — All `.cs` files paired with `.cs.meta`; `Battle.unity` listed (creates its own `.meta`); no new folders created (scripts go in existing `Assets/Scripts/Battle/UI/` and `Assets/Tests/Editor/Battle/`)
- [x] Method signature consistency — `EnemyWorldSpaceHealthBar.SetHP(int, int)` matches `HealthBarUI.SetHP(int, int)` it delegates to; `SetVisible(bool)` matches `SetVisible(bool)` on HealthBarUI
- [x] Unity Editor task isolation — All Editor tasks have explicit `> **Unity Editor task (user):**` callouts and are not mixed with code steps
- [x] MonoBehaviour separation — `EnemyWorldSpaceHealthBar` handles only lifecycle (Update follows transform, delegates all bar logic to HealthBarUI); `HealthBarUI` handles lerp animation in Update (lifecycle) and exposes static `GetHealthColor` (pure logic, testable)
- [x] No git commands — All commit steps use UVCS only
