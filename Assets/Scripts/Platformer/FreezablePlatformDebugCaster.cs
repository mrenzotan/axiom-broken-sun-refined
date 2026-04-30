using UnityEngine;
using UnityEngine.InputSystem;

namespace Axiom.Platformer
{
    [RequireComponent(typeof(FreezablePlatformController))]
    public class FreezablePlatformDebugCaster : MonoBehaviour
    {
        [SerializeField] private InputActionReference _debugFreezeAction;
        [SerializeField] private string _debugSpellId = "freeze";

        private FreezablePlatformController _controller;

        private void Awake()
        {
            _controller = GetComponent<FreezablePlatformController>();
        }

        private void OnEnable()
        {
            if (_debugFreezeAction == null || _debugFreezeAction.action == null)
            {
                Debug.LogError(
                    "[FreezablePlatformDebugCaster] _debugFreezeAction is not assigned or its action is null. " +
                    "Disabling component.", this);
                enabled = false;
                return;
            }

            _debugFreezeAction.action.performed += OnDebugFreezePerformed;
            _debugFreezeAction.action.Enable();
        }

        private void OnDisable()
        {
            if (_debugFreezeAction == null || _debugFreezeAction.action == null) return;
            _debugFreezeAction.action.performed -= OnDebugFreezePerformed;
            _debugFreezeAction.action.Disable();
        }

        private void OnDebugFreezePerformed(InputAction.CallbackContext _)
        {
            if (_controller == null) return;
            _controller.TryFreeze(_debugSpellId);
        }
    }
}
