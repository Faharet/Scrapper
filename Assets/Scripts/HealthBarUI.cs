using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI helper: обновляет полосу здоровья по компоненту `Health` на игроке.
/// Разместите элемент UI (Image/Slider) в Canvas и привяжите к полю `fillImage`.
/// Якорь Canvas-ректа — Top Left (чтобы хелзбар показывался в верхнем левом углу).
/// Подписывается на события `onDamage`/`onHeal` если они доступны, чтобы обновляться только при изменении.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Tooltip("Ссылка на компонент Health игрока. Если не задана, будет найдена по тегу 'Player'.")]
    public Health playerHealth;

    [Tooltip("Image с Fill type = Filled (fillAmount обновляется)")]
    public Image fillImage;

    [Tooltip("Альтернативно: можно использовать Slider (необязательно)")]
    public Slider slider;

    [Tooltip("Логирование изменений для отладки")]
    public bool debug = false;

    private void OnEnable()
    {
        TryAutoFind();
        Subscribe();
        // Ensure the Image is configured for filled behaviour (helps when user can't find the Type dropdown)
        if (fillImage != null)
        {
            try
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
            catch { }

            if (fillImage.sprite == null)
            {
                if (debug) Debug.LogWarning("HealthBarUI: 'fillImage' has no Source Image (Sprite). Assign a sprite so the fill is visible.");
            }
        }

        // If player health exists but is zero (not initialized), fill to max so UI starts full
        if (playerHealth != null)
        {
            try
            {
                if (playerHealth.CurrentHealth <= 0f)
                {
                    playerHealth.FillToMax();
                    if (debug) Debug.Log("HealthBarUI: playerHealth was zero — filled to max for initial UI.");
                }
            }
            catch { }
        }

        // Ensure the image has a sprite so the fill is visible (create a temporary white sprite at runtime if needed)
        EnsureFillSprite();

        Refresh();
    }

    private void EnsureFillSprite()
    {
        if (fillImage == null) return;

        if (fillImage.sprite == null)
        {
            // Create a tiny 1x1 white texture and make a sprite from it so the Image can render and fill.
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            Sprite runtimeSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            fillImage.sprite = runtimeSprite;

            // Make sure the image is configured as Filled (again) and visible
            try
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
            catch { }

            if (debug) Debug.Log("HealthBarUI: created temporary white sprite for fillImage so it becomes visible. Assign a proper UI sprite in Inspector to replace it.");
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TryAutoFind()
    {
        if (playerHealth == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                playerHealth = player.GetComponent<Health>();
        }
    }

    private void Subscribe()
    {
        if (playerHealth == null) return;
        // Subscribe to UnityEvents if present
        try
        {
            playerHealth.onDamage.AddListener(Refresh);
            playerHealth.onHeal.AddListener(Refresh);
            playerHealth.onDeath.AddListener(Refresh);
        }
        catch { /* ignore if events null or not present */ }
    }

    private void Unsubscribe()
    {
        if (playerHealth == null) return;
        try
        {
            playerHealth.onDamage.RemoveListener(Refresh);
            playerHealth.onHeal.RemoveListener(Refresh);
            playerHealth.onDeath.RemoveListener(Refresh);
        }
        catch { }
    }

    private void Update()
    {
        // Проверяем что ссылка на playerHealth актуальна
        if (playerHealth == null || playerHealth.gameObject == null)
        {
            // Переподключаемся к игроку (например после смены сцены)
            Unsubscribe();
            TryAutoFind();
            Subscribe();
            
            if (playerHealth != null)
            {
                Refresh();
            }
        }
        
        // Keep a fallback poll in case events are not wired or the Health implementation changes
        if (playerHealth != null && fillImage == null && slider == null && debug)
        {
            Debug.LogWarning("HealthBarUI: no UI target (fillImage/slider) assigned");
        }
    }

    public void Refresh()
    {
        if (playerHealth == null)
        {
            if (debug) Debug.Log("HealthBarUI.Refresh(): playerHealth is null");
            return;
        }

        float pct = playerHealth.GetHealthPercent();

        if (fillImage != null)
        {
            fillImage.fillAmount = Mathf.Clamp01(pct);
            if (debug) Debug.Log($"HealthBarUI: fillImage.fillAmount = {fillImage.fillAmount:F2}");
        }

        if (slider != null)
        {
            slider.value = Mathf.Clamp01(pct);
            if (debug) Debug.Log($"HealthBarUI: slider.value = {slider.value:F2}");
        }
    }
}
