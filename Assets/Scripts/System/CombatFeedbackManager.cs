using System.Collections;
using UnityEngine;

/// <summary>
/// 全局事件反馈系统：主要承接 TimeScale 等屏幕表现
/// </summary>
public class CombatFeedbackManager : MonoBehaviour
{
    public static CombatFeedbackManager Instance { get; private set; }

    [Header("Hitlag Settings")]
    public float hitlagTimeScale = 0.1f;
    public float hitlagDurationRealtime = 0.15f;

    [Header("Screen Shake Settings")]
    public bool enableScreenShake = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void TriggerAnnihilationFeedback()
    {
        // 1. 触发卡肉顿帧
        StartCoroutine(HitlagRoutine());

        // 2. 触发屏幕震动
        if (enableScreenShake)
        {
            // 之后可在场景内挂 Cinemachine Impulse Source
            Debug.Log("【视听表现】触发 Cinemachine 剧烈画面震动 Impulse！");
        }
    }

    private IEnumerator HitlagRoutine()
    {
        Time.timeScale = hitlagTimeScale;
        // 等待不受 TimeScale 缩放影响的真实时间延迟
        yield return new WaitForSecondsRealtime(hitlagDurationRealtime);
        Time.timeScale = 1f;
    }
}
