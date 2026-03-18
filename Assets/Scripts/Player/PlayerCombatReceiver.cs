using UnityEngine;
using UnityEngine.InputSystem;

// ==========================================
// Title: 玩家战斗受击处理器 (Player Combat Receiver)
// Description: 处理玩家被怪物命中时的完整博弈逻辑。
//              实现 IDamageable 接口，由 EnemyHitbox 直接调用 TakeDamage。
//              包含：空间规避、同色吸收、异色受伤、完美弹刀四大判定轨道。
// ==========================================

[RequireComponent(typeof(DemoPlayerController), typeof(PlayerPolarity))]
public class PlayerCombatReceiver : MonoBehaviour, IDamageable
{
    private DemoPlayerController controller;
    private PlayerPolarity polarity;

    [Header("❤️ 生存数值")]
    public float maxHP = 100f;
    public float currentHP = 100f;

    [Header("⚡ 秩序能量 (Ultimate Gauge)")]
    public float maxOrderEnergy = 100f;
    public float currentOrderEnergy = 0f;
    public float energyPerAbsorb = 25f;

    [Header("⚔️ 弹刀反伤参数")]
    [Tooltip("完美弹刀成功对攻击来源造成的熵值反伤")]
    public float parryCounterPostureDamage = 50f;

    private void Awake()
    {
        controller = GetComponent<DemoPlayerController>();
        polarity = GetComponent<PlayerPolarity>();
    }

    private void Update()
    {
        // 兼容新版 Input System 的处决按键检测
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryExecuteEnemy();
        }
    }

    // ══════════════════════════════════════════════════
    // 核心受击博弈方程（IDamageable 接口实现）
    // 这是整个游戏趣味性的灵魂，请勿轻易修改逻辑顺序！
    // ══════════════════════════════════════════════════
    public bool TakeDamage(AttackData attack)
    {
        // 可选的基础检测：如果已死亡，拒绝继续结算
        if (currentHP <= 0) return false;

        // 从旧枚举桥接到新协议枚举
        Polarity playerPolarity = PolarityBridge.FromPolarityColor(polarity.CurrentColor);

        // ────────────────────────────────
        // 轨道一：完美弹刀判定 (最高优先级)
        // 条件：正处于 Parry 窗口期 && 异色攻击（同色无法消除）
        // ────────────────────────────────
        if (polarity.IsParryWindow && attack.polarity != playerPolarity && attack.polarity != Polarity.Neutral)
        {
            Debug.Log("<color=orange>✨ 极性湮灭！完美弹刀！时间凝滞，反震攻击者！</color>");

            // 效果1：调用反馈中枢（顿帧+强震动）
            if (CombatFeedbackManager.Instance != null)
                CombatFeedbackManager.Instance.TriggerParryFeedback();

            // 效果2：对攻击来源施加熵值反伤
            if (attack.sourceObject != null)
            {
                EnemyPosture sourcePosture = attack.sourceObject.GetComponent<EnemyPosture>();
                if (sourcePosture != null)
                {
                    sourcePosture.AddPosture(parryCounterPostureDamage);
                }
            }

            // 效果3：能量小奖励（弹刀有技巧，稍给一点）
            GainOrderEnergy(energyPerAbsorb * 0.5f);

            // 反馈给攻击者：你被完美弹了，自己进入硬直
            return true;
        }

        // ────────────────────────────────
        // 轨道二：同色吸收 (稳妥解法)
        // 条件：攻击极性与玩家当前极性相同
        // ────────────────────────────────
        if (attack.polarity == playerPolarity)
        {
            Debug.Log("<color=cyan>⚡ 秩序包容！同色攻击被吸收，能量充能！</color>");

            // 效果1：充能秩序能量
            GainOrderEnergy(energyPerAbsorb);

            // 效果2：物理击退（保持存在感，但不扣血）
            ApplyKnockback(attack.sourcePosition);

            return false;
        }

        // ────────────────────────────────
        // 轨道三：异色受伤 (博弈失败)
        // 条件：攻击极性不同 && 没有弹刀 → 全额受伤
        // ────────────────────────────────
        Debug.Log("<color=red>🩸 混沌入侵！极性不符，受到全额伤害！</color>");
        currentHP -= attack.damage;

        // 效果1：调用反馈中枢（短促顿帧+中等震动）
        if (CombatFeedbackManager.Instance != null)
            CombatFeedbackManager.Instance.TriggerDamageFeedback();

        // 效果2：进入移动硬直（剥夺控制权）
        controller.EnterHitlag(0.2f);

        if (currentHP <= 0)
        {
            Debug.Log("<color=red>💀 玩家死亡！</color>");
            // TODO: 触发死亡表现（保留接口）
        }

        return false;
    }

    // ──────────────────────────────────
    // 辅助方法
    // ──────────────────────────────────

    private void GainOrderEnergy(float amount)
    {
        currentOrderEnergy = Mathf.Min(currentOrderEnergy + amount, maxOrderEnergy);
        if (currentOrderEnergy >= maxOrderEnergy)
        {
            Debug.Log("🔥 秩序能量已满蓄！可发动大招！");
        }
    }

    private void ApplyKnockback(Vector3 attackerPos)
    {
        // CharacterController 不使用 Rigidbody，通过 controller 接口实现击退
        Vector3 knockbackDir = (transform.position - attackerPos).normalized;
        knockbackDir.y = 0;
        // 暂用调试输出占位，后续在 DemoPlayerController 中扩展 AddExternalForce 接口
        Debug.Log($"[PlayerCombatReceiver] 施加击退方向: {knockbackDir}");
    }

    private void TryExecuteEnemy()
    {
        // 球形范围扫描前方处于可处决状态的敌人
        Collider[] hits = Physics.OverlapSphere(transform.position, 3f);
        foreach (var hit in hits)
        {
            EnemyPosture target = hit.GetComponent<EnemyPosture>();
            if (target != null && target.IsBroken)
            {
                Debug.Log("💠 [处决] 玩家白盒爆发网格线，切割空间！");
                // 触发宕机处决震动反馈
                if (CombatFeedbackManager.Instance != null)
                    CombatFeedbackManager.Instance.TriggerAnnihilationFeedback();

                // 销毁目标（稍作延迟保留震动视觉），后续替换为特效爆炸逻辑
                Destroy(hit.gameObject, 0.5f);
                break;
            }
        }
    }
}
