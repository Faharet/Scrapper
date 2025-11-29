using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Система адреналина: накапливается при нанесении урона врагам, используется для хила.
/// Привязывается к игроку.
/// </summary>
public class AdrenalineSystem : MonoBehaviour
{
    [Header("Adrenaline Settings")]
    [Tooltip("Максимальное количество адреналина")]
    [SerializeField] private float maxAdrenaline = 100f;
    
    [Tooltip("Адреналин получаемый за каждую единицу урона по врагу")]
    [SerializeField] private float adrenalinePerDamage = 5f;
    
    [Tooltip("Количество адреналина необходимое для хила")]
    [SerializeField] private float adrenalineHealCost = 100f;
    
    [Tooltip("Количество HP восстанавливаемое за хил")]
    [SerializeField] private float healAmount = 50f;
    
    [Tooltip("Клавиша для использования хила")]
    [SerializeField] private KeyCode healKey = KeyCode.Q;
    
    [Tooltip("Включить дебаг логи")]
    [SerializeField] private bool debug = false;

    [Header("Events")]
    public UnityEvent onAdrenalineChanged;
    public UnityEvent onAdrenalineHeal;
    public UnityEvent onAdrenalineInsufficient; // когда пытаемся хилиться без адреналина

    private float currentAdrenaline = 0f;
    private Health playerHealth;

    public float CurrentAdrenaline => currentAdrenaline;
    public float MaxAdrenaline => maxAdrenaline;

    private void Awake()
    {
        playerHealth = GetComponent<Health>();
        if (playerHealth == null)
        {
            Debug.LogError("AdrenalineSystem: Health component not found on player!");
        }
    }

    private void Update()
    {
        // Проверка нажатия клавиши хила
        if (Input.GetKeyDown(healKey))
        {
            if (debug) Debug.Log($"AdrenalineSystem: Heal key ({healKey}) pressed!");
            TryUseAdrenalineHeal();
        }
    }

    /// <summary>
    /// Добавить адреналин за нанесение урона
    /// </summary>
    public void AddAdrenaline(float damageDealt)
    {
        float gain = damageDealt * adrenalinePerDamage;
        currentAdrenaline = Mathf.Clamp(currentAdrenaline + gain, 0f, maxAdrenaline);
        
        if (debug) Debug.Log($"Adrenaline +{gain:F1} -> {currentAdrenaline:F1}/{maxAdrenaline}");
        
        onAdrenalineChanged?.Invoke();
    }

    /// <summary>
    /// Попытка использовать адреналин для хила
    /// </summary>
    public bool TryUseAdrenalineHeal()
    {
        if (playerHealth == null)
        {
            if (debug) Debug.LogWarning("AdrenalineSystem: cannot heal - no Health component");
            return false;
        }

        // Проверка: уже полное здоровье?
        if (playerHealth.CurrentHealth >= playerHealth.maxHealth)
        {
            if (debug) Debug.Log("AdrenalineSystem: already at full health");
            return false;
        }

        // Проверка: достаточно адреналина?
        if (currentAdrenaline < adrenalineHealCost)
        {
            if (debug) Debug.Log($"AdrenalineSystem: not enough adrenaline ({currentAdrenaline:F1}/{adrenalineHealCost})");
            onAdrenalineInsufficient?.Invoke();
            return false;
        }

        // Используем адреналин
        currentAdrenaline -= adrenalineHealCost;
        currentAdrenaline = Mathf.Max(0f, currentAdrenaline);

        // Хилим
        playerHealth.Heal(healAmount);

        if (debug) Debug.Log($"AdrenalineSystem: used {adrenalineHealCost} adrenaline to heal {healAmount} HP");

        onAdrenalineHeal?.Invoke();
        onAdrenalineChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// Получить процент заполнения адреналина (0..1)
    /// </summary>
    public float GetAdrenalinePercent()
    {
        return maxAdrenaline > 0f ? currentAdrenaline / maxAdrenaline : 0f;
    }
}
