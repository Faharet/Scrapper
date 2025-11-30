using UnityEngine;

/// <summary>
/// Подбираемая труба: разблокирует прыжки и рывок, меняет анимацию.
/// </summary>
public class PipePickup : PickupItem
{
    [Header("Pipe Settings")]
    [Tooltip("Эффект при подборе (частицы, вспышка)")]
    [SerializeField] private GameObject pickupEffect;
    
    protected override void OnPickup()
    {
        Debug.Log("🔧 PipePickup.OnPickup() вызван!");
        
        // Найти инвентарь игрока
        if (playerTransform != null)
        {
            Debug.Log($"PipePickup: playerTransform найден = {playerTransform.name}");
            
            PlayerInventory inventory = playerTransform.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                Debug.Log($"PipePickup: PlayerInventory найден, hasPipe до = {inventory.HasPipe}");
                inventory.GivePipe();
                Debug.Log($"PipePickup: GivePipe() вызван, hasPipe после = {inventory.HasPipe}");
            }
            else
            {
                Debug.LogError("PipePickup: PlayerInventory не найден на игроке! Добавьте компонент PlayerInventory.");
            }
        }
        else
        {
            Debug.LogError("PipePickup: playerTransform == null!");
        }
        
        // Эффект подбора
        if (pickupEffect != null)
        {
            Instantiate(pickupEffect, transform.position, Quaternion.identity);
        }
        
        Debug.Log("🔧 Труба подобрана!");
    }
}
