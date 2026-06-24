using System.Collections;
using System.Reflection;
using Axiom.Core;
using Axiom.Data;
using Axiom.Platformer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace PlatformerPlayModeTests
{
    public class PlatformerDeathAndHazardRegressionTests
    {
        private GameObject _gameManagerObject;
        private CharacterData _characterData;
        private GameObject _player;
        private GameObject _hazard;
        private GameObject _deathHandlerObject;
        private GameObject _defeatPanel;

        [SetUp]
        public void SetUp()
        {
            if (GameManager.Instance != null)
                Object.DestroyImmediate(GameManager.Instance.gameObject);

            _gameManagerObject = new GameObject("GameManager");
            GameManager gameManager = _gameManagerObject.AddComponent<GameManager>();
            _characterData = ScriptableObject.CreateInstance<CharacterData>();
            _characterData.baseMaxHP = 100;
            _characterData.baseMaxMP = 30;
            _characterData.baseATK = 12;
            _characterData.baseDEF = 6;
            _characterData.baseSPD = 8;
            gameManager.SetPlayerCharacterDataForTests(_characterData);
        }

        [TearDown]
        public void TearDown()
        {
            if (_deathHandlerObject != null) Object.DestroyImmediate(_deathHandlerObject);
            if (_defeatPanel != null) Object.DestroyImmediate(_defeatPanel);
            if (_hazard != null) Object.DestroyImmediate(_hazard);
            if (_player != null) Object.DestroyImmediate(_player);
            if (_gameManagerObject != null) Object.DestroyImmediate(_gameManagerObject);
            if (_characterData != null) Object.DestroyImmediate(_characterData);
        }

        [UnityTest]
        public IEnumerator DeathHandler_DisablesExplorationAttack_WhenHazardDeathShowsDefeatPanel()
        {
            _player = CreatePlayer();
            PlayerExplorationAttack attack = _player.GetComponent<PlayerExplorationAttack>();

            _deathHandlerObject = new GameObject("PlayerDeathHandler");
            PlayerDeathHandler deathHandler = _deathHandlerObject.AddComponent<PlayerDeathHandler>();
            SetField(deathHandler, "_deathAnimSeconds", 0f);
            SetField(deathHandler, "_defeatPanel", CreateDefeatPanel());

            GameManager.Instance.PlayerState.SetCurrentHp(0);
            yield return null;

            Assert.IsFalse(attack.enabled,
                "A dead Kaelen must not keep reading Enter/Attack after the death sequence starts.");
        }

        [UnityTest]
        public IEnumerator HazardTrigger_StopsDamageTicks_AfterFatalDamageBeforeRespawn()
        {
            _player = CreateHazardOnlyPlayer();
            GameManager.Instance.PlayerState.SetCurrentHp(10);

            _hazard = new GameObject("SpikeHazard");
            BoxCollider2D hazardCollider = _hazard.AddComponent<BoxCollider2D>();
            hazardCollider.isTrigger = true;
            HazardTrigger hazard = _hazard.AddComponent<HazardTrigger>();
            SetField(hazard, "_firstHitDamagePercent", 0);
            SetField(hazard, "_damagePerTickPercent", 10);
            SetField(hazard, "_tickIntervalSeconds", 0.05f);

            BoxCollider2D playerCollider = _player.GetComponent<BoxCollider2D>();
            _hazard.SendMessage("OnTriggerEnter2D", playerCollider);

            yield return new WaitForSeconds(0.08f);
            Assert.AreEqual(0, GameManager.Instance.PlayerState.CurrentHp,
                "The first DoT tick should be fatal for the regression setup.");

            GameManager.Instance.PlayerState.SetCurrentHp(GameManager.Instance.PlayerState.MaxHp);
            yield return new WaitForSeconds(0.08f);

            Assert.AreEqual(GameManager.Instance.PlayerState.MaxHp, GameManager.Instance.PlayerState.CurrentHp,
                "Respawn must not be damaged by a DoT coroutine from the pre-death overlap.");
        }

        private static GameObject CreatePlayer()
        {
            GameObject player = new GameObject("Player");
            player.SetActive(false);
            player.tag = "Player";
            player.AddComponent<Rigidbody2D>();
            player.AddComponent<BoxCollider2D>();
            GameObject groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(player.transform);
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(player.transform);
            visuals.AddComponent<Animator>();

            PlayerController controller = player.AddComponent<PlayerController>();
            SetField(controller, "groundCheck", groundCheck.transform);
            player.AddComponent<PlayerExplorationAttack>();
            player.SetActive(true);
            controller.enabled = false;
            return player;
        }

        private static GameObject CreateHazardOnlyPlayer()
        {
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            player.AddComponent<Rigidbody2D>();
            player.AddComponent<BoxCollider2D>();
            return player;
        }

        private GameObject CreateDefeatPanel()
        {
            _defeatPanel = new GameObject("DefeatPanel");
            _defeatPanel.SetActive(false);
            GameObject buttonObject = new GameObject("ContinueButton");
            buttonObject.transform.SetParent(_defeatPanel.transform);
            buttonObject.AddComponent<RectTransform>();
            buttonObject.AddComponent<Button>();
            return _defeatPanel;
        }

        private static void SetField<T>(object target, string name, T value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing serialized field {name}");
            field.SetValue(target, value);
        }
    }
}
