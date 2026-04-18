# DEV-67 Main Menu Audio Polish — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship DEV-67 by centralizing main-menu BGM, ambient loop, and UI click SFX through a single persistent audio path, with per-channel mixer routing, **making the existing Settings control work** (open the settings surface, **show** the music and SFX volume sliders), wiring those sliders and persistence through `AudioManager`, and no duplicate layers across `MainMenu` ↔ `Platformer` transitions.

**Architecture:** Follow `docs/GAME_PLAN.md`: `AudioManager` is a MonoBehaviour shell only (sources, mixer output routing, `DontDestroyOnLoad` child of `GameManager` or peer on same prefab). All playback rules, duplicate suppression, fade policy, and volume math live in a plain C# `AudioPlaybackService` covered by Edit Mode tests. Tunable clip references and default level multipliers live on a **ScriptableObject** in `Axiom.Data` so content stays data-driven. `MainMenuController` stays Unity-free; `MainMenuUI` references the **finished** menu hierarchy, implements **Settings open/close** so the volume sliders are shown, and forwards UI clicks and slider events to the audio API only.

**Tech Stack:** Unity 6 LTS, Unity UI + TMP (existing), `UnityEngine.Audio.AudioMixer` / mixer groups (to be authored), `PlayerPrefs` (or a tiny `AudioSettingsStore` wrapper) for volume persistence, NUnit via Unity Test Runner.

---

## Context gathered (pre-revision)

| Source | Notes |
|--------|--------|
| **Jira DEV-67** | Atlassian MCP `getJiraIssue` was not callable from this environment (missing `cloudId`). **Re-validate Acceptance Criteria in Jira** before closing the ticket; map each AC bullet to a checkbox in the Validation Gate below. |
| **`docs/GAME_PLAN.md`** | Unity 6.3 LTS, URP 2D; no MonoBehaviour business logic; single `GameManager` static; ScriptableObjects for tunable data; `Assets/Audio/` tree exists in the doc’s proposed layout. |
| **`CLAUDE.md` / codebase** | `MainMenuUI` wires `MainMenuController` in `Start()`; no audio today. **Main menu Canvas, Settings button, and volume sliders exist in the scene but the Settings entry point is not wired yet** (button does nothing / panel never shown). DEV-67 implements that behavior in code + Inspector refs — still **no new layout** unless a control is literally missing from hierarchy. `GameManager` + `SceneTransitionController` own scene loads (`SceneManager.LoadSceneAsync`). No `AudioMixer` assets found under `Assets/` yet — plan assumes creating mixer + groups in the Editor. |
| **Asmdefs** | Runtime: `Axiom.Core` (refs `Axiom.Data`, `UnityEngine.UI`, `Unity.InputSystem`). Data: `Axiom.Data` (no extra refs). Editor tests: `CoreTests` references `Axiom.Core`, `Axiom.Data`, TestRunner assemblies; `DataTests` uses `testReferences` for TestRunner — **mirror these patterns** for new tests (do not invent new asmdef names). |
| **Unity manual (authoritative patterns)** | Use **one** looping `AudioSource` per logical bus (BGM, ambient) with `loop = true`, and **one** one-shot source (or `PlayOneShot`) for UI; route outputs through **mixer group** `outputAudioMixerGroup` for independent RTPC; use `AudioMixer.SetFloat` on exposed parameters (decibel scale) for volume. |

---

## File map (creates / modifies)

| Path | Role |
|------|------|
| `Assets/Scripts/Data/MenuAudioConfig.cs` | ScriptableObject: clip refs + default linear multipliers (0..1) for BGM / ambient / UI. |
| `Assets/Tests/Editor/Data/MenuAudioConfigTests.cs` | Edit Mode tests for SO validation / guards. |
| `Assets/Scripts/Core/AudioSettingsStore.cs` | Plain C#: read/write normalized music & SFX levels (wraps `PlayerPrefs` or project-standard store). |
| `Assets/Scripts/Core/AudioPlaybackService.cs` | Plain C#: single-BGM / single-ambient policy, fade/stop rules, delegates to Unity audio via injected ports (see Task 2). |
| `Assets/Scripts/Core/AudioManager.cs` | MonoBehaviour: owns `AudioSource` instances, implements ports, calls service, survives with `GameManager`. |
| `Assets/Scripts/Core/GameManager.cs` | Optional: expose `AudioPlaybackService` or `AudioManager` reference after bootstrap; ensure single instance. |
| `Assets/Scripts/Core/MainMenuUI.cs` | Serialize references to **existing** menu controls; route clicks and slider events to `AudioManager` only. |
| `Assets/Tests/Editor/Core/AudioPlaybackServiceTests.cs` | Edit Mode tests for service + store. |
| `Assets/Tests/Editor/Core/AudioTransitionStabilityTests.cs` | Edit Mode tests simulating repeated “scene ready” / policy calls (no real scene load). |
| `Assets/Tests/PlayMode/Core/MainMenuAudioSmokeTests.cs` | **Optional** Play Mode smoke: load `MainMenu`, assert single active BGM state (if test harness allows). |
| `Assets/Audio/Mixer/MainAudioMixer.mixer` | **User-authored** in Editor; exposed params e.g. `MusicVol`, `AmbientVol`, `SfxVol`. |
| `Assets/Data/Audio/MenuAudioConfig.asset` | **User-authored** asset instance. |
| `Assets/Scenes/MainMenu.unity` | **Inspector only:** assign `AudioManager`, `MenuAudioConfig` (if referenced from scene), and hook `MainMenuUI` fields to **existing** buttons/sliders/panel roots (no new UI hierarchy for this ticket). |
| `Assets/Scenes/Platformer.unity` | Only if platformer entry must stop menu loops (usually handled in code on transition — modify only if needed). |

---

## Scope boundaries

**In scope**

- [ ] Centralized API on `AudioPlaybackService` (invoked via `AudioManager`): `PlayBgm`, `StopBgm` / `FadeOutBgm`, `PlayAmbientLoop`, `StopAmbientLoop`, `PlayUiClick`, `SetMusicVolume` / `SetSfxVolume` / getters, optional `SetAmbientVolume` if ambient is a separate exposed mixer param.
- [ ] Mixer routing: BGM, ambient, and UI (SFX bus) on **separate** mixer groups.
- [ ] **Settings control must work:** the existing **Settings** button opens the settings UI and **shows** the **music** and **SFX** volume sliders (panel/root becomes visible / interactable — match whatever pattern the scene already uses: `GameObject.SetActive`, `CanvasGroup`, etc.).
- [ ] **Wire those sliders:** subscribe or bind so values live-apply through `AudioManager` / mixer and **persist** across sessions (sliders already exist on Canvas; no new slider widgets unless hierarchy is incomplete).
- [ ] Single active BGM and single active ambient; transitions replace, never stack duplicates.
- [ ] No `AudioSource.Play*` calls from `MainMenuUI` / menu controllers — only through the centralized component/service.

**Explicit non-goals**

- Battle spell SFX migration.
- Full game-wide audio refactor beyond what DEV-67 needs for menu ↔ platformer stability.

**GAME_PLAN tension (“no premature abstraction”)**

- This ticket **intentionally** adds one concrete `AudioPlaybackService` + one `AudioManager`. Do **not** add interface hierarchies unless a second implementation is already scheduled; use small `internal`/`sealed` types and test through public behavior + injected fakes only where needed.

---

## Implementation plan (execute in order)

### Task 1: Menu audio data (`MenuAudioConfig`)

**Files:**

- Create: `Assets/Scripts/Data/MenuAudioConfig.cs`
- Create: `Assets/Tests/Editor/Data/MenuAudioConfigTests.cs`
- Create (Editor, user): `Assets/Data/Audio/MenuAudioConfig.asset` (+ any new folder `*.meta` siblings)

- [ ] **Step 1: Write the failing tests** (`DataTests` namespace to match existing Data tests)

```csharp
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace DataTests
{
    public class MenuAudioConfigTests
    {
        [Test]
        public void ValidateForRuntime_ReturnsFalse_WhenBgmMissing()
        {
            var cfg = ScriptableObject.CreateInstance<MenuAudioConfig>();
            cfg.SetClipsForTests(bgm: null, ambient: AudioClip.Create("a", 1, 1, 44100, false), ui: AudioClip.Create("u", 1, 1, 44100, false));

            Assert.IsFalse(cfg.ValidateForRuntime(out string _));
        }

        [Test]
        public void ValidateForRuntime_ReturnsTrue_WhenAllClipsAssigned()
        {
            var cfg = ScriptableObject.CreateInstance<MenuAudioConfig>();
            cfg.SetClipsForTests(
                bgm: AudioClip.Create("b", 1, 1, 44100, false),
                ambient: AudioClip.Create("a", 1, 1, 44100, false),
                ui: AudioClip.Create("u", 1, 1, 44100, false));

            Assert.IsTrue(cfg.ValidateForRuntime(out string message), message);
        }
    }
}
```

- [ ] **Step 2: Run tests in Unity** — Window → General → Test Runner → Edit Mode → run `DataTests`. **Expected:** compile errors until `MenuAudioConfig` exists.

- [ ] **Step 3: Implement `MenuAudioConfig`**

```csharp
using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "MenuAudioConfig", menuName = "Axiom/Audio/Menu Audio Config")]
    public sealed class MenuAudioConfig : ScriptableObject
    {
        [SerializeField] private AudioClip _bgm;
        [SerializeField] private AudioClip _ambientLoop;
        [SerializeField] private AudioClip _uiClick;
        [Range(0f, 1f)] [SerializeField] private float _bgmLinear = 1f;
        [Range(0f, 1f)] [SerializeField] private float _ambientLinear = 1f;
        [Range(0f, 1f)] [SerializeField] private float _uiLinear = 1f;

        public AudioClip Bgm => _bgm;
        public AudioClip AmbientLoop => _ambientLoop;
        public AudioClip UiClick => _uiClick;
        public float BgmLinear => _bgmLinear;
        public float AmbientLinear => _ambientLinear;
        public float UiLinear => _uiLinear;

#if UNITY_EDITOR
        /// <summary>Edit Mode tests only — <c>Axiom.Data</c> does not define <c>UNITY_INCLUDE_TESTS</c>.</summary>
        public void SetClipsForTests(AudioClip bgm, AudioClip ambient, AudioClip ui)
        {
            _bgm = bgm;
            _ambientLoop = ambient;
            _uiClick = ui;
        }
#endif

        /// <summary>False when required clips missing — check before starting menu audio.</summary>
        public bool ValidateForRuntime(out string error)
        {
            if (_bgm == null || _ambientLoop == null || _uiClick == null)
            {
                error = "MenuAudioConfig: assign BGM, Ambient, and UI clips.";
                return false;
            }
            error = null;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run Edit Mode tests again** — **Expected:** PASS.

> **Unity Editor task (user):** Create folder `Assets/Data/Audio/` if missing. Right-click → Create → Axiom → Audio → Menu Audio Config. Assign final WAV/OGG clips for BGM, ambient loop, and UI click.

- [ ] **Check in via UVCS:**  
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-67): add MenuAudioConfig ScriptableObject and tests`  
  - `Assets/Scripts/Data/MenuAudioConfig.cs`  
  - `Assets/Scripts/Data/MenuAudioConfig.cs.meta`  
  - `Assets/Tests/Editor/Data/MenuAudioConfigTests.cs`  
  - `Assets/Tests/Editor/Data/MenuAudioConfigTests.cs.meta`  
  - `Assets/Data/Audio/MenuAudioConfig.asset`  
  - `Assets/Data/Audio/MenuAudioConfig.asset.meta`  
  - If new folders: `Assets/Data/Audio.meta` (and parent folder `.meta` if created)

---

### Task 2: Volume persistence + playback service + `AudioManager` shell

**Files:**

- Create: `Assets/Scripts/Core/AudioSettingsStore.cs`
- Create: `Assets/Scripts/Core/AudioPlaybackService.cs`
- Create: `Assets/Scripts/Core/AudioManager.cs`
- Modify: `Assets/Scripts/Core/GameManager.cs` (only if a clean hook is needed for scene-ready or reference wiring)
- Create: `Assets/Tests/Editor/Core/AudioPlaybackServiceTests.cs`

**Design constraints**

- **Guard clause order:** In each public method, validate “no work needed” conditions (e.g. missing clip, service stopped) **before** throwing on dependencies that are irrelevant to that path. Example: `PlayBgm(null)` → no-op or documented early return; do not ask for mixer handles first.
- **Mixer volume:** Expose float params on the mixer in dB; map slider 0..1 → dB with a floor (e.g. `-80` dB at zero) using `Mathf.Log10(Mathf.Max(linear, 1e-4f)) * 20f`.
- **Single instance:** `AudioPlaybackService` tracks whether BGM / ambient are logically active; replacing a clip stops/restarts the corresponding source. `AudioManager` ensures sources are created once.

- [ ] **Step 1: Write failing `AudioSettingsStore` tests** in `Assets/Tests/Editor/Core/AudioPlaybackServiceTests.cs` (same file is fine for cohesion): round-trip `SetMusicVolume` / `SetSfxVolume`, clamp inputs outside 0..1, default values when keys absent.

- [ ] **Step 2: Implement `AudioSettingsStore`** (plain C#, `PlayerPrefs` keys namespaced e.g. `axiom.audio.music`, `axiom.audio.sfx`).

- [ ] **Step 3: Write failing `AudioPlaybackService` tests** using **fakes**: record method calls on a test double implementing a small nested port interface **inside the test file** (not production `IAudio*` in `Assets/Scripts` unless unavoidable). Assert: second `PlayBgm` stops/replaces first; `PlayUiClick` does not toggle BGM state; volume setters invoke mixer set delegate with clamped dB.

- [ ] **Step 4: Implement `AudioPlaybackService`** with constructor injection of: `(MenuAudioConfig config, AudioSettingsStore settings, Action<string, float> setMixerParam, Func<AudioClip, AudioSource> getBgmSource, ...)` — adjust to keep **Unity types out of the service** if possible; if `AudioClip` is required, keep service in `Axiom.Core` (already references Unity).

- [ ] **Step 5: Implement `AudioManager`**

  - `Awake`/`Start`: resolve `MenuAudioConfig`, build `AudioPlaybackService`, apply persisted volumes to mixer.
  - Child objects or same GO: three `AudioSource` components (BGM, Ambient, UI) with `playOnAwake = false`, routed to mixer groups via Inspector.
  - Subscribe to `GameManager.Instance.OnSceneReady` **once** (defensive unsubscribe) to call a service method like `OnSceneBecameActive(string sceneName)` that starts/stops menu loops per scene policy (e.g. menu BGM only in `MainMenu`, ambient optional per design).

- [ ] **Step 6: Run Edit Mode tests** — **Expected:** PASS.

> **Unity Editor task (user):** Create `Assets/Audio/Mixer/MainAudioMixer.mixer` with groups **Music**, **Ambient**, **SFX** (names illustrative). Expose attenuation parameters. Assign group outputs. On the `GameManager` prefab (or documented persistent object), add `AudioManager`, assign sources, mixer, mixer group refs, and `MenuAudioConfig` asset.

- [ ] **Check in via UVCS:**  
  Message: `feat(DEV-67): add AudioManager, playback service, and volume persistence`  
  - `Assets/Scripts/Core/AudioSettingsStore.cs` + `.meta`  
  - `Assets/Scripts/Core/AudioPlaybackService.cs` + `.meta`  
  - `Assets/Scripts/Core/AudioManager.cs` + `.meta`  
  - `Assets/Tests/Editor/Core/AudioPlaybackServiceTests.cs` + `.meta`  
  - `Assets/Scripts/Core/GameManager.cs` + `.meta` (if modified)  
  - `Assets/Audio/Mixer/MainAudioMixer.mixer` + `.meta`  
  - `Assets/Prefabs/` path to modified GameManager prefab (if prefab lives there) + `.meta`  
  - `Assets/Scenes/MainMenu.unity` + `.meta` **only if** scene references were saved in this task

---

### Task 3: **Working** Settings entry + hook **existing** sliders to audio (clicks + volume)

**Premise:** Main menu hierarchy **already includes** a Settings button, a settings panel (or root), and music / SFX **Slider**s. Today that Settings path **does not work** (nothing opens / sliders never shown). DEV-67 **implements** open/close behavior and audio wiring in `MainMenuUI` — still **no new Canvas layout** unless a referenced object is missing from the scene (then fix hierarchy in Editor, not new art).

**Acceptance for this task (product-level):**

- Pressing **Settings** shows the settings UI such that the player can **see** both volume sliders.
- **Close** / **Back** (whatever exists on the panel) hides the settings UI again.
- Slider drags update audio live; values persist after restart (covered again in Validation Gate).

**Files:**

- Modify: `Assets/Scripts/Core/MainMenuUI.cs`
- Modify: `Assets/Scenes/MainMenu.unity` (Inspector references — Settings `Button`, panel root, Close `Button`, both sliders, `AudioManager`)
- Create (optional): `Assets/Tests/PlayMode/Core/MainMenuAudioSmokeTests.cs` (no `asmdef` change — `CorePlayModeTests` already references `Axiom.Core`)

- [ ] **Step 1:** Add `[SerializeField]` fields on `MainMenuUI`: `AudioManager _audio`; **Settings** open `Button _settingsButton`; **settings panel root** `GameObject _settingsPanel` (or `RectTransform` root — same idea: one object you activate/deactivate); **Close** `Button _settingsCloseButton` (nullable if the design uses overlay click-outside only); **music** `Slider _musicVolumeSlider`; **sfx** `Slider _sfxVolumeSlider`. Keep existing New Game / Continue / Quit refs as they are.

- [ ] **Step 2:** In `Start()`, wire **Settings** → `ShowSettingsPanel()` (e.g. `_settingsPanel.SetActive(true)` or set `CanvasGroup.alpha` / `blocksRaycasts` / `interactable` to match how the scene is built). Wire **Close** → `HideSettingsPanel()`. Ensure the panel starts **hidden** on boot if that matches design (align with scene default; avoid double-flicker).

- [ ] **Step 3:** When the panel is shown, **sync sliders from persistence**: set `_musicVolumeSlider` / `_sfxVolumeSlider` `.value` from `AudioSettingsStore` or `AudioManager` getters **without** firing unwanted side effects (use temporary listener removal or `SetValueWithoutNotify` on `Slider` if available in your Unity version) so opening the panel does not stomp saved prefs with defaults.

- [ ] **Step 4:** Subscribe to `Slider.onValueChanged` for both sliders; handlers call `_audio.SetMusicVolume` / `SetSfxVolume` only (normalized 0..1).

- [ ] **Step 5:** For each main menu button that should audibly click (including **Settings** and **Close** when applicable), invoke `_audio.PlayUiClick()` on click, **only when** `Button.interactable` is true (especially **Continue** when disabled — no click SFX).

- [ ] **Step 6:** Keep `MainMenuController` free of Unity audio APIs (default: **no** `MainMenuController` changes).

- [ ] **Step 7:** Grep verification — no `AudioSource.Play` / `PlayOneShot` in `MainMenuUI.cs`.

> **Unity Editor task (user):** On `MainMenuUI` in `MainMenu.unity`, assign: **Settings** button, **settings panel root** (the object that must become visible so sliders are on-screen), **Close** button if present, **music** and **SFX** sliders, and `AudioManager`. Fix any hierarchy issues only if a referenced object is missing (e.g. slider under wrong inactive parent so it never appears).

- [ ] **Check in via UVCS:**  
  Message: `refactor(DEV-67): route main menu UI audio through AudioManager`  
  - `Assets/Scripts/Core/MainMenuUI.cs` + `.meta`  
  - `Assets/Scenes/MainMenu.unity` + `.meta`  
  - Play Mode test files + `.meta` if added

---

### Task 4: Scene transition stability + balance

**Files:**

- Modify: `Assets/Scripts/Core/AudioPlaybackService.cs` / `AudioManager.cs` (as needed)
- Create: `Assets/Tests/Editor/Core/AudioTransitionStabilityTests.cs`
- Possibly modify: `Assets/Scenes/Platformer.unity`

- [ ] **Step 1:** Encode explicit policy in the service, e.g. `OnSceneBecameActive(string name)`: when leaving `MainMenu`, fade or stop menu BGM/ambient; when entering `MainMenu`, start from config if not already playing that logical track (idempotent).

- [ ] **Step 2:** `AudioTransitionStabilityTests` — rapid repeated calls to the policy method must not increment internal “active layer” counters; assert call counts on fakes for `Play` vs `Stop`.

- [ ] **Step 3:** Manual + ears: BGM / ambient / voice (future) balance — adjust default linear multipliers on `MenuAudioConfig` and mixer baseline.

> **Unity Editor task (user):** Play Mode loop `MainMenu` ↔ `Platformer` (≥ 10 cycles) — confirm no stacking. Adjust mixer / source distances.

- [ ] **Check in via UVCS:**  
  Message: `fix(DEV-67): stabilize menu audio across scene transitions`  
  - All modified scripts + `.meta`  
  - `Assets/Tests/Editor/Core/AudioTransitionStabilityTests.cs` + `.meta`  
  - `Assets/Scenes/Platformer.unity` + `.meta` (if modified)  
  - `Assets/Scenes/MainMenu.unity` + `.meta` (if modified)

---

## Assembly definition notes

- **No new runtime asmdef** if all scripts remain under `Assets/Scripts/Core` and `Assets/Scripts/Data` with existing `Axiom.Core` / `Axiom.Data`.
- **No new test asmdef** if tests land in `Assets/Tests/Editor/Core` (`CoreTests`) and `Assets/Tests/Editor/Data` (`DataTests`).
- If you split files into a new folder without asmdef coverage, add `Axiom.<Module>.asmdef` with `autoReferenced: true` and mirror an Editor test asmdef — see `Assets/Scripts/Core/Axiom.Core.asmdef` and `Assets/Tests/Editor/Core/CoreTests.asmdef`.

---

## Validation gate (before marking DEV-67 complete)

- [ ] Re-read Jira DEV-67 Acceptance Criteria; every bullet traced to a task or validation item.
- [ ] Unity Test Runner → Edit Mode: `CoreTests`, `DataTests` — all green including new fixtures.
- [ ] Play Mode manual: **Settings** opens the panel and **both** volume sliders are **visible**; **Close** (or equivalent) returns to the main menu state; button clicks audible; `Continue` disabled produces **no** click sound if that is AC; sliders live-update music vs SFX independently.
- [ ] Restart build / editor: persisted volumes reload and apply.
- [ ] `MainMenu` ↔ `Platformer`: no duplicate BGM/ambient layers.
- [ ] Repo grep: `MainMenuUI` contains no direct `AudioSource.Play` / `PlayOneShot`.
- [ ] UVCS: every touched asset path appears in a check-in step with its `.meta` (and new folder `.meta` where applicable).

---

## Plan self-review (post-write checklist)

| Check | Status |
|-------|--------|
| Spec / AC coverage | Map Jira AC manually (MCP unavailable here). |
| Placeholder scan | No “TBD”; code blocks are concrete starters. |
| Type / signature alignment | Test method names ↔ production API must match at implementation time. |
| C# guard ordering | Documented in Task 2; apply in every public method. |
| Branch test coverage | Null clips, empty persistence, rapid transitions, slider clamp — covered in Tasks 1–4. |
| UVCS file audit | Each task lists `.cs`, `.meta`, scenes, mixer, prefab, folder `.meta` as applicable. |
| Unity Editor isolation | Editor tasks are standalone callouts, not mixed into code checkboxes. |

---

## Rollout order

1. Task 1 — `MenuAudioConfig` + tests  
2. Task 2 — persistence + service + `AudioManager` + mixer wiring  
3. Task 3 — `MainMenuUI`: **working Settings** (show sliders) + audio hooks  
4. Task 4 — transition idempotency + balance + stability tests  

---

**Plan complete and saved to `docs/superpowers/plans/2026-04-17-dev-67-main-menu-audio-polish-plan.md`. Two execution options:**

1. **Subagent-driven (recommended)** — Fresh subagent per task, review between tasks.  
2. **Inline execution** — Same session, `superpowers:executing-plans` with checkpoints.

Which approach do you want for implementation?
