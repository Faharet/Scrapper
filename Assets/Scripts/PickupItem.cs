using UnityEngine;

/// <summary>
/// Базовый класс для подбираемых предметов.
/// Наследники: PipePickup, NotePickup.
/// </summary>
public abstract class PickupItem : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("Радиус триггера для подбора")]
    [SerializeField] protected float pickupRadius = 1f;
    
    [Tooltip("Слой игрока")]
    [SerializeField] protected LayerMask playerLayer;
    
    [Tooltip("Подсказка UI (опционально)")]
    [SerializeField] protected GameObject pickupPrompt;
    
    [Tooltip("Звук подбора")]
    [SerializeField] protected AudioClip pickupSound;
    
    [Tooltip("Автоподбор при контакте или требуется кнопка")]
    [SerializeField] protected bool autoPickup = true;
    
    [Tooltip("Клавиша подбора (если не автоподбор)")]
    [SerializeField] protected KeyCode pickupKey = KeyCode.E;
    
    protected bool isPlayerNearby = false;
    protected Transform playerTransform;
    
    protected virtual void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }
        
        // Проверка расстояния до игрока
        float dist = Vector2.Distance(transform.position, playerTransform.position);
        bool wasNearby = isPlayerNearby;
        isPlayerNearby = dist <= pickupRadius;
        
        // Показать/скрыть подсказку
        if (pickupPrompt != null)
        {
            if (isPlayerNearby && !wasNearby)
                pickupPrompt.SetActive(true);
            else if (!isPlayerNearby && wasNearby)
                pickupPrompt.SetActive(false);
        }
        
        // Подбор
        if (isPlayerNearby)
        {
            if (autoPickup || Input.GetKeyDown(pickupKey))
            {
                Pickup();
            }
        }
    }
    
    protected void FindPlayer()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            playerTransform = player.transform;
    }
    
    protected virtual void Pickup()
    {
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        
        OnPickup();
        pickupPrompt.SetActive(false);
        // Уничтожить объект
        Destroy(gameObject);
    }
    
    /// <summary>Переопределяется в наследниках для специфичной логики</summary>
    protected abstract void OnPickup();
    
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
