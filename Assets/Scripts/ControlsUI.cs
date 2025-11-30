using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI панель для отображения текста записки.
/// </summary>
public class ControlsUI : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Панель с текстом записки")]
    [SerializeField] private GameObject notePanel;
    
    [Tooltip("Text компонент для текста")]
    [SerializeField] private Text noteText;
    
    [Tooltip("Клавиша закрытия панели")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    
    [Tooltip("Автоматически закрыть через N секунд (0 = не закрывать)")]
    [SerializeField] private float autoCloseTime = 0f;
    
    [Tooltip("Пауза игры при открытии записки")]
    [SerializeField] private bool pauseOnShow = true;
    
    private float showTimer = 0f;
    private bool isShowing = false;
    
    void Awake()
    {
        if (notePanel != null)
            notePanel.SetActive(false);
    }
    
    void Update()
    {
        if (!isShowing) return;
        
        // Закрытие по кнопке
        if (Input.GetKeyDown(closeKey) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E))
        {
            HideNote();
            return;
        }
        
        // Автозакрытие
        if (autoCloseTime > 0f)
        {
            showTimer += Time.unscaledDeltaTime;
            if (showTimer >= autoCloseTime)
            {
                HideNote();
            }
        }
    }
    
    /// <summary>Показать записку с текстом</summary>
    public void ShowNote(string text)
    {
        if (notePanel != null)
            notePanel.SetActive(true);
        
        if (noteText != null)
            noteText.text = text;
        
        isShowing = true;
        showTimer = 0f;
        
        if (pauseOnShow)
            Time.timeScale = 0f;
        
        Debug.Log("ControlsUI: Записка показана");
    }
    
    /// <summary>Скрыть записку</summary>
    public void HideNote()
    {
        if (notePanel != null)
            notePanel.SetActive(false);
        
        isShowing = false;
        
        if (pauseOnShow)
            Time.timeScale = 1f;
        
        Debug.Log("ControlsUI: Записка закрыта");
    }
}
