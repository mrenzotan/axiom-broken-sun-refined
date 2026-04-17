// Assets/Tests/Editor/Core/GameManagerWorldSnapshotTests.cs
using NUnit.Framework;
using UnityEngine;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Tests.Editor.Core
{
    public class GameManagerWorldSnapshotTests
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
            _gm = _go.AddComponent<GameManager>(); // triggers Awake → sets Instance
            _gm.SetPlayerCharacterDataForTests(CreateTestCharacterData());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go); // triggers OnDestroy → clears Instance
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
        public void CurrentWorldSnapshot_IsNullByDefault()
        {
            Assert.IsNull(_gm.CurrentWorldSnapshot);
        }

        [Test]
        public void SetWorldSnapshot_StoresSnapshot()
        {
            var snapshot = new WorldSnapshot();

            _gm.SetWorldSnapshot(snapshot);

            Assert.AreSame(snapshot, _gm.CurrentWorldSnapshot);
        }

        [Test]
        public void SetWorldSnapshot_ReplacesExistingSnapshot()
        {
            var first  = new WorldSnapshot();
            var second = new WorldSnapshot();
            _gm.SetWorldSnapshot(first);

            _gm.SetWorldSnapshot(second);

            Assert.AreSame(second, _gm.CurrentWorldSnapshot);
        }

        [Test]
        public void ClearWorldSnapshot_SetsPropertyToNull()
        {
            _gm.SetWorldSnapshot(new WorldSnapshot());

            _gm.ClearWorldSnapshot();

            Assert.IsNull(_gm.CurrentWorldSnapshot);
        }

        [Test]
        public void ClearWorldSnapshot_IsNoOp_WhenAlreadyNull()
        {
            Assert.DoesNotThrow(() => _gm.ClearWorldSnapshot());
        }
    }
}
