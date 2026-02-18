using System.Collections;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.InputSystem; // Обязательно добавляем это пространство имен!

public class GridMovement : MonoBehaviour
{
    [Header("Настройки движения")]
    public float moveSpeed = 5f;
    public float gridSize = 4f;

    private bool isMoving = false;

    void Update()
    {
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
            Vector3 moveDirection = new Vector3(inputX, 0f, inputZ);
            StartCoroutine(RollToGrid(moveDirection));
        }
    }


private IEnumerator RollToGrid(Vector3 direction)
{
    isMoving = true;

    Vector3 startPosition = transform.position;
    

    Bounds bounds = GetComponent<Collider>().bounds;
    

    float stepSize = bounds.size.x; 
    float halfSize = stepSize / 2f;

    Vector3 anchorPoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + (direction * halfSize);
    

    Vector3 axis = Vector3.Cross(Vector3.up, direction);

    Vector3 targetPosition = startPosition + (direction * stepSize);

    float anglePushed = 0f;
    float rollSpeed = 400f;


    while (anglePushed < 90f)
    {
        float angleForFrame = rollSpeed * Time.deltaTime;
        if (anglePushed + angleForFrame > 90f)
        {
            angleForFrame = 90f - anglePushed;
        }

        transform.RotateAround(anchorPoint, axis, angleForFrame);
        anglePushed += angleForFrame;
        yield return null;
    }


    transform.position = targetPosition;
    
    transform.eulerAngles = new Vector3(
        Mathf.Round(transform.eulerAngles.x / 90f) * 90f,
        Mathf.Round(transform.eulerAngles.y / 90f) * 90f,
        Mathf.Round(transform.eulerAngles.z / 90f) * 90f
    );

    isMoving = false;
}
}