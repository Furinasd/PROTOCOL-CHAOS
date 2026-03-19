using System.Collections;
using UnityEngine;

public enum EnemyState
{
    Idle,
    Telegraphing,
    Attacking,
    Recovering,
    Stunned
}

// ==========================================
// Title: 敌人攻击大脑 (Enemy Attack Brain)
// Description: 控制敌人的战斗节奏——何时发动随机攻击，并监听 EnemyPosture 进入宕机。
//              协同调用 EnemyVisualController 和 EnemyShapeMorpher 进行演出。
//              职责清晰：只做"决策与时序"，不负责视觉或数值结算。
// ==========================================

[RequireComponent(typeof(EnemyVisualController), typeof(EnemyShapeMorpher), typeof(EnemyPosture))]
public class EnemyAttackBrain : MonoBehaviour
{
    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;

    [Header("Attack Default Settings")]
    [Tooltip("每次攻击之间的冷却时间（秒）")]
    public float attackCooldown = 3f;
    private float cooldownTimer;

    [Header("⚔️ 攻击节奏参数")]
    public float telegraphDuration = 0.6f;
    public float attackActiveDuration = 0.25f;

    [Header("🎯 判定盒引用 (挂载在子物体)")]
    public EnemyHitbox redSweepHitbox;
    public EnemyHitbox blueSmashHitbox;

    [Header("🎯 攻击面板数值")]
    public float baseDamage = 10f;
    public float basePostureDamage = 20f;

    private EnemyVisualController visualController;
    private EnemyShapeMorpher shapeMorpher;
    private EnemyPosture posture;

    private void Awake()
    {
        visualController = GetComponent<EnemyVisualController>();
        shapeMorpher = GetComponent<EnemyShapeMorpher>();
        posture = GetComponent<EnemyPosture>();
        cooldownTimer = attackCooldown;

        // 监听宕机事件，立刻切换状态
        if (posture != null)
        {
            posture.OnPostureBroken += OnPostureBroken;
        }
    }

    private void OnDestroy()
    {
        if (posture != null)
            posture.OnPostureBroken -= OnPostureBroken;
    }

    private void Update()
    {
        if (CurrentState != EnemyState.Idle) return;

        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f)
        {
            InitiateRandomAttack();
            cooldownTimer = attackCooldown;
        }
    }

    private void InitiateRandomAttack()
    {
        CurrentState = EnemyState.Telegraphing;

        Polarity attackPolarity = Random.value > 0.5f ? Polarity.Red : Polarity.Blue;
        EnemyHitbox selectedHitbox = attackPolarity == Polarity.Red ? redSweepHitbox : blueSmashHitbox;

        StartCoroutine(AttackRoutine(attackPolarity, selectedHitbox, OnAttackEnd));
    }

    private IEnumerator AttackRoutine(Polarity attackPolarity, EnemyHitbox targetHitbox, System.Action onComplete)
    {
        // ── 1. 前摇：形变夸张拉伸与发光预警 ──
        visualController.GlowForTelegraph(attackPolarity, telegraphDuration);
        shapeMorpher.MorphForTelegraph(telegraphDuration);

        yield return new WaitForSeconds(telegraphDuration);

        if (CurrentState == EnemyState.Stunned) yield break; // 防御性判断：可能在此期间被打断

        // ── 2. 攻击判定期：瞬间将形变弹回产生打击顿挫感，激活判定区 ──
        CurrentState = EnemyState.Attacking;
        shapeMorpher.ResetShape(0.05f); // 瞬间发力弹回

        if (targetHitbox != null)
        {
            AttackData attackData = new AttackData
            {
                damage = baseDamage,
                postureDamage = basePostureDamage,
                polarity = attackPolarity,
                sourcePosition = transform.position,
                sourceObject = gameObject
            };
            targetHitbox.ActivateHitbox(attackData);
        }
        else
        {
            Debug.LogError($"[EnemyBrain] {attackPolarity} 对应的 Hitbox 引用为空，无法产生伤害判定！");
        }

        yield return new WaitForSeconds(attackActiveDuration);

        if (CurrentState == EnemyState.Stunned) 
        {
            if (targetHitbox != null) targetHitbox.DeactivateHitbox();
            yield break; // 攻击期间如果挂了就直接退出
        }

        // ── 3. 后摇：关闭判定盒，光效熄灭 ──
        CurrentState = EnemyState.Recovering;
        if (targetHitbox != null) targetHitbox.DeactivateHitbox();
        
        visualController.ResetVisual();

        onComplete?.Invoke();
    }

    private void OnAttackEnd()
    {
        if (CurrentState != EnemyState.Stunned)
        {
            CurrentState = EnemyState.Idle;
        }
    }

    private void OnPostureBroken()
    {
        CurrentState = EnemyState.Stunned;
        Debug.Log("[EnemyBrain] 宕机！停止所有新的攻击行为，播放眩晕演出。");
        visualController.SetStunnedVisual();
        shapeMorpher.ResetShape(0.1f);
    }
}
