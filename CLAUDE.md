# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Axiom of the Broken Sun** is a 2D side-scrolling platformer + turn-based RPG where players cast spells by speaking spell names aloud into their microphone. Built in Unity 6 LTS with URP 2D. The master reference for all design decisions is `docs/GAME_PLAN.md`.

## Project Context

Full game design doc and lore reference: `docs/GAME_PLAN.md`

## Unity & Build

This is a Unity project — there are no CLI build or test commands. All development happens through the Unity Editor and VS Code.

- **Scripting Backend:** Mono (default for development)
- **Target Platform:** Windows & macOS (no mobile, no WebGL)
- **Unity Version:** 6.0.4 LTS
- **IDE:** VS Code with C# Dev Kit + Unity extension (attach debugger via `launch.json`)
- **Solution file:** `Axiom of the Broken Sun Refined.slnx`
- **Run tests:** Unity Test Framework via Unity Editor → Window → General → Test Runner
- **Enter Play Mode Options** is enabled in Project Settings for faster iteration

## Architecture

### Scenes

- `Assets/Scenes/Battle.unity` — turn-based combat scene
- `Assets/Scenes/Platformer.unity` — side-scrolling platformer/exploration scene
- `Assets/Scenes/SampleScene.unity` — unused placeholder
- Planned: `MainMenu`, `World_01..N` (platformer), `Battle` (turn-based combat) as separate scenes

### Scripts Folder Structure (planned, follow this when creating scripts)

```
Assets/Scripts/
├── Battle/       # BattleManager, combat state machine, enemy AI, BattleController
│   └── UI/       # All battle scene UI — HealthBarUI, ActionMenuUI, BattleHUD, etc.
├── Core/         # GameManager singleton, scene transitions
├── Data/         # ScriptableObject definitions (SpellData, EnemyData, etc.)
├── Platformer/   # Player controller, camera, tilemap interactions
│   └── UI/       # Platformer HUD (HP display, etc.) — added as needed
└── Voice/        # VoskRecognizerService, MicrophoneInputHandler, SpellCastController
```

UI scripts live inside their owning scene's subfolder (`Battle/UI/`, `Platformer/UI/`), not in a shared top-level `UI/` folder. There is no `Assets/Scripts/UI/`.

### Non-Negotiable Code Standards

These are deliberate architectural constraints from a prior spaghetti-code rewrite — do not deviate:

1. **MonoBehaviours handle Unity lifecycle only** (`Start`, `Update`, `OnDestroy`). All logic lives in plain C# classes injected into them.
2. **No static singletons except `GameManager`** — the only cross-scene state keeper. Everything else is passed explicitly or via events/ScriptableObject channels.
3. **ScriptableObject-driven data** — no hardcoded spell names, enemy stats, or item values in code. All tunable data lives in `.asset` files under `Assets/Data/`.
4. **No premature abstraction** — don't create base classes, interfaces, or managers for systems with only one implementation yet.
5. **Dead code policy** — delete deferred/cut features. Never comment them out. UVCS preserves history.

### Voice Recognition Architecture (Phase 3)

Vosk speech recognition runs on a **producer/consumer threaded pattern** — the only accepted implementation:

- **Main thread:** `MicrophoneInputHandler` (MonoBehaviour) captures mic audio on push-to-talk, converts float samples to `short[]` (PCM16), enqueues into `ConcurrentQueue<short[]>`
- **Background thread:** `VoskRecognizerService` (plain C#, not MonoBehaviour) runs `AcceptWaveform()` exclusively here — never on main thread
- **Back to main thread:** Results enqueued into `ConcurrentQueue<string>`, dequeued in `Update()` by `SpellCastController`
- Vosk model: `vosk-model-en-us-0.22-lgraph` (~50MB) in `StreamingAssets/VoskModels/` — use the lgraph model, not the full model (full model causes frame drops)
- Grammar restricted to player's currently unlocked spells only (improves accuracy)

### Key Systems

| System             | Class                    | Type                                  |
| ------------------ | ------------------------ | ------------------------------------- |
| Cross-scene state  | `GameManager`            | MonoBehaviour (DontDestroyOnLoad)     |
| Turn-based combat  | `BattleManager`          | Plain C# (state machine)              |
| Speech recognition | `VoskRecognizerService`  | Plain C# (threaded service)           |
| Mic capture        | `MicrophoneInputHandler` | MonoBehaviour (capture only)          |
| Spell dispatch     | `SpellCastController`    | MonoBehaviour (polls queue in Update) |
| Spell vocabulary   | `SpellVocabularyManager` | ScriptableObject-driven service       |

## Tech Stack

- **Render Pipeline:** URP 2D (forward rendering)
- **Input:** New Input System (actions defined in `Assets/InputSystem_Actions.inputactions`)
- **Camera:** Cinemachine
- **UI:** Unity UI Canvas + TextMeshPro
- **Serialization:** `System.IO` JSON (save/load)
- **Animation:** 2D Sprite Animation + Animator
- **Tilemaps:** Unity 2D Tilemap + Rule Tiles
- **Version Control:** UVCS (Unity Version Control) — primary, tracks all files including binary assets and scenes · Git (scripts-only mirror → GitHub) — secondary, tracks `Assets/Scripts/`, docs, and config only; no Git LFS; see `docs/VERSION_CONTROL.md`

## Development Phases

See `docs/GAME_PLAN.md` Section 5 for full exit criteria per phase:

1. **Platformer Foundation** — player controller, tilemap, Cinemachine, animations
2. **Combat System** — BattleManager state machine, turn-based UI, enemy AI (no voice yet)
3. **Voice Spell System** — Vosk threaded integration, SpellCastController, push-to-talk
4. **Scene Bridge** — GameManager, battle triggers, scene transitions, world state restore
5. **Data Layer & Progression** — ScriptableObjects, save/load, XP/level system
6. **World & Content** — levels, full enemy/spell/item rosters, narrative (start only after Phases 1–4 complete)
7. **Polish & Release** — audio, visual juice, accessibility, profiling, builds

## Jira Integration

Project uses Jira Free with auto-assigned `DEV-#` ticket IDs. Labels organize by phase:
`phase-1-platformer`, `phase-2-combat`, `phase-3-voice`, `phase-4-bridge`, `phase-5-data`, `phase-6-world`, `phase-7-polish`, `unity`, `vosk`, `architecture`, `bug`, `content`

When generating Jira tickets: each feature area bullet in `docs/GAME_PLAN.md` → one Story; sub-steps → Subtasks; bugs → separate Bug tickets.
