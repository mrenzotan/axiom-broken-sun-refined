using NUnit.Framework;
using UnityEngine;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Tests.Editor.Core
{
    public class GameManagerTransitionTests
    {
        private GameObject _go;
        private GameManager _gm;

        [SetUp]
        public void SetUp()
        {
            // Destroy any stale Instance from an interrupted previous run so the
            // singleton guard in Awake never fires unexpectedly.
            if (GameManager.Instance != null)
                Object.DestroyImmediate(GameManager.Instance.gameObject);

            _go = new GameObject("GameManager");
            _gm = _go.AddComponent<GameManager>();
            _gm.SetPlayerCharacterDataForTests(CreateTestCharacterData());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        private CharacterData CreateTestCharacterData()
        {
            var cd = ScriptableObject.CreateInstance<CharacterData>();
            cd.characterName = "TestPlayer";
            cd.baseMaxHP = 100;
            cd.baseMaxMP = 50;
            cd.baseATK   = 10;
            cd.baseDEF   = 5;
            cd.baseSPD   = 8;
            return cd;
        }

        [Test]
        public void SceneTransition_IsNull_WhenNoChildController()
        {
            // GameManager has no SceneTransitionController child — should be null.
            Assert.IsNull(_gm.SceneTransition);
        }

        [Test]
        public void OnSceneReady_IsRaised_ByRaiseSceneReady()
        {
            bool fired = false;
            _gm.OnSceneReady += () => fired = true;

            _gm.RaiseSceneReady();

            Assert.IsTrue(fired);
        }

        [Test]
        public void RaiseSceneReady_DoesNotThrow_WhenNoSubscribers()
        {
            Assert.DoesNotThrow(() => _gm.RaiseSceneReady());
        }

        [Test]
        public void ConsumePendingCutsceneReturnToWorld_DefaultsFalse()
        {
            Assert.IsFalse(_gm.ConsumePendingCutsceneReturnToWorld());
        }

        [Test]
        public void BeginCutscene_WithReturnToWorld_StoresFlagUntilConsumed()
        {
            var cutscene = ScriptableObject.CreateInstance<CutsceneData>();

            _gm.BeginCutscene(cutscene, returnToWorldOnComplete: true);

            Assert.IsTrue(_gm.ConsumePendingCutsceneReturnToWorld(),
                "Boss victory cutscenes must return to the originating level after completion.");
            Assert.IsFalse(_gm.ConsumePendingCutsceneReturnToWorld(),
                "The return-to-world flag is one-shot so later cutscenes can follow nextSceneName.");
            Object.DestroyImmediate(cutscene);
        }

        [Test]
        public void BeginCutscene_WithoutReturnToWorld_DoesNotSetFlag()
        {
            var cutscene = ScriptableObject.CreateInstance<CutsceneData>();

            _gm.BeginCutscene(cutscene);

            Assert.IsFalse(_gm.ConsumePendingCutsceneReturnToWorld(),
                "Opening and normal story cutscenes should continue to use CutsceneData.nextSceneName.");
            Object.DestroyImmediate(cutscene);
        }

        [Test]
        public void SceneTransition_IsAssigned_WhenChildControllerExists()
        {
            // Destroy and recreate so Awake runs with the child present.
            Object.DestroyImmediate(_go);
            _go = new GameObject("GameManager");
            var childGo = new GameObject("Child");
            childGo.transform.SetParent(_go.transform);
            childGo.AddComponent<SceneTransitionController>();
            _gm = _go.AddComponent<GameManager>();

            Assert.IsNotNull(_gm.SceneTransition);
        }
    }
}
