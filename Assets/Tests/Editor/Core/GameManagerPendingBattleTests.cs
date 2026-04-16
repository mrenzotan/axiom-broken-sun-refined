using NUnit.Framework;
using UnityEngine;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Tests.Editor.Core
{
    public class GameManagerPendingBattleTests
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

            // AddComponent triggers Awake, which sets GameManager.Instance.
            _go = new GameObject("GameManager");
            _gm = _go.AddComponent<GameManager>();
            _gm.SetPlayerCharacterDataForTests(CreateTestCharacterData());
        }

        [TearDown]
        public void TearDown()
        {
            // DestroyImmediate triggers OnDestroy synchronously, which clears GameManager.Instance.
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
        public void PendingBattle_IsNullByDefault()
        {
            Assert.IsNull(_gm.PendingBattle);
        }

        [Test]
        public void SetPendingBattle_StoresEntry()
        {
            var entry = new BattleEntry(CombatStartState.Advantaged, enemyData: null);

            _gm.SetPendingBattle(entry);

            Assert.AreSame(entry, _gm.PendingBattle);
        }

        [Test]
        public void SetPendingBattle_ReplacesExistingEntry()
        {
            var first  = new BattleEntry(CombatStartState.Advantaged, enemyData: null);
            var second = new BattleEntry(CombatStartState.Surprised,  enemyData: null);

            _gm.SetPendingBattle(first);
            _gm.SetPendingBattle(second);

            Assert.AreSame(second, _gm.PendingBattle);
        }

        [Test]
        public void ClearPendingBattle_NullsProperty()
        {
            var entry = new BattleEntry(CombatStartState.Surprised, enemyData: null);
            _gm.SetPendingBattle(entry);

            _gm.ClearPendingBattle();

            Assert.IsNull(_gm.PendingBattle);
        }

        [Test]
        public void ClearPendingBattle_IsNoOp_WhenAlreadyNull()
        {
            Assert.DoesNotThrow(() => _gm.ClearPendingBattle());
        }
    }
}
