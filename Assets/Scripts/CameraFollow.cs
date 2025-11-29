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

    [Header("Hollow Knight-style Look-Ahead")]
    [Tooltip("Максимальное смещение вперёд в направлении взгляда игрока")]
    public float lookAheadDistance = 2f;
    [Tooltip("Вертикальное смещение дополнительно к offset.y")]
    public float verticalOffset = 0.0f;
    [Tooltip("Сглаживание смещения взгляда")]
    public float lookSmoothTime = 0.18f;
    [Tooltip("Если true, определяем направление взгляда по Rigidbody2D.velocity.x, иначе по localScale.x")] 
    public bool useRigidbodyForFacing = true;
    [Tooltip("Минимальная скорость по X для того, чтобы считать что игрок 'смотрит' в сторону движения")]
    public float facingVelocityThreshold = 0.1f;
    [Tooltip("Deadzone в мировых координатах — камера не начнёт двигаться пока игрок в этой зоне относительно центра камеры")]
    public Vector2 deadzone = new Vector2(0.2f, 0.2f);

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
    // internal look-ahead state
    private float currentLookAheadX = 0f;
    private float lookAheadVelocity = 0f;

    void LateUpdate()
    {
        if (target == null) return;

        if (cam == null) cam = GetComponent<Camera>();

        // Determine facing direction for look-ahead
        float facingSign = 0f;
        if (useRigidbodyForFacing)
        {
            var rbTarget = target.GetComponent<Rigidbody2D>();
            if (rbTarget != null && Mathf.Abs(rbTarget.linearVelocity.x) > facingVelocityThreshold)
                facingSign = Mathf.Sign(rbTarget.linearVelocity.x);
        }
        // fallback to transform.localScale.x if needed
        if (Mathf.Approximately(facingSign, 0f))
        {
            if (Mathf.Abs(target.localScale.x) > 0.001f)
                facingSign = Mathf.Sign(target.localScale.x);
        }

        // Desired look-ahead based on facing
        float desiredLookAhead = facingSign * lookAheadDistance;
        // Smoothly move current look-ahead to desired value
        currentLookAheadX = Mathf.SmoothDamp(currentLookAheadX, desiredLookAhead, ref lookAheadVelocity, lookSmoothTime);

        // Desired camera position includes offset and look-ahead
        Vector3 desiredPos = new Vector3(target.position.x + offset.x + currentLookAheadX, target.position.y + offset.y + verticalOffset, offset.z);

        // Deadzone: if player is within deadzone relative to camera center, don't move camera on that axis
        Vector3 cameraCenter = transform.position;
        Vector3 diff = desiredPos - cameraCenter;
        if (Mathf.Abs(diff.x) < deadzone.x) desiredPos.x = cameraCenter.x;
        if (Mathf.Abs(diff.y) < deadzone.y) desiredPos.y = cameraCenter.y;

        // Smoothly follow to the desired position
        Vector3 smoothed = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, smoothTime);

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
