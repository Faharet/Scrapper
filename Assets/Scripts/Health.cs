using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;
    public bool startFull = true;

    [Header("Damage/Invulnerability")]
    // ВАЖНО: Установите это значение > 0f в инспекторе игрока (например, 0.5f)
    [Tooltip("Время неуязвимости после получения урона (i-frames).")]
    public float invulnerabilityTime = 0.5f; 

    // НОВЫЕ ПОЛЯ ДЛЯ ВИЗУАЛА И АНИМАЦИИ
    [Header("Visuals")]
    [Tooltip("Ссылка на Animator. Если не задана, будет найдена автоматически.")]
    [SerializeField] private Animator _animator;
    [Tooltip("Ссылка на SpriteRenderer. Если не задана, будет найдена автоматически.")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Tooltip("Скорость мигания (количество включений/выключений в секунду).")]
    [SerializeField] private float _blinkSpeed = 20f; 

    [Header("Events")]
    public UnityEvent onDamage;
    public UnityEvent onHeal;
    public UnityEvent onDeath;

    private float currentHealth;
    private float lastDamageTime = -999f;

    public float CurrentHealth => currentHealth;
    
    // ДОБАВЛЕННЫЙ МЕТОД ДЛЯ HealthBarUI
    public float GetHealthPercent()
    {
        return currentHealth / maxHealth;
    }

    void Awake()
    {
        // Инициализация компонентов
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();

        if (startFull) currentHealth = maxHealth;
        else currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        if (_spriteRenderer == null)
        {
            Debug.LogWarning($"Health.cs: SpriteRenderer не найден на {gameObject.name}. Мигание не будет работать.");
        }
    }
    
    // НОВЫЙ МЕТОД: Обработка мигания в каждом кадре
    void Update()
    {
        HandleInvulnerabilityBlinking();
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;

        // ПРОВЕРКА НЕУЯЗВИМОСТИ
        if (invulnerabilityTime > 0f && Time.time - lastDamageTime < invulnerabilityTime)
            return;

        lastDamageTime = Time.time; // Активируем i-frames и мигание

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        // АКТИВАЦИЯ АНИМАЦИИ ПОЛУЧЕНИЯ УРОНА
        if (_animator != null)
        {
            // !!! Активируем триггер анимации "Hit" !!!
            _animator.SetTrigger("Hit"); 
        }

        onDamage?.Invoke();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }
    
    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        if (currentHealth <= 0f) return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        onHeal?.Invoke();
    }

    public void Die()
    {
        // Убеждаемся, что спрайт виден
        if (_spriteRenderer != null)
            _spriteRenderer.enabled = true;
            
        onDeath?.Invoke();
    }
    
    public void FillToMax()
    {
        currentHealth = maxHealth;
        onHeal?.Invoke();
    }

    // НОВЫЙ МЕТОД: Логика мигания спрайта
    private void HandleInvulnerabilityBlinking()
    {
        if (_spriteRenderer == null || invulnerabilityTime <= 0f) return;

        // Проверяем, активна ли неуязвимость
        bool isInvulnerable = Time.time - lastDamageTime < invulnerabilityTime;

        if (isInvulnerable)
        {
            // Мигание: показываем/скрываем спрайт
            if ((int)(Time.time * _blinkSpeed) % 2 == 0)
            {
                _spriteRenderer.enabled = false;
            }
            else
            {
                _spriteRenderer.enabled = true;
            }
        }
        else
        {
            // Убеждаемся, что спрайт виден, когда неуязвимость закончилась
            if (!_spriteRenderer.enabled)
                _spriteRenderer.enabled = true;
        }
    }
}