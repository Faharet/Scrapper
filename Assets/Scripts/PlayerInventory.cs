using UnityEngine;

/// <summary>
/// Инвентарь игрока: отслеживает предметы и разблокирует способности.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory State")]
    [SerializeField] private bool startWithPipe = false;
    
    private bool hasPipe = false;
    private Animator animator;
    
    // Public свойство для чтения состояния
    public bool HasPipe => hasPipe;
    
    void Awake()
    {
        animator = GetComponent<Animator>();
        hasPipe = startWithPipe;
        
        Debug.Log($"PlayerInventory.Awake(): animator = {(animator != null ? "найден" : "НЕ найден")}, startWithPipe = {startWithPipe}, hasPipe = {hasPipe}");
        
        if (hasPipe && animator != null)
        {
            animator.SetBool("HasPipe", true);
            Debug.Log("PlayerInventory: Установлен HasPipe = true в Awake");
        }
    }
    
    /// <summary>Дать игроку трубу и разблокировать способности</summary>
    public void GivePipe()
    {
        Debug.Log($"PlayerInventory.GivePipe() вызван! hasPipe до = {hasPipe}");
        
        if (hasPipe)
        {
            Debug.Log("PlayerInventory: Труба уже есть, выход.");
            return;
        }
        
        hasPipe = true;
        Debug.Log($"PlayerInventory: hasPipe установлен в true");
        
        // Переключить анимацию
        if (animator != null)
        {
            animator.SetBool("HasPipe", true);
            Debug.Log("PlayerInventory: animator.SetBool('HasPipe', true) выполнен");
            
            // Проверка что параметр реально установился
            bool check = animator.GetBool("HasPipe");
            Debug.Log($"PlayerInventory: Проверка animator.GetBool('HasPipe') = {check}");
        }
        else
        {
            Debug.LogWarning("PlayerInventory: Animator == null! Анимация не переключена.");
        }
        
        Debug.Log("✅ Игрок получил трубу! Прыжки и рывок разблокированы.");
    }
    
    /// <summary>Проверка: может ли игрок прыгать</summary>
    public bool CanJump()
    {
        return hasPipe;
    }
    
    /// <summary>Проверка: может ли игрок использовать рывок</summary>
    public bool CanDash()
    {
        return hasPipe;
    }
}
