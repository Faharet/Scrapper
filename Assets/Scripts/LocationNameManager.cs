using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Автоматически создаёт LocationNameDisplay при загрузке сцены
/// Названия локаций настраиваются в инспекторе
/// </summary>
public class LocationNameManager : MonoBehaviour
{
    [System.Serializable]
    public class SceneLocationInfo
    {
        public string sceneName;
        public string locationName;
        public string locationSubtitle;
    }

    [Header("Scene Locations")]
    [Tooltip("Список сцен и их названий")]
    [SerializeField] private SceneLocationInfo[] sceneLocations = new SceneLocationInfo[]
    {
        new SceneLocationInfo { sceneName = "Bunker", locationName = "Бункер", locationSubtitle = "Заброшенное убежище" },
        new SceneLocationInfo { sceneName = "Street", locationName = "Улица", locationSubtitle = "Разрушенный город" },
    };

    [Header("UI Prefab")]
    [Tooltip("Prefab с UI для отображения названия (опционально, если null - создаст автоматически)")]
    [SerializeField] private GameObject locationDisplayPrefab;

    private static LocationNameManager instance;

    private void Awake()
    {
        // Singleton с сохранением между сценами
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Подписываемся на события загрузки сцен
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Ищем информацию о текущей сцене
        SceneLocationInfo locationInfo = GetLocationInfo(scene.name);
        
        if (locationInfo != null && !string.IsNullOrEmpty(locationInfo.locationName))
        {
            ShowLocationName(locationInfo.locationName, locationInfo.locationSubtitle);
        }
    }

    private SceneLocationInfo GetLocationInfo(string sceneName)
    {
        foreach (var info in sceneLocations)
        {
            if (info.sceneName == sceneName)
            {
                return info;
            }
        }
        return null;
    }

    private void ShowLocationName(string name, string subtitle)
    {
        GameObject displayObject = null;

        if (locationDisplayPrefab != null)
        {
            // Используем prefab
            displayObject = Instantiate(locationDisplayPrefab);
        }
        else
        {
            // Создаём автоматически
            displayObject = CreateLocationDisplayUI(name, subtitle);
        }

        if (displayObject != null)
        {
            LocationNameDisplay display = displayObject.GetComponent<LocationNameDisplay>();
            if (display != null)
            {
                display.ShowLocation(name, subtitle);
            }
        }
    }

    /// <summary>
    /// Создаёт простой UI для отображения названия локации
    /// </summary>
    private GameObject CreateLocationDisplayUI(string name, string subtitle)
    {
        // Создаём Canvas
        GameObject canvasObj = new GameObject("LocationNameCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000; // Поверх всего

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // Создаём панель для текста
        GameObject panelObj = new GameObject("LocationPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(800, 200);

        CanvasGroup canvasGroup = panelObj.AddComponent<CanvasGroup>();

        // Создаём основной текст
        GameObject textObj = new GameObject("LocationText");
        textObj.transform.SetParent(panelObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0.5f);
        textRect.anchorMax = new Vector2(1, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0, 20);
        textRect.sizeDelta = new Vector2(0, 80);

        Text text = textObj.AddComponent<Text>();
        text.text = name;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 60;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        
        // Добавляем тень для читаемости
        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(3, -3);

        // Создаём подзаголовок
        GameObject subtitleObj = new GameObject("SubtitleText");
        subtitleObj.transform.SetParent(panelObj.transform, false);
        
        RectTransform subtitleRect = subtitleObj.AddComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0, 0.5f);
        subtitleRect.anchorMax = new Vector2(1, 0.5f);
        subtitleRect.pivot = new Vector2(0.5f, 0.5f);
        subtitleRect.anchoredPosition = new Vector2(0, -30);
        subtitleRect.sizeDelta = new Vector2(0, 40);

        Text subtitleText = subtitleObj.AddComponent<Text>();
        subtitleText.text = subtitle;
        subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subtitleText.fontSize = 30;
        subtitleText.alignment = TextAnchor.MiddleCenter;
        subtitleText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        
        Shadow subtitleShadow = subtitleObj.AddComponent<Shadow>();
        subtitleShadow.effectColor = Color.black;
        subtitleShadow.effectDistance = new Vector2(2, -2);

        // Добавляем компонент LocationNameDisplay
        LocationNameDisplay display = panelObj.AddComponent<LocationNameDisplay>();
        
        // Используем рефлексию для установки приватных полей
        var displayType = typeof(LocationNameDisplay);
        displayType.GetField("locationText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(display, text);
        displayType.GetField("subtitleText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(display, subtitleText);
        displayType.GetField("canvasGroup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(display, canvasGroup);

        return panelObj;
    }
}
