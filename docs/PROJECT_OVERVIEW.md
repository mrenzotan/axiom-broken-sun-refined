# Project Overview — Axiom of the Broken Sun

Welcome to the project. This document is your entry point. Read it first, then use the reference table at the bottom to find the right document for whatever you're working on.

---

## What We're Building

**Axiom of the Broken Sun** is a 2D pixel-art platformer RPG for PC (Windows/macOS). The twist: players cast spells in turn-based combat by **speaking the spell name aloud into their microphone**. The spell system is built on real high-school chemistry concepts — combustion, phase change, acid-base reactions, and precipitation.

**Target audience:** Players aged 12 and above, with a focus on those studying or curious about chemistry. The game serves as both a strategic RPG and a supplementary learning tool.

**Protagonist:** Kaelen, a former apothecary's apprentice whose left arm crystallized into a "Catalyst Arm" during a world-shattering event. He can only trigger reactions by speaking them aloud.

Full narrative and world-building: [`docs/LORE_AND_MECHANICS.md`](LORE_AND_MECHANICS.md)
Full design spec and UI/UX: [`docs/GAME_DESIGN_DOCUMENT.md`](GAME_DESIGN_DOCUMENT.md)

---

## Current Development State

The project is mid-development. Here is what is fully implemented vs. what is next.

### Implemented

| Area | Status | Key Scripts |
| ---- | ------ | ----------- |
| Platformer (Phase 1) | Complete | `PlayerController`, `PlayerMovement`, `PlayerAnimator`, `ParallaxController` |
| Combat System (Phase 2) | Complete | `BattleManager`, `BattleController`, `PlayerActionHandler`, `EnemyActionHandler`, `SpellEffectResolver` |
| Combat UI | Complete | `BattleHUD`, `ActionMenuUI`, `HealthBarUI`, `TurnIndicatorUI`, `FloatingNumberSpawner`, `ConditionBadgeUI`, `StatusMessageUI`, `SpellInputUI` |
| Chemistry Conditions | Complete | `CharacterStats` carries active conditions; `SpellEffectResolver` applies them; `ConditionBadgeUI` displays them |
| Spell VFX/SFX | Complete | `SpellVFXController` — plays a particle clip and audio clip assigned per `SpellData` asset |
| Voice Recognition (Phase 3) | Complete | `VoskRecognizerService`, `MicrophoneInputHandler`, `SpellCastController`, `SpellVocabularyManager`, `BattleVoiceBootstrap` |
| ScriptableObject Data | Partial | `SpellData` (3 spells: Combust, Freeze, Neutralize), `EnemyData` (1 test enemy: MeltspawnTest) |

### Not Yet Started

| Phase | What It Is |
| ----- | ---------- |
| Phase 4 — Scene Bridge | `GameManager` (DontDestroyOnLoad), battle trigger in platformer, scene transitions, world state restore |
| Phase 5 — Data & Progression | Save/load system, XP/leveling, full spell and enemy roster as ScriptableObject assets |
| Phase 6 — World & Content | Full level set, narrative, all enemy/spell/boss content |
| Phase 7 — Polish | Audio mix, VFX polish, accessibility, profiling, release builds |

> Phase 4 is next. `BattleController` is already prepared for it — `Initialize()` is the hook that `GameManager` will call on scene load.

---

## Tech Stack

| Technology | Role |
| ---------- | ---- |
| Unity 6.0.4 LTS | Engine |
| URP 2D | Render pipeline (forward rendering) |
| C# / Mono | Scripting backend |
| Vosk (`vosk-model-en-us-0.22-lgraph`) | Offline speech recognition — located in `StreamingAssets/VoskModels/` |
| New Input System | Player input — actions defined in `Assets/InputSystem_Actions.inputactions` |
| Cinemachine | Camera system |
| Unity UI + TextMeshPro | All in-game UI |
| 2D Sprite Animation + Animator | Character and enemy animations |
| Unity 2D Tilemap + Rule Tiles | Level geometry |
| UVCS (Unity Version Control) | **Primary** version control — source of truth for all files |
| Git → GitHub | Secondary, scripts/docs mirror only — for GitHub activity tracking |

---

## Assets Directory Structure

```
Assets/
├── Animations/
│   ├── Enemies/
│   │   └── Ice Slime/              # Animator controller + animation clips
│   ├── Player/                     # Animator controllers + animation clips
│   └── VFX/                        # Spell VFX animators and sprite animation clips
├── Art/
│   ├── Backgrounds/                # Parallax layer PNGs (Far, Mid, Near)
│   ├── Sprites/
│   │   ├── Enemies/Ice Slime/      # Enemy sprite sheet
│   │   ├── Player/                 # Kaelen sprite sheet
│   │   └── VFX/                    # Spell VFX sprite sheets + VFXAtlas
│   └── Tilemaps/                   # Rule tile assets + tilemap textures
├── Audio/
│   └── SFX/
│       └── Spells/                 # Spell sound effects (.ogg) — one set per spell
├── Data/                           # ScriptableObject asset files — never hardcode values in code
│   ├── Enemies/                    # ED_*.asset  (e.g. ED_MeltspawnTest.asset)
│   └── Spells/                     # SD_*.asset  (e.g. SD_Combust.asset)
├── Editor/                         # Editor-only tools (stripped from builds)
│   ├── LevelBuilderTool.cs
│   └── SceneSetup/
│       └── SceneSetupTool.cs
├── InputSystem_Actions.inputactions    # New Input System action map
├── Prefabs/
│   ├── Enemies/                    # Enemy prefabs (battle scene variants)
│   ├── Player/                     # Player prefabs (battle scene variant)
│   ├── UI/                         # Shared UI prefabs (FloatingNumber, ConditionBadge)
│   └── Voice/                      # MicrophoneInputHandler prefab
├── Scenes/
│   ├── Battle.unity                # Turn-based combat scene
│   ├── Platformer.unity            # Side-scrolling exploration scene
│   └── SampleScene.unity           # Unused placeholder
├── Scripts/                        # All runtime C# — see breakdown below
├── Tests/
│   └── Editor/                     # Edit-mode unit tests (Unity Test Framework)
│       ├── Battle/                 # BattleManager, SpellEffectResolver, CharacterStats, etc.
│       ├── SceneSetup/             # SceneSetupTool tests
│       ├── UI/                     # UI logic tests (AttackResult, StatusMessageQueue)
│       └── Voice/                  # Vosk, SpellMatcher, SpellVocabularyManager tests
└── ThirdParty/
    └── Vosk/                       # Vosk SDK — Vosk.dll + platform plugins (OSX/Windows)
```

### Scripts Breakdown

```
Assets/Scripts/
├── Battle/
│   ├── AttackResult.cs
│   ├── BattleAnimationService.cs
│   ├── BattleController.cs         # MonoBehaviour wrapper — Unity lifecycle only
│   ├── BattleManager.cs            # Pure C# state machine (no Unity calls)
│   ├── BattleState.cs
│   ├── CharacterStats.cs           # HP, MP, conditions, combat math
│   ├── CombatStartState.cs
│   ├── ConditionTurnResult.cs
│   ├── EnemyActionHandler.cs
│   ├── EnemyBattleAnimator.cs
│   ├── PlayerActionHandler.cs
│   ├── PlayerBattleAnimator.cs
│   ├── SpellEffectResolver.cs
│   ├── SpellResult.cs
│   ├── SpellVFXController.cs       # Particle + audio playback on spell cast
│   ├── StatusConditionEntry.cs
│   └── UI/                         # All battle scene UI components
│       ├── ActionMenuUI.cs
│       ├── BattleHUD.cs
│       ├── ConditionBadgeUI.cs
│       ├── FloatingNumberInstance.cs
│       ├── FloatingNumberSpawner.cs
│       ├── HealthBarUI.cs
│       ├── SpellInputUI.cs
│       ├── SpellInputUILogic.cs
│       ├── StatusMessageQueue.cs
│       ├── StatusMessageUI.cs
│       └── TurnIndicatorUI.cs
├── Core/                           # (Phase 4) GameManager singleton — not yet implemented
├── Data/                           # ScriptableObject definitions
│   ├── ChemicalCondition.cs        # Enum: Burning, Frozen, Corroding, etc.
│   ├── EnemyData.cs                # Enemy stats and innate conditions
│   ├── SpellData.cs                # Spell name, damage, MP cost, effects, VFX/SFX clips
│   └── SpellEffectType.cs          # Enum: Damage, Heal, Shield, etc.
├── Platformer/
│   ├── InputSystem_Actions.cs      # Auto-generated Input System wrapper
│   ├── ParallaxBackground.cs
│   ├── ParallaxController.cs
│   ├── PlayerAnimator.cs
│   ├── PlayerController.cs
│   └── PlayerMovement.cs
└── Voice/
    ├── BattleVoiceBootstrap.cs     # Wires the pipeline at scene start
    ├── MicrophoneCapture.cs
    ├── MicrophoneInputHandler.cs   # Main thread mic capture → PCM16 queue
    ├── SpellCastController.cs      # Polls result queue in Update()
    ├── SpellResultMatcher.cs       # Matches Vosk JSON output to SpellData
    ├── SpellVocabularyManager.cs   # Builds Vosk grammar from unlocked spells
    └── VoskRecognizerService.cs    # Background thread — runs AcceptWaveform()
```

**Rule:** UI scripts live inside their scene's subfolder (`Battle/UI/`), not a shared top-level `UI/` folder.

---

## Data Assets

All tunable game data lives in `.asset` files under `Assets/Data/` — never hardcoded in scripts.

```
Assets/Data/
├── Spells/
│   ├── SD_Combust.asset
│   ├── SD_Freeze.asset
│   └── SD_Neutralize.asset
└── Enemies/
    └── ED_MeltspawnTest.asset
```

To add a new spell or enemy: create a new ScriptableObject asset in the appropriate folder. No code changes needed.

---

## Architecture Rules (Non-Negotiable)

These constraints exist to prevent the spaghetti-code problems from the previous rewrite. Don't deviate.

1. **MonoBehaviours handle Unity lifecycle only** (`Start`, `Update`, `OnDestroy`). All logic lives in plain C# classes injected into them.
2. **No static singletons except `GameManager`** — the only cross-scene state keeper. Everything else is passed explicitly or via C# events.
3. **ScriptableObject-driven data** — no hardcoded spell names, enemy stats, or values in code.
4. **No premature abstraction** — don't create base classes or interfaces for systems with only one implementation yet.
5. **Dead code policy** — delete cut features. Never comment them out. UVCS preserves history.

---

## Voice Recognition Architecture

The Vosk pipeline follows a strict producer/consumer threaded pattern:

```
Main thread (MicrophoneInputHandler)
  → captures mic audio on push-to-talk
  → converts float[] to short[] (PCM16)
  → enqueues into ConcurrentQueue<short[]>

Background thread (VoskRecognizerService)
  → runs AcceptWaveform() — never on main thread

Back to main thread (SpellCastController.Update())
  → dequeues result strings
  → SpellResultMatcher matches to SpellData
  → calls BattleController.OnSpellCast(spell)
```

Grammar is restricted to the player's currently unlocked spells for better recognition accuracy.

---

## Version Control Workflow

**UVCS is the source of truth.** Git/GitHub is a write-only mirror for GitHub activity.

| Action | Tool |
| ------ | ---- |
| All collaboration, scene/art/code sync | UVCS |
| Pushing code and docs for GitHub activity | Git → GitHub |
| Never pull from GitHub | — |

**Check-in / commit message format:**

```
<type>(DEV-###): <short description>
```

Types: `feat`, `fix`, `chore`, `docs`, `refactor`, `test`

Full workflow and setup instructions (including first-time Git setup): [`docs/VERSION_CONTROL.md`](VERSION_CONTROL.md)

---

## AI Tool Usage Guide

We use AI coding tools (Claude Code, etc.) for development. To get accurate, project-aware answers, provide the right context documents depending on your task.

### Always include as base context
- `CLAUDE.md` — architecture rules, folder structure, non-negotiables. AI tools are configured to read this automatically in this repo.

### Attach these only when your task needs them

| Task | Attach this document |
| ---- | -------------------- |
| Implementing or modifying spells, combat conditions, or enemy interactions | `docs/game-mechanics/chemistry-spell-combat-system.md` |
| Designing new gameplay systems, spells, or enemy behaviors (lore/concept work) | `docs/LORE_AND_MECHANICS.md` |
| Writing Jira tickets or planning a new phase/feature | `docs/GAME_DESIGN_DOCUMENT.md` |
| Version control questions or setup | `docs/VERSION_CONTROL.md` |

**Why not attach everything?** These are large documents. Attaching irrelevant context makes AI responses less accurate and burns context window. Use the table above to attach only what the task requires.

### Jira integration

Tickets use auto-assigned `DEV-###` IDs. Always include the ticket ID in your commit/check-in message. Labels by phase: `phase-1-platformer`, `phase-2-combat`, `phase-3-voice`, `phase-4-bridge`, etc.
