# DEV-24: Spell Effects — Sprite-based VFX and SFX per Spell on Cast

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Play a sprite-based animation and/or sound effect in the Battle scene whenever the player successfully casts a spell.

**Architecture:** Two new optional fields are added to `SpellData`: `castVfxClip` (AnimationClip) and `castSfxVariants` (AudioClip array, 1-5 clips per spell). A new `SpellVFXController` MonoBehaviour (following the existing `PlayerBattleAnimator` pattern) sits in the Battle scene and uses an `AnimatorOverrideController` to swap in the per-spell `AnimationClip` at runtime, shows the `SpriteRenderer` for the clip's duration, and calls `AudioSource.PlayOneShot()` with a randomly selected variant to prevent repetitive audio. `BattleController` calls `SpellVFXController.Play(spell)` immediately after the `OnSpellRecognized` event fires.

**Tech Stack:** Unity 6 LTS, URP 2D, C#, `AnimatorOverrideController`, `AudioSource.PlayOneShot`, `Axiom.Battle` assembly, `Axiom.Data` assembly, Unity Test Framework (NUnit Edit Mode)

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `Assets/Scripts/Data/SpellData.cs` | Add `castVfxClip` (AnimationClip) and `castSfxVariants` (AudioClip[]) optional fields |
| Create | `Assets/Scripts/Battle/SpellVFXController.cs` | MonoBehaviour: swap Animator clip via override, show SpriteRenderer for clip duration, play one-shot SFX |
| Modify | `Assets/Scripts/Battle/BattleController.cs` | Add `_spellVfxController` serialized field; call `Play()` inside `OnSpellCast()` |
| Create | `Assets/Tests/Editor/Battle/SpellDataVFXTests.cs` | Edit Mode test: new SpellData fields are null by default |

No new `.asmdef` is required. `SpellVFXController` lives in `Assets/Scripts/Battle/` under the existing `Axiom.Battle` assembly. `SpellDataVFXTests.cs` is covered by the existing `BattleTests.asmdef` which already references both `Axiom.Battle` and `Axiom.Data`.

---

### Task 1: Extend SpellData with castVfxClip and castSfx

**Files:**
- Modify: `Assets/Scripts/Data/SpellData.cs`
- Create: `Assets/Tests/Editor/Battle/SpellDataVFXTests.cs`

> Both fields are intentionally optional. `null`/empty means "no effect for this spell." All runtime code null-checks before using them. `castSfxVariants` is an array — assign 1 clip for a fixed sound, or 3-5 clips for random variation (game-audio best practice: avoid repeating the same sound every cast).

- [ ] **Write the failing test**

Create `Assets/Tests/Editor/Battle/SpellDataVFXTests.cs`:

```csharp
using NUnit.Framework;
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
    }
}
```

- [ ] **Run tests — confirm they fail**

Unity Editor → Window → General → Test Runner → Edit Mode tab → run `SpellDataVFXTests`. Expected: compile error — `castVfxClip` and `castSfxVariants` do not exist on `SpellData` yet.

- [ ] **Add the fields to SpellData**

In `Assets/Scripts/Data/SpellData.cs`, append the following block after the `[Header("Chemistry Condition System")]` section and before the `#if UNITY_EDITOR` block:

```csharp
[Header("Spell Effects")]

[Tooltip("Sprite animation clip played at the VFX spawn point when this spell is cast. Leave empty for no visual effect.")]
public AnimationClip castVfxClip;

[Tooltip("Sound effects played when this spell is cast. Assign 1-5 clips — one is chosen at random each cast to avoid repetition. Leave empty for no audio effect.")]
public AudioClip[] castSfxVariants;
```

- [ ] **Run tests — confirm they pass**

Unity Editor → Test Runner → Edit Mode → run `SpellDataVFXTests`. Expected: 2 PASS.

- [ ] **Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-24): add castVfxClip and castSfx fields to SpellData`
- `Assets/Scripts/Data/SpellData.cs`
- `Assets/Scripts/Data/SpellData.cs.meta`
- `Assets/Tests/Editor/Battle/SpellDataVFXTests.cs`
- `Assets/Tests/Editor/Battle/SpellDataVFXTests.cs.meta`

---

### Task 2: Create SpellVFXController

**Files:**
- Create: `Assets/Scripts/Battle/SpellVFXController.cs`

> **No Edit Mode unit tests for this class.** `SpellVFXController` is a MonoBehaviour that drives `Animator`, `SpriteRenderer`, and `AudioSource` — all Unity APIs unavailable outside Play Mode. This follows the same convention as `PlayerBattleAnimator` and `EnemyBattleAnimator`, which are also verified manually. Verification is in Task 5.

- [ ] **Create SpellVFXController.cs**

Create `Assets/Scripts/Battle/SpellVFXController.cs`:

```csharp
using System.Collections;
using UnityEngine;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour that plays a per-spell sprite animation and SFX when a spell is cast.
    /// Wired into BattleController via the _spellVfxController serialized field.
    ///
    /// Unity Editor setup required on the SpellVFX GameObject in the Battle scene:
    ///   - SpriteRenderer    — starts disabled; this controller enables it for the clip duration.
    ///   - Animator          — must use SpellVFXAnimator controller (one state named "SpellVFX"
    ///                         using a placeholder clip named exactly "SpellVFXBase").
    ///   - AudioSource       — Play On Awake: off, Loop: off, Spatial Blend: 0 (fully 2D).
    /// </summary>
    public class SpellVFXController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Animator on this GameObject. Must use the SpellVFX base AnimatorController.")]
        private Animator _animator;

        [SerializeField]
        [Tooltip("SpriteRenderer on this GameObject. Keep disabled at start; this controller manages visibility.")]
        private SpriteRenderer _spriteRenderer;

        [SerializeField]
        [Tooltip("AudioSource on this GameObject for one-shot SFX playback.")]
        private AudioSource _audioSource;

        // Must match the state name in the SpellVFXAnimator controller.
        private const string VfxStateName = "SpellVFX";

        // Must match the placeholder clip name in the SpellVFXAnimator controller.
        private const string BaseClipName = "SpellVFXBase";

        private AnimatorOverrideController _overrideController;

        private void Awake()
        {
            if (_animator == null) return;
            _overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
            _animator.runtimeAnimatorController = _overrideController;

            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;
        }

        /// <summary>
        /// Plays the VFX clip and/or SFX from the given SpellData.
        /// Fields are optional — null castVfxClip or null castSfx are silently skipped.
        /// If called while a previous effect is playing, it is interrupted immediately.
        /// No-op if spell is null.
        /// </summary>
        public void Play(SpellData spell)
        {
            if (spell == null) return;
            StopAllCoroutines();
            StartCoroutine(PlaySequence(spell));
        }

        private IEnumerator PlaySequence(SpellData spell)
        {
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
    }
}
```

- [ ] **Verify no compile errors**

Check the Unity Console. Expected: none. `Axiom.Data` is already referenced by `Axiom.Battle.asmdef` so `SpellData` and `AnimationClip` resolve without any `.asmdef` changes.

- [ ] **Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-24): add SpellVFXController MonoBehaviour`
- `Assets/Scripts/Battle/SpellVFXController.cs`
- `Assets/Scripts/Battle/SpellVFXController.cs.meta`

---

### Task 3: Wire SpellVFXController into BattleController

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

- [ ] **Add the serialized field**

In `Assets/Scripts/Battle/BattleController.cs`, in the `[SerializeField]` block near `_playerAnimator` and `_enemyAnimator` (around line 48), add:

```csharp
[SerializeField]
[Tooltip("Assign the SpellVFXController from the Battle scene. Leave unassigned to skip VFX/SFX on spell cast.")]
private SpellVFXController _spellVfxController;
```

- [ ] **Call Play inside OnSpellCast**

In `OnSpellCast()`, immediately after the line `OnSpellRecognized?.Invoke(spell);`, add:

```csharp
_spellVfxController?.Play(spell);
```

The block should read:

```csharp
_isAwaitingVoiceSpell     = false;
_playerDamageVisualsFired = true;
OnSpellRecognized?.Invoke(spell);
_spellVfxController?.Play(spell);

SpellResult result = _resolver.Resolve(spell, _playerStats, _enemyStats);
```

- [ ] **Verify no compile errors**

Check the Unity Console. Expected: none.

- [ ] **Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-24): wire SpellVFXController.Play into BattleController.OnSpellCast`
- `Assets/Scripts/Battle/BattleController.cs`
- `Assets/Scripts/Battle/BattleController.cs.meta`

---

### Task 4: Unity Editor — Asset Setup and Scene Wiring

> All steps in this task are performed in the Unity Editor. No code is written. You (the user) execute every step below.

**4a — Create folder structure**

> **Unity Editor task (user):** In the Project window, create the following folders (right-click → Create → Folder). Unity generates `.meta` files automatically.

```
Assets/Art/Sprites/VFX/
Assets/Animations/VFX/
Assets/Audio/SFX/Spells/
```

**4b — Import VFX sprite sheets**

> **Unity Editor task (user):** For each spell that needs a visual effect:

**Naming convention** (follow this exactly for consistency across the project):
```
vfx_<spellname>_<frame_number>.png   ← individual frames, zero-padded
vfx_<spellname>_sheet.png           ← packed sprite sheet
```
Examples: `vfx_ember_sheet.png`, `vfx_cryo_sheet.png`

1. Import the sprite sheet PNG into `Assets/Art/Sprites/VFX/<SpellName>/`
2. Select the imported texture → Inspector:
   - Texture Type: **Sprite (2D and UI)**
   - Sprite Mode: **Multiple**
   - Filter Mode: **Point (no filter)** (for pixel art) or **Bilinear** (for painted sprites)
3. Click **Sprite Editor** → Slice by **Grid By Cell Size** matching your frame dimensions → **Apply**
4. **Never use `SpriteRenderer.FlipX`** — if sprite orientation needs correcting, set `Transform.localScale.x = -1` on the SpellVFX GameObject in the Inspector

**Sprite Atlas (draw call optimization):** Once all spell VFX sheets are imported, create a Unity Sprite Atlas to batch them into a single draw call:
- Right-click in Project → **Create → 2D → Sprite Atlas**
- Name it `VFXAtlas`, save to `Assets/Art/Sprites/VFX/`
- In the Atlas Inspector → **Objects for Packing**: drag all VFX sprite sheets in
- Enable **Include in Build**

**4c — Create per-spell Animation Clips**

> **Unity Editor task (user):** For each spell VFX:

1. In the Project window, select all sliced frames for that spell
2. Drag them onto the **Hierarchy** to auto-generate an AnimationClip (or use Animation window → Create New Clip)
3. Save the clip to `Assets/Animations/VFX/<SpellName>_VFX.anim`
4. Select the `.anim` file → Inspector → **Loop Time: unchecked** (each VFX plays once)

**Frame rate and count guidance:**
- Set sample rate to **8–12 FPS** in the Animation window (sufficient for VFX, avoids over-animation)
- Target **5–8 frames total** per spell VFX — snappy effects read better in combat than long ones

**3-part animation structure** (apply this when drawing the frames):

| Part | Frames | Purpose | Notes |
|------|--------|---------|-------|
| **Flash / Anticipation** | 1–2 | Brief bright flash, telegraphs the spell | Use squash — small and compressed |
| **Impact / Main effect** | 2–4 | Core visual (flame, ice burst, etc.) | Full size, maximum contrast |
| **Dissipation / Tail** | 1–2 | Fade or scatter out | Use stretch — elongate the dissipation |

This follows the game-art animation principles (squash & stretch, anticipation, follow-through) and makes spell VFX feel punchy and readable at battle distance.

**4d — Create the SpellVFXBase placeholder clip**

> **Unity Editor task (user):**

1. In `Assets/Animations/VFX/`, right-click → Create → Animation
2. Name it exactly **`SpellVFXBase`** — this exact string is the lookup key used by `AnimatorOverrideController` at runtime. Any spelling difference will cause a silent no-op at runtime.
3. Leave it as an empty single-frame clip (no keyframes needed — it is a placeholder only)

**4e — Create the SpellVFXAnimator controller**

> **Unity Editor task (user):**

1. In `Assets/Animations/VFX/`, right-click → Create → Animator Controller → name it **`SpellVFXAnimator`**
2. Open the **Animator** window (Window → Animation → Animator)
3. Delete any auto-created default states (select → Delete)
4. Right-click in the Animator graph → **Create State → Empty** → name the state **`SpellVFX`** (exact name — matches `VfxStateName` constant in `SpellVFXController`)
5. Right-click the `SpellVFX` state → **Set as Layer Default State**
6. Select the `SpellVFX` state → Inspector:
   - **Motion:** drag in the `SpellVFXBase` placeholder clip
   - **Loop Time:** unchecked (the code controls duration via `WaitForSeconds`)
7. Do **not** add any Transition arrows or Parameters — the code restarts the state directly via `_animator.Play("SpellVFX", 0, 0f)`

**4f — Create the SpellVFX GameObject in the Battle scene**

> **Unity Editor task (user):**

1. Open `Assets/Scenes/Battle.unity`
2. Hierarchy → right-click → **Create Empty** → name it **`SpellVFX`**
3. In the Transform, position it between the player and enemy (e.g., `X: 0, Y: 0, Z: 0` or wherever the center of the battlefield is)
4. Add these components (Add Component button):
   - **Sprite Renderer**
     - Sprite: *(leave empty)*
     - enabled: **unchecked** (the controller manages this)
     - Order In Layer: set higher than the player and enemy sprites so VFX renders on top
   - **Animator**
     - Controller: drag in `SpellVFXAnimator`
     - Apply Root Motion: **unchecked**
   - **Audio Source**
     - Play On Awake: **unchecked**
     - Loop: **unchecked**
     - Spatial Blend: **0** (fully 2D — no 3D positional rolloff)
   - **Spell VFX Controller** (the script)
5. In the **Spell VFX Controller** component, assign:
   - `_animator` → drag the **Animator** component from this same GameObject
   - `_spriteRenderer` → drag the **Sprite Renderer** component from this same GameObject
   - `_audioSource` → drag the **Audio Source** component from this same GameObject

**4g — Assign SpellVFXController to BattleController**

> **Unity Editor task (user):**

1. Select the **BattleController** GameObject in the Hierarchy
2. In Inspector, find the **Spell VFX Controller** field (added in Task 3)
3. Drag the `SpellVFX` GameObject into that field

**4h — Import SFX clips**

> **Unity Editor task (user):** Spell SFX falls in the **Player SFX** audio category — non-3D (Spatial Blend: 0), one-shot, and second-highest priority in the audio hierarchy. Aim for **3–5 variants per spell** to prevent the same sound playing every cast.

For each spell SFX variant (`.ogg` preferred on PC, or `.wav`):

1. Place the file in `Assets/Audio/SFX/Spells/`
   - Naming: `sfx_spell_<spellname>_<variant_number>.ogg` (e.g., `sfx_spell_ember_01.ogg`, `sfx_spell_ember_02.ogg`)
2. Select it → Inspector:
   - Load Type: **Decompress On Load** (correct for short one-shot clips under ~1 second)
   - Compression Format: **Vorbis** (OGG Vorbis is the recommended format for PC — good quality, no licensing issues)
   - Force To Mono: your call depending on the source; stereo is fine for one-shot SFX

**4i — Assign castVfxClip and castSfx on SpellData assets**

> **Unity Editor task (user):** For each `.asset` file in `Assets/Data/Spells/`:

1. Select the SpellData asset → Inspector
2. Scroll to the **Spell Effects** header
3. Drag the matching `.anim` clip into **Cast Vfx Clip**
4. Drag **all variant SFX clips** for this spell into the **Cast Sfx Variants** array (click the array size field and set count, then drag each clip into its slot). Assign 1 clip minimum, up to 5 for variety.
5. Leave both empty for spells that have no effect — `null`/empty is handled gracefully

- [ ] **Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-24): add SpellVFX scene object, animator, and VFX/SFX assets`

Stage all new/modified files, including:
- `Assets/Art/Sprites/VFX/` and all frame textures + `.meta` files
- `Assets/Animations/VFX/SpellVFXBase.anim` + `.meta`
- `Assets/Animations/VFX/SpellVFXAnimator.controller` + `.meta`
- All per-spell `.anim` files + `.meta` files
- `Assets/Audio/SFX/Spells/` SFX files + `.meta` files
- `Assets/Scenes/Battle.unity` (new SpellVFX GameObject + BattleController field assignment)
- All new folder `.meta` files (e.g., `Assets/Art/Sprites/VFX.meta`, `Assets/Animations/VFX.meta`, `Assets/Audio/SFX/Spells.meta`)
- Updated `Assets/Data/Spells/*.asset` files with the new VFX/SFX assignments

---

### Task 5: Manual Verification in Play Mode

> No UVCS commit for this task — verification only.

- [ ] **Enter Play Mode**

Open `Assets/Scenes/Battle.unity` → press **Play**.

- [ ] **Cast a spell via voice**

Hold push-to-talk, speak a spell name. Verify each of the following:

1. **VFX plays:** The SpellVFX sprite animation appears at its position for the duration of the clip, then disappears. The `SpriteRenderer` is not visible during idle turns.
2. **SFX plays:** The sound fires at the moment of cast (not delayed to damage resolution).
3. **No VFX → no artifact:** Cast a spell with `castVfxClip` left empty — no sprite flicker, no animation error.
4. **No SFX → no artifact:** Cast a spell with `castSfxVariants` left empty — no audio error in Console.
5. **Console is clean:** No null reference exceptions, no missing clip warnings, no animation state errors.

- [ ] **Confirm the `SpellVFXBase` override is working**

If more than one spell has a `castVfxClip` assigned, cast each in sequence and verify each plays its own distinct animation (not the same clip repeatedly) — confirming the `AnimatorOverrideController` swap is working correctly.

- [ ] **Confirm SFX variation**

Cast the same spell 5–6 times in a row. If 3+ SFX variants are assigned, verify the sounds are not identical every cast — the random selection should produce audible variation. Check the Console: no IndexOutOfRange or NullReference errors.
