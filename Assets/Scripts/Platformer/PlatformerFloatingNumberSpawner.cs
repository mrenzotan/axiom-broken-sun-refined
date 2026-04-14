using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// World-space pooled floating-number spawner for the platformer scene.
    /// Attach to a persistent GameObject in the Platformer scene.
    /// </summary>
    public class PlatformerFloatingNumberSpawner : MonoBehaviour
    {
        [SerializeField] private PlatformerFloatingNumberInstance _prefab;
        [SerializeField] private int _poolSize = 8;

        private readonly Queue<PlatformerFloatingNumberInstance> _pool = new Queue<PlatformerFloatingNumberInstance>();

        private const float MpVerticalOffset = 0.6f;

        private void Awake()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                PlatformerFloatingNumberInstance instance = Instantiate(_prefab, transform);
                instance.gameObject.SetActive(false);
                _pool.Enqueue(instance);
            }
        }

        /// <summary>
        /// Spawns HP (green) and MP (cyan) floating numbers at worldPosition.
        /// Shows the healed amount, or "HP MAX"/"MP MAX" if already at full.
        /// </summary>
        public void SpawnHealNumbers(Vector2 worldPosition, int healedHp, int healedMp)
        {
            if (_prefab == null)
            {
                Debug.LogWarning("[PlatformerFloatingNumberSpawner] Prefab not assigned.", this);
                return;
            }

            string hpText = healedHp > 0 ? $"+{healedHp}" : "HP MAX";
            Spawn(worldPosition, hpText, Color.green);

            string mpText = healedMp > 0 ? $"+{healedMp}" : "MP MAX";
            Spawn(new Vector2(worldPosition.x, worldPosition.y + MpVerticalOffset), mpText, Color.cyan);
        }

        private void Spawn(Vector2 position, string text, Color color)
        {
            PlatformerFloatingNumberInstance instance = GetFromPool();
            instance.transform.position = position;
            instance.gameObject.SetActive(true);
            instance.Play(text, color, ReturnToPool);
        }

        private PlatformerFloatingNumberInstance GetFromPool()
        {
            if (_pool.Count > 0)
                return _pool.Dequeue();

            return Instantiate(_prefab, transform);
        }

        private void ReturnToPool(PlatformerFloatingNumberInstance instance)
        {
            instance.gameObject.SetActive(false);
            _pool.Enqueue(instance);
        }
    }
}
