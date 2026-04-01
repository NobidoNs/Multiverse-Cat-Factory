using UnityEngine;

[RequireComponent(typeof(Camera))]
public class IsoCameraController : MonoBehaviour
{
    [Header("Настройки скорости")]
    [Tooltip("Скорость перемещения на WASD")]
    public float moveSpeed = 5f;

    [Tooltip("Скорость зума колесиком")]
    public float zoomSpeed = 2f;

    [Tooltip("Скорость перетаскивания мышкой (Shift + ЛКМ)")]
    public float dragSpeed = 0.5f;

    [Header("Настройки угла камеры")]
    [Tooltip("Угол наклона камеры вниз (45 градусов)")]
    [Range(0f, 90f)]
    public float cameraAngleX = 45f;

    private Camera cam;
    private bool isDragging = false;
    private Vector3 lastMouseWorldPos;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        // 1. Фиксация угла камеры — только наклон вниз, без поворота вбок
        transform.rotation = Quaternion.Euler(cameraAngleX, 0f, 0f);

        HandleMovement();
        HandleZoom();
        HandleDrag();
    }

    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal"); // A и D
        float v = Input.GetAxis("Vertical");   // W и S

        if (h != 0 || v != 0)
        {
            Vector3 forward = transform.forward;
            forward.y = 0;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0;
            right.Normalize();

            Vector3 moveDirection = (forward * v) + (right * h);
            transform.position += moveDirection * moveSpeed * Time.deltaTime;
        }
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0)
        {
            if (cam.orthographic)
            {
                cam.orthographicSize -= scroll * zoomSpeed;
                cam.orthographicSize = Mathf.Max(0.5f, cam.orthographicSize);
            }
            else
            {
                transform.position += transform.forward * scroll * zoomSpeed;
            }
        }
    }

    void HandleDrag()
    {
        // Проверяем зажатый Shift
        if (!Input.GetKey(KeyCode.LeftShift))
        {
            isDragging = false;
            return;
        }

        // Начало перетаскивания — нажали ЛКМ
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            lastMouseWorldPos = GetMouseWorldPosition();
        }
        // Конец перетаскивания — отпустили ЛКМ
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
        // Во время перетаскивания — держим ЛКМ
        else if (isDragging)
        {
            Vector3 currentMouseWorldPos = GetMouseWorldPosition();
            Vector3 delta = currentMouseWorldPos - lastMouseWorldPos;

            // Двигаем камеру в ПРОТИВОПОЛОЖНУЮ сторону от движения мыши в мире
            // Это создаёт эффект что мы тянем мир за собой
            transform.position -= delta * dragSpeed;

            lastMouseWorldPos = currentMouseWorldPos;
        }
    }

    // Конвертирует позицию мыши в точку на плоскости Y=0 в мире
    Vector3 GetMouseWorldPosition()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // Находим пересечение с плоскостью Y=0
        float planeY = 0f;
        float t = (planeY - ray.origin.y) / ray.direction.y;

        return ray.origin + ray.direction * t;
    }
}