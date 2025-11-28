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

    [Header("Bounds (optional)")]
    [Tooltip("Если true — камера будет зажата внутри границ комнаты")]
    public bool useBounds = false;
    [Tooltip("Ручные границы комнаты в мировых координатах (используется если boundsCollider == null)")]
    public Vector2 minBounds = new Vector2(-10f, -5f);
    public Vector2 maxBounds = new Vector2(10f, 5f);
    [Tooltip("Опциональный BoxCollider2D на объекте комнаты. Если задан — его границы переопределяют min/max.")]
    public BoxCollider2D boundsCollider;

    private Vector3 velocity = Vector3.zero;
    private Camera cam;

    void LateUpdate()
    {
        if (target == null) return;

        if (cam == null) cam = GetComponent<Camera>();

        Vector3 targetPos = new Vector3(target.position.x + offset.x, target.position.y + offset.y, offset.z);

        // Smooth follow first
        Vector3 smoothed = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);

        // If bounds are enabled, clamp the camera so viewport stays inside the room
        if (useBounds && cam != null && cam.orthographic)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            // Determine world-space min/max from collider if provided
            Vector2 worldMin = minBounds;
            Vector2 worldMax = maxBounds;
            if (boundsCollider != null)
            {
                Bounds b = boundsCollider.bounds;
                worldMin = new Vector2(b.min.x, b.min.y);
                worldMax = new Vector2(b.max.x, b.max.y);
            }

            // If room is smaller than viewport, center camera within room bounds
            float minX = worldMin.x + halfWidth;
            float maxX = worldMax.x - halfWidth;
            float minY = worldMin.y + halfHeight;
            float maxY = worldMax.y - halfHeight;

            // If min > max (room smaller than viewport), center between min/max of room
            if (minX > maxX)
            {
                float centerX = (worldMin.x + worldMax.x) * 0.5f;
                smoothed.x = centerX;
            }
            else
            {
                smoothed.x = Mathf.Clamp(smoothed.x, minX, maxX);
            }

            if (minY > maxY)
            {
                float centerY = (worldMin.y + worldMax.y) * 0.5f;
                smoothed.y = centerY;
            }
            else
            {
                smoothed.y = Mathf.Clamp(smoothed.y, minY, maxY);
            }
        }

        transform.position = smoothed;
    }
}
