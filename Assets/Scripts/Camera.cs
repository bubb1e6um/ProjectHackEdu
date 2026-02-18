using UnityEngine;

public class Camera : MonoBehaviour
{

    public Transform target;

    public float smoothSpeed = 10f;

    private Vector3 offset;

    void Start()
    {
        
        if (target != null)
        {
            offset = transform.position - target.position;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}