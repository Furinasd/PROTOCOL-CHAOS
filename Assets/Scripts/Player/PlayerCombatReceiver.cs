    using UnityEngine;

[RequireComponent(typeof(DemoPlayerController), typeof(PlayerPolarity))]
public class PlayerCombatReceiver : MonoBehaviour
{
    private DemoPlayerController controller;
    private PlayerPolarity polarity;

    private void Awake()
    {
        controller = GetComponent<DemoPlayerController>();
        polarity = GetComponent<PlayerPolarity>();
    }

    // 碰撞检测核心入口：接收怪物的命中
    private void OnTriggerEnter(Collider other)
    {
        EnemyHitbox hitbox = other.GetComponent<EnemyHitbox>();
        if (hitbox != null)
        {
            HandleAttackReception(hitbox);
        }
    }

    private void HandleAttackReception(EnemyHitbox hitbox)
    {
        // 第一轨：空间规避校验
        if ((hitbox.IsLowAttack && controller.IsJumping) || 
            (hitbox.IsHighAttack && controller.IsDodging))
        {
            Debug.Log("<color=green>【空间规避成功】无需颜色判定，无事发生</color>");
            return;
        }

        // 第二轨：极性博弈校验
        if (hitbox.AttackColor == polarity.CurrentColor)
        {
            // 状态：同色判定
            Debug.Log("<color=cyan>【同色吸收】物理击退保底，无伤害</color>");
            ApplyKnockback(hitbox.transform.position);
            return;
        }
        else
        {
            // 状态：异色判定
            if (polarity.IsParryWindow)
            {
                // 异色 + Parry 窗口期内
                Debug.Log("<color=yellow>【极性湮灭触发！】高收益奖励</color>");
                
                // 给怪物上巨额熵值
                if (hitbox.OwnerPosture != null)
                {
                    hitbox.OwnerPosture.AddPosture(50f);
                }

                // 调度全局管理器实现果汁感效果
                if (CombatFeedbackManager.Instance != null)
                {
                    CombatFeedbackManager.Instance.TriggerAnnihilationFeedback();
                }
            }
            else
            {
                // 异色未 Parry 或 发呆中
                Debug.Log("<color=red>【博弈失败】受到伤害，玩家硬直</color>");
                TakeDamage();
            }
        }
    }

    private void ApplyKnockback(Vector3 attackerPos)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 dir = (transform.position - attackerPos).normalized;
            dir.y = 0; 
            rb.AddForce(dir * 10f, ForceMode.Impulse);
        }
    }

    private void TakeDamage()
    {
        // 在此实现掉血和受击动画硬直
    }
}
