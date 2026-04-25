using UnityEngine;
using UnityEngine.InputSystem;

namespace Axiom.Platformer
{
    [RequireComponent(typeof(MeltableObstacleController))]
    public class MeltableObstacleDebugCaster : MonoBehaviour
    {
        [SerializeField] private InputActionReference _debugMeltAction;
        [SerializeField] private string _debugSpellId = "combust";

        private MeltableObstacleController _controller;

        private void Awake()
        {
            _controller = GetComponent<MeltableObstacleController>();
        }

        private void OnEnable()
        {
            if (_debugMeltAction == null || _debugMeltAction.action == null)
            {
                Debug.LogError(
                    "[MeltableObstacleDebugCaster] _debugMeltAction is not assigned or its action is null. " +
                    "Disabling component.", this);
                enabled = false;
                return;
            }

            _debugMeltAction.action.performed += OnDebugMeltPerformed;
            _debugMeltAction.action.Enable();
        }

        private void OnDisable()
        {
            if (_debugMeltAction == null || _debugMeltAction.action == null) return;
            _debugMeltAction.action.performed -= OnDebugMeltPerformed;
            _debugMeltAction.action.Disable();
        }

        private void OnDebugMeltPerformed(InputAction.CallbackContext _)
        {
            if (_controller == null) return;
            _controller.TryMelt(_debugSpellId);
        }
    }
}
