using UnityEngine;

/// <summary>
/// Drives this background layer's X position each frame using parallax offset math.
/// Attach one instance per background layer. Camera resolved automatically via Camera.main.
/// </summary>
public class ParallaxController : MonoBehaviour
{
    [SerializeField] private float parallaxFactor = 0.5f;

    private ParallaxBackground _background;
    private Transform          _cameraTransform;

    private void Start()
    {
        if (Camera.main == null)
        {
            Debug.LogError("[ParallaxController] Camera.main not found — parallax disabled.");
            enabled = false;
            return;
        }
        _cameraTransform = Camera.main.transform;
        _background      = new ParallaxBackground(transform.position.x, parallaxFactor);
    }

    private void Update()
    {
        float newX = _background.CalculateOffsetX(_cameraTransform.position.x);
        transform.position = new Vector3(newX, transform.position.y, transform.position.z);
    }
}
