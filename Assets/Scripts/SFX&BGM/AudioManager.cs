using UnityEngine;
using DG.Tweening;

// ==========================================
// Title: 全局音频管理器 (ACT 动作游戏定制)
// Description: 
//   1. 支持主 BGM 音轨、常规音效(SFX)、高光音效(Highlight)。
//   2. 完美适配 Hitlag(顿帧)：普通 SFX 会随时间停止暂停/变调，
//      而高光音效（弹刀、处决金属声）忽略 TimeScale，清脆爆发。
//   3. 宇宙真空感 (Ducking)：在处决瞬间压爆 BGM 音量，突出高光音效。
// ==========================================
[DefaultExecutionOrder(-50)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("🎵 音轨组 (运行时自动生成)")]
    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private AudioSource highlightSource; // 高光无视暂停通道

    private float originalBgmVolume = 1f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 如果是在 Loader 场景实例化，可以取消注释以跨场景存活
            // DontDestroyOnLoad(gameObject);
            InitializeSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeSources()
    {
        bgmSource = gameObject.AddComponent<AudioSource>();
        sfxSource = gameObject.AddComponent<AudioSource>();
        highlightSource = gameObject.AddComponent<AudioSource>();

        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        sfxSource.loop = false;
        sfxSource.playOnAwake = false;

        highlightSource.loop = false;
        highlightSource.playOnAwake = false;
        
        // 【ACT 游戏听觉核心机制】
        // 当发生极性湮灭或完美弹刀时，游戏会触发 Time.timeScale = 0.01f 的顿帧。
        // 普通的音效会随时间减慢（或暂停），但"清脆的打铁声 / UI 提示音"必须无视物理时间，
        // 此时通过 ignoreListenerPause 并配合 AudioListener.pause 即可完美实现！
        highlightSource.ignoreListenerPause = true; 
    }

    /// <summary>
    /// 播放背景音乐
    /// </summary>
    public void PlayBGM(AudioClip clip, float volume = 0.5f)
    {
        if (clip == null || bgmSource.clip == clip) return;
        
        originalBgmVolume = volume;
        bgmSource.clip = clip;
        bgmSource.volume = volume;
        bgmSource.Play();
    }

    /// <summary>
    /// 播放常规音效（受游戏顿帧和变调影响）
    /// 例如：脚步声、挥砍风声、受击惨叫
    /// </summary>
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    /// <summary>
    /// 播放高光音效（无视时间减慢与暂停，绝对清脆）
    /// 例如：极其核心的完美弹反清脆金属声、处决音效、获得 UI 能量声
    /// </summary>
    public void PlayHighlightSFX(AudioClip clip, float volume = 1f)
    {
        if (clip != null)
        {
            highlightSource.PlayOneShot(clip, volume);
        }
    }

    /// <summary>
    /// 🌌 "全宇宙安静" 真空感压制 (Ducking)
    /// </summary>
    /// <param name="duration">回弹需要的时间（秒）</param>
    /// <param name="duckRatio">压制倍率（例如 0.2 代表瞬间降至原本20%的音量）</param>
    public void TriggerVacuumEffect(float duration = 1.5f, float duckRatio = 0.1f)
    {
        bgmSource.DOKill();
        
        // 瞬间压暗 BGM
        bgmSource.volume = originalBgmVolume * duckRatio;
        
        // 缓慢恢复（SetUpdate(true) 保证在顿帧时间冻结期间，BGM音量仍按真实时间回弹）
        bgmSource.DOFade(originalBgmVolume, duration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
    }
}
