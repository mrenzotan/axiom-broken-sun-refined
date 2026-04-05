# GAME_PLAN.md — Axiom of the Broken Sun

> This document serves as the master reference for the game's context, tech stack, and phased
> build order. Use it as context when generating Jira epics, user stories, and tasks via
> Claude Code + Atlassian MCP.

---

## 1. Game Overview

### Title

**Axiom of the Broken Sun**

### Genre

2D Side-Scrolling Platformer + Turn-Based RPG Combat

### Platform

PC (Windows & macOS) — offline, single-player

### Elevator Pitch

A dark fantasy RPG where a young apothecary's apprentice uses a chemically-transformed arm
to cast powerful elemental spells — activated entirely by the player's own voice.

---

## 2. Lore & Setting

### The World

The world has fallen into chaos following a cataclysmic event known as the **Cognition
Cascade** — a disaster that disrupted the very fabric of matter and its chemical composition.
In the aftermath, reality itself became unstable. The Sun appeared fractured and broken in
the sky, a permanent scar marking the world's wound.

### The Antagonist — Null-King

The Cognition Cascade was not an accident. It was deliberately triggered by the
**Null-King**, a malevolent force who sought to unmake the world entirely by severing the
bonds between all matter.

### The Protagonist

A young boy who worked as an **apothecary's apprentice** miraculously survived the
Cognition Cascade. The event permanently transformed his left arm into a **Catalyst Arm**
— a living conduit capable of absorbing raw elemental matter and catalyzing it into powerful
**Chemical Spells**.

Armed with his chemistry knowledge and this new ability, he sets out to confront the
Null-King and restore the world.

### Spell System — Catalyst Arm

The protagonist does not cast spells through button inputs. Instead, the player
**speaks spell names aloud** into their microphone. The Catalyst Arm responds to the
player's voice, recognizing spoken chemical spell names and executing them in battle.

Spells are rooted in chemistry and elemental logic (e.g., combining absorbed elements to
form compounds). The player's spell vocabulary grows as the story progresses and as the
protagonist levels up — reflecting his growing mastery as an apothecary and chemist.

**Spell acquisition methods:**

- Leveling up (mastery of new chemical reactions)
- Story-driven moments (key narrative beats unlock signature spells)

**Spell vocabulary size:** Medium set (30–100 spells)

---

## 3. Tech Stack

| Layer                  | Technology                                                                                        |
| ---------------------- | ------------------------------------------------------------------------------------------------- |
| **Engine**             | Unity 6.3 LTS                                                                                     |
| **Render Pipeline**    | Universal Render Pipeline (URP) — 2D template                                                     |
| **Language**           | C#                                                                                                |
| **IDE**                | Visual Studio Code + C# Dev Kit + Unity extension                                                 |
| **Version Control**    | Git (local) — optionally Unity Version Control (UVCS) for team scale                              |
| **Speech Recognition** | Vosk (offline, open-source) — direct integration via C# bindings (`vosk-model-en-us-0.22-lgraph`) |
| **Camera**             | Cinemachine (Unity Package Manager)                                                               |
| **Tilemaps**           | Unity 2D Tilemap + Tilemap Collider 2D                                                            |
| **UI**                 | Unity UI (Canvas) + TextMeshPro                                                                   |
| **Data Architecture**  | ScriptableObjects for spells, enemies, items, characters                                          |
| **Scene Management**   | Unity SceneManager + persistent GameManager singleton                                             |
| **Save System**        | JSON serialization via `System.IO`                                                                |
| **Project Tracking**   | Jira (via Atlassian MCP + Claude Code)                                                            |

### Speech Recognition Details — Vosk (Direct Integration)

- **Model:** `vosk-model-en-us-0.22-lgraph` — lightweight graph model, chosen over
  `vosk-model-en-us-0.22` (full model causes significant in-game frame drops due to
  heavier CPU load during streaming)
- Fully offline — no internet calls during gameplay
- Vocabulary restriction: the `VoskRecognizer` is initialized with a JSON grammar
  containing only the player's currently unlocked spell names, improving accuracy
  and reducing recognition noise
- **Threading architecture (critical for performance):**
  - Audio capture callback runs on the main thread but only enqueues raw samples
    into a `ConcurrentQueue<short[]>` — no processing, no frame impact
  - `AcceptWaveform()` (the expensive Vosk call) runs exclusively on a background
    `Task` / `Thread`, consuming from the queue continuously
  - Results are enqueued back into a `ConcurrentQueue<string>` and dequeued on the
    main thread in `Update()` for safe Unity API calls
  - This producer/consumer pattern keeps the main thread fully unblocked during
    all speech recognition processing
- Model size: ~50MB, placed in `StreamingAssets/VoskModels/`
- Push-to-talk activation (hold key) gates microphone streaming to avoid continuous
  background CPU usage during exploration and platforming

---

## 4. Core Game Loop

```
[Platformer World]
  Player explores side-scrolling levels
       │
       ▼
[Encounter Trigger]
  Player contacts an enemy or enters a battle zone
       │
       ▼
[Battle Scene loads]
  Turn-based combat begins
  Player selects action: Attack / Spell / Item / Flee
       │
       ├── Spell selected →
       │     Microphone activates
       │     Player speaks spell name aloud
       │     Vosk matches against unlocked spell vocabulary
       │     Catalyst Arm animation plays → spell executes
       │
       ▼
[Battle resolves]
  Win → XP, loot, possible new spell unlock
  Lose → Game Over / checkpoint
  Flee → Return to platformer world
       │
       ▼
[Return to Platformer World]
  World state restored, progression saved
```

---

## 5. Phased Build Order

### PHASE 1 — Platformer Foundation

**Goal:** A controllable character that moves and navigates a tilemap world.

**Epics / Feature Areas:** _(label: `phase-1-platformer`)_

- Player Controller — move left/right, jump, fall, coyote time, jump buffering
- Tilemap World — create a test level using Unity Tilemap + Rule Tiles
- Camera System — Cinemachine follow camera with confiner bounds and horizontal lookahead
- Player Animations — idle, run, jump, fall sprite animations
- Basic Scene Setup — environment lighting (URP 2D Lights), background layers

**Exit Criteria:**
The player character can move, jump, and navigate a tiled level with a following camera.
Movement must feel responsive and polished before proceeding.

---

### PHASE 2 — Combat System (Isolated Scene)

**Goal:** A fully functional turn-based battle in a standalone scene, disconnected from the
platformer world.

**Epics / Feature Areas:** _(label: `phase-2-combat`)_

- BattleManager — controls turn order, state machine (PlayerTurn, EnemyTurn,
  Victory, Defeat); accepts a `CombatStartState` enum (`Advantaged`, `Surprised`)
  at battle load time — `Advantaged` (player hit enemy first) gives the player the
  first turn; `Surprised` (enemy hit player first) gives the enemy the first turn
- Player Actions — Attack, Spell (placeholder), Item, Flee
- Enemy AI — basic enemy with attack behavior, HP, and damage logic
- Stats System — HP, MP, ATK, DEF, SPD as a base `CharacterStats` class
- Battle UI — health bars, action menu, turn indicator, damage numbers,
  status messages (TextMeshPro)
- Battle Animations — attack/hurt/defeat sprite animations for player and enemy

**Exit Criteria:**
Player can enter the battle scene, select actions, deal and receive damage, and reach a
win or loss state — all without voice input yet.

---

### PHASE 3 — Voice Spell System

**Goal:** Replace the placeholder spell action with fully functional voice-activated spells
using Vosk integrated directly via C# bindings — with a clean threaded architecture that
causes zero frame drops.

**Epics / Feature Areas:** _(label: `phase-3-voice`)_

- Vosk Service Setup — install Vosk C# bindings via NuGet/dll, place
  `vosk-model-en-us-0.22-lgraph` in `StreamingAssets/VoskModels/`, verify model loads
  correctly on both Windows and macOS
- Threaded Recognition Architecture — implement `VoskRecognizerService` as a
  standalone C# service (not a MonoBehaviour) with:
  - `ConcurrentQueue<short[]>` for audio input (main thread enqueues, background dequeues)
  - `ConcurrentQueue<string>` for results (background enqueues, main thread dequeues)
  - Background `Task` running `AcceptWaveform()` — never on the main thread
  - Clean `Start()` / `Stop()` / `Dispose()` lifecycle
- Spell Vocabulary Manager — `SpellVocabularyManager` ScriptableObject-driven
  service that builds and reloads the Vosk JSON grammar from the player's currently
  unlocked spell list; called on spell unlock events
- Microphone Input Handler — `MicrophoneInputHandler` MonoBehaviour that
  captures mic data via `Unity Microphone` API on push-to-talk (hold key), converts
  float samples to `short[]` (PCM16), and enqueues into `VoskRecognizerService` —
  no recognition logic here, capture only
- SpellCastController — MonoBehaviour that polls the result queue each
  `Update()`, matches recognized text against `SpellData` ScriptableObjects, and
  dispatches the spell to `BattleManager` for execution
- Spell Input UI — listening indicator, push-to-talk prompt, recognized spell
  name feedback, and "not recognized" error state
- Spell Effects — visual (particle systems / URP shader effects) and audio SFX
  per spell on cast
- Fallback & Edge Cases — mic not available, no match after timeout, recognized
  word not in current unlocked set, PTT released before recognition completes

**Exit Criteria:**
Player can speak a spell name aloud during their turn and have it execute correctly in
battle, including animations and effects.

---

### PHASE 4 — Scene Bridge (World ↔ Battle)

**Goal:** Connect the platformer world and battle scene into one seamless game loop.

**Epics / Feature Areas:** _(label: `phase-4-bridge`)_

- GameManager Singleton — persistent across scenes, holds player state,
  current party stats, inventory, and active scene context
- Enemy Patrol System — enemy objects in the world roam or patrol along
  defined paths; they actively detect and chase the player within an
  aggro radius (similar to Honkai: Star Rail / Expedition 33 overworld mobs)
- Battle Trigger System — combat engagement via two paths, each producing a
  different `CombatStartState` passed to `BattleManager` on scene load:
  - **Advantaged** — player attacks the enemy first (e.g. player strikes the
    mob in the overworld); player takes the first turn in battle
  - **Surprised** — enemy contacts the player first (e.g. mob catches the
    player from behind or player walks into a patrolling enemy); enemy takes
    the first turn in battle
- Scene Transition — animated transition (fade or effect) from platformer
  to battle scene and back
- World State Preservation — freeze and restore platformer world state
  (enemy positions, player position, interactable states) after battle resolves
- Post-Battle Outcomes — XP gain, level-up prompt, loot drop, enemy removal
  from world on defeat

**Exit Criteria:**
Engaging an enemy on the map (either by striking it or being struck by it) loads the
battle scene with correct enemy data and the appropriate `CombatStartState`. After the
battle, the player is returned to the exact world position with all state intact.

---

### PHASE 5 — Data Layer & Progression

**Goal:** Replace hardcoded values with a clean data architecture and implement
player progression.

**Epics / Feature Areas:** _(label: `phase-5-data`)_

- ScriptableObject Definitions — `SpellData`, `EnemyData`, `ItemData`,
  `CharacterData` assets
- Spell Unlock System — level-up spell learning + story-trigger spell grants;
  integrates with Vocabulary Manager from phase-3-voice
- Inventory System — item collection, use in battle (potions, ethers, etc.)
- Level & XP System — XP thresholds, level-up stat growth, spell unlock events
- Save / Load System — serialize game state to JSON via `System.IO`;
  save on scene transition and at save points in the world
- New Game / Continue Flow — main menu scene with new game and load game options

**Exit Criteria:**
All game data is driven by ScriptableObjects. Player progress persists between sessions
via a save file. Spells unlock and become available to the voice recognizer dynamically.

---

### PHASE 6 — World & Content Build-Out

**Goal:** Build the actual game levels, enemies, and story progression.

**Epics / Feature Areas:** _(label: `phase-6-world`)_

- Level Design — design and build playable levels (tilemap, hazards, platforms,
  secrets)
- Enemy Roster — create ScriptableObject data + battle behaviors for all enemies
- Spell Roster — define all 30–100 Chemical Spells with names, elements,
  effects, MP cost, and unlock conditions
- NPC & Narrative — dialogue system, story cutscenes, key narrative moments
  that unlock spells
- Boss Encounters — unique boss battle logic extending BattleManager
- Environmental Storytelling — background art, parallax layers, world details
  reflecting the Cognition Cascade's aftermath

---

### PHASE 7 — Polish & Release Prep

**Goal:** Bring the game to a shippable state.

**Epics / Feature Areas:** _(label: `phase-7-polish`)_

- Art Pass — replace all placeholder assets with final sprites and UI art:
  - Battle UI: HP bar sprite (track + fill), MP bar sprite, turn indicator
    arrow, action menu panel background, damage number font/style
  - Battle scene: player battle sprite, enemy battle sprites, battle
    background art
  - Platformer UI: HUD HP display (if applicable)
  - Platformer world: player sprite sheets (idle, run, jump, fall), tileset
    art, background/parallax layers
  - All colored placeholder squares/rectangles removed; no placeholder art
    ships in the final build
- Audio — background music, ambient sound, SFX for all actions and spells
- Visual Juice — screen shake, hit-stop, particle polish, UI animations
- Accessibility — option to display spell name on screen as fallback (for
  players with microphone issues); adjustable voice sensitivity
- Settings Menu — audio volume, display resolution, microphone device selection
- Performance Profiling — Unity Profiler pass, draw call optimization,
  GC allocation review
- Build & Distribution — Windows and macOS builds, build pipeline via
  Unity Build Automation or local build scripts

---

## 6. Key Architectural Decisions

| Decision           | Choice                                     | Rationale                                                                                                            |
| ------------------ | ------------------------------------------ | -------------------------------------------------------------------------------------------------------------------- |
| Render Pipeline    | URP 2D                                     | Active development by Unity, 2D lighting support, future-proof                                                       |
| Speech Recognition | Vosk direct (vosk-model-en-us-0.22-lgraph) | Offline, vocabulary restriction, proven model choice; threaded producer/consumer pattern keeps main thread unblocked |
| Data Architecture  | ScriptableObjects                          | Decoupled from code, easy to extend spell/enemy/item lists                                                           |
| Scene Strategy     | Separate platformer + battle scenes        | Separation of concerns, cleaner state management                                                                     |
| State Persistence  | GameManager singleton (DontDestroyOnLoad)  | Single source of truth across scene transitions                                                                      |
| Save Format        | JSON via System.IO                         | Human-readable, no external dependency, sufficient for PC                                                            |
| Naming — Battle vs Combat | "Battle" for in-scene encounter; "Combat" for engagement layer | See note below |
| Sprite Flipping | `Transform.localScale.x = -1` on the sprite child, never `SpriteRenderer.FlipX` | See note below |

### Sprite Flipping

**Never use `SpriteRenderer.FlipX` to mirror sprites.** Use `Transform.localScale.x = -1` on the sprite child GameObject instead, set once in the scene/prefab — never via code or animation.

**Why:** `SpriteRenderer.FlipX` interacts with Unity's Animator `Write Defaults` setting in a way that is extremely difficult to control. When any animation clip in a controller has `FlipX` keyframed (or when Write Defaults is ON), the Animator overwrites the `FlipX` value during state transitions, fighting against any runtime or Inspector value you set. Disabling Write Defaults does not fully resolve this if clips are inconsistent. This has caused a hard-to-trace orientation bug in a prior project on this team.

`localScale.x = -1` lives outside the Animator's property namespace (unless scale is explicitly animated, which it should not be), so Write Defaults never touches it.

**Pattern:**
```
Enemy (root — Rigidbody2D, Collider2D, scripts)
└── Visual (child — SpriteRenderer, Animator, *BattleAnimator script)
       Transform.localScale.x = -1  ← set in Inspector, never animated
```

### Naming Convention: "Battle" vs "Combat"

These two terms are used deliberately and are **not interchangeable**:

| Term | Scope | Examples |
|------|-------|---------|
| **Battle** | The in-scene turn-based encounter once it has started | `BattleManager`, `BattleState`, `BattleController`, `Battle` scene, `Assets/Scripts/Battle/` |
| **Combat** | The broader engagement layer — how an encounter is initiated | `CombatStartState` (`Advantaged` / `Surprised`) |

**Rationale:** "Battle" follows JRPG convention (Final Fantasy, Pokémon, Sea of Stars all use "Battle") and is immediately readable to anyone familiar with the genre. "Combat" is reserved for the entry-condition concept — `CombatStartState` describes *how* the player entered the fight (did they strike first, or were they surprised?), which is a different concern from managing the battle itself.

---

## 7. Out of Scope (v1.0)

- Multiplayer of any kind
- Mobile or console builds
- Cloud saves
- Procedurally generated levels
- Voice-to-text for free-form dialogue (voice is spell-activation only)
- Modding support

---

## 8. Reference Titles

These games inform specific design decisions:

| Game                  | What to reference                                       |
| --------------------- | ------------------------------------------------------- |
| **Final Fantasy I**   | Turn-based battle structure, action menu layout         |
| **Pokémon**           | Spell/move data architecture, per-turn action selection |
| **Honkai: Star Rail** | Turn order visualization, enemy telegraphing, overworld mob patrol and combat engagement system |
| **Expedition 33**     | Dark tone, narrative-driven spell unlocks, advantaged/surprised combat engagement states        |
| **Sea of Stars**      | Platformer-to-battle transition feel                    |

---

## 9. Folder Structure (Proposed)

```
Assets/
├── Art/
│   ├── Sprites/
│   ├── Tilemaps/
│   └── UI/
├── Audio/
│   ├── Music/
│   └── SFX/
├── Data/
│   ├── Characters/
│   ├── Enemies/
│   ├── Spells/
│   └── Items/
├── Prefabs/
│   ├── Player/
│   ├── Enemies/
│   └── UI/
├── Scenes/
│   ├── MainMenu
│   ├── World_01
│   └── Battle
├── Scripts/
│   ├── Battle/           ← battle logic + all battle scene UI
│   │   └── UI/           ← HealthBarUI, ActionMenuUI, BattleHUD, etc.
│   ├── Core/
│   ├── Data/
│   ├── Platformer/       ← platformer player/camera scripts
│   │   └── UI/           ← platformer HUD (HP display, etc.) — added as needed
│   └── Voice/
└── ThirdParty/
    └── Vosk/               ← Vosk C# bindings dll

StreamingAssets/
└── VoskModels/
    └── vosk-model-en-us-0.22-lgraph/

docs/
└── GAME_PLAN.md       ← this file
```

---

## 10. Architecture & Code Quality Principles

This project is a **clean rewrite from scratch.** A previous codebase existed but was
abandoned due to accumulated spaghetti code, redundant systems, and dead code. All
architectural decisions here are made deliberately to prevent those issues from recurring.

### Non-negotiable standards for this codebase:

- **Single Responsibility** — every class does one thing. `MicrophoneInputHandler` captures
  audio. `VoskRecognizerService` recognizes speech. `SpellCastController` maps results to
  actions. Never combined.
- **No MonoBehaviour business logic** — MonoBehaviours handle Unity lifecycle only
  (Start, Update, OnDestroy). All logic lives in plain C# classes/services injected
  into them.
- **No static singletons for game logic** — use a single `GameManager` for cross-scene
  state only. Everything else is passed explicitly or via events/ScriptableObject channels.
- **ScriptableObject-driven data** — no hardcoded spell names, stats, or enemy values
  anywhere in code. All tunable data lives in `.asset` files.
- **Threaded speech recognition** — `AcceptWaveform()` must never run on the main thread.
  The producer/consumer `ConcurrentQueue` pattern is the only accepted implementation.
- **Dead code policy** — if a feature is cut or deferred, delete the code. Do not comment
  it out. Git history preserves it if needed.
- **No premature abstraction** — do not create base classes, interfaces, or managers for
  systems that only have one implementation yet.

---

## 11. Claude Code Usage Notes

This project uses **Jira Free**, which fixes the ticket ID prefix to `DEV-#` (no custom
prefixes per board). Phase and feature area organization is handled entirely through
**labels** and **components** instead.

### Ticket ID format

All tickets follow the auto-assigned Jira format: `DEV-1`, `DEV-2`, `DEV-3`, etc.

### Label taxonomy

Use these labels to group and filter tickets by phase and type:

| Label                | Meaning                            |
| -------------------- | ---------------------------------- |
| `phase-1-platformer` | Phase 1 — Platformer Foundation    |
| `phase-2-combat`     | Phase 2 — Combat System            |
| `phase-3-voice`      | Phase 3 — Voice Spell System       |
| `phase-4-bridge`     | Phase 4 — Scene Bridge             |
| `phase-5-data`       | Phase 5 — Data Layer & Progression |
| `phase-6-world`      | Phase 6 — World & Content          |
| `phase-7-polish`     | Phase 7 — Polish & Release         |
| `unity`              | Unity-specific implementation      |
| `vosk`               | Speech recognition related         |
| `architecture`       | Core structural / design decisions |
| `bug`                | Defect or broken behaviour         |
| `content`            | Art, audio, narrative assets       |

### Prompt pattern for ticket generation

```
Using GAME_PLAN.md as context, create Jira stories for Phase [N] ([phase name]).
Each story should have:
- A clear one-line summary
- Acceptance criteria (bullet points)
- Label: [phase-label]
- Story type: Story or Task
```

### Task granularity guide

- Each bullet point under a phase's "Feature Areas" → one **Story** (`DEV-#`)
- Sub-steps within a story (e.g. individual scripts or components) → **Subtasks**
- Bugs found during development → separate **Bug** tickets with `bug` label
- Do not create tickets for Phase 6+ until Phases 1–4 are complete
