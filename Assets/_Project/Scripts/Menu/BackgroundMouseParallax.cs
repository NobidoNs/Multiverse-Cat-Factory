using UnityEngine;

public class SimpleParallax : MonoBehaviour
{
    [Header("Настройки")]
    public float maxOffset = 50f;      // Амплитуда (меньше = плавнее)
    public float smoothSpeed = 1.25f;     // Плавность (2-4 идеально)

    private Vector3 _currentVelocity;
    private Vector3 _targetPosition;

    void LateUpdate()
    {
        // Нормализуем мышь от -1 до 1
        float nx = (Input.mousePosition.x / Screen.width) * 2f - 1f;
        float ny = (Input.mousePosition.y / Screen.height) * 2f - 1f;

        // Целевое смещение (фон убегает от курсора)
        _targetPosition.x = -nx * maxOffset;
        _targetPosition.y = -ny * maxOffset;
        _targetPosition.z = 0f;

        // Плавное движение
        transform.localPosition = Vector3.SmoothDamp(
            transform.localPosition,
            _targetPosition,
            ref _currentVelocity,
            smoothSpeed * 0.15f
        );
    }
}