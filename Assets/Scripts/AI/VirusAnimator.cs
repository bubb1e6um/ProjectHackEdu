using UnityEngine;

/// <summary>
/// Visual animator for the dodecahedron enemy body.
/// Handles: idle Y-bob and physically-based rolling with smooth direction transitions.
/// </summary>
public class VirusAnimator : MonoBehaviour
{
    [Header("Visual Target")]
    [Tooltip("Child GameObject that holds the dodecahedron mesh.")]
    public Transform dodecVisual;

    [Header("Rolling")]
    [Tooltip("Effective rolling radius (inradius). Converts travel distance → rotation angle.")]
    public float rollRadius = 0.38f;
    [Tooltip("How fast the roll axis blends to a new direction. Higher = snappier turns.")]
    public float directionSmoothSpeed = 10f;

    [Header("Idle Bob")]
    public float bobAmplitude = 0.06f;
    public float bobFrequency = 1.4f;

    private float   _startLocalY;
    private Vector3 _lastWorldPos;
    private Vector3 _smoothVelocityXZ;   // exponentially-smoothed XZ velocity

    void Start()
    {
        _startLocalY       = transform.localPosition.y;
        _lastWorldPos      = transform.position;
        _smoothVelocityXZ  = Vector3.zero;
    }

    void Update()
    {
        // ── Vertical bob ──────────────────────────────────────────────
        Vector3 localPos = transform.localPosition;
        localPos.y       = _startLocalY + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.localPosition = localPos;

        // ── Rolling with smooth direction transition ───────────────────
        if (dodecVisual != null)
        {
            Vector3 worldPos    = transform.position;
            Vector3 rawDeltaXZ  = new Vector3(
                worldPos.x - _lastWorldPos.x, 0f,
                worldPos.z - _lastWorldPos.z);

            // Blend the velocity toward the raw delta each frame.
            // On direction change the blend takes several frames → smooth roll-axis sweep.
            _smoothVelocityXZ = Vector3.Lerp(
                _smoothVelocityXZ,
                rawDeltaXZ,
                1f - Mathf.Exp(-directionSmoothSpeed * Time.deltaTime));

            float smoothSpeed = _smoothVelocityXZ.magnitude;
            if (smoothSpeed > 0.0001f)
            {
                // Use the smoothed direction for the roll axis so it sweeps gradually.
                Vector3 rollAxis  = Vector3.Cross(Vector3.up, _smoothVelocityXZ.normalized);
                // Use raw distance for the roll angle so the rotation stays physically consistent.
                float   rollAngle = rawDeltaXZ.magnitude / rollRadius * Mathf.Rad2Deg;
                dodecVisual.Rotate(rollAxis, rollAngle, Space.World);
            }
        }

        _lastWorldPos = transform.position;
    }
}
