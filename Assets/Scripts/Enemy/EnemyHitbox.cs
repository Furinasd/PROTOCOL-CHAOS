using UnityEngine;
using System.Collections.Generic;

// ==========================================
// Title: 敌方纯代码伤害判定盒 (OverlapBox 方案)
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

    // Zero-GC: 预分配 OverlapBox 结果缓存，避免每帧堆分配触发 GC。
    // Phase4 下同层碰撞体可能显著增多，容量过小会导致 NonAlloc 截断并漏判。
    private readonly Collider[] hitBuffer = new Collider[64];
    private readonly Collider[] nearMissBuffer = new Collider[64];

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
        
        // 【核心修复】：计算缩放后的实际体积。OverlapBox 需要半长宽高。
        // 原本忽略了 transform.lossyScale，导致物理判定区域比 Gizmos 看到的要小。
        Vector3 scaledHalfSize = Vector3.Scale(hitboxSize, transform.lossyScale) / 2f;

        // 1. 核心区伤害判定 (正常受击与弹刀)
        int hitCount = Physics.OverlapBoxNonAlloc(worldCenter, scaledHalfSize, hitBuffer, transform.rotation, targetLayer);
        if (hitCount >= hitBuffer.Length)
        {
            Debug.LogWarning($"[EnemyHitbox] 命中盒结果达到缓存上限({hitBuffer.Length})，可能存在漏判，请收紧 targetLayer 或继续扩容缓存。", this);
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            // 尝试获取玩家的受击接口
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null) 
            {
                // Debug.Log($"[Hitbox] {gameObject.name} 触碰了 {hit.name}，但其父级未找到 IDamageable 接口。");
                continue;
            }

            // 如果这个接口组件在这一刀里已经挨过打了，直接跳过
            if (alreadyHitComponents.Contains(damageable)) 
            {
                // Debug.Log($"[Hitbox] {gameObject.name} 再次触碰了 {hit.name}，已忽略重复命中。");
                continue;
            }

            // 记录为已命中接口
            alreadyHitComponents.Add(damageable);
            Debug.Log($"<color=orange>[Hitbox] {gameObject.name} 成功命中目标：{hit.name}</color>");

            // 发送伤害数据包，玩家根据自身极性和状态进行博弈结算
            currentAttackData.hitDirection = (hit.transform.position - transform.position).normalized;
            
            // 返回值 isPerfectParried 是玩家告诉怪物："我完美弹反了你的攻击！"
            bool isPerfectParried = damageable.TakeDamage(currentAttackData);

            if (isPerfectParried)
            {
                Debug.Log("💥 怪物：我的攻击被极性湮灭弹回了！即将进入大硬直。");

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
            }
        }

        // 2. 边缘区/近失 (Graze) 判定：只用于捕捉那些用物理位移躲开核心区的玩家
        // 扩大 1.5 倍判定框，专门奖励成功蹭破攻击边缘的玩家
        Vector3 nearMissHalfSize = scaledHalfSize * 1.5f; 
        int nearMissCount = Physics.OverlapBoxNonAlloc(worldCenter, nearMissHalfSize, nearMissBuffer, transform.rotation, targetLayer);
        if (nearMissCount >= nearMissBuffer.Length)
        {
            Debug.LogWarning($"[EnemyHitbox] NearMiss 结果达到缓存上限({nearMissBuffer.Length})，可能存在漏判，请收紧 targetLayer 或继续扩容缓存。", this);
        }
        for (int i = 0; i < nearMissCount; i++)
        {
            Collider hit = nearMissBuffer[i];
            PlayerCombatReceiver receiver = hit.GetComponentInParent<PlayerCombatReceiver>();
            if (receiver != null && !alreadyHitComponents.Contains(receiver))
            {
                // 如果刚好在冲刺/跳跃瞬间擦弹
                if (receiver.CheckPerfectDodgeNearMiss())
                {
                    alreadyHitComponents.Add(receiver); // 同样记录防重复
                }
            }
        }
    }

    // 可视化辅助工具！
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isActive ? new Color(1, 0, 0, 0.5f) : new Color(0, 1, 0, 0.2f);
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.matrix = rotationMatrix;
        Gizmos.DrawCube(hitboxCenterOffset, hitboxSize);
    }
}
