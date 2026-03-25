using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System;

// ==========================================
// Title: 玩家战斗受击处理器 (Player Combat Receiver)
// Description: 处理玩家被怪物命中时的完整博弈逻辑。
//              包含：空间规避(闪避)、同色吸收、异色受伤、完美弹刀四大判定轨道。
// ==========================================

[RequireComponent(typeof(PlayerController), typeof(PlayerPolarity), typeof(PlayerEnergySystem))]
public class PlayerCombatReceiver : MonoBehaviour, IDamageable
{
    public event Action OnSamePolarityAbsorbed;
    public event Action OnPerfectParrySucceeded;
    public event Action<float, float> OnHealthChanged;

    private PlayerController controller;
    private PlayerPolarity polarity;
    private PlayerEnergySystem energySystem;

    [Header("❤️ 生存数值")]
    public float maxHP = 100f;
    public float currentHP = 100f;

    [Header("⚔️ 弹刀/对冲参数")]
    public float parryCounterPostureDamage = 50f;
    public int parryEnergyCost = 3; // 可在 Inspector 调整消耗 (设计稿默认为 3)
    public float blockWindow = 0.22f; // 对冲判定窗口
    public float movingParryBonusWindow = 0.1f; // 移动时额外判定
    public float movingThreshold = 0.02f; // 低于该位移视作静止

    [Header("📘 教程规则开关")]
    [SerializeField] private bool allowAbsorbEnergyReward = true;
    [SerializeField] private bool allowDodgeEnergyReward = true;

    // 状态标记
    public bool isStandingOnAnomalyCore = false;
    private float lastBlockInputTime = -10f;
    private Vector3 lastFramePosition;
    private bool wasMovingThisFrame;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        polarity = GetComponent<PlayerPolarity>();
        energySystem = GetComponent<PlayerEnergySystem>();
        lastFramePosition = transform.position;
        NotifyHealthChanged();
    }

    private void Update()
    {
        // 核心：监听请求 (通过缓冲队列)
        Vector3 delta = transform.position - lastFramePosition;
        wasMovingThisFrame = new Vector2(delta.x, delta.z).sqrMagnitude > movingThreshold * movingThreshold;
        lastFramePosition = transform.position;

        if (controller != null && controller.ConsumeBuffer(PlayerController.InputType.Parry))
        {
            lastBlockInputTime = Time.unscaledTime;
            Debug.Log("<color=white>🛡️ 玩家进入格挡姿态...</color>");
        }

        // 输入兜底：防止缓冲被其他逻辑竞争消费，确保移动中也能稳定打开弹刀窗口。
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            lastBlockInputTime = Time.unscaledTime;
        }

        if (controller != null && controller.ConsumeBuffer(PlayerController.InputType.Execute))
        {
            TryExecuteEnemy();
        }
    }

    // ==========================================
    // 核心战斗判定逻辑：四阶段轨道系统
    // ==========================================
    public bool TakeDamage(AttackData attack)
    {
        if (currentHP <= 0) return false;

        // 获取当前极性枚举
        Polarity playerPolarity = PolarityBridge.FromPolarityColor(polarity.CurrentColor);

        // ---------------------------------------------------------
        // 轨道 1：空间规避 (Spatial Evasion) - 充能手段
        // 【修复】：不再依赖 IsDodging/IsJumping 状态（Hitlag 会清空它们），
        //           改为直接检查"最后一次 Dash/Jump 距今是否在 0.3s 内"。
        // ---------------------------------------------------------
        float timeSinceDash = Time.time - controller.LastDashTime;
        float timeSinceJump = Time.time - controller.LastJumpTime;
        if (timeSinceDash <= 0.3f || timeSinceJump <= 0.3f)
        {
            Debug.Log($"<color=white>💨 完美规避！(Dash:{timeSinceDash:F2}s / Jump:{timeSinceJump:F2}s) 获取 2 格秩序能量。</color>");
            if (allowDodgeEnergyReward)
            {
                energySystem.AddEnergy(2);
            }

            if (PlayerCombatVFX.Instance != null) PlayerCombatVFX.Instance.TriggerDodgeVFX();
            if (CombatFeedbackManager.Instance != null) CombatFeedbackManager.Instance.TriggerDamageFeedback();

            return false; // 规避成功
        }

        // ---------------------------------------------------------
        // 轨道 2：极性对冲/弹刀 (Polarity Annihilation) - 核心输出
        // ---------------------------------------------------------
        float effectiveWindow = blockWindow + (wasMovingThisFrame ? movingParryBonusWindow : 0f);
        bool isBlocking = (Time.unscaledTime - lastBlockInputTime) <= effectiveWindow;
        bool isDifferentPolarity = attack.polarity != playerPolarity && attack.polarity != Polarity.Neutral;

        if (isBlocking && isDifferentPolarity)
        {
            // 执行条件：消耗配置的能量格
            if (energySystem.currentEnergyGrids >= parryEnergyCost)
            {
                energySystem.TryConsumeEnergy(parryEnergyCost);
                Debug.Log("<color=cyan>✨ 极性湮灭！成功弹刀异色攻击！</color>");

                // 🎵 播放极限支援/弹刀音效（无视物理减速）
                if (AudioManager.Instance != null && AudioManager.Instance.sfxPerfectParry != null)
                    AudioManager.Instance.PlayHighlightSFX(AudioManager.Instance.sfxPerfectParry);

                if (CombatFeedbackManager.Instance != null)
                    CombatFeedbackManager.Instance.TriggerParryFeedback();

                // 造成反伤（对怪物 Posture）
                if (attack.sourceObject != null)
                {
                    EnemyPosture sourcePosture = attack.sourceObject.GetComponent<EnemyPosture>();
                    if (sourcePosture != null)
                    {
                        // 基础反伤，核心区加成
                        float finalDamage = parryCounterPostureDamage * (isStandingOnAnomalyCore ? 2f : 1f);
                        sourcePosture.AddPosture(finalDamage);

                        if (isStandingOnAnomalyCore)
                        {
                            // 特殊污染区内弹刀成功：究极反馈链路
                            energySystem.AddEnergy(1);
                            if (CombatFeedbackManager.Instance != null)
                                CombatFeedbackManager.Instance.TriggerAnomalyCoreParryFeedback();

                            if (PlayerCombatVFX.Instance != null)
                                PlayerCombatVFX.Instance.TriggerAbsorptionVFX(new Color(1f, 0.2f, 1f, 1f));

                            sourcePosture.TriggerAnomalyCoreParryUIFeedback();
                            Debug.Log("<color=magenta>✴ 核心污染区弹刀成功：触发究极秩序反制！</color>");
                        }
                        else if (CombatHUDManager.Instance != null)
                        {
                            CombatHUDManager.Instance.ForceRefreshBossUI();
                        }
                    }
                }

                OnPerfectParrySucceeded?.Invoke();
                return true; // 返回 true 告知怪物被弹飞
            }
            else
            {
                Debug.Log("<color=red>❌ 弹刀失败：能量不足 3 格，无法湮灭攻击！</color>");
                // Fallthrough 进入受击流程
            }
        }

        // ---------------------------------------------------------
        // 轨道 3：同色吸收 (Polarity Absorption) - 稳妥解/充能
        // ---------------------------------------------------------
        if (attack.polarity == playerPolarity)
        {
            Debug.Log("<color=cyan>⚡ 同色吸收！不扣血，获得 1 格秩序能量。</color>");
            if (allowAbsorbEnergyReward)
            {
                energySystem.AddEnergy(1);
            }
            OnSamePolarityAbsorbed?.Invoke();

            // 🎵 播放同色吸收钝击音效
            if (AudioManager.Instance != null && AudioManager.Instance.sfxEnergyAbsorb != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.sfxEnergyAbsorb);

            ApplyKnockback(attack.sourcePosition);
            controller.ApplySlowdown(0.8f, 0.4f);

            if (CombatFeedbackManager.Instance != null)
                CombatFeedbackManager.Instance.TriggerDamageFeedback();

            if (controller.visualRoot != null)
                controller.visualRoot.DOShakePosition(0.4f, 0.5f, 20, 90, false, true).SetUpdate(true);

            controller.EnterHitlag(0.05f);
            return false;
        }

        // ---------------------------------------------------------
        // 轨道 4：混沌入侵 (Standard Damage) - 落地扣血
        // ---------------------------------------------------------
        float calculatedDamage = attack.damage;
        if (isStandingOnAnomalyCore)
        {
            calculatedDamage *= 2f;
            Debug.Log("<color=red>💥 核心过载！在异常源内受击，伤害翻倍！</color>");
        }

        Debug.Log($"<color=red>🩸 混沌入侵！极性不符或闪避失败，受到 {calculatedDamage} 判定伤害！</color>");
        currentHP -= calculatedDamage;
        NotifyHealthChanged();

        // 🎵 播放受击音效
        if (AudioManager.Instance != null && AudioManager.Instance.sfxPlayerDamage != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.sfxPlayerDamage);

        if (controller.visualRoot != null)
            controller.visualRoot.DOShakePosition(0.4f, 0.8f, 25, 90, false, true).SetUpdate(true);

        ApplyKnockback(attack.sourcePosition);
        if (CombatFeedbackManager.Instance != null)
            CombatFeedbackManager.Instance.TriggerDamageFeedback();

        // 【新规：UI 反馈】同步触发血条抖动
        if (CombatHUDManager.Instance != null)
            CombatHUDManager.Instance.TriggerGlitchEffect(isPlayer: true);

        controller.EnterHitlag(0.2f);

        // 阵亡检测
        if (currentHP <= 0)
        {
            Die();
        }

        return false;
    }

    public void Die()
    {
        if (currentHP > 0) currentHP = 0;
        NotifyHealthChanged();

        Debug.Log("<color=red>💀 玩家阵亡！触发物理锚定与仪式感定格...</color>");

        // 【1. 物理锚定】：瞬间凝固，防止无底洞掉落
        if (controller != null)
        {
            controller.enabled = false;
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }

        // 【2. 死亡反馈】：极致定格与震动
        if (CombatFeedbackManager.Instance != null)
        {
            // 比普通受伤更强烈的冲击感
            CombatFeedbackManager.Instance.TriggerDamageFeedback();
            // 额外手动触发一个超长顿帧（0.5s）
            if (TimeManager.Instance != null)
                TimeManager.Instance.DoHitstop(0.5f, 0.05f);
        }

        // 【3. 状态清理】：恢复时间缩放并杀死所有残留 Tween，防止干扰 UI 动画
        DOTween.KillAll();

        // 【4. 触发 UI】：交给 GameOverManager 处理接下来的视觉演出
        if (GameOverManager.Instance != null)
        {
            GameOverManager.Instance.TriggerGameOver();
        }
        else
        {
            Debug.LogWarning("[PlayerCombatReceiver] GameOverManager not found, falling back to instant reload.");
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(currentHP, maxHP);
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

                // 🎵 播放处决重击音效并触发全局真空压耳效果 (Ducking)
                if (AudioManager.Instance != null)
                {
                    if (AudioManager.Instance.sfxExecutionHit != null)
                        AudioManager.Instance.PlayHighlightSFX(AudioManager.Instance.sfxExecutionHit);
                    AudioManager.Instance.TriggerVacuumEffect(1.2f, 0.1f);
                }

                // 核心：调用敌人的终结序列（包含材质替换与爆缩）
                TutorialBoss tutorialBoss = target.GetComponent<TutorialBoss>();
                if (tutorialBoss != null)
                {
                    tutorialBoss.TryExecuteFromPlayer();
                }
                else
                {
                    target.Execute();
                }
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

            Debug.Log($"[Combat] NearMiss detected. IsDodging:{controller.IsDodging}, IsJumping:{controller.IsJumping}, timeSinceDash:{timeSinceDash:F2}, timeSinceJump:{timeSinceJump:F2}");

            if (timeSinceDash <= 0.3f || timeSinceJump <= 0.3f)
            {
                Debug.Log($"<color=white>💨 空间极限规避 (Near Miss)！获取 2 格秩序能量奖励。</color>");
                if (allowDodgeEnergyReward)
                {
                    energySystem.AddEnergy(2);
                }

                if (PlayerCombatVFX.Instance != null)
                    PlayerCombatVFX.Instance.TriggerDodgeVFX();

                if (CombatFeedbackManager.Instance != null)
                    CombatFeedbackManager.Instance.TriggerDamageFeedback();

                return true;
            }
        }
        return false;
    }

    public void ConfigureTutorialRules(bool canGainAbsorbEnergy, bool canGainDodgeEnergy)
    {
        allowAbsorbEnergyReward = canGainAbsorbEnergy;
        allowDodgeEnergyReward = canGainDodgeEnergy;
    }
}
