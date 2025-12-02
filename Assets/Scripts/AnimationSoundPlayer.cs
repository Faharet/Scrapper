using UnityEngine;

/// <summary>
/// Компонент для воспроизведения звуков через Animation Events
/// Добавь этот компонент на объект с Animator
/// В Animation создай Event и вызови PlaySound или PlayRandomSound
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AnimationSoundPlayer : MonoBehaviour
{
    [System.Serializable]
    public class SoundClip
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)]
        public float volume = 1f;
        [Range(0f, 2f)]
        public float pitch = 1f;
    }

    [Header("Sound Library")]
    [Tooltip("Библиотека звуков - добавь сюда все звуки для анимаций")]
    [SerializeField] private SoundClip[] sounds;

    [Header("Audio Source Settings")]
    [Tooltip("Использовать отдельный AudioSource для каждого звука (для наложения)")]
    [SerializeField] private bool createSeparateSourcePerSound = false;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Настройки по умолчанию
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D звук
    }

    /// <summary>
    /// Воспроизвести звук по имени (вызывается из Animation Event)
    /// </summary>
    /// <param name="soundName">Имя звука из библиотеки</param>
    public void PlaySound(string soundName)
    {
        SoundClip sound = GetSoundByName(soundName);
        if (sound == null)
        {
            Debug.LogWarning($"AnimationSoundPlayer: Звук '{soundName}' не найден в библиотеке!");
            return;
        }

        if (sound.clip == null)
        {
            Debug.LogWarning($"AnimationSoundPlayer: AudioClip для '{soundName}' не назначен!");
            return;
        }

        if (createSeparateSourcePerSound)
        {
            // Создаём временный AudioSource для наложения звуков
            AudioSource.PlayClipAtPoint(sound.clip, transform.position, sound.volume);
        }
        else
        {
            audioSource.pitch = sound.pitch;
            audioSource.PlayOneShot(sound.clip, sound.volume);
        }
    }

    /// <summary>
    /// Воспроизвести случайный звук из группы (для вариативности)
    /// Имена должны быть через запятую: "attack1,attack2,attack3"
    /// </summary>
    public void PlayRandomSound(string soundNames)
    {
        string[] names = soundNames.Split(',');
        if (names.Length == 0) return;

        string randomName = names[Random.Range(0, names.Length)].Trim();
        PlaySound(randomName);
    }

    /// <summary>
    /// Воспроизвести звук с кастомной громкостью
    /// </summary>
    public void PlaySoundWithVolume(string soundNameAndVolume)
    {
        // Формат: "soundName:0.5"
        string[] parts = soundNameAndVolume.Split(':');
        string soundName = parts[0].Trim();
        float volume = parts.Length > 1 ? float.Parse(parts[1]) : 1f;

        SoundClip sound = GetSoundByName(soundName);
        if (sound != null && sound.clip != null)
        {
            audioSource.pitch = sound.pitch;
            audioSource.PlayOneShot(sound.clip, volume);
        }
    }

    /// <summary>
    /// Остановить текущий звук
    /// </summary>
    public void StopSound()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    private SoundClip GetSoundByName(string name)
    {
        foreach (var sound in sounds)
        {
            if (sound.name == name)
                return sound;
        }
        return null;
    }

    // Дополнительные методы для удобства

    /// <summary>
    /// Для атак игрока
    /// </summary>
    public void PlayAttackSound()
    {
        PlaySound("attack");
    }

    /// <summary>
    /// Для получения урона
    /// </summary>
    public void PlayHitSound()
    {
        PlaySound("hit");
    }

    /// <summary>
    /// Для шагов
    /// </summary>
    public void PlayFootstep()
    {
        PlayRandomSound("footstep1,footstep2,footstep3");
    }

    /// <summary>
    /// Для прыжка
    /// </summary>
    public void PlayJumpSound()
    {
        PlaySound("jump");
    }

    /// <summary>
    /// Для приземления
    /// </summary>
    public void PlayLandSound()
    {
        PlaySound("land");
    }

    /// <summary>
    /// Для рывка
    /// </summary>
    public void PlayDashSound()
    {
        PlaySound("dash");
    }

    /// <summary>
    /// Для смерти
    /// </summary>
    public void PlayDeathSound()
    {
        PlaySound("death");
    }
}
