# Battle Condition Feedback UI (DEV-28) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a physical-immunity message to the battle log and colored turn-count badges below each character's HP bar for active time-limited conditions.

**Architecture:** Two new `BattleController` events (`OnPhysicalAttackImmune`, `OnConditionsChanged`) carry immunity and condition-change signals to `BattleHUD`. A new `ConditionBadgeUI` MonoBehaviour renders the badge row by reading `CharacterStats.ActiveStatusConditions` and a new `GetMaterialTransformTurns()` method.

**Tech Stack:** Unity 6 LTS · C# · Unity UI (Canvas) · TextMeshPro · NUnit (Unity Test Runner)

**Spec:** `docs/superpowers/specs/2026-04-06-battle-condition-feedback-ui.md`
**Jira:** DEV-28

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `Assets/Scripts/Battle/CharacterStats.cs` | Add `GetMaterialTransformTurns(ChemicalCondition)` |
| Modify | `Assets/Scripts/Battle/BattleController.cs` | Add 2 events; update fire points in `FirePlayerDamageVisuals`, `FireEnemyDamageVisuals`, `ProcessPlayerTurnStart`, `ProcessEnemyTurnStart`, `OnSpellCast` |
| **Create** | `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs` | MonoBehaviour that renders a row of condition badges; `Refresh(CharacterStats)` |
| Modify | `Assets/Scripts/Battle/UI/BattleHUD.cs` | Subscribe to 2 new events; add 2 `[SerializeField] ConditionBadgeUI` refs; delegate to badge UI |
| Modify | `Assets/Tests/Editor/Battle/CharacterStatsTests.cs` | Tests for `GetMaterialTransformTurns` |

---

## Task 1: `CharacterStats.GetMaterialTransformTurns`

**Files:**
- Modify: `Assets/Scripts/Battle/CharacterStats.cs`
- Test: `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`

- [ ] **Step 1: Write the failing tests**

  Open `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`. Append these four tests at the bottom of the class (before the final `}`):

  ```csharp
  // ---- GetMaterialTransformTurns ----

  [Test]
  public void GetMaterialTransformTurns_ActiveTransformation_ReturnsTurnsRemaining()
  {
      var stats = MakeStats();
      var innate = new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
          { Axiom.Data.ChemicalCondition.Liquid };
      stats.Initialize(innate);

      // Simulate a Freeze reaction: consume Liquid, apply Solid for 2 turns
      stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Liquid);
      stats.ApplyMaterialTransformation(
          Axiom.Data.ChemicalCondition.Solid,
          Axiom.Data.ChemicalCondition.Liquid,
          2);

      Assert.AreEqual(2, stats.GetMaterialTransformTurns(Axiom.Data.ChemicalCondition.Solid));
  }

  [Test]
  public void GetMaterialTransformTurns_InnateCondition_ReturnsZero()
  {
      var stats = MakeStats();
      var innate = new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
          { Axiom.Data.ChemicalCondition.Liquid };
      stats.Initialize(innate);

      // Liquid is innate/permanent — not a transformation
      Assert.AreEqual(0, stats.GetMaterialTransformTurns(Axiom.Data.ChemicalCondition.Liquid));
  }

  [Test]
  public void GetMaterialTransformTurns_ConditionNotPresent_ReturnsZero()
  {
      var stats = MakeStats();
      stats.Initialize();

      Assert.AreEqual(0, stats.GetMaterialTransformTurns(Axiom.Data.ChemicalCondition.Solid));
  }

  [Test]
  public void GetMaterialTransformTurns_AfterTransformationExpires_ReturnsZero()
  {
      var stats = MakeStats();
      var innate = new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
          { Axiom.Data.ChemicalCondition.Liquid };
      stats.Initialize(innate);

      stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Liquid);
      stats.ApplyMaterialTransformation(
          Axiom.Data.ChemicalCondition.Solid,
          Axiom.Data.ChemicalCondition.Liquid,
          1);

      // Tick once — 1-turn transformation expires
      stats.ProcessConditionTurn();

      Assert.AreEqual(0, stats.GetMaterialTransformTurns(Axiom.Data.ChemicalCondition.Solid));
  }
  ```

- [ ] **Step 2: Run tests to confirm they fail**

  Unity Editor → Window → General → Test Runner → EditMode tab → select these four tests → Run Selected.
  Expected: all four fail with `CS0117` or similar (method does not exist yet).

- [ ] **Step 3: Implement `GetMaterialTransformTurns` in `CharacterStats`**

  Open `Assets/Scripts/Battle/CharacterStats.cs`. Find the `// ── Helpers ──` comment at the bottom of the class (before `DefaultDurationFor`). Insert the new method directly before it:

  ```csharp
  // ── Condition queries (continued) ────────────────────────────────────────

  /// <summary>
  /// Returns the turns remaining for a condition that is active as a temporary
  /// material transformation (e.g. Liquid frozen into Solid for N turns).
  /// Returns 0 if the condition is not active as a transformation — i.e. it is
  /// either an innate permanent condition or not present at all.
  /// </summary>
  public int GetMaterialTransformTurns(ChemicalCondition condition)
  {
      foreach (var transform in _materialTransformations)
          if (transform.ReplacementCondition == condition)
              return transform.TurnsRemaining;
      return 0;
  }
  ```

- [ ] **Step 4: Run tests to confirm they pass**

  Unity Editor → Test Runner → EditMode → run the same four tests.
  Expected: all four pass.

- [ ] **Step 5: Commit**

  ```
  test(DEV-28): add CharacterStats.GetMaterialTransformTurns tests
  feat(DEV-28): add GetMaterialTransformTurns to CharacterStats
  ```

  Stage and commit `CharacterStats.cs` and `CharacterStatsTests.cs`.

---

## Task 2: `OnPhysicalAttackImmune` event on `BattleController`

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

> No unit tests for this task — `BattleController` is a MonoBehaviour. Manual verification is in Task 5.

- [ ] **Step 1: Add the event declaration**

  Open `Assets/Scripts/Battle/BattleController.cs`. Find the `// ── UI Events ──` block (around line 60). Insert after `OnConditionDamageTick`:

  ```csharp
  /// <summary>
  /// Fires when a physical attack is fully blocked by the target's material state
  /// (Liquid or Vapor — IsPhysicallyImmune). No damage is dealt.
  /// Parameters: attacker CharacterStats, target CharacterStats.
  /// BattleHUD subscribes to show a specific immunity message in the log.
  /// </summary>
  public event Action<CharacterStats, CharacterStats> OnPhysicalAttackImmune;
  ```

- [ ] **Step 2: Update `FirePlayerDamageVisuals` to branch on immunity**

  Find `private void FirePlayerDamageVisuals()` (around line 420). Replace its body:

  ```csharp
  private void FirePlayerDamageVisuals()
  {
      if (_playerDamageVisualsFired) return;
      _playerDamageVisualsFired = true;

      if (_pendingPlayerAttack.IsImmune)
      {
          OnPhysicalAttackImmune?.Invoke(_playerStats, _enemyStats);
          return; // No damage occurred — skip OnDamageDealt to avoid a "0 damage" floating number
      }

      OnDamageDealt?.Invoke(_enemyStats, _pendingPlayerAttack.Damage, _pendingPlayerAttack.IsCrit);
      if (_pendingPlayerAttack.TargetDefeated)
          OnCharacterDefeated?.Invoke(_enemyStats);
  }
  ```

- [ ] **Step 3: Update `FireEnemyDamageVisuals` to branch on immunity**

  Find `private void FireEnemyDamageVisuals()` (around line 429). Replace its body:

  ```csharp
  private void FireEnemyDamageVisuals()
  {
      if (_enemyDamageVisualsFired) return;
      _enemyDamageVisualsFired = true;

      if (_pendingEnemyAttack.IsImmune)
      {
          OnPhysicalAttackImmune?.Invoke(_enemyStats, _playerStats);
          return; // No damage occurred — skip OnDamageDealt
      }

      OnDamageDealt?.Invoke(_playerStats, _pendingEnemyAttack.Damage, _pendingEnemyAttack.IsCrit);
      if (_pendingEnemyAttack.TargetDefeated)
          OnCharacterDefeated?.Invoke(_playerStats);
  }
  ```

- [ ] **Step 4: Commit**

  ```
  feat(DEV-28): add OnPhysicalAttackImmune event to BattleController
  ```

---

## Task 3: `OnConditionsChanged` event on `BattleController`

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

- [ ] **Step 1: Add the event declaration**

  In the same `// ── UI Events ──` block, insert after `OnPhysicalAttackImmune`:

  ```csharp
  /// <summary>
  /// Fires when a character's active condition list may have changed —
  /// after ProcessConditionTurn() ticks conditions, or after a spell applies a new condition.
  /// Parameter: the CharacterStats whose conditions changed.
  /// BattleHUD subscribes to refresh ConditionBadgeUI for the matching character.
  /// </summary>
  public event Action<CharacterStats> OnConditionsChanged;
  ```

- [ ] **Step 2: Fire after player turn condition processing**

  Find `private void ProcessPlayerTurnStart()`. After the `result.ActionSkipped` block (at the end of the method, before the closing `}`), add:

  ```csharp
  OnConditionsChanged?.Invoke(_playerStats);
  ```

  The full method should look like:

  ```csharp
  private void ProcessPlayerTurnStart()
  {
      ConditionTurnResult result = _playerStats.ProcessConditionTurn();
      if (result.TotalDamageDealt > 0)
          OnConditionDamageTick?.Invoke(_playerStats, result.TotalDamageDealt, Axiom.Data.ChemicalCondition.None);

      if (result.ActionSkipped)
      {
          Debug.Log("[Battle] Player is Frozen — turn skipped.");
          _isProcessingAction = true;
          _playerDamageVisualsFired = true;
          StartCoroutine(CompletePlayerAction(targetDefeated: false));
      }

      OnConditionsChanged?.Invoke(_playerStats);
  }
  ```

- [ ] **Step 3: Fire after enemy turn condition processing**

  Find `private void ProcessEnemyTurnStart()`. Add `OnConditionsChanged?.Invoke(_enemyStats);` at the end, before `ExecuteEnemyTurn()` and after the `ActionSkipped` block. The full method:

  ```csharp
  private void ProcessEnemyTurnStart()
  {
      ConditionTurnResult result = _enemyStats.ProcessConditionTurn();
      if (result.TotalDamageDealt > 0)
          OnConditionDamageTick?.Invoke(_enemyStats, result.TotalDamageDealt, Axiom.Data.ChemicalCondition.None);

      if (result.ActionSkipped)
      {
          Debug.Log("[Battle] Enemy is Frozen — turn skipped.");
          _battleManager.OnEnemyActionComplete(false);
          OnConditionsChanged?.Invoke(_enemyStats);
          return;
      }

      OnConditionsChanged?.Invoke(_enemyStats);
      ExecuteEnemyTurn();
  }
  ```

- [ ] **Step 4: Fire after a spell resolves in `OnSpellCast`**

  Find `public void OnSpellCast(SpellData spell)`. After `SpellResult result = _resolver.Resolve(spell, _playerStats, _enemyStats);` and the `switch` block (before `OnDamageDealt?.Invoke(_playerStats, 0, false)`), add:

  ```csharp
  // Conditions on either character may have changed due to the spell.
  OnConditionsChanged?.Invoke(_playerStats);
  OnConditionsChanged?.Invoke(_enemyStats);
  ```

- [ ] **Step 5: Commit**

  ```
  feat(DEV-28): add OnConditionsChanged event to BattleController
  ```

---

## Task 4: `ConditionBadgeUI` new MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs`

> `ConditionBadgeUI` is a MonoBehaviour that renders UI — unit testing it would require play-mode Unity tests. Manual verification is in Task 6 (scene setup). Focus here is on getting the script compiling correctly.

- [ ] **Step 1: Create `ConditionBadgeUI.cs`**

  Create `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs` with this content:

  ```csharp
  using TMPro;
  using UnityEngine;
  using UnityEngine.UI;
  using Axiom.Battle;
  using Axiom.Data;

  namespace Axiom.Battle
  {
      /// <summary>
      /// Renders a horizontal row of colored pill badges for a character's active
      /// time-limited conditions (status conditions + temporary material transformations).
      ///
      /// Call Refresh() whenever the character's condition list may have changed.
      /// Permanent innate material conditions (e.g. always-Liquid) are not shown —
      /// only conditions with a turn countdown appear.
      ///
      /// Inspector setup required:
      ///   _badgePrefab — a prefab with an Image (background, on root) + TMP_Text child
      ///   _container   — a Transform with HorizontalLayoutGroup + ContentSizeFitter
      /// </summary>
      public class ConditionBadgeUI : MonoBehaviour
      {
          [SerializeField]
          [Tooltip("Prefab for one badge. Root must have an Image; child must have a TMP_Text.")]
          private GameObject _badgePrefab;

          [SerializeField]
          [Tooltip("Parent container. Add HorizontalLayoutGroup + ContentSizeFitter (horizontal).")]
          private Transform _container;

          /// <summary>
          /// Clears and rebuilds the badge row from the character's current condition state.
          /// Safe to call with a null stats argument (clears the row).
          /// </summary>
          public void Refresh(CharacterStats stats)
          {
              // Clear existing badges
              foreach (Transform child in _container)
                  Destroy(child.gameObject);

              if (stats == null) return;

              // Status conditions — always time-limited (Frozen, Burning, Evaporating, Corroded, Crystallized)
              foreach (var entry in stats.ActiveStatusConditions)
                  SpawnBadge(entry.Condition, entry.TurnsRemaining);

              // Material conditions — only show if they are temporary transformations (turns > 0)
              foreach (var condition in stats.ActiveMaterialConditions)
              {
                  int turns = stats.GetMaterialTransformTurns(condition);
                  if (turns > 0)
                      SpawnBadge(condition, turns);
              }
          }

          private void SpawnBadge(ChemicalCondition condition, int turnsRemaining)
          {
              GameObject badge = Instantiate(_badgePrefab, _container);

              TMP_Text label = badge.GetComponentInChildren<TMP_Text>();
              if (label != null)
                  label.text = $"{LabelFor(condition)} ({turnsRemaining})";

              Image bg = badge.GetComponent<Image>();
              if (bg != null)
                  bg.color = ColorFor(condition);
          }

          private static string LabelFor(ChemicalCondition condition)
          {
              switch (condition)
              {
                  case ChemicalCondition.Frozen:       return "Frozen";
                  case ChemicalCondition.Burning:      return "Burning";
                  case ChemicalCondition.Evaporating:  return "Evaporate";
                  case ChemicalCondition.Corroded:     return "Corroded";
                  case ChemicalCondition.Crystallized: return "Crystal";
                  case ChemicalCondition.Solid:        return "Solid";
                  case ChemicalCondition.Vapor:        return "Vapor";
                  case ChemicalCondition.Liquid:       return "Liquid";
                  default:                             return condition.ToString();
              }
          }

          private static Color ColorFor(ChemicalCondition condition)
          {
              switch (condition)
              {
                  case ChemicalCondition.Frozen:
                  case ChemicalCondition.Solid:        return new Color(0.23f, 0.56f, 0.83f); // blue
                  case ChemicalCondition.Burning:      return new Color(0.79f, 0.25f, 0.13f); // red-orange
                  case ChemicalCondition.Evaporating:
                  case ChemicalCondition.Vapor:        return new Color(0.60f, 0.80f, 0.90f); // pale blue
                  case ChemicalCondition.Corroded:     return new Color(0.47f, 0.70f, 0.22f); // acid green
                  case ChemicalCondition.Crystallized: return new Color(0.60f, 0.44f, 0.85f); // purple
                  default:                             return new Color(0.50f, 0.50f, 0.50f); // grey
              }
          }
      }
  }
  ```

- [ ] **Step 2: Check for compile errors**

  Unity Editor → wait for script compilation (bottom right spinner). Console must show no errors related to `ConditionBadgeUI`.

- [ ] **Step 3: Commit**

  ```
  feat(DEV-28): add ConditionBadgeUI MonoBehaviour
  ```

---

## Task 5: `BattleHUD` wiring

**Files:**
- Modify: `Assets/Scripts/Battle/UI/BattleHUD.cs`

- [ ] **Step 1: Add `[SerializeField]` fields for badge components**

  Open `Assets/Scripts/Battle/UI/BattleHUD.cs`. Find the `[Header("UI Components")]` block (around line 33). Add two new fields at the bottom of that block:

  ```csharp
  [SerializeField] private ConditionBadgeUI      _playerConditionBadges;
  [SerializeField] private ConditionBadgeUI      _enemyConditionBadges;
  ```

- [ ] **Step 2: Subscribe to the two new events in `Setup()`**

  Find the `// Subscribe to battle events` comment in `Setup()`. Add these two subscriptions after the existing ones:

  ```csharp
  _battleController.OnPhysicalAttackImmune  += HandlePhysicalAttackImmune;
  _battleController.OnConditionsChanged     += HandleConditionsChanged;
  ```

- [ ] **Step 3: Add the two new event handlers**

  At the bottom of the class (before `Unsubscribe()`), add:

  ```csharp
  private void HandlePhysicalAttackImmune(CharacterStats attacker, CharacterStats target)
  {
      // Find the specific material condition causing immunity (Liquid or Vapor)
      string conditionName = "that material";
      foreach (var cond in target.ActiveMaterialConditions)
      {
          if (cond == Axiom.Data.ChemicalCondition.Liquid
              || cond == Axiom.Data.ChemicalCondition.Vapor)
          {
              conditionName = cond.ToString();
              break;
          }
      }
      _statusMessageUI.Post($"{target.Name} is {conditionName} — physical attacks pass right through!");
  }

  private void HandleConditionsChanged(CharacterStats target)
  {
      if (target == _playerStats)
          _playerConditionBadges?.Refresh(target);
      else if (target == _enemyStats)
          _enemyConditionBadges?.Refresh(target);
  }
  ```

- [ ] **Step 4: Unsubscribe in `Unsubscribe()`**

  Find `private void Unsubscribe()`. Add these two lines alongside the existing unsubscriptions:

  ```csharp
  _battleController.OnPhysicalAttackImmune  -= HandlePhysicalAttackImmune;
  _battleController.OnConditionsChanged     -= HandleConditionsChanged;
  ```

- [ ] **Step 5: Check for compile errors**

  Unity Editor → wait for compilation → Console must be error-free.

- [ ] **Step 6: Commit**

  ```
  feat(DEV-28): wire OnPhysicalAttackImmune and OnConditionsChanged in BattleHUD
  ```

---

## Task 6: Scene setup — create badge prefab and wire GameObjects

**Files:**
- Modify: `Assets/Scenes/Battle.unity` (scene changes only — no code)

> All steps in this task are performed in the Unity Editor. No code changes.

- [ ] **Step 1: Create the badge prefab**

  In the **Project** panel, right-click `Assets/Prefabs` (create the folder if it doesn't exist) → Create → UI → Canvas… or just create a UI GameObject temporarily.

  Easier approach via Hierarchy:
  1. In the **Hierarchy**, right-click → UI → Image. Name it `ConditionBadge`.
  2. Set the Image's **Color** to any placeholder (it's set at runtime via `ColorFor`).
  3. Set the Image **RectTransform**: Width = 90, Height = 20.
  4. Right-click `ConditionBadge` → UI → Text - TextMeshPro. Name it `Label`.
  5. Select `Label` → Inspector: Font Size = 10, Alignment = Center Middle, Overflow = Overflow.
  6. Select `ConditionBadge` → drag it to `Assets/Prefabs/UI/ConditionBadge.prefab` to save as prefab.
  7. Delete the temporary GameObject from the Hierarchy.

- [ ] **Step 2: Add badge container to the Enemy Panel**

  1. In the **Hierarchy**, expand the Battle Canvas → find the enemy panel (the parent that holds the enemy HealthBarUI).
  2. Right-click the enemy panel → UI → Empty → name it `EnemyConditionBadges`.
  3. With `EnemyConditionBadges` selected, **Add Component** → `ConditionBadgeUI`.
  4. **Add Component** → Layout → Horizontal Layout Group. Settings: Child Alignment = Middle Left, Control Child Size = Width + Height checked, spacing = 4.
  5. **Add Component** → Layout → Content Size Fitter. Horizontal Fit = Preferred Size.
  6. In `ConditionBadgeUI` Inspector: drag `Assets/Prefabs/UI/ConditionBadge.prefab` into `Badge Prefab`. Drag `EnemyConditionBadges` itself into `Container`.
  7. Use RectTransform anchors to position `EnemyConditionBadges` just below the enemy HP bar (Left = 0, Right = stretch, Height = 22, positioned below the bar).

- [ ] **Step 3: Add badge container to the Player Panel**

  Repeat Step 2 for the player panel:
  1. Find the player slot panel in the Hierarchy.
  2. Add a child `PlayerConditionBadges` with the same components and settings.
  3. Position it just below the player HP/MP bars.

- [ ] **Step 4: Wire SerializeFields on `BattleHUD`**

  1. Select the `BattleHUD` GameObject in the Hierarchy.
  2. In the Inspector, find the new `Player Condition Badges` and `Enemy Condition Badges` slots.
  3. Drag `PlayerConditionBadges` into `Player Condition Badges`.
  4. Drag `EnemyConditionBadges` into `Enemy Condition Badges`.

- [ ] **Step 5: Manual verification — immunity message**

  1. Ensure the `BattleController` Inspector has `EnemyData` set to `ED_MeltspawnTest` (which has innate `Liquid`).
  2. Enter Play Mode.
  3. Click **Attack**.
  4. Expected message in the log: `"Meltspawn is Liquid — physical attacks pass right through!"`
  5. Confirm no floating "0" number appears over the enemy.
  6. Exit Play Mode.

- [ ] **Step 6: Manual verification — condition badges**

  1. Stay in the Battle scene. Open BattleController Inspector, remove the EnemyData assignment so the enemy has no innate conditions.
  2. Enter Play Mode. Cast the Freeze spell (or a spell that inflicts Frozen) on the enemy.
  3. Expected: a blue `Frozen (1)` badge appears below the enemy HP bar.
  4. End the turn. Expected: the badge disappears (Frozen expired after 1 turn).
  5. Cast a spell that applies a reaction resulting in a material transformation (e.g. Freeze on Liquid enemy to produce `Solid`).
  6. Expected: `Solid (N)` badge appears and counts down each turn.
  7. Exit Play Mode.

- [ ] **Step 7: Commit**

  ```
  feat(DEV-28): add condition badge prefab and wire battle scene UI
  ```

---

## Self-Review

**Spec coverage check:**

| Spec requirement | Covered by |
|-----------------|------------|
| Immunity message: `"[Name] is [Liquid/Vapor] — physical attacks pass right through!"` | Task 5 `HandlePhysicalAttackImmune` |
| Immunity message fires for player→enemy and enemy→player | Task 2 — both `FirePlayerDamageVisuals` and `FireEnemyDamageVisuals` |
| `OnDamageDealt` suppressed in immune path | Task 2 — early `return` after `OnPhysicalAttackImmune?.Invoke` |
| Badges inline below HP bar | Task 6 scene setup |
| Only time-limited conditions show badges | Task 4 `Refresh()` — status conditions + transform turns > 0 |
| Status conditions show `TurnsRemaining` | Task 4 `entry.TurnsRemaining` |
| Temporary material transformations show turns via `GetMaterialTransformTurns` | Tasks 1 + 4 |
| Permanent innate conditions excluded | Task 4 — `GetMaterialTransformTurns` returns 0 for innate; 0 is skipped |
| Badge row refreshes on turn tick | Task 3 `ProcessPlayerTurnStart` / `ProcessEnemyTurnStart` |
| Badge row refreshes after spell applies condition | Task 3 `OnSpellCast` fire point |
| `Unsubscribe()` cleans up new event handlers | Task 5 Step 4 |
| `ConditionBadgeUI` in `Assets/Scripts/Battle/UI/` | Task 4 |
