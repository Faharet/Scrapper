using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Триггер для активации перехода между сценами при контакте с игроком
/// Используется для дверей, выходов, порталов и т.д.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Имя сцены для загрузки (должно быть добавлено в Build Settings)")]
    [SerializeField] private string targetSceneName = "";
    
    [Tooltip("Или используйте индекс сцены из Build Settings")]
    [SerializeField] private int targetSceneIndex = -1;
    
    [SerializeField] private bool useSceneIndex = false; // Использовать индекс вместо имени

    [Header("Interaction Settings")]
    [Tooltip("Требуется нажатие клавиши для перехода")]
    [SerializeField] private bool requireKeyPress = true;
    
    [Tooltip("Клавиша для активации перехода")]
    [SerializeField] private KeyCode interactionKey = KeyCode.W;

    [Header("Requirements")]
    [Tooltip("Требуется труба для выхода")]
    [SerializeField] private bool requirePipe = true;
    
    [Tooltip("Сообщение если нет трубы")]
    [SerializeField] private string blockedPromptText = "Нужна труба чтобы выйти";

    [Header("Visual Feedback")]
    [Tooltip("Текстовая подсказка для игрока")]
    [SerializeField] private string promptText = "Нажми W чтобы войти";
    
    [SerializeField] private GameObject promptUI; // UI элемент с подсказкой (опционально)
    [SerializeField] private UnityEngine.UI.Text promptTextUI; // Text компонент для изменения текста
    
    [Header("Player Detection")]
    [SerializeField] private LayerMask playerLayer = 1 << 6; // Layer игрока

    private bool playerInRange = false;
    private Collider2D triggerCollider;
    private PlayerInventory playerInventory;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        
        // Убеждаемся что коллайдер - триггер
        if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning($"SceneTransitionTrigger на {gameObject.name}: Collider2D должен быть триггером! Исправляю автоматически.");
            triggerCollider.isTrigger = true;
        }

        // Проверяем настройки сцены
        if (!useSceneIndex && string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError($"SceneTransitionTrigger на {gameObject.name}: targetSceneName не задан!");
        }

        if (useSceneIndex && targetSceneIndex < 0)
        {
            Debug.LogError($"SceneTransitionTrigger на {gameObject.name}: targetSceneIndex должен быть >= 0!");
        }

        // Скрываем подсказку изначально
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    private void Update()
    {
        // Если игрок в зоне и требуется нажатие клавиши
        if (playerInRange && requireKeyPress)
        {
            if (Input.GetKeyDown(interactionKey))
            {
                // Проверяем наличие трубы перед переходом
                if (requirePipe && !CheckPlayerHasPipe())
                {
                    Debug.Log("SceneTransitionTrigger: Переход заблокирован - нет трубы!");
                    UpdatePromptText(false);
                    return;
                }
                
                TriggerSceneTransition();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Проверяем что это игрок
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            playerInRange = true;
            
            // Получаем компонент PlayerInventory
            if (playerInventory == null)
            {
                playerInventory = other.GetComponent<PlayerInventory>();
            }
            
            Debug.Log($"SceneTransitionTrigger: Игрок вошёл в зону перехода");

            // Проверяем доступность перехода и показываем соответствующую подсказку
            bool canTransition = !requirePipe || CheckPlayerHasPipe();
            UpdatePromptText(canTransition);

            // Показываем подсказку
            if (promptUI != null)
            {
                promptUI.SetActive(true);
            }

            // Если не требуется нажатие клавиши - переходим сразу (только если можем)
            if (!requireKeyPress && canTransition)
            {
                TriggerSceneTransition();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Проверяем что это игрок
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            playerInRange = false;
            
            Debug.Log($"SceneTransitionTrigger: Игрок покинул зону перехода");

            // Скрываем подсказку
            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Проверяет есть ли у игрока труба
    /// </summary>
    private bool CheckPlayerHasPipe()
    {
        if (playerInventory == null)
        {
            Debug.LogWarning("SceneTransitionTrigger: PlayerInventory не найден на игроке!");
            return false;
        }
        
        return playerInventory.HasPipe;
    }

    /// <summary>
    /// Обновляет текст подсказки в зависимости от доступности перехода
    /// </summary>
    private void UpdatePromptText(bool canTransition)
    {
        if (promptTextUI != null)
        {
            promptTextUI.text = canTransition ? promptText : blockedPromptText;
        }
    }

    /// <summary>
    /// Активирует переход на другую сцену
    /// </summary>
    private void TriggerSceneTransition()
    {
        // Проверяем что SceneTransition готов
        if (SceneTransition.Instance == null)
        {
            Debug.LogError("SceneTransitionTrigger: SceneTransition.Instance не найден! Создайте GameObject со скриптом SceneTransition в сцене.");
            return;
        }

        if (!SceneTransition.Instance.IsReady())
        {
            Debug.LogWarning("SceneTransitionTrigger: SceneTransition не готов (возможно уже выполняется переход)");
            return;
        }

        Debug.Log($"SceneTransitionTrigger: Начинаем переход на сцену {(useSceneIndex ? targetSceneIndex.ToString() : targetSceneName)}");

        // Скрываем подсказку перед переходом
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        // Запускаем переход
        if (useSceneIndex)
        {
            SceneTransition.Instance.TransitionToScene(targetSceneIndex);
        }
        else
        {
            SceneTransition.Instance.TransitionToScene(targetSceneName);
        }

        // Отключаем дальнейшие взаимодействия
        playerInRange = false;
        enabled = false;
    }

    // Визуализация в редакторе
    private void OnDrawGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            
            if (col is BoxCollider2D boxCol)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(boxCol.offset, boxCol.size);
            }
            else if (col is CircleCollider2D circleCol)
            {
                Gizmos.DrawSphere(transform.position + (Vector3)circleCol.offset, circleCol.radius);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = Color.green;
            
            if (col is BoxCollider2D boxCol)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(boxCol.offset, boxCol.size);
            }
            else if (col is CircleCollider2D circleCol)
            {
                Gizmos.DrawWireSphere(transform.position + (Vector3)circleCol.offset, circleCol.radius);
            }
        }
    }
}
