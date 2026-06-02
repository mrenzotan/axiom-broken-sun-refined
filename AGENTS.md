# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## 12-rule template

These rules apply to every task in this project unless explicitly overridden.
Bias: caution over speed on non-trivial work. Use judgment on trivial tasks.

### Rule 1 — Think Before Coding

State assumptions explicitly. If uncertain, ask rather than guess.
Present multiple interpretations when ambiguity exists.
Push back when a simpler approach exists.
Stop when confused. Name what's unclear.

### Rule 2 — Simplicity First

Minimum code that solves the problem. Nothing speculative.
No features beyond what was asked. No abstractions for single-use code.
Test: would a senior engineer say this is overcomplicated? If yes, simplify.

### Rule 3 — Surgical Changes

Touch only what you must. Clean up only your own mess.
Don't "improve" adjacent code, comments, or formatting.
Don't refactor what isn't broken. Match existing style.

### Rule 4 — Goal-Driven Execution

Define success criteria. Loop until verified.
Don't follow steps. Define success and iterate.
Strong success criteria let you loop independently.

### Rule 5 — Use the model only for judgment calls

Use me for: classification, drafting, summarization, extraction.
Do NOT use me for: routing, retries, deterministic transforms.
If code can answer, code answers.

### Rule 6 — Token budgets are not advisory

Per-task: 4,000 tokens. Per-session: 30,000 tokens.
If approaching budget, summarize and start fresh.
Surface the breach. Do not silently overrun.

### Rule 7 — Surface conflicts, don't average them

If two patterns contradict, pick one (more recent / more tested).
Explain why. Flag the other for cleanup.
Don't blend conflicting patterns.

### Rule 8 — Read before you write

Before adding code, read exports, immediate callers, shared utilities.
"Looks orthogonal" is dangerous. If unsure why code is structured a way, ask.

### Rule 9 — Tests verify intent, not just behavior

Tests must encode WHY behavior matters, not just WHAT it does.
A test that can't fail when business logic changes is wrong.

### Rule 10 — Checkpoint after every significant step

Summarize what was done, what's verified, what's left.
Don't continue from a state you can't describe back.
If you lose track, stop and restate.

### Rule 11 — Match the codebase's conventions, even if you disagree

Conformance > taste inside the codebase.
If you genuinely think a convention is harmful, surface it. Don't fork silently.

### Rule 12 — Fail loud

"Completed" is wrong if anything was skipped silently.
"Tests pass" is wrong if any were skipped.
Default to surfacing uncertainty, not hiding it.

## Project Overview

**Axiom of the Broken Sun** is a 2D side-scrolling platformer + turn-based RPG where players cast spells by speaking spell names aloud into their microphone. Built in Unity 6 LTS with URP 2D. The master reference for all design decisions is `docs/GAME_PLAN.md`.

## Project Context

Full game design doc and lore reference: `docs/GAME_PLAN.md`

### Contextual Reference Documents

Only attach these when the task specifically requires them — they're large and not needed for most coding tasks:

| Document | When to use |
| -------- | ----------- |
| `docs/game-mechanics/chemistry-spell-combat-system.md` | Implementing or modifying any spell, enemy, or combat interaction that involves the chemistry condition system — authoritative field reference, resolver order, invariants, and examples |
| `docs/LORE_AND_MECHANICS.md` | Designing new gameplay systems or spells (lore justification, chemistry concepts, enemy behaviors) |
| `docs/GAME_DESIGN_DOCUMENT.md` | Writing Jira tickets or planning development phases (feature scope, asset lists, UI/UX specs) |

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

### Scripts Folder Structure (follow this when creating new scripts)

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
| Cross-scene state  | `GameManager`            | MonoBehaviour (DontDestroyOnLoad) — **Phase 4, not yet implemented** |
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
- **Version Control:** UVCS (Unity Version Control) — primary, tracks all files including binary assets and scenes · Git (scripts-only mirror → GitHub) — secondary, tracks `Assets/Scripts/`, docs, and config only; no Git LFS; branches: `main` (stable) and `dev` (integration); see `docs/VERSION_CONTROL.md`

## Development Phases

See `docs/GAME_PLAN.md` Section 5 for full exit criteria per phase:

1. ~~**Platformer Foundation**~~ ✓ — player controller, tilemap, Cinemachine, animations
2. ~~**Combat System**~~ ✓ — BattleManager state machine, turn-based UI, enemy AI (no voice yet)
3. ~~**Voice Spell System**~~ ✓ — Vosk threaded integration, SpellCastController, push-to-talk
4. **Scene Bridge** ← current — GameManager, battle triggers, scene transitions, world state restore
5. **Data Layer & Progression** — ScriptableObjects, save/load, XP/level system
6. **World & Content** — levels, full enemy/spell/item rosters, narrative (start only after Phases 1–4 complete)
7. **Polish & Release** — audio, visual juice, accessibility, profiling, builds

## Jira Integration

Project uses Jira Free with auto-assigned `DEV-#` ticket IDs. Labels organize by phase:
`phase-1-platformer`, `phase-2-combat`, `phase-3-voice`, `phase-4-bridge`, `phase-5-data`, `phase-6-world`, `phase-7-polish`, `unity`, `vosk`, `architecture`, `bug`, `content`

When generating Jira tickets: each feature area bullet in `docs/GAME_PLAN.md` → one Story; sub-steps → Subtasks; bugs → separate Bug tickets.

## Commit / Check-in Message Format

All UVCS check-ins and git commits must follow this format:

```
<type>(DEV-##): <short description>
```

Types: `feat`, `fix`, `chore`, `docs`, `refactor`, `test`. See `docs/VERSION_CONTROL.md` for the full reference and examples.
