using UnityEngine;
using System.Collections;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 触发顿帧 (Hitstop)
    /// </summary>
    /// <param name="duration">持续时间 (秒)</param>
    /// <param name="timeScale">缩放基准 (通常为 0 或 0.05)</param>
    public void DoHitstop(float duration, float timeScale = 0.05f)
    {
        StopAllCoroutines();
        StartCoroutine(HitstopRoutine(duration, timeScale));
    }

    private IEnumerator HitstopRoutine(float duration, float timeScale)
    {
        float originalScale = 1.0f;
        Time.timeScale = timeScale;
        
        // 使用 RealTimeSinceStartup 因为 timeScale 可能会被设为 0
        yield return new WaitForSecondsRealtime(duration);
        
        Time.timeScale = originalScale;
    }
}
