using UnityEngine;
using DG.Tweening;

/// <summary>
/// 极简模式：不再显示头顶血条，仅负责将受击事件转发给全局 CombatHUDManager 以显示伤害数字。
/// </summary>
[RequireComponent(typeof(EnemyPosture))]
public class EnemyWorldHUD : MonoBehaviour
{
    private EnemyPosture posture;
    private float lastHpRaw;

    private void Awake()
    {
        posture = GetComponent<EnemyPosture>();
        if (posture == null)
        {
            Destroy(this);
            return;
        }
        
        // 订阅生命值变化以触发全局伤害数字
        posture.OnHealthChanged += HandleHealthChanged;
        lastHpRaw = posture.currentHP;
    }

    private void OnDestroy()
    {
        if (posture != null)
            posture.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(float current, float max)
    {
        float damage = Mathf.Max(0f, lastHpRaw - current);
        lastHpRaw = current;

        if (damage > 0.01f)
        {
            // 转发至全局对象池显示漂字
            CombatHUDManager.Instance?.SpawnHeadDamagePopup(transform.position + Vector3.up * 2.5f, Mathf.RoundToInt(damage));
        }
    }

    // 外部调用接口保持兼容（空实现）
    public void ForceSyncNow() { }
    public void TriggerAnomalyCoreParryPulse() { }
}
