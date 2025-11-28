using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Tooltip("Health component to follow")]
    public Health targetHealth;
    [Tooltip("UI Slider representing health")]
    public Slider slider;
    [Tooltip("Optional: world-space target to follow (e.g. the character)")]
    public Transform followTarget;
    [Tooltip("Screen offset for the bar")]
    public Vector3 screenOffset = new Vector3(0, 40, 0);

    void Start()
    {
        if (targetHealth == null)
        {
            Debug.LogWarning("HealthBar: targetHealth not set.");
            enabled = false;
            return;
        }
        if (slider != null)
        {
            slider.maxValue = targetHealth != null ? targetHealth.maxHealth : 1f;
            slider.value = targetHealth.CurrentHealth;
        }
    }

    void Update()
    {
        if (targetHealth == null) return;
        if (slider != null)
        {
            slider.value = targetHealth.CurrentHealth;
        }

        if (followTarget != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(followTarget.position) + screenOffset;
            transform.position = screenPos;
        }
    }
}
