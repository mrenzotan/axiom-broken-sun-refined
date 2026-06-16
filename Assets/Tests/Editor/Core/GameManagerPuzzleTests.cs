using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace CoreTests
{
    public class GameManagerPuzzleTests
    {
        private GameObject _gameManagerObject;
        private GameManager _gameManager;

        [SetUp]
        public void SetUp()
        {
            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();
            _gameManager.SetPlayerCharacterDataForTests(CreateTestCharacterData());
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameManagerObject != null)
                Object.DestroyImmediate(_gameManagerObject);
        }

        [Test]
        public void IsPuzzleSolved_ReturnsFalse_ByDefault()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            Assert.IsFalse(_gameManager.IsPuzzleSolved("ice_wall_a"));
        }

        [Test]
        public void IsPuzzleSolved_ReturnsFalse_ForNullOrEmpty()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            Assert.IsFalse(_gameManager.IsPuzzleSolved(null));
            Assert.IsFalse(_gameManager.IsPuzzleSolved(string.Empty));
        }

        [Test]
        public void MarkPuzzleSolved_ThenIsPuzzleSolved_ReturnsTrue_InSameScene()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            _gameManager.MarkPuzzleSolved("ice_wall_a");
            Assert.IsTrue(_gameManager.IsPuzzleSolved("ice_wall_a"));
        }

        [Test]
        public void MarkPuzzleSolved_IsScopedPerScene()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            _gameManager.MarkPuzzleSolved("ice_wall_a");

            _gameManager.PlayerState.SetActiveScene("Level_2-1");
            Assert.IsFalse(_gameManager.IsPuzzleSolved("ice_wall_a"),
                "Same puzzle ID in a different scene must not read as solved.");
        }

        [Test]
        public void MarkPuzzleSolved_NullOrEmpty_IsIgnored()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            _gameManager.MarkPuzzleSolved(null);
            _gameManager.MarkPuzzleSolved(string.Empty);
            Assert.IsFalse(_gameManager.IsPuzzleSolved(null));
            Assert.IsFalse(_gameManager.IsPuzzleSolved(string.Empty));
        }

        [Test]
        public void ClearSolvedPuzzles_RemovesEverything()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            _gameManager.MarkPuzzleSolved("ice_wall_a");
            _gameManager.ClearSolvedPuzzles();
            Assert.IsFalse(_gameManager.IsPuzzleSolved("ice_wall_a"));
        }

        [Test]
        public void ClearSolvedPuzzlesInScene_LeavesOtherScenesIntact()
        {
            _gameManager.MarkPuzzleSolvedInScene("Level_1-1", "ice_wall_a");
            _gameManager.MarkPuzzleSolvedInScene("Level_1-2", "ice_wall_b");

            _gameManager.ClearSolvedPuzzlesInScene("Level_1-1");

            Assert.IsFalse(_gameManager.IsPuzzleSolvedInScene("Level_1-1", "ice_wall_a"),
                "Respawn-scene puzzles must re-form.");
            Assert.IsTrue(_gameManager.IsPuzzleSolvedInScene("Level_1-2", "ice_wall_b"),
                "Puzzles in other already-cleared scenes stay solved.");
        }

        private CharacterData CreateTestCharacterData(
            int maxHp = 100, int maxMp = 50, int atk = 10, int def = 5, int spd = 8)
        {
            var cd = ScriptableObject.CreateInstance<CharacterData>();
            cd.characterName = "TestPlayer";
            cd.baseMaxHP = maxHp;
            cd.baseMaxMP = maxMp;
            cd.baseATK = atk;
            cd.baseDEF = def;
            cd.baseSPD = spd;
            return cd;
        }
    }
}
