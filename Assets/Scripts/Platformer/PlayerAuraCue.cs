using System.Collections;
using Axiom.Platformer;
using UnityEngine;

/// <summary>
/// MonoBehaviour — drives the proximity "aura" SpriteRenderer behind the player.
/// Lifecycle + visuals only; visibility logic lives in <see cref="AuraVisibilityState"/>.
/// Proximity forwarders call EnterPuzzleRange / ExitPuzzleRange; the cast sequencer calls
/// Suppress while a cast animation plays.
/// </summary>
public class PlayerAuraCue : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Aura SpriteRenderer, sorted BEHIND the player sprite (lower sorting order, same layer).")]
    private SpriteRenderer _auraRenderer;

    [SerializeField]
    [Tooltip("Aura frames sliced from Aura-Sheet.png, in play order.")]
    private Sprite[] _frames;

    [SerializeField, Min(1f)]
    [Tooltip("Aura animation frames per second.")]
    private float _fps = 12f;

    private readonly AuraVisibilityState _state = new();
    private Coroutine _cycle;

    private void Awake()
    {
        if (_auraRenderer != null) _auraRenderer.enabled = false;
    }

    public void EnterPuzzleRange(ISpellPuzzle puzzle)
    {
        _state.Enter(puzzle);
        Refresh();
    }

    public void ExitPuzzleRange(ISpellPuzzle puzzle)
    {
        _state.Exit(puzzle);
        Refresh();
    }

    public void Suppress(bool suppressed)
    {
        _state.SetSuppressed(suppressed);
        Refresh();
    }

    private void Refresh()
    {
        if (_auraRenderer == null) return;

        if (_state.IsVisible)
        {
            if (_cycle == null)
            {
                _auraRenderer.enabled = true;
                _cycle = StartCoroutine(CycleFrames());
            }
        }
        else if (_cycle != null)
        {
            StopCoroutine(_cycle);
            _cycle = null;
            _auraRenderer.enabled = false;
        }
    }

    private IEnumerator CycleFrames()
    {
        if (_frames == null || _frames.Length == 0) yield break;

        var wait = new WaitForSeconds(1f / _fps);
        int index = 0;
        while (true)
        {
            _auraRenderer.sprite = _frames[index];
            index = (index + 1) % _frames.Length;
            yield return wait;
        }
    }
}
