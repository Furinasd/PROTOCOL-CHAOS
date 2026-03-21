using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

// ==========================================
// Title: 战斗反馈与打击感调度中心 (Combat Feedback Manager)
// Description: 统筹顿帧(Hitlag)、相机震动(Camera Shake)与时间缩放。
//              践行逻辑与表现解耦的原则——战斗逻辑只需调用此单例，
//              无需关心如何控制摄像机或 TimeScale 细节。
// ==========================================

[RequireComponent(typeof(CinemachineImpulseSource))]
public class CombatFeedbackManager : MonoBehaviour
{
    // 单例：全局极速调用
    public static CombatFeedbackManager Instance { get; private set; }

    [Header("🎥 相机震动参数 (Cinemachine Impulse)")]
    private CinemachineImpulseSource impulseSource;
    
    [Tooltip("完美弹刀时的震动强度（夸张）")]
    public float parryShakeForce = 2.0f;
    
    [Tooltip("受击时的震动强度")]
    public float hitShakeForce = 1.0f;
    
    [Tooltip("宕机处决时的震动强度（最强）")]
    public float executeShakeForce = 3.0f;

    [Header("⏱️ 顿帧安全锁 (Hitlag)")]
    private bool isHitlagging = false;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }
        
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    // ──────────────────────────────────
    // 公开接口：由游戏逻辑调用（3种场合）
    // ──────────────────────────────────

    /// <summary>
    /// 【完美弹刀】综合反馈：强烈凝滞感 + 剧烈震动
    /// </summary>
    public void TriggerParryFeedback()
    {
        GenerateImpulse(parryShakeForce);
        TriggerHitlag(timeScale: 0.05f, duration: 0.15f);
    }

    /// <summary>
    /// 【普通受伤】综合反馈：短促顿帧 + 中等震动
    /// </summary>
    public void TriggerDamageFeedback()
    {
        GenerateImpulse(hitShakeForce);
        TriggerHitlag(timeScale: 0.4f, duration: 0.1f);
    }

    /// <summary>
    /// 【宕机处决】综合反馈：最夸张的震动 + 极致顿帧（极客感）。
    /// </summary>
    public void TriggerAnnihilationFeedback()
    {
        GenerateImpulse(executeShakeForce);
        // 执行瞬间给一个短暂的“极寒”冻结感，然后瞬间恢复
        TriggerHitlag(timeScale: 0.02f, duration: 0.25f);
        
        Debug.Log("<color=black>⬛ [Feedback] 处决瞬间：时空停滞，万物死寂。</color>");
    }

    // ──────────────────────────────────
    // 内部实现
    // ──────────────────────────────────

    private void GenerateImpulse(float force)
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulseWithForce(force);
        }
    }

    /// <summary>
    /// 核心顿帧控制器：使用 unscaledDeltaTime 等待，防止 TimeScale 被自身吞噬。
    /// 内置安全锁，防止连续触发导致恢复时序错乱。
    /// </summary>
    private void TriggerHitlag(float timeScale, float duration)
    {
        if (isHitlagging) return;
        StartCoroutine(HitlagCoroutine(timeScale, duration));
    }

    private IEnumerator HitlagCoroutine(float timeScale, float duration)
    {
        isHitlagging = true;
        float originalTimeScale = Time.timeScale;
        Time.timeScale = timeScale;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // 关键：不受TimeScale影响的等待
            yield return null;
        }

        Time.timeScale = originalTimeScale;
        isHitlagging = false;
    }
}
