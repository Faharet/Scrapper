using UnityEngine;

/// <summary>
/// Управляет фоновой музыкой в сцене
/// Автоматически воспроизводит музыку при запуске сцены и сохраняет между сценами
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BackgroundMusicManager : MonoBehaviour
{
    [Header("Music Settings")]
    [Tooltip("Музыкальный трек для этой сцены")]
    [SerializeField] private AudioClip musicClip;
    
    [Tooltip("Громкость музыки (0-1)")]
    [SerializeField] [Range(0f, 1f)] private float volume = 0.5f;
    
    [Tooltip("Зациклить музыку")]
    [SerializeField] private bool loop = true;
    
    [Tooltip("Сохранять музыку между сценами (DontDestroyOnLoad)")]
    [SerializeField] private bool persistBetweenScenes = true;

    [Header("Fade Settings")]
    [Tooltip("Плавное появление при старте (секунды)")]
    [SerializeField] private float fadeInDuration = 2f;
    
    [Tooltip("Плавное затухание при смене музыки (секунды)")]
    [SerializeField] private float fadeOutDuration = 1.5f;

    private AudioSource audioSource;
    private static BackgroundMusicManager instance;
    private float targetVolume;
    private bool isFading = false;
    private float fadeTimer = 0f;
    private float fadeStartVolume = 0f;
    private float fadeEndVolume = 0f;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        targetVolume = volume;

        // Singleton с сохранением между сценами
        if (persistBetweenScenes)
        {
            if (instance != null && instance != this)
            {
                // Если уже есть музыка и это тот же трек - не создаём новый
                if (instance.audioSource.clip == musicClip)
                {
                    Destroy(gameObject);
                    return;
                }
                else
                {
                    // Другой трек - плавно меняем музыку
                    instance.ChangeMusicWithFade(musicClip, volume);
                    Destroy(gameObject);
                    return;
                }
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        SetupAudioSource();
        PlayMusicWithFadeIn();
    }

    private void SetupAudioSource()
    {
        audioSource.clip = musicClip;
        audioSource.loop = loop;
        audioSource.playOnAwake = false;
        audioSource.volume = 0f; // Начинаем с 0 для fade in
    }

    private void PlayMusicWithFadeIn()
    {
        if (musicClip == null)
        {
            Debug.LogWarning("BackgroundMusicManager: музыкальный клип не назначен!");
            return;
        }

        audioSource.Play();

        if (fadeInDuration > 0f)
        {
            StartFade(0f, targetVolume, fadeInDuration);
        }
        else
        {
            audioSource.volume = targetVolume;
        }
    }

    private void ChangeMusicWithFade(AudioClip newClip, float newVolume)
    {
        if (newClip == musicClip) return;

        StopAllCoroutines();
        StartCoroutine(ChangeMusicCoroutine(newClip, newVolume));
    }

    private System.Collections.IEnumerator ChangeMusicCoroutine(AudioClip newClip, float newVolume)
    {
        // Fade out текущей музыки
        if (fadeOutDuration > 0f)
        {
            StartFade(audioSource.volume, 0f, fadeOutDuration);
            yield return new WaitForSeconds(fadeOutDuration);
        }

        // Меняем клип
        audioSource.Stop();
        audioSource.clip = newClip;
        musicClip = newClip;
        targetVolume = newVolume;

        // Fade in новой музыки
        audioSource.Play();
        if (fadeInDuration > 0f)
        {
            StartFade(0f, targetVolume, fadeInDuration);
        }
        else
        {
            audioSource.volume = targetVolume;
        }
    }

    private void StartFade(float fromVolume, float toVolume, float duration)
    {
        isFading = true;
        fadeTimer = 0f;
        fadeStartVolume = fromVolume;
        fadeEndVolume = toVolume;
        audioSource.volume = fromVolume;
    }

    private void Update()
    {
        if (isFading)
        {
            fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTimer / Mathf.Max(fadeInDuration, fadeOutDuration));
            audioSource.volume = Mathf.Lerp(fadeStartVolume, fadeEndVolume, t);

            if (t >= 1f)
            {
                isFading = false;
                audioSource.volume = fadeEndVolume;
            }
        }
    }

    /// <summary>
    /// Изменить громкость музыки
    /// </summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        targetVolume = volume;
        if (!isFading)
            audioSource.volume = volume;
    }

    /// <summary>
    /// Поставить музыку на паузу
    /// </summary>
    public void Pause()
    {
        if (audioSource.isPlaying)
            audioSource.Pause();
    }

    /// <summary>
    /// Возобновить музыку
    /// </summary>
    public void Resume()
    {
        if (!audioSource.isPlaying)
            audioSource.UnPause();
    }

    /// <summary>
    /// Остановить музыку с плавным затуханием
    /// </summary>
    public void StopMusicWithFade()
    {
        StartFade(audioSource.volume, 0f, fadeOutDuration);
        Invoke(nameof(StopMusic), fadeOutDuration);
    }

    private void StopMusic()
    {
        audioSource.Stop();
    }

    /// <summary>
    /// Получить текущий экземпляр менеджера музыки
    /// </summary>
    public static BackgroundMusicManager Instance => instance;
}
