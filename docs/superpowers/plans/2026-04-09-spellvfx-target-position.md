# SpellVFX Target Position Routing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route the SpellVFX GameObject to the correct character's world position at cast time — enemy position for Damage spells, player position for Heal and Shield spells.

**Architecture:** Add a `Vector3 position` parameter to `SpellVFXController.Play()` and apply it via `transform.position` inside the coroutine before the clip plays. `BattleController.OnSpellCast()` selects the correct world position from the already-serialized `_enemyAnimator` or `_playerAnimator` transforms based on `spell.effectType`, then passes it through. No new classes, no new GameObjects, no new serialized fields.

**Tech Stack:** Unity 6 LTS, URP 2D, C#, Unity Test Framework (Edit Mode / NUnit)

**Jira:** DEV-29

---

## Files Modified

| File | Change |
|------|--------|
| `Assets/Scripts/Battle/SpellVFXController.cs` | Add `Vector3 position` param to `Play()` and `PlaySequence()`; set `transform.position` before clip plays |
| `Assets/Scripts/Battle/BattleController.cs` | Compute target world position in `OnSpellCast()` based on `spell.effectType`; pass to `Play()` |
| `Assets/Tests/Editor/Battle/SpellDataVFXTests.cs` | Add null-safety test for the new `Play(spell, position)` signature |

---

### Task 1: Update SpellVFXController to accept and apply a spawn position

**Files:**
- Modify: `Assets/Scripts/Battle/SpellVFXController.cs`
- Modify: `Assets/Tests/Editor/Battle/SpellDataVFXTests.cs`

- [ ] **Write the failing test**

  Open `Assets/Tests/Editor/Battle/SpellDataVFXTests.cs`. Add the following test **inside the existing `SpellDataVFXTests` class**, after the two existing tests:

  ```csharp
  [Test]
  public void SpellVFXController_PlayWithPosition_DoesNotThrowWhenOptionalRefsAreNull()
  {
      // Arrange — _animator, _spriteRenderer, _audioSource are all null (unassigned
      // Inspector refs). PlaySequence's null-guards must prevent any NullReferenceException.
      var go = new GameObject();
      var controller = go.AddComponent<SpellVFXController>();
      var spell = ScriptableObject.CreateInstance<SpellData>();
      spell.castVfxClip = null;
      spell.castSfxVariants = null;

      // Act & Assert
      Assert.DoesNotThrow(() => controller.Play(spell, new Vector3(3f, 1f, 0f)));

      Object.DestroyImmediate(go);
      Object.DestroyImmediate(spell);
  }
  ```

  The full file after adding the test:

  ```csharp
  using NUnit.Framework;
  using Axiom.Battle;
  using Axiom.Data;
  using UnityEngine;

  namespace Axiom.Tests.Battle
  {
      public class SpellDataVFXTests
      {
          [Test]
          public void SpellData_CastVfxClip_IsNullByDefault()
          {
              var spell = ScriptableObject.CreateInstance<SpellData>();
              Assert.IsNull(spell.castVfxClip);
              Object.DestroyImmediate(spell);
          }

          [Test]
          public void SpellData_CastSfxVariants_IsNullOrEmptyByDefault()
          {
              var spell = ScriptableObject.CreateInstance<SpellData>();
              Assert.IsTrue(spell.castSfxVariants == null || spell.castSfxVariants.Length == 0);
              Object.DestroyImmediate(spell);
          }

          [Test]
          public void SpellVFXController_PlayWithPosition_DoesNotThrowWhenOptionalRefsAreNull()
          {
              // Arrange — _animator, _spriteRenderer, _audioSource are all null (unassigned
              // Inspector refs). PlaySequence's null-guards must prevent any NullReferenceException.
              var go = new GameObject();
              var controller = go.AddComponent<SpellVFXController>();
              var spell = ScriptableObject.CreateInstance<SpellData>();
              spell.castVfxClip = null;
              spell.castSfxVariants = null;

              // Act & Assert
              Assert.DoesNotThrow(() => controller.Play(spell, new Vector3(3f, 1f, 0f)));

              Object.DestroyImmediate(go);
              Object.DestroyImmediate(spell);
          }
      }
  }
  ```

- [ ] **Run the test to confirm it fails (compile error expected)**

  Unity Editor → Window → General → Test Runner → Edit Mode tab → Run the `SpellDataVFXTests` suite.
  Expected: **Compile error** — `SpellVFXController.Play()` has no overload that accepts a `Vector3`.

- [ ] **Update `SpellVFXController.Play()` and `PlaySequence()`**

  In `Assets/Scripts/Battle/SpellVFXController.cs`, replace the existing `Play()` and `PlaySequence()` methods (lines 55–84) with:

  ```csharp
  /// <summary>
  /// Plays the VFX clip and/or SFX from the given SpellData at the specified world position.
  /// Fields are optional — null castVfxClip or null castSfx are silently skipped.
  /// If called while a previous effect is playing, it is interrupted immediately.
  /// No-op if spell is null.
  /// </summary>
  public void Play(SpellData spell, Vector3 position)
  {
      if (spell == null) return;
      StopAllCoroutines();
      StartCoroutine(PlaySequence(spell, position));
  }

  private IEnumerator PlaySequence(SpellData spell, Vector3 position)
  {
      // Reposition to the target character before any visual appears.
      transform.position = position;

      // SFX: pick a random variant and fire immediately at cast time.
      // Using an array of 1-5 variants prevents the same sound from playing every cast.
      if (spell.castSfxVariants != null && spell.castSfxVariants.Length > 0 && _audioSource != null)
      {
          var clip = spell.castSfxVariants[UnityEngine.Random.Range(0, spell.castSfxVariants.Length)];
          if (clip != null)
              _audioSource.PlayOneShot(clip);
      }

      // VFX shows for the exact duration of the animation clip.
      if (spell.castVfxClip != null && _animator != null && _spriteRenderer != null)
      {
          _overrideController[BaseClipName] = spell.castVfxClip;
          _animator.Play(VfxStateName, 0, 0f);
          _spriteRenderer.enabled = true;

          yield return new WaitForSeconds(spell.castVfxClip.length);

          _spriteRenderer.enabled = false;
      }
  }
  ```

- [ ] **Run the test to confirm it passes**

  Unity Test Runner → Edit Mode → `SpellDataVFXTests` → Run All.
  Expected: All **3 tests pass**, including `SpellVFXController_PlayWithPosition_DoesNotThrowWhenOptionalRefsAreNull`.

---

### Task 2: Update BattleController to route position by spell effect type

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

- [ ] **Locate the VFX call in `OnSpellCast()` (around line 313)**

  Find this block inside `OnSpellCast()`:

  ```csharp
  _isAwaitingVoiceSpell     = false;
  _playerDamageVisualsFired = true;
  OnSpellRecognized?.Invoke(spell);
  _spellVfxController?.Play(spell);
  ```

- [ ] **Replace the `Play(spell)` call with position-routed call**

  Update those four lines to:

  ```csharp
  _isAwaitingVoiceSpell     = false;
  _playerDamageVisualsFired = true;
  OnSpellRecognized?.Invoke(spell);

  if (_spellVfxController != null)
  {
      Vector3 vfxPosition = spell.effectType == SpellEffectType.Damage
          ? (_enemyAnimator  != null ? _enemyAnimator.transform.position  : Vector3.zero)
          : (_playerAnimator != null ? _playerAnimator.transform.position : Vector3.zero);
      _spellVfxController.Play(spell, vfxPosition);
  }
  ```

  > **Why `Vector3.zero` fallback:** `_enemyAnimator` and `_playerAnimator` are optional serialized refs used for animation only. If the Battle scene is tested without animators wired up, VFX still plays at world origin rather than throwing a `NullReferenceException`.

- [ ] **Confirm no other callers of the old `Play(spell)` signature exist**

  In VS Code, search the `Assets/` folder for `_spellVfxController` to verify `BattleController.OnSpellCast()` is the only call site. Expected: exactly one result.

- [ ] **Verify the project compiles with no errors**

  Switch to Unity Editor and check the Console window. Expected: **0 compiler errors**.

- [ ] **Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files below → Check in with message:
  `feat(DEV-29): route SpellVFX spawn position to target character by effect type`
  - `Assets/Scripts/Battle/SpellVFXController.cs`
  - `Assets/Scripts/Battle/BattleController.cs`
  - `Assets/Tests/Editor/Battle/SpellDataVFXTests.cs`

---

### Task 3: Manual Play Mode verification

**Files:** None (verification only)

- [ ] **Open the Battle scene and enter Play Mode**

  Open `Assets/Scenes/Battle.unity` → press Play.

- [ ] **Verify Damage spell VFX position**

  Cast a spell with `effectType = Damage` (press the Spell button, speak or trigger the spell).
  Expected: The SpellVFX animation appears at the **enemy's** screen position.

- [ ] **Verify Heal spell VFX position**

  Cast a spell with `effectType = Heal`.
  Expected: The SpellVFX animation appears at the **player's** screen position.

- [ ] **Verify Shield spell VFX position**

  Cast a spell with `effectType = Shield`.
  Expected: The SpellVFX animation appears at the **player's** screen position.

- [ ] **Verify null-VFX spells produce no errors**

  Cast a spell that has no `castVfxClip` assigned.
  Expected: No visual, no Console errors.

- [ ] **Press Stop and confirm no lingering Console errors**
