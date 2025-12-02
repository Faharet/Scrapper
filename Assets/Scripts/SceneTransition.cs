using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Управляет анимированными переходами между сценами с затемнением экрана
/// Singleton паттерн для доступа из любой точки игры
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [Header("Transition Settings")]
    [SerializeField] private Image fadeImage; // Чёрное изображение для затемнения
    [SerializeField] private float fadeDuration = 1f; // Длительность затемнения/появления
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Optional")]
    [SerializeField] private AudioClip transitionSound; // Звук перехода (опционально)
    [SerializeField] private AudioSource audioSource;

    private bool isTransitioning = false;

    private void Awake()
    {
        // Singleton паттерн
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Проверяем наличие fadeImage
        if (fadeImage == null)
        {
            Debug.LogError("SceneTransition: fadeImage не назначен! Создайте Canvas с чёрным Image.");
            return;
        }

        // Настраиваем Canvas для правильного отображения поверх всего
        Canvas canvas = fadeImage.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // Поверх всех UI элементов
            
            // DontDestroyOnLoad для Canvas чтобы он сохранялся между сценами
            DontDestroyOnLoad(canvas.gameObject);
            
            Debug.Log("SceneTransition: Canvas настроен (ScreenSpaceOverlay, sortingOrder=9999)");
        }

        // Убеждаемся что Image заполняет весь экран
        RectTransform rectTransform = fadeImage.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        // Убеждаемся что изображение изначально прозрачное
        SetAlpha(0f);
        
        Debug.Log("SceneTransition: Инициализирован, fadeImage настроен на alpha=0");

        // Создаём AudioSource если его нет
        if (audioSource == null && transitionSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    private void Update()
    {
        // Постоянно проверяем что fadeImage скрыт когда не происходит переход
        if (!isTransitioning && fadeImage != null)
        {
            if (fadeImage.color.a > 0.01f)
            {
                Debug.LogWarning($"SceneTransition: fadeImage видим без перехода! alpha={fadeImage.color.a:F2}, принудительно скрываем");
                SetAlpha(0f);
            }
        }
    }

    /// <summary>
    /// Переход на другую сцену с анимацией затемнения
    /// </summary>
    /// <param name="sceneName">Имя сцены для загрузки</param>
    public void TransitionToScene(string sceneName)
    {
        if (isTransitioning)
        {
            Debug.LogWarning("SceneTransition: переход уже выполняется!");
            return;
        }

        StartCoroutine(TransitionRoutine(sceneName));
    }

    /// <summary>
    /// Переход на сцену по индексу
    /// </summary>
    public void TransitionToScene(int sceneIndex)
    {
        if (isTransitioning)
        {
            Debug.LogWarning("SceneTransition: переход уже выполняется!");
            return;
        }

        StartCoroutine(TransitionRoutine(sceneIndex));
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        isTransitioning = true;

        // Воспроизводим звук если есть
        if (transitionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(transitionSound);
        }

        // Затемнение (fade out)
        yield return StartCoroutine(Fade(0f, 1f, fadeDuration));

        // Загружаем новую сцену
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        
        // Ждём загрузки
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Осветление (fade in)
        yield return StartCoroutine(Fade(1f, 0f, fadeDuration));
        
        // Принудительно убеждаемся что fadeImage полностью прозрачен
        SetAlpha(0f);
        Debug.Log("SceneTransition: Переход завершён (sceneName), fadeImage скрыт (alpha=0)");

        isTransitioning = false;
    }

    private IEnumerator TransitionRoutine(int sceneIndex)
    {
        isTransitioning = true;

        // Воспроизводим звук если есть
        if (transitionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(transitionSound);
        }

        // Затемнение (fade out)
        yield return StartCoroutine(Fade(0f, 1f, fadeDuration));

        // Загружаем новую сцену
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
        
        // Ждём загрузки
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Осветление (fade in)
        yield return StartCoroutine(Fade(1f, 0f, fadeDuration));
        
        // Принудительно убеждаемся что fadeImage полностью прозрачен
        SetAlpha(0f);
        Debug.Log("SceneTransition: Переход завершён (sceneIndex), fadeImage скрыт (alpha=0)");

        isTransitioning = false;
    }

    /// <summary>
    /// Корутина плавного изменения прозрачности экрана
    /// </summary>
    private IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curveValue = fadeCurve.Evaluate(t);
            float alpha = Mathf.Lerp(startAlpha, endAlpha, curveValue);
            
            SetAlpha(alpha);
            
            yield return null;
        }

        SetAlpha(endAlpha);
    }

    /// <summary>
    /// Устанавливает прозрачность fadeImage
    /// </summary>
    private void SetAlpha(float alpha)
    {
        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            color.a = alpha;
            fadeImage.color = color;
            
            // Логирование для отладки
            if (alpha > 0.01f)
            {
                Debug.Log($"SceneTransition: SetAlpha({alpha:F2}) - FadeImage теперь виден");
            }
        }
    }

    /// <summary>
    /// Тестовый метод для проверки затемнения (вызови из редактора или кода)
    /// </summary>
    [ContextMenu("Test Fade")]
    public void TestFade()
    {
        if (fadeImage != null)
        {
            StartCoroutine(TestFadeRoutine());
        }
        else
        {
            Debug.LogError("SceneTransition: fadeImage == null!");
        }
    }

    private IEnumerator TestFadeRoutine()
    {
        Debug.Log("SceneTransition: Начинаем тестовое затемнение...");
        
        // Затемнение
        yield return StartCoroutine(Fade(0f, 1f, fadeDuration));
        
        Debug.Log("SceneTransition: Экран затемнён, ждём 1 секунду...");
        yield return new WaitForSeconds(1f);
        
        // Осветление
        yield return StartCoroutine(Fade(1f, 0f, fadeDuration));
        
        Debug.Log("SceneTransition: Тест завершён!");
    }

    /// <summary>
    /// Проверка готовности системы
    /// </summary>
    public bool IsReady()
    {
        return fadeImage != null && !isTransitioning;
    }

    /// <summary>
    /// Проверка активности перехода
    /// </summary>
    public bool IsTransitioning()
    {
        return isTransitioning;
    }
}
