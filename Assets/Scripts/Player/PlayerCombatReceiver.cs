using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DG.Tweening;

// ==========================================
// Title: 玩家战斗受击处理器 (Player Combat Receiver)
// Description: 处理玩家被怪物命中时的完整博弈逻辑。
//              包含：空间规避(闪避)、同色吸收、异色受伤、完美弹刀四大判定轨道。
// ==========================================

[RequireComponent(typeof(PlayerController), typeof(PlayerPolarity), typeof(PlayerEnergySystem))]
public class PlayerCombatReceiver : MonoBehaviour, IDamageable
{
    private PlayerController controller;
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
        controller = GetComponent<PlayerController>();
        polarity = GetComponent<PlayerPolarity>();
        energySystem = GetComponent<PlayerEnergySystem>();
    }

    private void Update()
    {
        // 核心：监听请求 (通过缓冲队列)
        if (controller != null && controller.ConsumeBuffer(PlayerController.InputType.Parry))
        {
            lastBlockInputTime = Time.time;
            Debug.Log("<color=white>🛡️ 玩家进入格挡姿态...</color>");
        }

        if (controller != null && controller.ConsumeBuffer(PlayerController.InputType.Execute))
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
            float timeSinceDash = Time.time - controller.LastDashTime;
            float timeSinceJump = Time.time - controller.LastJumpTime;
            
            if (timeSinceDash <= 0.25f || timeSinceJump <= 0.25f)
            {
                Debug.Log($"<color=white>💨 完美规避！(Dash:{timeSinceDash:F2}s / Jump:{timeSinceJump:F2}s) 能量 +2</color>");
                energySystem.AddEnergy(2); 

                // 触发玩家端粒子特效
                if (PlayerCombatVFX.Instance != null)
                    PlayerCombatVFX.Instance.TriggerDodgeVFX();
                
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
        
        // 【白皮书差异修复】：在特殊污染区允许弹紫色 (Neutral) 攻击
        bool canParryNeutral = isBlocking && attack.polarity == Polarity.Neutral && isStandingOnAnomalyCore;
        
        if ((isBlocking && attack.polarity != playerPolarity && attack.polarity != Polarity.Neutral) || canParryNeutral)
        {
            int cost = isStandingOnAnomalyCore ? 0 : 3; // 修复：能量消耗从 2 提升至 3

            if (energySystem.TryConsumeEnergy(cost))
            {
                Debug.Log(canParryNeutral ? "<color=purple>🌟 奇迹！异常源内成功弹回了混沌紫光！</color>" : "<color=orange>✨ 极性湮灭！完美弹刀！</color>");

                if (CombatFeedbackManager.Instance != null)
                    CombatFeedbackManager.Instance.TriggerParryFeedback();

                if (attack.sourceObject != null)
                {
                    EnemyPosture sourcePosture = attack.sourceObject.GetComponent<EnemyPosture>();
                    if (sourcePosture != null)
                    {
                        // 修正：在特殊污染区弹刀造成双倍伤害 (2f)
                        float finalDamage = parryCounterPostureDamage * (isStandingOnAnomalyCore ? 2f : 1f);
                        sourcePosture.AddPosture(finalDamage);
                    }
                }

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
                
                if (CombatFeedbackManager.Instance != null)
                {
                    CombatFeedbackManager.Instance.TriggerDamageFeedback();
                    if (controller.visualRoot != null)
                        controller.visualRoot.DOShakePosition(0.4f, 0.5f, 20, 90, false, true);
                }
                
                controller.EnterHitlag(0.6f);
                return false;
            }
        }

        // ────────────────────────────────
        // 轨道二：同色吸收 (稳妥解法)
        // ────────────────────────────────
        if (attack.polarity == playerPolarity)
        {
            Debug.Log("<color=cyan>⚡ 秩序包容！同色攻击被吸收。代价：产生物理击退与暂时减速。</color>");
            energySystem.AddEnergy(1);
            
            ApplyKnockback(attack.sourcePosition);
            controller.ApplySlowdown(1.5f, 0.5f);

            // 触发吸收粒子：颜色跟随极性
            if (PlayerCombatVFX.Instance != null)
            {
                Color vfxColor = (playerPolarity == Polarity.Blue) ? new Color(0.2f, 0.5f, 1f) : new Color(1f, 0.2f, 0.2f);
                PlayerCombatVFX.Instance.TriggerAbsorptionVFX(vfxColor);
            }
            
            return false;
        }

        // 诊断逻辑
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
            if (controller.visualRoot != null)
                controller.visualRoot.DOShakePosition(0.5f, 0.7f, 25, 90, false, true); 
        }

        Debug.Log($"<color=red>🩸 混沌入侵！极性不符或闪避失败，受到 {calculatedDamage} 判定伤害！</color>");
        currentHP -= calculatedDamage;

        // 【补全反馈】：即便未被吸收，受到真实伤害（异色/中性）时也应触发物理击退，防止受击感太轻
        ApplyKnockback(attack.sourcePosition);

        if (CombatFeedbackManager.Instance != null)
            CombatFeedbackManager.Instance.TriggerDamageFeedback();

        controller.EnterHitlag(0.2f);

        if (currentHP <= 0)
        {
            Debug.Log("<color=red>💀 玩家阵亡！正在重新加载场景...</color>");
            Time.timeScale = 1f;
            DOTween.KillAll(); 
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        return false;
    }

    private void ApplyKnockback(Vector3 attackerPos)
    {
        Vector3 knockbackDir = transform.position - attackerPos;
        knockbackDir.y = 0; // 先移除垂直分量
        knockbackDir.Normalize(); // 再归一化，保证水平力度充足
        
        // 调用真实的物理击退接口，稍微增加力度以强化“代价”感知
        controller.AddKnockback(knockbackDir, 15f);
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

    /// <summary>
    /// 用于处理“物理空间规避 (Near Miss)”——玩家闪出了怪物的命中核心区，但仍在边缘蹭过，
    /// 此时不会调用 TakeDamage（因为没被打中），但我们要给他发放完美闪避的能量奖励！
    /// </summary>
    public bool CheckPerfectDodgeNearMiss()
    {
        if (controller.IsDodging || controller.IsJumping)
        {
            float timeSinceDash = Time.time - controller.LastDashTime;
            float timeSinceJump = Time.time - controller.LastJumpTime;
            
            if (timeSinceDash <= 0.25f || timeSinceJump <= 0.25f)
            {
                Debug.Log($"<color=white>💨 空间极限规避 (Near Miss)！(Dash:{timeSinceDash:F2}s / Jump:{timeSinceJump:F2}s) 能量 +2</color>");
                energySystem.AddEnergy(2); 

                if (PlayerCombatVFX.Instance != null)
                    PlayerCombatVFX.Instance.TriggerDodgeVFX();
                
                if (CombatFeedbackManager.Instance != null)
                    CombatFeedbackManager.Instance.TriggerDamageFeedback(); // 用轻微震动反馈擦弹感
                
                return true;
            }
        }
        return false;
    }
}
