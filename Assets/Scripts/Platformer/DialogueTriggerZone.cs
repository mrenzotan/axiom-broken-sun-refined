using UnityEngine;
using UnityEngine.InputSystem;
using Axiom.Data;

namespace Axiom.Platformer
{
    /// <summary>
    /// MonoBehaviour trigger zone that starts a cutscene when the player is in range
    /// and presses the interact button (E).
    /// Blocks player movement and voice input during the cutscene, then restores control when done.
    ///
    /// Attach to a GameObject with a Collider2D (Is Trigger enabled) and a DialogueTriggerZone script.
    /// Assign a CutsceneSequence asset in the Inspector.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DialogueTriggerZone : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Cutscene sequence to play when the player presses interact in this zone.")]
        private CutsceneSequence _cutsceneSequence;

        private bool _playerInZone;
        private CutsceneController _cutsceneController;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInZone = true;
            Debug.Log("[DialogueTriggerZone] Player entered zone. Press E to interact.");
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInZone = false;
            Debug.Log("[DialogueTriggerZone] Player left zone.");
        }

        private void Update()
        {
            if (!_playerInZone) return;

            // Check for interact input using new Input System.
            // Using E key from keyboard.
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                TriggerCutscene();
            }
        }

        private void TriggerCutscene()
        {
            if (_cutsceneSequence == null)
            {
                Debug.LogWarning("[DialogueTriggerZone] No cutscene sequence assigned.", this);
                return;
            }

            // Get or create the cutscene controller.
            _cutsceneController = FindAnyObjectByType<CutsceneController>();
            if (_cutsceneController == null)
            {
                Debug.LogError(
                    "[DialogueTriggerZone] CutsceneController not found in the scene. " +
                    "Attach a CutsceneController MonoBehaviour to manage cutscenes.",
                    this);
                return;
            }

            Debug.Log("[DialogueTriggerZone] Starting cutscene!");
            // Start the cutscene.
            _cutsceneController.StartCutscene(_cutsceneSequence);
            
            // Disable further triggers after starting.
            _playerInZone = false;
        }
    }
}
