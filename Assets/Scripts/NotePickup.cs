using UnityEngine;

/// <summary>
/// Подбираемая записка: показывает UI с текстом управления.
/// </summary>
public class NotePickup : PickupItem
{
    [Header("Note Settings")]
    [TextArea(3, 10)]
    [Tooltip("Текст записки с управлением")]
    [SerializeField] private string noteText = 
        "УПРАВЛЕНИЕ:\n\n" +
        "← → - Движение\n" +
        "X - Атака\n" +
        "C - Рывок (после подбора трубы)\n" +
        "Space - Прыжок (после подбора трубы)\n" +
        "Q - Хил (при наличии адреналина)\n\n" +
        "Собирай адреналин атакуя врагов!";
    
    [Tooltip("Ссылка на UI панель записки")]
    [SerializeField] private ControlsUI controlsUI;
    
    protected override void OnPickup()
    {
        // Показать UI с текстом
        if (controlsUI != null)
        {
            controlsUI.ShowNote(noteText);
        }
        else
        {
            // Попытка найти UI в сцене
            ControlsUI ui = FindObjectOfType<ControlsUI>();
            if (ui != null)
            {
                ui.ShowNote(noteText);
            }
            else
            {
                Debug.LogWarning("NotePickup: ControlsUI не найден! Создайте Canvas с компонентом ControlsUI.");
                Debug.Log($"📜 Записка: {noteText}");
            }
        }
        
        Debug.Log("📜 Записка подобрана!");
    }
}
