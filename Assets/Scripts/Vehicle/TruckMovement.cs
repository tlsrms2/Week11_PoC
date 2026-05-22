using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(TruckBody))]
public class TruckMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float reverseSpeed = 3f;
    [SerializeField] private float steerSpeed = 90f; // Degrees per second
    [SerializeField] private bool moveOnlyDuringDefense = true;

    [Header("Road Bounds")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private Vector2 minPosition = new Vector2(-6f, -3f);
    [SerializeField] private Vector2 maxPosition = new Vector2(6f, 3f);

    [Header("Debug")]
    [SerializeField] private bool drawBoundsGizmo = true;

    [Header("Steering Toggle")]
    [SerializeField] private bool useCarSteering = false;

    [Header("Fixed Position")]
    [SerializeField] private bool isFixedPosition = true;
    [SerializeField] private Vector3 fixedPosition = Vector3.zero;

    private TruckBody truckBody;

    public bool UseCarSteering => useCarSteering;
    public Vector2 MinPosition => minPosition;
    public bool IsFixedPosition => isFixedPosition;
    public Vector3 FixedPosition => fixedPosition;

    private void Awake()
    {
        truckBody = GetComponent<TruckBody>();
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        reverseSpeed = Mathf.Max(0f, reverseSpeed);
        steerSpeed = Mathf.Max(0f, steerSpeed);

        if (minPosition.x > maxPosition.x)
        {
            float previousMinX = minPosition.x;
            minPosition.x = maxPosition.x;
            maxPosition.x = previousMinX;
        }

        if (minPosition.y > maxPosition.y)
        {
            float previousMinY = minPosition.y;
            minPosition.y = maxPosition.y;
            maxPosition.y = previousMinY;
        }
    }

    private void Update()
    {
        if (isFixedPosition)
        {
            transform.position = new Vector3(fixedPosition.x, fixedPosition.y, transform.position.z);
            transform.rotation = Quaternion.identity;
            return;
        }

        if (truckBody != null && truckBody.IsDestroyed)
        {
            return;
        }

        if (moveOnlyDuringDefense && !GamePhaseManager.IsDefense)
        {
            return;
        }

        if (useCarSteering)
        {
            // 1. Car Steering Simulation Mode
            float steerInput = 0f;
            float driveInput = 0f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                {
                    driveInput += 1f;
                }
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                {
                    driveInput -= 1f;
                }

                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                {
                    steerInput += 1f;
                }
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                {
                    steerInput -= 1f;
                }
            }

            if (Mathf.Approximately(driveInput, 0f) && Mathf.Approximately(steerInput, 0f))
            {
                return;
            }

            float currentSpeed = 0f;
            if (driveInput > 0f)
            {
                currentSpeed = moveSpeed;
            }
            else if (driveInput < 0f)
            {
                currentSpeed = -reverseSpeed;
            }

            if (!Mathf.Approximately(currentSpeed, 0f) && !Mathf.Approximately(steerInput, 0f))
            {
                float speedFactor = currentSpeed / moveSpeed;
                float rotationAmount = steerInput * steerSpeed * speedFactor * Time.deltaTime;
                transform.Rotate(0f, 0f, rotationAmount);
            }

            if (!Mathf.Approximately(currentSpeed, 0f))
            {
                Vector3 moveDirection = transform.right;
                Vector3 nextPosition = transform.position + moveDirection * (currentSpeed * Time.deltaTime);
                transform.position = ClampToRoadBounds(nextPosition);
            }
        }
        else
        {
            // 2. Simple 2D Translation Mode (No Rotation)
            float moveX = 0f;
            float moveY = 0f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                {
                    moveY += 1f;
                }
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                {
                    moveY -= 1f;
                }
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                {
                    moveX += 1f;
                }
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                {
                    moveX -= 1f;
                }
            }

            Vector3 moveDirection = new Vector3(moveX, moveY, 0f).normalized;
            if (moveDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.identity; // Force straight orientation facing right
                Vector3 nextPosition = transform.position + moveDirection * (moveSpeed * Time.deltaTime);
                transform.position = ClampToRoadBounds(nextPosition);
            }
        }
    }

    private Vector3 ClampToRoadBounds(Vector3 position)
    {
        if (!useBounds)
        {
            return position;
        }

        position.x = Mathf.Clamp(position.x, minPosition.x, maxPosition.x);
        position.y = Mathf.Clamp(position.y, minPosition.y, maxPosition.y);
        return position;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawBoundsGizmo || !useBounds)
        {
            return;
        }

        Vector3 center = new Vector3(
            (minPosition.x + maxPosition.x) * 0.5f,
            (minPosition.y + maxPosition.y) * 0.5f,
            transform.position.z);

        Vector3 size = new Vector3(
            Mathf.Abs(maxPosition.x - minPosition.x),
            Mathf.Abs(maxPosition.y - minPosition.y),
            0f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);
    }
}
