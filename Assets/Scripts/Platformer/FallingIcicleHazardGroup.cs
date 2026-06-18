using System;
using System.Collections.Generic;
using Axiom.Core;
using UnityEngine;

namespace Axiom.Platformer
{
    public enum FallingIcicleState
    {
        Idle,
        Warning,
        Falling,
        Disappeared,
    }

    /// <summary>
    /// Plain C# state for one-shot falling icicles. Unity object movement and
    /// collision queries stay in FallingIcicleHazardGroup.
    /// </summary>
    public sealed class FallingIcicleLogic
    {
        private float _warningElapsed;

        public FallingIcicleState State { get; private set; } = FallingIcicleState.Idle;

        public bool TryStartWarning(bool playerInPath)
        {
            if (!playerInPath || State != FallingIcicleState.Idle)
                return false;

            _warningElapsed = 0f;
            State = FallingIcicleState.Warning;
            return true;
        }

        public bool TickWarning(float deltaTime, float warningSeconds)
        {
            if (State != FallingIcicleState.Warning)
                return false;

            _warningElapsed += Math.Max(0f, deltaTime);
            if (_warningElapsed < Math.Max(0.01f, warningSeconds))
                return false;

            State = FallingIcicleState.Falling;
            return true;
        }

        public void MarkDisappeared()
        {
            if (State == FallingIcicleState.Falling)
                State = FallingIcicleState.Disappeared;
        }
    }

    public static class FallingIcicleAssignment
    {
        public static int CountAssignedIcicles(IReadOnlyList<SpriteRenderer> assignedIcicles)
        {
            if (assignedIcicles == null)
                return 0;

            int count = 0;
            for (int i = 0; i < assignedIcicles.Count; i++)
            {
                if (assignedIcicles[i] != null)
                    count++;
            }

            return count;
        }
    }

    /// <summary>
    /// Attach one instance to a scene object, then assign only the SpriteRenderers
    /// that should behave as one-shot falling icicle hazards.
    /// </summary>
    [DisallowMultipleComponent]
    public class FallingIcicleHazardGroup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Only these assigned decorative icicles become hazards. Unassigned icicles stay decorative.")]
        private List<SpriteRenderer> _hazardIcicles = new List<SpriteRenderer>();

        [SerializeField, Min(0.05f)]
        [Tooltip("Half-width of the vertical detection path under each icicle.")]
        private float _pathHalfWidth = 0.35f;

        [SerializeField, Min(0.5f)]
        [Tooltip("How far each icicle can fall before it is treated as touching ground and disappears.")]
        private float _pathLength = 14f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds the warning line is shown before the icicle falls.")]
        private float _warningSeconds = 0.45f;

        [SerializeField, Min(0.1f)]
        [Tooltip("World units per second while falling.")]
        private float _fallSpeed = 18f;

        [SerializeField, Range(0, 100)]
        [Tooltip("Percent of max HP dealt once if the falling icicle hits the player.")]
        private int _hitDamagePercent = 20;

        [SerializeField]
        [Tooltip("Overlap box size used while the icicle is falling.")]
        private Vector2 _damageBoxSize = new Vector2(0.9f, 1.2f);

        [SerializeField]
        [Tooltip("Overlap box offset from the icicle transform while falling.")]
        private Vector2 _damageBoxOffset = Vector2.zero;

        [SerializeField]
        [Tooltip("Optional player layer mask. If empty, the project Player layer is used.")]
        private LayerMask _playerDetectionLayers;

        [SerializeField]
        private Color _warningColor = new Color(0.2f, 0.85f, 1f, 0.9f);

        [SerializeField, Range(0.01f, 0.25f)]
        private float _warningLineWidth = 0.06f;

        private readonly List<FallingIcicleInstance> _activeIcicles = new List<FallingIcicleInstance>();
        private readonly Collider2D[] _overlapResults = new Collider2D[8];
        private Material _warningMaterial;
        private int _effectivePlayerLayerMask;
        private ContactFilter2D _playerContactFilter;

        private void Awake()
        {
            _effectivePlayerLayerMask = ResolvePlayerLayerMask();
            _playerContactFilter = new ContactFilter2D();
            _playerContactFilter.SetLayerMask(_effectivePlayerLayerMask);
            _playerContactFilter.useTriggers = true;
            _warningMaterial = CreateWarningMaterial();
            BuildIcicleInstances();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            for (int i = 0; i < _activeIcicles.Count; i++)
                _activeIcicles[i].Tick(this, deltaTime);
        }

        private void OnDestroy()
        {
            if (_warningMaterial != null)
                Destroy(_warningMaterial);
        }

        private int ResolvePlayerLayerMask()
        {
            if (_playerDetectionLayers.value != 0)
                return _playerDetectionLayers.value;

            int playerMask = LayerMask.GetMask("Player");
            return playerMask != 0 ? playerMask : ~0;
        }

        private Material CreateWarningMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            return shader != null ? new Material(shader) : null;
        }

        private void BuildIcicleInstances()
        {
            if (FallingIcicleAssignment.CountAssignedIcicles(_hazardIcicles) == 0)
                Debug.LogWarning("[FallingIcicleHazardGroup] No hazard icicles assigned.", this);

            for (int i = 0; i < _hazardIcicles.Count; i++)
            {
                SpriteRenderer renderer = _hazardIcicles[i];
                if (renderer == null)
                    continue;

                LineRenderer warningLine = CreateWarningLine(renderer.transform.name);
                _activeIcicles.Add(new FallingIcicleInstance(renderer, warningLine, _pathLength));
            }
        }

        private LineRenderer CreateWarningLine(string sourceName)
        {
            var lineObject = new GameObject(sourceName + "_WarningLine");
            lineObject.transform.SetParent(transform, worldPositionStays: false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.enabled = false;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = _warningLineWidth;
            line.endWidth = _warningLineWidth;
            line.startColor = _warningColor;
            line.endColor = _warningColor;
            line.material = _warningMaterial;
            line.sortingLayerName = "Foreground";
            line.sortingOrder = 20;
            return line;
        }

        private bool IsPlayerInPath(Vector3 startPosition)
        {
            Vector2 center = new Vector2(startPosition.x, startPosition.y - (_pathLength * 0.5f));
            Vector2 size = new Vector2(_pathHalfWidth * 2f, _pathLength);
            int count = Physics2D.OverlapBox(center, size, 0f, _playerContactFilter, _overlapResults);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _overlapResults[i];
                if (hit != null && hit.CompareTag("Player"))
                    return true;
            }

            return false;
        }

        private void TryDamagePlayer(Vector3 iciclePosition, ref bool hasDamagedPlayer)
        {
            if (hasDamagedPlayer)
                return;

            Vector2 center = (Vector2)iciclePosition + _damageBoxOffset;
            int count = Physics2D.OverlapBox(center, _damageBoxSize, 0f, _playerContactFilter, _overlapResults);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _overlapResults[i];
                if (hit == null || !hit.CompareTag("Player"))
                    continue;

                ApplyDamage(hit);
                hasDamagedPlayer = true;
                return;
            }
        }

        private void ApplyDamage(Collider2D playerCollider)
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[FallingIcicleHazardGroup] GameManager not found - icicle damage ignored.", this);
                return;
            }

            PlayerState state = GameManager.Instance.PlayerState;
            HazardDamageResult result = HazardDamageResolver.Resolve(
                currentHp: state.CurrentHp,
                maxHp: state.MaxHp,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: _hitDamagePercent);
            state.SetCurrentHp(result.NewHp);

            PlayerHurtFeedback feedback = playerCollider.GetComponentInParent<PlayerHurtFeedback>();
            feedback?.PlayHurtAnimation();
            feedback?.FlashOnTick();
        }

        private sealed class FallingIcicleInstance
        {
            private readonly SpriteRenderer _renderer;
            private readonly Transform _transform;
            private readonly LineRenderer _warningLine;
            private readonly FallingIcicleLogic _logic = new FallingIcicleLogic();
            private readonly Vector3 _startPosition;
            private readonly float _targetY;
            private bool _hasDamagedPlayer;

            public FallingIcicleInstance(SpriteRenderer renderer, LineRenderer warningLine, float pathLength)
            {
                _renderer = renderer;
                _transform = renderer.transform;
                _warningLine = warningLine;
                _startPosition = _transform.position;
                _targetY = _startPosition.y - pathLength;
            }

            public void Tick(FallingIcicleHazardGroup group, float deltaTime)
            {
                if (_logic.State == FallingIcicleState.Idle)
                {
                    if (_logic.TryStartWarning(group.IsPlayerInPath(_startPosition)))
                        ShowWarningLine(group);
                    return;
                }

                if (_logic.State == FallingIcicleState.Warning)
                {
                    if (_logic.TickWarning(deltaTime, group._warningSeconds))
                        HideWarningLine();
                    return;
                }

                if (_logic.State != FallingIcicleState.Falling)
                    return;

                Vector3 position = _transform.position;
                position.y = Mathf.Max(_targetY, position.y - (group._fallSpeed * deltaTime));
                _transform.position = position;

                group.TryDamagePlayer(position, ref _hasDamagedPlayer);

                if (position.y <= _targetY + 0.001f)
                {
                    _renderer.enabled = false;
                    _logic.MarkDisappeared();
                }
            }

            private void ShowWarningLine(FallingIcicleHazardGroup group)
            {
                if (_warningLine == null)
                    return;

                _warningLine.SetPosition(0, _startPosition);
                _warningLine.SetPosition(1, new Vector3(_startPosition.x, _targetY, _startPosition.z));
                _warningLine.startWidth = group._warningLineWidth;
                _warningLine.endWidth = group._warningLineWidth;
                _warningLine.startColor = group._warningColor;
                _warningLine.endColor = group._warningColor;
                _warningLine.enabled = true;
            }

            private void HideWarningLine()
            {
                if (_warningLine != null)
                    _warningLine.enabled = false;
            }
        }
    }
}
