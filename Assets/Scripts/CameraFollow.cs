using UnityEngine;

/// <summary>
/// Простая 2D-камера, которая плавно следует за целевым Transform.
/// Применяется на `Main Camera`.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Tooltip("Цель для слежения (обычно игрок)")]
    public Transform target;
    [Tooltip("Смещение камеры относительно цели (z должен сохранять -10 для 2D)")]
    public Vector3 offset = new Vector3(0f, 1.2f, -10f);
    [Tooltip("Время сглаживания движения камеры (чем меньше — тем резче)")]
    public float smoothTime = 0.12f;

    private Vector3 velocity = Vector3.zero;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPos = new Vector3(target.position.x + offset.x, target.position.y + offset.y, offset.z);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);
    }
}
