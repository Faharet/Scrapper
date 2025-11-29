using UnityEngine;
using System.Collections;

/// <summary>
/// Per-character exposure/brightness controller (ONLY affects this character's sprites).
/// Usage:
/// 1. (Preferred) Assign a material using a custom sprite shader with a float _Exposure (default 1.0).
///    ShaderGraph: multiply final color.rgb * _Exposure.
/// 2. If no _Exposure property exists, falls back to multiplying SpriteRenderer.color.
/// 3. Call SetExposure(value) or PulseExposure(peak, duration).
/// 4. Optional: call BindAdrenaline(AdrenalineSystem sys) to pulse on heal.
/// </summary>
[DisallowMultipleComponent]
public class SpriteExposureController : MonoBehaviour
{
    [Header("Target & Mode")]
    [Tooltip("If empty, auto-grabs all SpriteRenderers in children.")]
    [SerializeField] private SpriteRenderer[] spriteRenderers;

    [Tooltip("Minimum exposure (safety clamp).")]
    [SerializeField] private float minExposure = 0.2f;

    [Tooltip("Maximum exposure (safety clamp).")]
    [SerializeField] private float maxExposure = 3.0f;

    [Tooltip("Start exposure at Awake.")]
    [SerializeField] private float startExposure = 1.0f;

    [Tooltip("If true, uses color multiplication fallback even if material has _Exposure.")]
    [SerializeField] private bool forceColorMode = false;

    [Header("Lerp Settings")]
    [Tooltip("Default lerp duration for SmoothExposure.")]
    [SerializeField] private float defaultLerpTime = 0.35f;

    [Header("Pulse Settings")]
    [Tooltip("Curve for pulse shaping (0..1 time). Value multiplies peak exposure.")]
    [SerializeField] private AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Return exposure to baseline after pulse.")]
    [SerializeField] private bool restoreAfterPulse = true;

    [Tooltip("Baseline exposure after pulse ends.")]
    [SerializeField] private float baselineExposure = 1.0f;

    private bool hasExposureProperty = false;
    private float currentExposure;
    private Coroutine pulseRoutine;
    private Coroutine lerpRoutine;

    void Awake()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>();

        // Detect exposure property on first material
        foreach (var sr in spriteRenderers)
        {
            if (sr != null && sr.sharedMaterial != null && sr.sharedMaterial.HasProperty("_Exposure"))
            {
                hasExposureProperty = true;
                break;
            }
        }
        currentExposure = Mathf.Clamp(startExposure, minExposure, maxExposure);
        ApplyExposureImmediate(currentExposure);
    }

    /// <summary>Immediate exposure set without lerp.</summary>
    public void SetExposure(float value)
    {
        value = Mathf.Clamp(value, minExposure, maxExposure);
        currentExposure = value;
        ApplyExposureImmediate(value);
    }

    /// <summary>Lerp exposure to target over duration.</summary>
    public void SmoothExposure(float target, float duration = -1f)
    {
        if (duration <= 0f) duration = defaultLerpTime;
        target = Mathf.Clamp(target, minExposure, maxExposure);
        if (lerpRoutine != null) StopCoroutine(lerpRoutine);
        lerpRoutine = StartCoroutine(LerpExposureRoutine(target, duration));
    }

    /// <summary>Pulses exposure to a peak then back (optional).</summary>
    public void PulseExposure(float peak, float duration)
    {
        peak = Mathf.Clamp(peak, minExposure, maxExposure);
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulseRoutine(peak, duration));
    }

    /// <summary>Bind to AdrenalineSystem events (optional).</summary>
    public void BindAdrenaline(AdrenalineSystem sys, float healPulsePeak = 2f, float healPulseDuration = 0.4f)
    {
        if (sys == null) return;
        sys.onAdrenalineHeal.AddListener(() => PulseExposure(healPulsePeak, healPulseDuration));
    }

    private IEnumerator LerpExposureRoutine(float target, float duration)
    {
        float start = currentExposure;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float v = Mathf.Lerp(start, target, p);
            ApplyExposureImmediate(v);
            yield return null;
        }
        ApplyExposureImmediate(target);
    }

    private IEnumerator PulseRoutine(float peak, float duration)
    {
        float start = currentExposure;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float shaped = pulseCurve != null ? pulseCurve.Evaluate(p) : p;
            float v = Mathf.Lerp(start, peak, shaped);
            ApplyExposureImmediate(v);
            yield return null;
        }
        if (restoreAfterPulse)
        {
            ApplyExposureImmediate(baselineExposure);
            currentExposure = baselineExposure;
        }
    }

    private void ApplyExposureImmediate(float value)
    {
        currentExposure = value;
        if (hasExposureProperty && !forceColorMode)
        {
            foreach (var sr in spriteRenderers)
            {
                if (sr == null) continue;
                // Use per-instance material to avoid editing shared material (unless desired)
                if (sr.material != null && sr.material.HasProperty("_Exposure"))
                    sr.material.SetFloat("_Exposure", value);
            }
        }
        else
        {
            // Fallback multiply color (keep alpha)
            foreach (var sr in spriteRenderers)
            {
                if (sr == null) continue;
                Color baseColor = sr.color;
                // Assume 1.0 base intensity; clamp to avoid overflow on UI
                float mult = value;
                sr.color = new Color(Mathf.Clamp01(baseColor.r * mult), Mathf.Clamp01(baseColor.g * mult), Mathf.Clamp01(baseColor.b * mult), baseColor.a);
            }
        }
    }
}
