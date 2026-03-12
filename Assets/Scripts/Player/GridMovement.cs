using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class GridMovement : MonoBehaviour
{
    [Header("Настройки")]
    public float moveSpeed = 300f; 
    public float gridSize = 4f;   
    private bool isMoving = false;
    public LayerMask obstacleMask;
    

    void Update()
    {
        if (Time.timeScale == 0f) return;
        if (isMoving) return;
        if (Keyboard.current == null) return;

        float inputX = 0f;
        float inputZ = 0f;


        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) inputZ = 1f;
        else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) inputZ = -1f;

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) inputX = 1f;
        else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) inputX = -1f;

        if (inputX != 0) inputZ = 0; 

        if (inputX != 0 || inputZ != 0)
        {
            Vector3 direction = new Vector3(inputX, 0, inputZ);
            
            Vector3 rayOrigin = transform.position + (Vector3.up * (gridSize / 2f));
            
            if (Physics.Raycast(rayOrigin, direction, gridSize, obstacleMask))
            {
                return; 
            }
            StartCoroutine(RollToGrid(direction));
        }
    }

    private IEnumerator RollToGrid(Vector3 direction)
    {
        isMoving = true;

        float startX = Mathf.Round(transform.position.x / gridSize) * gridSize;
        float startY = Mathf.Round(transform.position.y / gridSize) * gridSize;
        float startZ = Mathf.Round(transform.position.z / gridSize) * gridSize;
        
        float halfSize = gridSize / 2f; 
        Vector3 idealStartPosition = new Vector3(startX, halfSize, startZ);

        Vector3 targetPosition = idealStartPosition + (direction * gridSize);

        Vector3 anchorPoint = idealStartPosition + (Vector3.down * halfSize) + (direction * halfSize);
        Vector3 axis = Vector3.Cross(Vector3.up, direction);

        float anglePushed = 0f;

        while (anglePushed < 90f)
        {
            float angleForFrame = moveSpeed * Time.deltaTime;
            
            if (anglePushed + angleForFrame > 90f)
            {
                angleForFrame = 90f - anglePushed;
            }

            transform.RotateAround(anchorPoint, axis, angleForFrame);
            anglePushed += angleForFrame;
            yield return null;
        }

        transform.position = new Vector3(
            Mathf.Round(targetPosition.x),
            0.5f,
            Mathf.Round(targetPosition.z)
        );
        

        transform.rotation = Quaternion.Euler(
            Mathf.Round(transform.eulerAngles.x / 90f) * 90f,
            Mathf.Round(transform.eulerAngles.y / 90f) * 90f,
            Mathf.Round(transform.eulerAngles.z / 90f) * 90f
        );

        isMoving = false;
    }
}