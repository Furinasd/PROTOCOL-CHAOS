using UnityEngine;

// ==========================================
// Title: 敌人攻击判定盒 (Enemy Hitbox)
// Description: 挂载在怪物攻击期间动态生成的判定碰撞体上。
//              当玩家碰触此触发器，自动向玩家派发 AttackData 数据包。
//              玩家身上的 PlayerCombatReceiver 实现 IDamageable 接口，负责处理后续博弈逻辑。
// ==========================================

[RequireComponent(typeof(Collider))]
public class EnemyHitbox : MonoBehaviour
{
    [Header("▶ 攻击数据 (由 EnemyShapeMorpher 或 EnemyCombatEntity 填充)")]
    public Polarity attackPolarity = Polarity.Red;
    public float damage = 10f;
    public float postureDamage = 20f;
    
    [Header("空间高度判定 (用于玩家闪避校验)")]
    public bool IsLowAttack;  // 下段横扫：玩家跳跃可无视
    public bool IsHighAttack; // 上段重砸：玩家下蹲/冲刺可无视
    
    [Header("来源引用 (向旧接口兼容)")]
    [Tooltip("若不为空，弹刀成功后反向打给怪物的熵值伤害将通过此引用传递")]
    public EnemyPosture OwnerPosture; // 保留：PlayerCombatReceiver 仍在使用
    
    // 旧接口桥接：EnemyShapeMorpher 等脚本仍使用 PolarityColor，此属性做自动转换
    public PolarityColor AttackColor
    {
        get => PolarityBridge.ToPolarityColor(attackPolarity);
        set => attackPolarity = PolarityBridge.FromPolarityColor(value);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 检查碰到的物体是否实现了 IDamageable（即是否是玩家受击体）
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null) return;

        // 组装攻击数据包
        AttackData data = new AttackData
        {
            damage = this.damage,
            postureDamage = this.postureDamage,
            polarity = this.attackPolarity,
            sourcePosition = transform.position,
            sourceObject = OwnerPosture != null ? OwnerPosture.gameObject : gameObject
        };

        // 派发给受击目标，接收反馈（true = 完美弹刀成功，攻击者要被硬直）
        bool isPerfectParry = damageable.TakeDamage(data);

        if (isPerfectParry && OwnerPosture != null)
        {
            // 弹刀反馈：给怪物施加大量熵值（让玩家感受到"破防"的成就感）
            Debug.Log("[EnemyHitbox] 完美弹刀！反向给怪物施加巨额熵值！");
            // OwnerPosture.AddPosture(data.postureDamage * 2f); // 可在接入完整结算后启用
        }
    }
}
