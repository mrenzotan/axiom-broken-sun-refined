# Platformer Mana Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **This project uses Unity Version Control (UVCS), not git — every check-in step is a UVCS Pending Changes check-in.**

**Goal:** Add world-space floating-number feedback in the platformer when a spell is cast (`-N MP`) and when a spell is attempted near a resolvable puzzle without enough mana (`Not enough MP`).

**Architecture:** Reuse the existing pooled, world-space `PlatformerFloatingNumberSpawner` (Battle's spawner is UI/`RectTransform`-space — wrong coordinate system). A new pure 3-state decision `PlatformerSpellWorldCaster.EvaluateCast` replaces the controller's inline `castable` bool so the fail-feedback trigger is unit-testable. `PlatformerVoiceSpellController` (MonoBehaviour seam) routes the decision: `Castable` → request cast (unchanged), `InsufficientMana` → spawn the fail cue, `NoTarget` → nothing; and on the deferred fire-frame it spawns the spend cue using the measured MP delta.

**Tech Stack:** Unity 6.0.4 LTS, C#, URP 2D, TextMeshPro (world-space, via the existing instance prefab), NUnit EditMode tests, Unity Version Control (UVCS).

**Spec:** [docs/superpowers/specs/2026-06-24-dev-118-platformer-mana-feedback-design.md](../specs/2026-06-24-dev-118-platformer-mana-feedback-design.md)
**Jira:** DEV-118 — *Visual Feedback on Environmental Puzzle Spell Casts in Platformer Scenes*

> **Revision (2026-06-24):** Task 4 was retargeted away from `Platformer.unity` — that is a now-**archived test scene** (`Assets/Scenes/Archive/Platformer.unity`). The real platforming scenes are `Level_*-*.unity`. `PlatformerVoiceSpellController` is not a direct scene object; it lives on `Assets/Prefabs/Voice/PlatformerVoiceRig.prefab`, instanced into the **7** scenes that have voice casting (`Level_1-1/1-2/1-3`, `Level_2-1/2-2`, `Level_3-1/3-2`). Per the chosen approach, `_floatingNumbers` is wired as a **per-scene prefab-instance override** in each of those scenes (not a runtime fallback). Tasks 1–3 (code) are unchanged.

## Global Constraints

- **MonoBehaviour = lifecycle/wiring only; all logic in plain C#.** `EvaluateCast` is a pure static method; the spawn methods are thin formatting over an already-written private `Spawn`.
- **No premature abstraction.** No interface/seam is introduced for the spawner — it is reached via a `[SerializeField]` reference, exactly as `SavePointTrigger` does.
- **Floating numbers only.** No `PlatformerHpHudUI` / `HealthBarUI` changes — no mana-bar flash or shake.
- **Fail feedback fires only near a resolvable puzzle** (spoken spell would resolve an in-range obstacle but MP is short). A recognized spell with nothing nearby stays silent.
- **No fizzle animation.** The cast clip still plays only on a real, MP-paid cast.
- **Version control is UVCS, not git.** Check-in message format: `<type>(DEV-118): <short description>` (`feat`, `fix`, `chore`, `docs`, `refactor`, `test`). Never run `git add` / `git commit`.
- **EditMode tests** live in `Assets/Tests/Editor/Voice/` (asmdef `VoiceTests`, already references `Axiom.Voice`, `Axiom.Platformer`, `Axiom.Core`, `Axiom.Data`). No new asmdef is needed; every file in this plan already exists and is **modified**.

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs` | Pure cast decision + resolution helpers | Modify — add `CastEvaluation` enum + `EvaluateCast` |
| `Assets/Tests/Editor/Voice/PlatformerSpellWorldCasterTests.cs` | Unit tests for the caster | Modify — add `EvaluateCast` tests + helpers |
| `Assets/Scripts/Platformer/PlatformerFloatingNumberSpawner.cs` | World-space pooled number spawner | Modify — add `SpawnManaSpent`, `SpawnInsufficientMana` |
| `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs` | MonoBehaviour seam: match → decide → cast/feedback | Modify — `_floatingNumbers` field, `EvaluateCast` switch, spend-delta cue |
| `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs` | Controller behavior tests | Modify — add insufficient-MP guard test |
| `Assets/Scenes/Level_1-1.unity` … `Level_3-2.unity` (7 rig-bearing scenes) | Per-scene wiring | **Unity Editor task (user)** — override each `PlatformerVoiceRig` instance's `_floatingNumbers` with that scene's spawner |

---

## Task 1: `EvaluateCast` — pure 3-state cast decision

**Files:**
- Modify: `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs`
- Test: `Assets/Tests/Editor/Voice/PlatformerSpellWorldCasterTests.cs`

**Interfaces:**
- Consumes: existing `PlatformerSpellWorldCaster.HasResolvableTarget(spell, 5 lists)`; `SpellData.spellName`, `SpellData.mpCost`; obstacle controllers' `Set*` test seams.
- Produces: `enum CastEvaluation { NoTarget, InsufficientMana, Castable }` and
  `static CastEvaluation EvaluateCast(SpellData spell, int currentMp, IReadOnlyList<MeltableObstacleController>, IReadOnlyList<FreezablePlatformController>, IReadOnlyList<BurnableObstacleController>, IReadOnlyList<SteamVentController>, IReadOnlyList<AcidPuddleController>)` — consumed by Task 3.

- [ ] **Step 1: Add test helpers + failing tests** to `PlatformerSpellWorldCasterTests.cs`

Add a `mpCost` overload of `MakeSpell`, a `SetPrivateField` helper, an in-range meltable factory, and four `EvaluateCast` tests. Insert the helpers next to the existing `MakeSpell` and the tests after the existing `HasResolvableTarget_*` tests (inside the existing `class PlatformerSpellWorldCasterTests`):

```csharp
        private SpellData MakeSpell(string name, int mpCost)
        {
            SpellData spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = name;
            spell.mpCost = mpCost;
            return spell;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, $"field {fieldName} not found");
            field.SetValue(target, value);
        }

        // An in-range meltable obstacle that accepts `accepts` — the minimal HasResolvableTarget hit.
        private static MeltableObstacleController MakeInRangeMeltable(SpellData accepts)
        {
            var go = new GameObject("MeltableObstacle");
            var obstacle = go.AddComponent<MeltableObstacleController>();
            obstacle.SetPlayerInRange(true);
            SetPrivateField(obstacle, "_meltSpells",
                new System.Collections.Generic.List<SpellData> { accepts });
            return obstacle;
        }

        [Test]
        public void EvaluateCast_NoInRangeTarget_ReturnsNoTarget()
        {
            // WHY: with nothing nearby to resolve, the controller must stay silent —
            // no cast AND no "not enough MP" cue — even if MP is plentiful.
            SpellData spell = MakeSpell("melt", 5);

            CastEvaluation result = PlatformerSpellWorldCaster.EvaluateCast(
                spell, 999,
                Array.Empty<MeltableObstacleController>(),
                Array.Empty<FreezablePlatformController>(),
                Array.Empty<BurnableObstacleController>(),
                Array.Empty<SteamVentController>(),
                Array.Empty<AcidPuddleController>());

            Assert.AreEqual(CastEvaluation.NoTarget, result);

            Object.DestroyImmediate(spell);
        }

        [Test]
        public void EvaluateCast_InRangeTargetButInsufficientMp_ReturnsInsufficientMana()
        {
            // WHY: this is the exact fail-feedback trigger — a resolvable puzzle in range
            // that the player cannot currently afford.
            SpellData spell = MakeSpell("melt", 8);
            MeltableObstacleController obstacle = MakeInRangeMeltable(spell);

            CastEvaluation result = PlatformerSpellWorldCaster.EvaluateCast(
                spell, 5,
                new[] { obstacle },
                Array.Empty<FreezablePlatformController>(),
                Array.Empty<BurnableObstacleController>(),
                Array.Empty<SteamVentController>(),
                Array.Empty<AcidPuddleController>());

            Assert.AreEqual(CastEvaluation.InsufficientMana, result);

            Object.DestroyImmediate(obstacle.gameObject);
            Object.DestroyImmediate(spell);
        }

        [Test]
        public void EvaluateCast_InRangeTargetAndEnoughMp_ReturnsCastable()
        {
            SpellData spell = MakeSpell("melt", 8);
            MeltableObstacleController obstacle = MakeInRangeMeltable(spell);

            CastEvaluation result = PlatformerSpellWorldCaster.EvaluateCast(
                spell, 8,
                new[] { obstacle },
                Array.Empty<FreezablePlatformController>(),
                Array.Empty<BurnableObstacleController>(),
                Array.Empty<SteamVentController>(),
                Array.Empty<AcidPuddleController>());

            Assert.AreEqual(CastEvaluation.Castable, result);

            Object.DestroyImmediate(obstacle.gameObject);
            Object.DestroyImmediate(spell);
        }

        [Test]
        public void EvaluateCast_NullSpell_ReturnsNoTarget()
        {
            // WHY: a null spell must short-circuit to NoTarget before mpCost is ever read.
            CastEvaluation result = PlatformerSpellWorldCaster.EvaluateCast(
                null, 0,
                Array.Empty<MeltableObstacleController>(),
                Array.Empty<FreezablePlatformController>(),
                Array.Empty<BurnableObstacleController>(),
                Array.Empty<SteamVentController>(),
                Array.Empty<AcidPuddleController>());

            Assert.AreEqual(CastEvaluation.NoTarget, result);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Unity Editor → Window → General → Test Runner → EditMode → run `PlatformerSpellWorldCasterTests`.
Expected: the four `EvaluateCast_*` tests FAIL to compile / are not found — `CastEvaluation` and `EvaluateCast` do not exist yet. (The existing `HasResolvableTarget_*` tests are unaffected.)

- [ ] **Step 3: Implement `CastEvaluation` + `EvaluateCast`** in `PlatformerSpellWorldCaster.cs`

Add the enum at namespace scope (above the class) and the method directly after `HasResolvableTarget` (before `TryCast`):

```csharp
    public enum CastEvaluation
    {
        NoTarget,
        InsufficientMana,
        Castable
    }
```

```csharp
        public static CastEvaluation EvaluateCast(
            SpellData spell,
            int currentMp,
            IReadOnlyList<MeltableObstacleController> meltableObstacles,
            IReadOnlyList<FreezablePlatformController> freezablePlatforms,
            IReadOnlyList<BurnableObstacleController> burnableObstacles,
            IReadOnlyList<SteamVentController> steamVents,
            IReadOnlyList<AcidPuddleController> acidPuddles)
        {
            // HasResolvableTarget already guards spell == null, so mpCost below is null-safe.
            if (!HasResolvableTarget(spell, meltableObstacles, freezablePlatforms,
                    burnableObstacles, steamVents, acidPuddles))
                return CastEvaluation.NoTarget;

            if (currentMp < spell.mpCost)
                return CastEvaluation.InsufficientMana;

            return CastEvaluation.Castable;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Test Runner → EditMode → run `PlatformerSpellWorldCasterTests`.
Expected: all tests PASS (3 existing `HasResolvableTarget_*` + 4 new `EvaluateCast_*`).

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-118): add EvaluateCast 3-state platformer spell-cast decision`
  - `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs`
  - `Assets/Tests/Editor/Voice/PlatformerSpellWorldCasterTests.cs`

---

## Task 2: Floating-number cues — spend & insufficient-mana

**Files:**
- Modify: `Assets/Scripts/Platformer/PlatformerFloatingNumberSpawner.cs`

**Interfaces:**
- Consumes: existing private `Spawn(Vector2 position, string text, Color color)`, the `_pool`, the `_prefab` null-guard pattern, and `MpVerticalOffset`.
- Produces: `void SpawnManaSpent(Vector2 worldPosition, int spentMp)` and `void SpawnInsufficientMana(Vector2 worldPosition)` — consumed by Task 3.

> **No EditMode test for this task — intentional.** Both methods are thin string/color formatting over the already-present private `Spawn`, which calls `Instantiate(_prefab)` / pooling — a MonoBehaviour + prefab path not reachable in EditMode without instantiating a TMP prefab. Adding a spy/interface here would be premature abstraction (Global Constraints). These cues are verified in Play Mode in Task 4. The branch logic worth testing (when each cue fires) lives in `EvaluateCast` (Task 1) and the controller routing (Task 3).

- [ ] **Step 1: Add the two cue methods + the insufficient color** to `PlatformerFloatingNumberSpawner.cs`

Add the color constant next to `MpVerticalOffset`, and the two public methods after the existing `SpawnHealNumbers` (above the private `Spawn`):

```csharp
        // Soft red — distinct from the cyan MP numbers; final value dialed in Play Mode.
        private static readonly Color InsufficientManaColor = new Color(0.9f, 0.3f, 0.3f);
```

```csharp
        /// <summary>
        /// Spawns a "-N MP" cyan number above worldPosition, emphasizing mana consumed by a cast.
        /// </summary>
        public void SpawnManaSpent(Vector2 worldPosition, int spentMp)
        {
            if (_prefab == null)
            {
                Debug.LogWarning("[PlatformerFloatingNumberSpawner] Prefab not assigned.", this);
                return;
            }

            Spawn(new Vector2(worldPosition.x, worldPosition.y + MpVerticalOffset), $"-{spentMp} MP", Color.cyan);
        }

        /// <summary>
        /// Spawns a red "Not enough MP" message above worldPosition when a cast is attempted without enough mana.
        /// </summary>
        public void SpawnInsufficientMana(Vector2 worldPosition)
        {
            if (_prefab == null)
            {
                Debug.LogWarning("[PlatformerFloatingNumberSpawner] Prefab not assigned.", this);
                return;
            }

            Spawn(new Vector2(worldPosition.x, worldPosition.y + MpVerticalOffset), "Not enough MP", InsufficientManaColor);
        }
```

- [ ] **Step 2: Verify compilation**

Return to the Unity Editor and let it recompile. Expected: no compile errors in the Console; `PlatformerFloatingNumberSpawner` now exposes `SpawnManaSpent` and `SpawnInsufficientMana`.

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message: `feat(DEV-118): add mana-spent and insufficient-mana floating cues`
  - `Assets/Scripts/Platformer/PlatformerFloatingNumberSpawner.cs`

---

## Task 3: Wire mana feedback into `PlatformerVoiceSpellController`

**Files:**
- Modify: `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs`
- Test: `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs`

**Interfaces:**
- Consumes: `PlatformerSpellWorldCaster.EvaluateCast` + `CastEvaluation` (Task 1); `PlatformerFloatingNumberSpawner.SpawnManaSpent` / `SpawnInsufficientMana` (Task 2); existing `_player` (`PlayerController`), `_castSequencer`, `Resolve*` list helpers, `PlatformerSpellWorldCaster.TryCast`, `PlayerState.CurrentMp`.
- Produces: no new public surface (the feedback is internal wiring) + one new `[SerializeField] PlatformerFloatingNumberSpawner _floatingNumbers` assigned in Task 4.

- [ ] **Step 1: Add the insufficient-MP guard test** to `PlatformerVoiceSpellControllerTests.cs`

Add after the existing `Update_NeutralizeSpell_PuddleOutOfRange_DoesNotDissolveOrSpendMp` test (reuses the file's existing `SetPrivateField`, `InvokePrivateMethod`, `CreateCharacterData` helpers):

```csharp
        [Test]
        public void Update_RecognizedSpell_InRangePuzzleButInsufficientMp_DoesNotCastOrSpendMp()
        {
            // WHY: insufficient MP near a resolvable puzzle must NOT cast or spend MP — the
            // controller shows "Not enough MP" feedback instead (the float is Play-Mode verified).
            // This guards the EvaluateCast switch so it never casts when the player is broke.
            SpellData combust = ScriptableObject.CreateInstance<SpellData>();
            combust.spellName = "combust";
            combust.mpCost = 8;

            GameObject obstacleGo = new GameObject("MeltableObstacle");
            var obstacle = obstacleGo.AddComponent<MeltableObstacleController>();
            obstacle.SetPlayerInRange(true);
            SetPrivateField(obstacle, "_meltSpells", new System.Collections.Generic.List<SpellData> { combust });

            GameObject gameManagerGo = null;
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                gameManagerGo = new GameObject("GameManager");
                gameManager = gameManagerGo.AddComponent<GameManager>();
            }
            CharacterData characterData = CreateCharacterData();
            gameManager.SetPlayerCharacterDataForTests(characterData);
            gameManager.PlayerState.SetCurrentMp(5); // below combust.mpCost (8)

            GameObject controllerGo = new GameObject("PlatformerVoiceSpellController");
            var controller = controllerGo.AddComponent<PlatformerVoiceSpellController>();
            SetPrivateField(controller, "_meltableObstacles", new[] { obstacle });

            var resultQueue = new ConcurrentQueue<string>();
            resultQueue.Enqueue("{\"text\": \"combust\"}");
            controller.Inject(resultQueue, new[] { combust }, gameManager.PlayerState);

            InvokePrivateMethod(controller, "Update");
            InvokePrivateMethod(controller, "OnPlayerSpellFireFrame");

            Assert.IsFalse(obstacle.IsMelted, "insufficient MP must not resolve the obstacle");
            Assert.AreEqual(5, gameManager.PlayerState.CurrentMp, "insufficient MP must not be spent");

            Object.DestroyImmediate(controllerGo);
            if (gameManagerGo != null)
                Object.DestroyImmediate(gameManagerGo);
            Object.DestroyImmediate(obstacleGo);
            Object.DestroyImmediate(combust);
            Object.DestroyImmediate(characterData);
        }
```

- [ ] **Step 2: Run the guard test — confirm it passes (green)**

Test Runner → EditMode → run `PlatformerVoiceSpellControllerTests`.
Expected: PASS. This is a characterization/guard test — the current gating (`HasResolvableTarget && CurrentMp >= cost`) already rejects insufficient MP, so it must be green *before* the refactor and stay green after. `_floatingNumbers` is left unset (null) in the test → the spawn call is a no-op, so no spawner instantiation is needed.

- [ ] **Step 3: Add the `_floatingNumbers` serialized field** to `PlatformerVoiceSpellController.cs`

Insert after the `_auraCue` field (around line 40):

```csharp
        [SerializeField]
        [Tooltip("World-space floating-number spawner. Shows '-N MP' on a cast and 'Not enough MP' when broke. Assign the scene's PlatformerFloatingNumberSpawner.")]
        private PlatformerFloatingNumberSpawner _floatingNumbers;
```

- [ ] **Step 4: Replace the `castable` gate in `Update()` with the `EvaluateCast` switch**

Replace this block (currently lines ~94–104):

```csharp
                bool castable =
                    PlatformerSpellWorldCaster.HasResolvableTarget(
                        matched,
                        ResolveMeltableObstacles(),
                        ResolveFreezablePlatforms(),
                        ResolveBurnableObstacles(),
                        ResolveSteamVents(),
                        ResolveAcidPuddles())
                    && playerState.CurrentMp >= matched.mpCost;

                if (castable) _castSequencer.RequestCast(matched);
```

with:

```csharp
                CastEvaluation evaluation = PlatformerSpellWorldCaster.EvaluateCast(
                    matched,
                    playerState.CurrentMp,
                    ResolveMeltableObstacles(),
                    ResolveFreezablePlatforms(),
                    ResolveBurnableObstacles(),
                    ResolveSteamVents(),
                    ResolveAcidPuddles());

                switch (evaluation)
                {
                    case CastEvaluation.Castable:
                        _castSequencer.RequestCast(matched);
                        break;
                    case CastEvaluation.InsufficientMana:
                        if (_floatingNumbers != null && _player != null)
                            _floatingNumbers.SpawnInsufficientMana(_player.transform.position);
                        break;
                    // CastEvaluation.NoTarget: no nearby resolvable puzzle — stay silent.
                }
```

- [ ] **Step 5: Emit the spend cue in `ResolveAction` using the measured MP delta**

Replace the existing `ResolveAction` body (currently lines ~115–126):

```csharp
        private void ResolveAction(SpellData spell)
        {
            PlayerState playerState = _playerState ?? GameManager.Instance?.PlayerState;
            int mpBefore = playerState?.CurrentMp ?? 0;

            PlatformerSpellWorldCaster.TryCast(
                spell,
                ResolveMeltableObstacles(),
                ResolveFreezablePlatforms(),
                ResolveBurnableObstacles(),
                ResolveSteamVents(),
                ResolveAcidPuddles(),
                playerState);

            if (playerState != null && _floatingNumbers != null && _player != null)
            {
                int spent = mpBefore - playerState.CurrentMp;
                if (spent > 0)
                    _floatingNumbers.SpawnManaSpent(_player.transform.position, spent);
            }
        }
```

- [ ] **Step 6: Run the full Voice EditMode suite**

Test Runner → EditMode → run `PlatformerVoiceSpellControllerTests` (all) + `PlatformerSpellWorldCasterTests`.
Expected: PASS — the 5 original controller tests still resolve and spend MP (their `_floatingNumbers` is null → spend cue no-ops, MP accounting unchanged), plus the new guard test and Task 1's `EvaluateCast` tests.

- [ ] **Step 7: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-118): wire platformer mana feedback into voice spell controller`
  - `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs`
  - `Assets/Tests/Editor/Voice/PlatformerVoiceSpellControllerTests.cs`

---

## Task 4: Per-scene wiring + Play-Mode verification (Unity Editor — user)

**Files:**
- Modify (7 scenes): `Assets/Scenes/Level_1-1.unity`, `Level_1-2.unity`, `Level_1-3.unity`, `Level_2-1.unity`, `Level_2-2.unity`, `Level_3-1.unity`, `Level_3-2.unity`

> **Scene reality (corrected — see Revision note up top):** `Platformer.unity` is an archived **test** scene — do **not** wire it. `PlatformerVoiceSpellController` lives on **`Assets/Prefabs/Voice/PlatformerVoiceRig.prefab`**, which is instanced only into the **7** scenes that have voice casting: `Level_1-1`, `Level_1-2`, `Level_1-3`, `Level_2-1`, `Level_2-2`, `Level_3-1`, `Level_3-2`. (`Level_1-4`, `2-3`, `3-3`, `4-1` have no rig — **skip** them.) Each rig-bearing scene already contains exactly one `PlatformerFloatingNumberSpawner` scene object.
>
> Because a **prefab asset cannot reference a per-scene object**, `_floatingNumbers` must be set as a **per-scene prefab-instance override** in each of those 7 scenes — it cannot be set once on the prefab. (This is the same reason the controller's obstacle lists sit empty `[]` on the prefab; here we wire the spawner explicitly rather than via a runtime fallback.)

- [ ] **Step 1: Override `_floatingNumbers` in each of the 7 rig scenes (user)**

For **each** scene in {`Level_1-1`, `Level_1-2`, `Level_1-3`, `Level_2-1`, `Level_2-2`, `Level_3-1`, `Level_3-2`}:
  1. Open the scene.
  2. In the Hierarchy, select the **`PlatformerVoiceRig`** instance → its `PlatformerVoiceSpellController` component.
  3. Drag that scene's **`PlatformerFloatingNumberSpawner`** object into the **Floating Numbers** field. It will show as a prefab **override** (blue change-bar).
  4. **Do NOT use "Apply"/"Apply All to Prefab"** — a scene-object reference cannot be stored in the prefab asset, and applying would reset the field back to *None*. Keep it a scene-local override and **save the scene**.

- [ ] **Step 2: Play-Mode verification — spend cue (AC3 / AC4)**

Open a wired scene (e.g., `Level_1-1`). Enter Play Mode. Walk Kaelen into a puzzle's proximity (aura shows). With **enough MP**, speak the resolving spell.
Expected: the cast animation plays, the obstacle resolves on the fire-frame, and a **cyan `-N MP`** number rises from the player and fades — `N` equal to the spell's `mpCost`. The HUD mana bar also drops (unchanged).

- [ ] **Step 3: Play-Mode verification — insufficient cue (AC2)**

In a wired scene, drain MP below a spell's cost (cast until low, or use a low-cost/high-cost pairing). Stand in range of a puzzle that spell resolves and speak it.
Expected: a **red `Not enough MP`** message rises from the player; **no** cast animation, **no** obstacle change, **no** MP spent.

- [ ] **Step 4: Play-Mode verification — silent no-target + readability (AC7 / AC8)**

Speak a known spell with **no** matching puzzle in range → nothing appears (no fail cue). Then repeat steps 2–3 while **moving** with the camera following.
Expected: both cues stay legible above the player, clear quickly, never block input, and don't obscure the puzzle being solved.

- [ ] **Step 5: Check in the wired scenes via UVCS**

Unity Version Control → Pending Changes → stage the 7 modified scenes below → Check in with message: `feat(DEV-118): wire floating-number spawner on voice rig across level scenes`
  - `Assets/Scenes/Level_1-1.unity`, `Level_1-2.unity`, `Level_1-3.unity`, `Level_2-1.unity`, `Level_2-2.unity`, `Level_3-1.unity`, `Level_3-2.unity`

> **Spot-check before Play Mode:** wiring is correct only if each of the 7 scenes shows up as modified after Step 1. If a scene is missing from Pending Changes, its override didn't take (or was accidentally applied to the prefab) — re-do Step 1 for that scene.

---

## Done When

- All EditMode tests green: `PlatformerSpellWorldCasterTests` (incl. 4 new `EvaluateCast_*`), `PlatformerVoiceSpellControllerTests` (5 original + 1 new guard).
- Play Mode (in a rig-bearing `Level_*` scene) shows the cyan `-N MP` spend cue on a successful cast and the red `Not enough MP` cue when attempting a resolvable spell without enough MP, with no cue for unrelated spells.
- All **7** rig-bearing scenes (`Level_1-1/1-2/1-3`, `Level_2-1/2-2`, `Level_3-1/3-2`) have the `_floatingNumbers` override set (scene-local, not applied to the prefab) and checked in.
- The five DEV-118 mana-feedback ACs (insufficient feedback, consumption emphasis, more-apparent-than-bar, Battle-spawner reuse conclusion, readability/non-interference) are satisfied per the spec's coverage table.
- No `PlatformerHpHudUI` / `HealthBarUI` changes; no new asmdef; UVCS check-ins for Tasks 1–4.
