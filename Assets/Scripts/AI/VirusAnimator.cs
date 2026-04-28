using UnityEngine;

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
        Vector3 localPos = transform.localPosition;
        localPos.y       = _startLocalY + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.localPosition = localPos;

        if (dodecVisual != null)
        {
            Vector3 worldPos    = transform.position;
            Vector3 rawDeltaXZ  = new Vector3(
                worldPos.x - _lastWorldPos.x, 0f,
                worldPos.z - _lastWorldPos.z);

            // Exponential blend toward raw delta — on direction change this takes several frames,
            // which sweeps the roll axis smoothly instead of snapping.
            _smoothVelocityXZ = Vector3.Lerp(
                _smoothVelocityXZ,
                rawDeltaXZ,
                1f - Mathf.Exp(-directionSmoothSpeed * Time.deltaTime));

            float smoothSpeed = _smoothVelocityXZ.magnitude;
            if (smoothSpeed > 0.0001f)
            {
                // Smoothed direction → gradual roll-axis sweep; raw distance → physically correct angle
                Vector3 rollAxis  = Vector3.Cross(Vector3.up, _smoothVelocityXZ.normalized);
                float   rollAngle = rawDeltaXZ.magnitude / rollRadius * Mathf.Rad2Deg;
                dodecVisual.Rotate(rollAxis, rollAngle, Space.World);
            }
        }

        _lastWorldPos = transform.position;
    }
}
