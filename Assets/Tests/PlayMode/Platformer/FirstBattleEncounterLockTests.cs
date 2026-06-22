using System.Collections;
using System.Reflection;
using Axiom.Platformer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlatformerPlayModeTests
{
    public class FirstBattleEncounterLockTests
    {
        private GameObject _player;
        private GameObject _trigger;

        [TearDown]
        public void TearDown()
        {
            if (_trigger != null) Object.DestroyImmediate(_trigger);
            if (_player != null) Object.DestroyImmediate(_player);
        }

        [UnityTest]
        public IEnumerator FirstBattleIntro_EnterLocksMovementAndAttack_ReleaseRestoresBoth()
        {
            _player = new GameObject("Player");
            _player.SetActive(false);
            _player.tag = "Player";
            _player.AddComponent<Rigidbody2D>();
            BoxCollider2D playerCollider = _player.AddComponent<BoxCollider2D>();
            var groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(_player.transform);

            PlayerController playerController = _player.AddComponent<PlayerController>();
            SetField(playerController, "groundCheck", groundCheck.transform);
            PlayerExplorationAttack playerAttack = _player.AddComponent<PlayerExplorationAttack>();

            _trigger = new GameObject("Tutorial_Surprised");
            _trigger.SetActive(false);
            BoxCollider2D triggerCollider = _trigger.AddComponent<BoxCollider2D>();
            triggerCollider.isTrigger = true;
            TutorialPromptTrigger tutorialPrompt = _trigger.AddComponent<TutorialPromptTrigger>();
            SetField(tutorialPrompt, "_lockMovementWhileInside", true);
            SetField(tutorialPrompt, "_lockAttackWhileInside", true);
            SetField(tutorialPrompt, "_playerController", playerController);
            SetField(tutorialPrompt, "_playerAttack", playerAttack);

            _player.SetActive(true);
            _trigger.SetActive(true);
            _trigger.SendMessage("OnTriggerEnter2D", playerCollider);

            Assert.IsTrue(playerController.IsMovementLocked,
                "Kaelen must not move away while the first enemy closes the gap.");
            Assert.IsTrue(playerAttack.IsInputLocked,
                "Attack would convert the required Surprised contact into an Advantaged battle.");

            tutorialPrompt.ReleasePlayerLock();
            Assert.IsFalse(playerController.IsMovementLocked);
            Assert.IsFalse(playerAttack.IsInputLocked);

            tutorialPrompt.ReleasePlayerLock();
            Assert.IsFalse(playerController.IsMovementLocked,
                "Transition release must be idempotent with trigger exit or teardown.");

            SetField(tutorialPrompt, "_lockAttackWhileInside", false);
            _trigger.SendMessage("OnTriggerEnter2D", playerCollider);
            Assert.IsTrue(playerController.IsMovementLocked);
            Assert.IsFalse(playerAttack.IsInputLocked,
                "Existing movement-only tutorial zones must continue permitting attack.");
            tutorialPrompt.ReleasePlayerLock();

            yield return null;
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
