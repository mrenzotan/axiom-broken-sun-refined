using System.Reflection;
using Axiom.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Axiom.Tests.UI
{
    public class ActionMenuUIMessageBlockTests
    {
        private GameObject _root;
        private GameObject _eventSystemObject;
        private ActionMenuUI _menu;
        private Button[] _buttons;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ActionMenu");
            _menu = _root.AddComponent<ActionMenuUI>();
            _buttons = new[]
            {
                CreateButton("Attack"),
                CreateButton("Spell"),
                CreateButton("Item"),
                CreateButton("Flee"),
                CreateButton("SpellList")
            };

            SetButtonField("_attackButton", _buttons[0]);
            SetButtonField("_spellButton", _buttons[1]);
            SetButtonField("_itemButton", _buttons[2]);
            SetButtonField("_fleeButton", _buttons[3]);
            SetButtonField("_spellListButton", _buttons[4]);
        }

        [TearDown]
        public void TearDown()
        {
            if (_eventSystemObject != null)
            {
                InvokeEventSystemLifecycle(_eventSystemObject.GetComponent<EventSystem>(), "OnDisable");
                Object.DestroyImmediate(_eventSystemObject);
            }
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void SetMessageBlocked_True_DisablesAllButtons()
        {
            _menu.SetMessageBlocked(true);

            foreach (Button button in _buttons)
                Assert.IsFalse(button.interactable, $"{button.name} should be disabled.");
        }

        [Test]
        public void SetMessageBlocked_RepeatedTrue_RestoresExactPriorState()
        {
            bool[] priorState = { true, false, true, false, true };
            for (int i = 0; i < _buttons.Length; i++)
                _buttons[i].interactable = priorState[i];

            _menu.SetMessageBlocked(true);
            _menu.SetMessageBlocked(true);
            _menu.SetMessageBlocked(false);

            for (int i = 0; i < _buttons.Length; i++)
                Assert.AreEqual(priorState[i], _buttons[i].interactable,
                    $"{_buttons[i].name} should return to its pre-message state.");
        }

        [Test]
        public void SetMessageBlocked_False_FocusesFirstRestoredInteractableButton()
        {
            _eventSystemObject = new GameObject("EventSystem");
            EventSystem eventSystem = _eventSystemObject.AddComponent<EventSystem>();
            InvokeEventSystemLifecycle(eventSystem, "OnEnable");
            Assert.AreSame(eventSystem, EventSystem.current, "Test EventSystem should be registered before focus is tested.");
            _buttons[0].interactable = false;

            _menu.SetMessageBlocked(true);
            _menu.SetMessageBlocked(false);

            Assert.AreSame(_buttons[1].gameObject, eventSystem.currentSelectedGameObject);
        }

        private static void InvokeEventSystemLifecycle(EventSystem eventSystem, string methodName)
        {
            MethodInfo method = typeof(EventSystem).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing EventSystem.{methodName} lifecycle method.");
            method.Invoke(eventSystem, null);
        }

        private Button CreateButton(string name)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_root.transform);
            return buttonObject.GetComponent<Button>();
        }

        private void SetButtonField(string fieldName, Button button)
        {
            FieldInfo field = typeof(ActionMenuUI).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing serialized field {fieldName}.");
            field.SetValue(_menu, button);
        }
    }
}
