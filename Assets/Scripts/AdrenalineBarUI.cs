using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI компонент для отображения адреналина.
/// Размещается в Canvas рядом с HealthBarUI (справа от хелзбара).
/// Привязывается к AdrenalineSystem на игроке.
/// Работает через Image.fillAmount - простое процентное заполнение.
/// </summary>
public class AdrenalineBarUI : MonoBehaviour
{
    [Tooltip("Ссылка на AdrenalineSystem игрока. Если не задана, будет найдена автоматически.")]
    [SerializeField] private AdrenalineSystem adrenalineSystem;

    [Tooltip("Image адреналина (будет заполняться через fillAmount)")]
    [SerializeField] private Image fillImage;

    [Tooltip("Альтернативно: можно использовать Slider")]
    [SerializeField] private Slider slider;

    [Tooltip("Опциональный текст для отображения значения адреналина")]
    [SerializeField] private Text valueText;

    [Tooltip("Логирование для отладки")]
    [SerializeField] private bool debug = false;

    private void OnEnable()
    {
        TryAutoFind();
        Subscribe();
        
        // Настраиваем fillImage для радиального заполнения снизу вверх
        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Bottom;
            fillImage.fillClockwise = true;
        }
        
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TryAutoFind()
    {
        if (adrenalineSystem == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                adrenalineSystem = player.GetComponent<AdrenalineSystem>();
        }
    }

    private void Subscribe()
    {
        if (adrenalineSystem == null) return;
        try
        {
            adrenalineSystem.onAdrenalineChanged.AddListener(Refresh);
            adrenalineSystem.onAdrenalineHeal.AddListener(Refresh);
        }
        catch { }
    }

    private void Unsubscribe()
    {
        if (adrenalineSystem == null) return;
        try
        {
            adrenalineSystem.onAdrenalineChanged.RemoveListener(Refresh);
            adrenalineSystem.onAdrenalineHeal.RemoveListener(Refresh);
        }
        catch { }
    }

    public void Refresh()
    {
        if (adrenalineSystem == null)
        {
            if (debug) Debug.Log("AdrenalineBarUI.Refresh(): adrenalineSystem is null");
            return;
        }

        float pct = adrenalineSystem.GetAdrenalinePercent();

        // Просто устанавливаем fillAmount
        if (fillImage != null)
        {
            fillImage.fillAmount = pct;
        }

        if (slider != null)
        {
            slider.value = pct;
        }

        if (valueText != null)
        {
            valueText.text = $"{adrenalineSystem.CurrentAdrenaline:F0}/{adrenalineSystem.MaxAdrenaline:F0}";
        }

        if (debug) Debug.Log($"AdrenalineBarUI: refreshed to {pct * 100f:F1}%");
    }
}
