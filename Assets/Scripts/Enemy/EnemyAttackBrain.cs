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
//              已从 EnemyShapeMorpher 升级为对接 EnemyVisualController。
//              职责清晰：只做"决策与时序"，不负责视觉或数值结算。
// ==========================================

[RequireComponent(typeof(EnemyVisualController), typeof(EnemyPosture))]
public class EnemyAttackBrain : MonoBehaviour
{
    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;

    [Header("Attack Settings")]
    [Tooltip("每次攻击之间的冷却时间（秒）")]
    public float attackCooldown = 3f;
    private float cooldownTimer;

    private EnemyVisualController visualController;
    private EnemyPosture posture;

    private void Awake()
    {
        visualController = GetComponent<EnemyVisualController>();
        posture = GetComponent<EnemyPosture>();
        cooldownTimer = attackCooldown;

        // 监听宕机事件，立刻切换状态
        posture.OnPostureBroken += OnPostureBroken;
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

        bool isRedSweep = Random.value > 0.5f;
        if (isRedSweep)
        {
            visualController.StartRedSweepAttack(OnAttackEnd);
        }
        else
        {
            visualController.StartBlueSmashAttack(OnAttackEnd);
        }
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
        Debug.Log("[EnemyBrain] 宕机！停止所有新的攻击行为。");
    }
}
