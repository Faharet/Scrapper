using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Простой компонент для показа названия локации при старте сцены
/// Работает со спрайтами - просто назначь спрайт названия локации
/// </summary>
public class SimpleLocationDisplay : MonoBehaviour
{
    [Header("Location Sprite")]
    [Tooltip("Спрайт с названием локации")]
    [SerializeField] private Sprite locationSprite;
    
    [Header("Settings")]
    [SerializeField] private float fadeInTime = 1f;
    [SerializeField] private float displayTime = 3f;
    [SerializeField] private float fadeOutTime = 1f;

    private Image imageComponent;
    private CanvasGroup canvasGroup;

    private void Start()
    {
        // Получаем компоненты
        imageComponent = GetComponentInChildren<Image>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Устанавливаем спрайт
        if (imageComponent != null && locationSprite != null)
        {
            imageComponent.sprite = locationSprite;
        }

        // Скрываем изначально
        canvasGroup.alpha = 0f;

        // Запускаем анимацию
        StartCoroutine(ShowLocationRoutine());
    }

    private IEnumerator ShowLocationRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        // Fade in
        float elapsed = 0f;
        while (elapsed < fadeInTime)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = elapsed / fadeInTime;
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Display
        yield return new WaitForSeconds(displayTime);

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - (elapsed / fadeOutTime);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        
        Debug.Log("SimpleLocationDisplay: Анимация завершена, скрываем объект");

        // Скрываем объект
        gameObject.SetActive(false);
    }
}
