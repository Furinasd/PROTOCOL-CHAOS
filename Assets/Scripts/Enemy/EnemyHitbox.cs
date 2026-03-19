using UnityEngine;
using System.Collections.Generic;

// ==========================================
// Title: 敌方纯代码伤害判定盒 (OverlapBox 方案)
// Author: 白糖 & 精灵小姐 (Refactored by TD)
// Description: 脱离物理引擎黑盒，实现完全由状态机驱动的精准打击判定。
// 附带防重复击中(HashSet)与可视化调试(Gizmos)。
// ==========================================
public class EnemyHitbox : MonoBehaviour
{
    [Header("📐 碰撞盒参数 (Gizmos可视化)")]
    public Vector3 hitboxCenterOffset;
    public Vector3 hitboxSize = new Vector3(2f, 2f, 2f);
    public LayerMask targetLayer; // 设置为 Player 所在的 Layer

    [Header("⚔️ 战斗数据")]
    private AttackData currentAttackData;
    private bool isActive = false;

    // 防止在同一次攻击动作中，对同一个目标造成多次伤害
    private HashSet<IDamageable> alreadyHitComponents = new HashSet<IDamageable>();

    /// <summary>
    /// 激活判定盒（由 EnemyAttackBrain 在进入 Attacking 状态时调用）
    /// </summary>
    public void ActivateHitbox(AttackData data)
    {
        currentAttackData = data;
        isActive = true;
        alreadyHitComponents.Clear(); // 每次挥刀清空已命中列表
    }

    /// <summary>
    /// 关闭判定盒（由 EnemyAttackBrain 在 Attacking 结束时调用）
    /// </summary>
    public void DeactivateHitbox()
    {
        isActive = false;
        alreadyHitComponents.Clear();
    }

    private void Update()
    {
        // 只有在被 Brain 激活的短短零点几秒内，才执行高性能扫描
        if (!isActive) return;

        CheckCollision();
    }

    private void CheckCollision()
    {
        // 计算实际的世界坐标中心点
        Vector3 worldCenter = transform.position + transform.TransformDirection(hitboxCenterOffset);

        // 核心代码：执行 Box 相交检测
        Collider[] hits = Physics.OverlapBox(worldCenter, hitboxSize / 2f, transform.rotation, targetLayer);

        foreach (Collider hit in hits)
        {
            // 尝试获取玩家的受击接口
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            // 如果这个接口组件在这一刀里已经挨过打了，直接跳过
            if (alreadyHitComponents.Contains(damageable)) continue;

            // 记录为已命中接口
            alreadyHitComponents.Add(damageable);

            // 发送伤害数据包，玩家根据自身极性和状态进行博弈结算
            currentAttackData.hitDirection = (hit.transform.position - transform.position).normalized;
            
            // 返回值 isPerfectParried 是玩家告诉怪物："我完美弹反了你的攻击！"
            bool isPerfectParried = damageable.TakeDamage(currentAttackData);

            if (isPerfectParried)
            {
                Debug.Log("💥 怪物：我的攻击被极性湮灭弹回了！");

                // 1. 调用系统全局反馈（强烈震动+顿帧）
                if (CombatFeedbackManager.Instance != null)
                {
                    CombatFeedbackManager.Instance.TriggerParryFeedback();
                }

                // 2. 怪物遭到反噬：让其直接进入大硬直（通过打满 posture）
                EnemyPosture posture = GetComponentInParent<EnemyPosture>();
                if (posture != null)
                {
                    posture.AddPosture(posture.maxPosture); 
                }
                else
                {
                    Debug.LogWarning("[EnemyHitbox] 弹刀成功，但未能找到怪物的 EnemyPosture 以触发晕眩！");
                }
            }
        }
    }

    // TD 专属：可视化辅助工具！
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isActive ? new Color(1, 0, 0, 0.5f) : new Color(0, 1, 0, 0.2f);
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.matrix = rotationMatrix;
        Gizmos.DrawCube(hitboxCenterOffset, hitboxSize);
    }
}
