using UnityEngine;

namespace Axiom.Platformer
{
    [RequireComponent(typeof(Collider2D))]
    public class BurnableObstacleProximityForwarder : MonoBehaviour
    {
        [SerializeField] private BurnableObstacleController _controller;

        private PlayerAuraCue _auraCue;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
            if (_controller == null)
                _controller = GetComponentInParent<BurnableObstacleController>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_controller == null) return;
            _controller.SetPlayerInRange(true);

            _auraCue = other.GetComponentInParent<PlayerAuraCue>();
            if (_auraCue != null) _auraCue.EnterPuzzleRange(_controller);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_controller == null) return;
            _controller.SetPlayerInRange(false);

            if (_auraCue != null)
            {
                _auraCue.ExitPuzzleRange(_controller);
                _auraCue = null;
            }
        }

        private void OnDisable()
        {
            if (_auraCue != null)
            {
                if (_controller != null) _auraCue.ExitPuzzleRange(_controller);
                _auraCue = null;
            }
        }
    }
}
