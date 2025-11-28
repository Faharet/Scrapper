using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;
    public bool startFull = true;

    [Header("Damage/Invulnerability")]
    public float invulnerabilityTime = 0.0f;

    [Header("Events")]
    public UnityEvent onDamage;
    public UnityEvent onHeal;
    public UnityEvent onDeath;

    private float currentHealth;
    private float lastDamageTime = -999f;

    public float CurrentHealth => currentHealth;

    void Awake()
    {
        if (startFull) currentHealth = maxHealth;
        else currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;

        if (invulnerabilityTime > 0f && Time.time - lastDamageTime < invulnerabilityTime)
            return;

        lastDamageTime = Time.time;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        onDamage?.Invoke();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        if (currentHealth <= 0f) return; // dead can't be healed here

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        onHeal?.Invoke();
    }

    public void Die()
    {
        onDeath?.Invoke();
    }

    // Optional helper for UI
    public float GetHealthPercent()
    {
        if (maxHealth <= 0f) return 0f;
        return currentHealth / maxHealth;
    }
}
