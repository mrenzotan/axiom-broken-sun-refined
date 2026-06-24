using System.Reflection;
using Axiom.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Axiom.Tests.UI
{
    public class StatusMessageUITests
    {
        private GameObject _root;
        private GameObject _messageObject;
        private GameObject _backgroundObject;
        private GameObject _textObject;
        private GameObject _continueObject;
        private StatusMessageUI _ui;
        private Button _continueButton;
        private Component _text;
        private GameObject _eventSystemObject;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("StatusMessageUITestRoot");
            _root.SetActive(false);

            _eventSystemObject = new GameObject("EventSystem");
            _eventSystemObject.AddComponent<EventSystem>();

            _backgroundObject = new GameObject("MessageLogBG");

            _messageObject = new GameObject("MessageLog");
            _messageObject.transform.SetParent(_root.transform);

            _textObject = new GameObject("Text");
            _textObject.transform.SetParent(_messageObject.transform);
            _text = _textObject.AddComponent(GetTmpTextType());

            _continueObject = new GameObject("Continue");
            _continueObject.transform.SetParent(_messageObject.transform);
            _continueButton = _continueObject.AddComponent<Button>();

            _ui = _root.AddComponent<StatusMessageUI>();
            SetField("_text", _text);
            SetField("_messageRoot", _messageObject);
            SetField("_backgroundRoot", _backgroundObject);
            SetField("_continueButton", _continueButton);
            SetField("_charactersPerSecond", 30f);

            InvokeLifecycle("Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);

            if (_backgroundObject != null)
                Object.DestroyImmediate(_backgroundObject);

            if (_eventSystemObject != null)
                Object.DestroyImmediate(_eventSystemObject);
        }

        [Test]
        public void InitialState_HidesMessageBoxAndBackground()
        {
            Assert.IsFalse(_messageObject.activeSelf,
                "The shared message box should be hidden until battle narration or voice spell text is active.");
            Assert.IsFalse(_backgroundObject.activeSelf);
        }

        [Test]
        public void ShowSpellPrompt_ShowsMessageBoxAndHidesContinueButton()
        {
            _ui.ShowSpellPrompt("Hold [Left Shift] to speak a spell");

            Assert.IsTrue(_messageObject.activeSelf);
            Assert.IsTrue(_backgroundObject.activeSelf);
            Assert.IsFalse(_continueObject.activeSelf,
                "Voice spell prompts share the message box, but they are not acknowledgment-gated logs.");
            Assert.IsFalse(_continueButton.interactable);
            Assert.AreEqual("Hold [Left Shift] to speak a spell", GetText());
        }

        [Test]
        public void ClearSpellPrompt_WithNoQueuedLog_HidesMessageBoxAndBackground()
        {
            _ui.ShowSpellPrompt("Hold [Left Shift] to speak a spell");

            _ui.ClearSpellPrompt();

            Assert.IsFalse(_messageObject.activeSelf);
            Assert.IsFalse(_backgroundObject.activeSelf);
        }

        [Test]
        public void Continue_WhileSpellPromptVisible_DoesNotAdvanceQueuedBattleLog()
        {
            _ui.Post("Battle log line");

            _ui.ShowSpellPrompt("Hold [Left Shift] to speak a spell");
            _ui.Continue();
            _ui.ClearSpellPrompt();

            Assert.AreEqual(string.Empty, GetText(),
                "Continue belongs to battle logs only; pressing it during a voice prompt must not reveal or advance unread narration.");
        }

        [Test]
        public void ClearSpellPrompt_WithQueuedLog_SelectsContinueButtonForKeyboardNavigation()
        {
            _ui.ShowSpellPrompt("Hold [Left Shift] to speak a spell");
            _ui.Post("Queued battle log line");

            _ui.ClearSpellPrompt();

            Assert.AreSame(_continueObject, EventSystem.current.currentSelectedGameObject,
                "When a hidden queued battle log becomes visible, keyboard/controller submit should immediately activate Continue.");
        }

        private static System.Type GetTmpTextType()
        {
            return System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
        }

        private void SetField(string fieldName, object value)
        {
            typeof(StatusMessageUI)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_ui, value);
        }

        private void InvokeLifecycle(string methodName)
        {
            typeof(StatusMessageUI)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_ui, null);
        }

        private string GetText()
        {
            return (string)_text.GetType().GetProperty("text").GetValue(_text);
        }
    }
}
