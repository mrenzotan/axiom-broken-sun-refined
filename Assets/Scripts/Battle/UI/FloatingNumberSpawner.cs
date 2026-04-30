using UnityEngine;
using UnityEngine.Pool;

namespace Axiom.Battle
{
    /// <summary>
    /// Maintains an ObjectPool of FloatingNumberInstance prefabs.
    /// Call Spawn() to show a floating damage, heal, or crit number
    /// at a given RectTransform's canvas position.
    /// </summary>
    public class FloatingNumberSpawner : MonoBehaviour
    {
        public enum NumberType { Damage, Heal, Crit, Shield, Mana }

        [SerializeField]
        [Tooltip("Prefab with FloatingNumberInstance, TMP_Text, and CanvasGroup components.")]
        private FloatingNumberInstance _prefab;

        [SerializeField] private int _defaultPoolSize = 8;
        [SerializeField] private int _maxPoolSize = 20;

        private static readonly Color DamageColor = new Color(0.92f, 0.30f, 0.20f); // red
        private static readonly Color HealColor   = new Color(0.15f, 0.76f, 0.36f); // green
        private static readonly Color CritColor    = new Color(0.95f, 0.61f, 0.07f); // gold
        private static readonly Color ShieldColor  = new Color(0.28f, 0.62f, 0.95f); // blue
        private static readonly Color ManaColor    = new Color(0.28f, 0.62f, 0.95f); // blue (same visual as old Shield fallback)

        private IObjectPool<FloatingNumberInstance> _pool;

        private void Awake()
        {
            _pool = new ObjectPool<FloatingNumberInstance>(
                createFunc: CreateInstance,
                actionOnGet: instance => instance.gameObject.SetActive(true),
                actionOnRelease: instance => instance.gameObject.SetActive(false),
                actionOnDestroy: instance => Destroy(instance.gameObject),
                collectionCheck: false,
                defaultCapacity: _defaultPoolSize,
                maxSize: _maxPoolSize);
        }

        /// <summary>
        /// Spawns a floating number above the given origin slot.
        /// </summary>
        public void Spawn(RectTransform origin, int amount, NumberType type)
        {
            Color color;
            float scale;
            string label;

            switch (type)
            {
                case NumberType.Heal:
                    color = HealColor;
                    scale = 1f;
                    label = $"+{amount}";
                    break;
                case NumberType.Crit:
                    color = CritColor;
                    scale = 1.4f;
                    label = $"{amount}!";
                    break;
                case NumberType.Shield:
                    color = ShieldColor;
                    scale = 1f;
                    label = $"+{amount} Shield";
                    break;
                case NumberType.Mana:
                    color = ManaColor;
                    scale = 1f;
                    label = $"+{amount} Mana";
                    break;
                default: // Damage
                    color = DamageColor;
                    scale = 1f;
                    label = $"-{amount}";
                    break;
            }

            var instance = _pool.Get();
            instance.Play(label, color, scale, origin.position);
        }

        private FloatingNumberInstance CreateInstance()
        {
            var instance = Instantiate(_prefab, transform);
            instance.Initialize(_pool);
            instance.gameObject.SetActive(false);
            return instance;
        }
    }
}
