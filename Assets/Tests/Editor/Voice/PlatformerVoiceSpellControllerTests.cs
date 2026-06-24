using System.Collections.Concurrent;
using System.Reflection;
using Axiom.Core;
using Axiom.Data;
using Axiom.Platformer;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Voice.Tests
{
    public class PlatformerVoiceSpellControllerTests
    {
        [Test]
        public void Update_RecognizedMeltSpell_MeltsInRangeObstacle()
        {
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
            gameManager.PlayerState.SetCurrentMp(20);

            GameObject controllerGo = new GameObject("PlatformerVoiceSpellController");
            var controller = controllerGo.AddComponent<PlatformerVoiceSpellController>();
            SetPrivateField(controller, "_meltableObstacles", new[] { obstacle });

            var resultQueue = new ConcurrentQueue<string>();
            resultQueue.Enqueue("{\"text\": \"combust\"}");
            controller.Inject(resultQueue, new[] { combust }, gameManager.PlayerState);

            InvokePrivateMethod(controller, "Update");
            // Resolution is deferred to the cast animation's fire-frame; Update only requests the cast.
            Assert.IsFalse(obstacle.IsMelted, "resolution must be deferred until the cast fire-frame");
            InvokePrivateMethod(controller, "OnPlayerSpellFireFrame");

            Assert.IsTrue(obstacle.IsMelted);
            Assert.AreEqual(12, gameManager.PlayerState.CurrentMp);

            Object.DestroyImmediate(controllerGo);
            if (gameManagerGo != null)
                Object.DestroyImmediate(gameManagerGo);
            Object.DestroyImmediate(obstacleGo);
            Object.DestroyImmediate(combust);
            Object.DestroyImmediate(characterData);
        }

        [Test]
        public void Update_RecognizedFreezeSpell_FreezesInRangeWaterPlatformAndSpendsMp()
        {
            SpellData freeze = ScriptableObject.CreateInstance<SpellData>();
            freeze.spellName = "freeze";
            freeze.mpCost = 6;

            GameObject platformGo = new GameObject("FreezablePlatform");
            var platform = platformGo.AddComponent<FreezablePlatformController>();
            platform.SetPlayerInRange(true);
            SetPrivateField(platform, "_freezeSpells", new System.Collections.Generic.List<SpellData> { freeze });

            GameObject gameManagerGo = null;
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                gameManagerGo = new GameObject("GameManager");
                gameManager = gameManagerGo.AddComponent<GameManager>();
            }
            CharacterData characterData = CreateCharacterData();
            gameManager.SetPlayerCharacterDataForTests(characterData);
            gameManager.PlayerState.SetCurrentMp(20);

            GameObject controllerGo = new GameObject("PlatformerVoiceSpellController");
            var controller = controllerGo.AddComponent<PlatformerVoiceSpellController>();
            SetPrivateField(controller, "_freezablePlatforms", new[] { platform });

            var resultQueue = new ConcurrentQueue<string>();
            resultQueue.Enqueue("{\"text\": \"freeze\"}");
            controller.Inject(resultQueue, new[] { freeze }, gameManager.PlayerState);

            InvokePrivateMethod(controller, "Update");
            // Resolution is deferred to the cast animation's fire-frame; Update only requests the cast.
            InvokePrivateMethod(controller, "OnPlayerSpellFireFrame");

            Assert.IsTrue(platform.IsFrozen);
            Assert.AreEqual(14, gameManager.PlayerState.CurrentMp);

            Object.DestroyImmediate(controllerGo);
            if (gameManagerGo != null)
                Object.DestroyImmediate(gameManagerGo);
            Object.DestroyImmediate(platformGo);
            Object.DestroyImmediate(freeze);
            Object.DestroyImmediate(characterData);
        }

        [Test]
        public void Update_RecognizedCombustSpell_IgnitesInRangeBurnableObstacleAndSpendsMp()
        {
            SpellData combust = ScriptableObject.CreateInstance<SpellData>();
            combust.spellName = "combust";
            combust.mpCost = 8;

            GameObject obstacleGo = new GameObject("BurnableObstacle");
            var obstacle = obstacleGo.AddComponent<BurnableObstacleController>();
            obstacle.SetPlayerInRange(true);
            SetPrivateField(obstacle, "_igniteSpells", new System.Collections.Generic.List<SpellData> { combust });

            GameObject gameManagerGo = null;
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                gameManagerGo = new GameObject("GameManager");
                gameManager = gameManagerGo.AddComponent<GameManager>();
            }
            CharacterData characterData = CreateCharacterData();
            gameManager.SetPlayerCharacterDataForTests(characterData);
            gameManager.PlayerState.SetCurrentMp(20);

            GameObject controllerGo = new GameObject("PlatformerVoiceSpellController");
            var controller = controllerGo.AddComponent<PlatformerVoiceSpellController>();
            SetPrivateField(controller, "_burnableObstacles", new[] { obstacle });

            var resultQueue = new ConcurrentQueue<string>();
            resultQueue.Enqueue("{\"text\": \"combust\"}");
            controller.Inject(resultQueue, new[] { combust }, gameManager.PlayerState);

            InvokePrivateMethod(controller, "Update");
            // Resolution is deferred to the cast animation's fire-frame; Update only requests the cast.
            InvokePrivateMethod(controller, "OnPlayerSpellFireFrame");

            Assert.IsTrue(obstacle.IsBurned);
            Assert.AreEqual(12, gameManager.PlayerState.CurrentMp);

            Object.DestroyImmediate(controllerGo);
            if (gameManagerGo != null)
                Object.DestroyImmediate(gameManagerGo);
            Object.DestroyImmediate(obstacleGo);
            Object.DestroyImmediate(combust);
            Object.DestroyImmediate(characterData);
        }

        [Test]
        public void Update_RecognizedNeutralizeSpell_DissolvesInRangeAcidPuddleAndSpendsMp()
        {
            SpellData neutralize = ScriptableObject.CreateInstance<SpellData>();
            neutralize.spellName = "neutralize";
            neutralize.mpCost = 6;

            GameObject puddleGo = new GameObject("AcidPuddle");
            puddleGo.AddComponent<BoxCollider2D>();
            var puddle = puddleGo.AddComponent<AcidPuddleController>();
            puddle.SetPlayerInRange(true);
            SetPrivateField(puddle, "_neutralizeSpells",
                new System.Collections.Generic.List<SpellData> { neutralize });

            GameObject gameManagerGo = null;
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                gameManagerGo = new GameObject("GameManager");
                gameManager = gameManagerGo.AddComponent<GameManager>();
            }
            CharacterData characterData = CreateCharacterData();
            gameManager.SetPlayerCharacterDataForTests(characterData);
            gameManager.PlayerState.SetCurrentMp(20);

            GameObject controllerGo = new GameObject("PlatformerVoiceSpellController");
            var controller = controllerGo.AddComponent<PlatformerVoiceSpellController>();
            SetPrivateField(controller, "_acidPuddles", new[] { puddle });

            var resultQueue = new ConcurrentQueue<string>();
            resultQueue.Enqueue("{\"text\": \"neutralize\"}");
            controller.Inject(resultQueue, new[] { neutralize }, gameManager.PlayerState);

            InvokePrivateMethod(controller, "Update");
            // Resolution is deferred to the cast animation's fire-frame; Update only requests the cast.
            InvokePrivateMethod(controller, "OnPlayerSpellFireFrame");

            Assert.IsTrue(puddle.IsNeutralized);
            Assert.AreEqual(14, gameManager.PlayerState.CurrentMp);

            Object.DestroyImmediate(controllerGo);
            if (gameManagerGo != null)
                Object.DestroyImmediate(gameManagerGo);
            Object.DestroyImmediate(puddleGo);
            Object.DestroyImmediate(neutralize);
            Object.DestroyImmediate(characterData);
        }

        [Test]
        public void Update_NeutralizeSpell_PuddleOutOfRange_DoesNotDissolveOrSpendMp()
        {
            // WHY: the edge-zone gate — casting near a puddle you're not in range of must
            // neither dissolve it nor waste MP.
            SpellData neutralize = ScriptableObject.CreateInstance<SpellData>();
            neutralize.spellName = "neutralize";
            neutralize.mpCost = 6;

            GameObject puddleGo = new GameObject("AcidPuddle");
            puddleGo.AddComponent<BoxCollider2D>();
            var puddle = puddleGo.AddComponent<AcidPuddleController>();
            // SetPlayerInRange NOT called -> out of range.
            SetPrivateField(puddle, "_neutralizeSpells",
                new System.Collections.Generic.List<SpellData> { neutralize });

            GameObject gameManagerGo = null;
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                gameManagerGo = new GameObject("GameManager");
                gameManager = gameManagerGo.AddComponent<GameManager>();
            }
            CharacterData characterData = CreateCharacterData();
            gameManager.SetPlayerCharacterDataForTests(characterData);
            gameManager.PlayerState.SetCurrentMp(20);

            GameObject controllerGo = new GameObject("PlatformerVoiceSpellController");
            var controller = controllerGo.AddComponent<PlatformerVoiceSpellController>();
            SetPrivateField(controller, "_acidPuddles", new[] { puddle });

            var resultQueue = new ConcurrentQueue<string>();
            resultQueue.Enqueue("{\"text\": \"neutralize\"}");
            controller.Inject(resultQueue, new[] { neutralize }, gameManager.PlayerState);

            InvokePrivateMethod(controller, "Update");
            // A fire-frame must resolve nothing when no cast was requested (out of range → no RequestCast).
            InvokePrivateMethod(controller, "OnPlayerSpellFireFrame");

            Assert.IsFalse(puddle.IsNeutralized);
            Assert.AreEqual(20, gameManager.PlayerState.CurrentMp);

            Object.DestroyImmediate(controllerGo);
            if (gameManagerGo != null)
                Object.DestroyImmediate(gameManagerGo);
            Object.DestroyImmediate(puddleGo);
            Object.DestroyImmediate(neutralize);
            Object.DestroyImmediate(characterData);
        }

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

        private static CharacterData CreateCharacterData()
        {
            CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
            data.baseMaxHP = 100;
            data.baseMaxMP = 30;
            data.baseATK = 12;
            data.baseDEF = 6;
            data.baseSPD = 8;
            return data;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);
            method.Invoke(target, null);
        }
    }
}
