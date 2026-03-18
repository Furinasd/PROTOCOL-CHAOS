using UnityEngine;

public enum EnemyState
{
    Idle,
    Telegraphing,
    Attacking,
    Recovering,
    Stunned
}

[RequireComponent(typeof(EnemyShapeMorpher), typeof(EnemyPosture))]
public class EnemyAttackBrain : MonoBehaviour
{
    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;
    
    [Header("Attack Settings")]
    public float attackCooldown = 3f;
    private float cooldownTimer;

    private EnemyShapeMorpher morpher;
    private EnemyPosture posture;

    private void Awake()
    {
        morpher = GetComponent<EnemyShapeMorpher>();
        posture = GetComponent<EnemyPosture>();
        cooldownTimer = attackCooldown;
    }

    private void Update()
    {
        if (posture.IsBroken)
        {
            CurrentState = EnemyState.Stunned;
            return;
        }

        if (CurrentState == EnemyState.Idle)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
            {
                InitiateRandomAttack();
                cooldownTimer = attackCooldown;
            }
        }
    }

    private void InitiateRandomAttack()
    {
        CurrentState = EnemyState.Telegraphing;
        
        bool isRedSweep = Random.value > 0.5f; // 50%概率红扫，50%概率蓝砸
        if (isRedSweep)
        {
            morpher.StartRedSweepAttack(OnAttackEnd);
        }
        else
        {
            morpher.StartBlueSmashAttack(OnAttackEnd);
        }
    }

    private void OnAttackEnd()
    {
        CurrentState = EnemyState.Idle;
    }
}
