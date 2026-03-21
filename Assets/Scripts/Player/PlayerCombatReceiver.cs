using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DG.Tweening;

// ==========================================
// Title: 玩家战斗受击处理器 (Player Combat Receiver)
// Description: 处理玩家被怪物命中时的完整博弈逻辑。
//              包含：空间规避(闪避)、同色吸收、异色受伤、完美弹刀四大判定轨道。
// ==========================================

[RequireComponent(typeof(DemoPlayerController), typeof(PlayerPolarity), typeof(PlayerEnergySystem))]
public class PlayerCombatReceiver : MonoBehaviour, IDamageable
{
    private DemoPlayerController controller;
    private PlayerPolarity polarity;
    private PlayerEnergySystem energySystem;

    [Header("❤️ 生存数值")]
    public float maxHP = 100f;
    public float currentHP = 100f;

    [Header("⚔️ 弹刀反伤参数")]
    public float parryCounterPostureDamage = 50f;

    // 状态标记
    public bool isStandingOnAnomalyCore = false;
    private float lastBlockInputTime = -10f;
    private float blockWindow = 0.25f; // 左键格挡的有效判定持续时间

    private void Awake()
    {
        controller = GetComponent<DemoPlayerController>();
        polarity = GetComponent<PlayerPolarity>();
        energySystem = GetComponent<PlayerEnergySystem>();
    }

    private void Update()
    {
        // 核心：监听左键格挡输入
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            lastBlockInputTime = Time.time;
            Debug.Log("<color=white>🛡️ 玩家进入格挡姿态...</color>");
        }

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryExecuteEnemy();
        }
    }

    public bool TakeDamage(AttackData attack)
    {
        if (currentHP <= 0) return false;

        Polarity playerPolarity = PolarityBridge.FromPolarityColor(polarity.CurrentColor);

        // ────────────────────────────────
        // 轨道〇：空间规避 (闪避无敌与完美闪避)
        // ────────────────────────────────
        if (controller.IsDodging || controller.IsJumping)
        {
            // 【系统对齐】：同时检测冲刺瞬间与起跳瞬间产生的无敌回能
            float timeSinceDash = Time.time - controller.LastDashTime;
            float timeSinceJump = Time.time - controller.LastJumpTime;
            
            // 判定起跳或冲刺前 0.25 秒内属于完美规避
            if (timeSinceDash <= 0.25f || timeSinceJump <= 0.25f)
            {
                Debug.Log($"<color=white>💨 完美规避！(Dash:{timeSinceDash:F2}s / Jump:{timeSinceJump:F2}s) 能量 +2</color>");
                energySystem.AddEnergy(2); 
                
                if (CombatFeedbackManager.Instance != null)
                    CombatFeedbackManager.Instance.TriggerDamageFeedback(); 
            }
            else
            {
                Debug.Log("<color=grey>💨 空间规避成功。</color>");
            }
            return false;
        }

        // ────────────────────────────────
        // 轨道一：完美弹刀判定 (左键点击 + 异色攻击)
        // ────────────────────────────────
        bool isBlocking = (Time.time - lastBlockInputTime) <= blockWindow;
        if (isBlocking && attack.polarity != playerPolarity && attack.polarity != Polarity.Neutral)
        {
            int cost = isStandingOnAnomalyCore ? 0 : 2; // 弹刀消耗改为 2 格！高光区保持 0 消耗。

            if (energySystem.TryConsumeEnergy(cost))
            {
                Debug.Log("<color=orange>✨ 极性湮灭！完美弹刀！</color>");

                if (CombatFeedbackManager.Instance != null)
                    CombatFeedbackManager.Instance.TriggerParryFeedback();

                if (attack.sourceObject != null)
                {
                    EnemyPosture sourcePosture = attack.sourceObject.GetComponent<EnemyPosture>();
                    if (sourcePosture != null)
                    {
                        float finalDamage = parryCounterPostureDamage * (isStandingOnAnomalyCore ? 3f : 1f);
                        sourcePosture.AddPosture(finalDamage);
                    }
                }

                // 踩在高光区完美弹刀触发全屏净化！
                if (isStandingOnAnomalyCore)
                {
                    Debug.Log("<color=yellow>🌟 异常源核心区逆转！全场污染净化！</color>");
                    PuddleManager.TriggerGlobalPurify();
                }

                return true;
            }
            else
            {
                Debug.Log("<color=red>🩸 能量不足！强行越级弹刀导致防御溃散，受到全额惩罚！</color>");
                currentHP -= attack.damage;
                
                // 强化惩罚反馈：强震动 + 明显的受击顿帧
                if (CombatFeedbackManager.Instance != null)
                {
                    CombatFeedbackManager.Instance.TriggerDamageFeedback();
                    // 额外给一个特大震动，代表“破盾”
                    transform.DOShakePosition(0.4f, 0.5f, 20, 90, false, true);
                }
                
                controller.EnterHitlag(0.6f); // 极大的防守溃散硬直惩罚
                return false;
            }
        }

        // ────────────────────────────────
        // 轨道二：同色吸收 (稳妥解法)
        // ────────────────────────────────
        if (attack.polarity == playerPolarity)
        {
            Debug.Log("<color=cyan>⚡ 秩序包容！同色攻击被吸收。代价：产生物理击退与暂时减速。</color>");
            energySystem.AddEnergy(1); // 同色吸收奖励 1 格能量
            
            // 效果：击退 + 50% 减速 1.5 秒
            ApplyKnockback(attack.sourcePosition);
            controller.ApplySlowdown(1.5f, 0.5f); 
            
            return false;
        }

        // 诊断逻辑：为什么没触发弹刀？
        if (isBlocking)
        {
            if (attack.polarity == playerPolarity) 
                Debug.Log("<color=white>❕ 弹刀失败：同色攻击只能吸收，不能弹刀！请切换极性后再点击左键。</color>");
            else if (attack.polarity == Polarity.Neutral)
                Debug.Log("<color=purple>⚠️ 弹刀失败：无法格挡紫光危险技！只能闪避！</color>");
        }
        else if (attack.polarity != playerPolarity && attack.polarity != Polarity.Neutral)
        {
             Debug.Log("<color=grey>🖱️ 弹刀失败：未在命中瞬间按下左键（格挡窗口 0.25s）。</color>");
        }

        float calculatedDamage = attack.damage;
        if (isStandingOnAnomalyCore)
        {
            calculatedDamage *= 2f;
            Debug.Log("<color=red>💥 核心过载！在异常源内受击，伤害翻倍！</color>");
            transform.DOShakePosition(0.5f, 0.7f, 25, 90, false, true); 
        }

        Debug.Log($"<color=red>🩸 混沌入侵！极性不符或闪避失败，受到 {calculatedDamage} 判定伤害！</color>");
        currentHP -= calculatedDamage;

        if (CombatFeedbackManager.Instance != null)
            CombatFeedbackManager.Instance.TriggerDamageFeedback();

        controller.EnterHitlag(0.2f);

        if (currentHP <= 0)
        {
            Debug.Log("<color=red>💀 玩家阵亡！正在重新加载场景...</color>");
            
            // 重要：重置全局物理状态，防止 TimeScale 永久卡在 0.05
            Time.timeScale = 1f;
            DOTween.KillAll(); 
            
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        return false;
    }

    private void ApplyKnockback(Vector3 attackerPos)
    {
        Vector3 knockbackDir = (transform.position - attackerPos).normalized;
        knockbackDir.y = 0;
        
        // 调用真实的物理击退接口
        controller.AddKnockback(knockbackDir, 12f);
        Debug.Log($"[PlayerCombatReceiver] 物理击退启动方: {knockbackDir}");
    }

    private void TryExecuteEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 3.5f); // 略微增加范围以提升手感
        foreach (var hit in hits)
        {
            EnemyPosture target = hit.GetComponentInParent<EnemyPosture>(); // 适配可能的子级碰撞体
            if (target != null && target.IsBroken)
            {
                // 触发战斗反馈单例的多重演出
                if (CombatFeedbackManager.Instance != null)
                    CombatFeedbackManager.Instance.TriggerAnnihilationFeedback();

                // 核心：调用敌人的终结序列（包含材质替换与爆缩）
                target.Execute();
                break;
            }
        }
    }
}
