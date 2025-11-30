using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Показывает название локации при начале сцены с анимацией появления/исчезновения
/// </summary>
public class LocationNameDisplay : MonoBehaviour
{
    [Header("Location Info")]
    [Tooltip("Название локации (например: 'Бункер', 'Улица', 'Подземелье')")]
    [SerializeField] private string locationName = "Локация";
    
    [Tooltip("Дополнительное описание (опционально)")]
    [SerializeField] private string locationSubtitle = "";

    [Header("UI References")]
    [SerializeField] private Text locationText;
    [SerializeField] private Text subtitleText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation Settings")]
    [Tooltip("Задержка перед показом (секунды)")]
    [SerializeField] private float delayBeforeShow = 0.5f;
    
    [Tooltip("Длительность появления (секунды)")]
    [SerializeField] private float fadeInDuration = 1f;
    
    [Tooltip("Время показа текста (секунды)")]
    [SerializeField] private float displayDuration = 3f;
    
    [Tooltip("Длительность исчезновения (секунды)")]
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("Optional")]
    [Tooltip("Звук появления названия")]
    [SerializeField] private AudioClip displaySound;
    [SerializeField] private AudioSource audioSource;

    private void Start()
    {
        // Настройка компонентов
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        // Скрываем изначально
        canvasGroup.alpha = 0f;

        // Устанавливаем текст
        if (locationText != null)
        {
            locationText.text = locationName;
        }

        if (subtitleText != null)
        {
            if (string.IsNullOrEmpty(locationSubtitle))
            {
                subtitleText.gameObject.SetActive(false);
            }
            else
            {
                subtitleText.text = locationSubtitle;
            }
        }

        // Создаём AudioSource если нужен
        if (audioSource == null && displaySound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Запускаем анимацию показа
        StartCoroutine(DisplayLocationRoutine());
    }

    private IEnumerator DisplayLocationRoutine()
    {
        // Задержка перед показом
        yield return new WaitForSeconds(delayBeforeShow);

        // Воспроизводим звук
        if (displaySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(displaySound);
        }

        // Fade In
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Держим на экране
        yield return new WaitForSeconds(displayDuration);

        // Fade Out
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        // Опционально: уничтожаем объект после показа
        // Destroy(gameObject);
    }

    /// <summary>
    /// Изменить название локации и показать заново
    /// </summary>
    public void ShowLocation(string name, string subtitle = "")
    {
        locationName = name;
        locationSubtitle = subtitle;

        if (locationText != null)
            locationText.text = locationName;

        if (subtitleText != null)
        {
            if (string.IsNullOrEmpty(locationSubtitle))
            {
                subtitleText.gameObject.SetActive(false);
            }
            else
            {
                subtitleText.gameObject.SetActive(true);
                subtitleText.text = locationSubtitle;
            }
        }

        StopAllCoroutines();
        StartCoroutine(DisplayLocationRoutine());
    }
}
