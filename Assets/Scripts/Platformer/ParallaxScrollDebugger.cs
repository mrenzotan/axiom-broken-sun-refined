using UnityEngine;

// Diagnostic-only. Attach alongside ParallaxController on each layer to log
// camera-relative scroll deltas. Delete after verifying parallax behavior.
public class ParallaxScrollDebugger : MonoBehaviour
{
    [SerializeField] private float logIntervalSeconds = 1f;

    private Transform _cam;
    private float _previousScreenRelX;
    private float _previousCameraX;
    private float _accumulatedTime;

    private void Start()
    {
        if (Camera.main == null) { enabled = false; return; }
        _cam = Camera.main.transform;
        _previousScreenRelX = transform.position.x - _cam.position.x;
        _previousCameraX = _cam.position.x;
    }

    private void Update()
    {
        _accumulatedTime += Time.deltaTime;
        if (_accumulatedTime < logIntervalSeconds) return;

        float screenRelX    = transform.position.x - _cam.position.x;
        float screenDelta   = screenRelX - _previousScreenRelX;
        float cameraDelta   = _cam.position.x - _previousCameraX;
        float scrollFraction = Mathf.Approximately(cameraDelta, 0f) ? 0f : screenDelta / cameraDelta;

        _previousScreenRelX = screenRelX;
        _previousCameraX    = _cam.position.x;
        _accumulatedTime    = 0f;

        Debug.Log($"[Parallax] {name}  worldX={transform.position.x:F2}  ΔcamX={cameraDelta:+0.00;-0.00}  ΔscreenRel={screenDelta:+0.00;-0.00}  ratio(ΔscreenRel/ΔcamX)={scrollFraction:F2}");
    }
}
