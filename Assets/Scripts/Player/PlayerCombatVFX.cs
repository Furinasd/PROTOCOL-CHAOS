using UnityEngine;

// ==========================================
// Title: 玩家战斗反馈效果器 (Combat VFX Handler)
// Description: 挂载在玩家身上。管理极限闪避与同色吸收成功时的粒子爆发反馈。
//              在 PlayerCombatReceiver 中调用 TriggerParryVFX / TriggerDodgeVFX。
// ==========================================
public class PlayerCombatVFX : MonoBehaviour
{
    [Header("反馈粒子系统引用")]
    [Tooltip("极限闪避成功时爆发的蓝/白粒子（One-shot）")]
    public ParticleSystem perfectDodgeVFX;

    [Tooltip("同色吸收成功时爆发的彩色粒子（One-shot）")]
    public ParticleSystem absorptionVFX;

    private static PlayerCombatVFX _instance;
    public static PlayerCombatVFX Instance => _instance;

    private void Awake()
    {
        _instance = this;
    }

    /// <summary>
    /// 极限闪避/起跳无敌帧成功时调用。
    /// </summary>
    public void TriggerDodgeVFX()
    {
        if (perfectDodgeVFX != null)
        {
            perfectDodgeVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            perfectDodgeVFX.Play();
        }
    }

    /// <summary>
    /// 同色吸收成功时调用，颜色与玩家当前极性对应。
    /// </summary>
    public void TriggerAbsorptionVFX(Color color)
    {
        if (absorptionVFX == null) return;

        // 运行时动态修改粒子颜色以匹配极性
        var main = absorptionVFX.main;
        main.startColor = color;

        absorptionVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        absorptionVFX.Play();
    }
}
