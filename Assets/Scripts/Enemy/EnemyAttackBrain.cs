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
// Title: 敌人攻击大脑 (Enemy Attack Brain) - Phase 5 强化版
// Description: 包含：1. 距离感知的 AI 出招权重；2. 快慢刀节奏欺骗；3. 残血紫光二连击。
// ==========================================

[RequireComponent(typeof(EnemyVisualController), typeof(EnemyShapeMorpher), typeof(EnemyPosture))]
public class EnemyAttackBrain : MonoBehaviour
{
    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;

    [Header("Attack Default Settings")]
    public float attackCooldown = 3f;
    private float cooldownTimer;

    [Header("⚔️ 基础节奏参数")]
    public float telegraphDuration = 0.6f;
    public float attackActiveDuration = 0.25f;

    [Header("🎯 判定盒引用")]
    public EnemyHitbox redSweepHitbox;
    public EnemyHitbox blueSmashHitbox;

    [Header("🎯 攻击面板数值")]
    public float baseDamage = 10f;
    public float basePostureDamage = 20f;

    [Header("🧪 环境预制体")]
    public GameObject chaosPuddlePrefab;

    private EnemyVisualController visualController;
    private EnemyShapeMorpher shapeMorpher;
    private EnemyPosture posture;
    private EnemyTracker tracker;
    private Transform playerTransform;

    private void Awake()
    {
        visualController = GetComponent<EnemyVisualController>();
        shapeMorpher = GetComponent<EnemyShapeMorpher>();
        posture = GetComponent<EnemyPosture>();
        tracker = GetComponent<EnemyTracker>();
        cooldownTimer = attackCooldown;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

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
            InitiateAIAction();
            cooldownTimer = attackCooldown;
        }
    }

    private void InitiateAIAction()
    {
        if (CurrentState == EnemyState.Stunned) return;

        // 【机制三：残血紫光博弈】
        if (posture.HealthPercentage < 0.3f && Random.value < 0.4f)
        {
            StartCoroutine(PurpleBluffRoutine());
            return;
        }

        // 【机制一：距离感知权重】
        float dist = playerTransform != null ? Vector3.Distance(transform.position, playerTransform.position) : 10f;
        Polarity attackPolarity;
        
        if (dist > 5f)
        {
            // 远距离：80% 几率红光突刺
            attackPolarity = Random.value < 0.8f ? Polarity.Red : Polarity.Blue;
        }
        else
        {
            // 近距离：80% 几率蓝光重砸
            attackPolarity = Random.value < 0.8f ? Polarity.Blue : Polarity.Red;
        }

        // 【机制二：快慢刀变体】
        bool isSlowAttack = Random.value < 0.3f;
        float finalTelegraph = isSlowAttack ? telegraphDuration + 0.3f : telegraphDuration;

        CurrentState = EnemyState.Telegraphing;
        EnemyHitbox selectedHitbox = attackPolarity == Polarity.Red ? redSweepHitbox : blueSmashHitbox;

        StartCoroutine(AttackRoutine(attackPolarity, selectedHitbox, finalTelegraph, OnAttackEnd));
    }

    private IEnumerator AttackRoutine(Polarity attackPolarity, EnemyHitbox targetHitbox, float duration, System.Action onComplete)
    {
        visualController.GlowForTelegraph(attackPolarity, duration);
        
        // 【视觉联动】：根据极性区分形变类型
        int morphType = (attackPolarity == Polarity.Red) ? 2 : 1;
        shapeMorpher.MorphForTelegraph(duration, morphType);

        if (tracker != null)
        {
            float turnSpeed = attackPolarity == Polarity.Blue ? 2f : 10f;
            tracker.StartTracking(turnSpeed);
        }

        float lockInTime = 0.15f;
        float trackDuration = Mathf.Max(0f, duration - lockInTime);
        yield return new WaitForSeconds(trackDuration);

        if (tracker != null) tracker.StopTracking();
        yield return new WaitForSeconds(lockInTime);

        if (CurrentState == EnemyState.Stunned) yield break;

        // 攻击瞬间
        CurrentState = EnemyState.Attacking;
        shapeMorpher.ResetShape(0.05f);

        if (targetHitbox != null)
        {
            AttackData data = new AttackData {
                damage = baseDamage,
                postureDamage = basePostureDamage,
                polarity = attackPolarity,
                sourcePosition = transform.position,
                sourceObject = gameObject
            };
            targetHitbox.ActivateHitbox(data);
        }

        yield return new WaitForSeconds(attackActiveDuration);

        if (CurrentState != EnemyState.Stunned)
        {
            if (targetHitbox != null) targetHitbox.DeactivateHitbox();
            
            // 【阶段六：环境污染生成】
            TrySpawnChaosPuddle(attackPolarity);

            visualController.ResetVisual();
            CurrentState = EnemyState.Recovering;
        }

        onComplete?.Invoke();
    }

    private void TrySpawnChaosPuddle(Polarity polarity)
    {
        // 向地面发射射线
        Vector3 spawnPos = transform.position + transform.forward * 2f; // 在前方2米生成
        Debug.Log($"<color=white>🔍 [Brain] 尝试在 {spawnPos} 下方生成污染区...</color>");
        
        RaycastHit[] hits = Physics.RaycastAll(spawnPos + Vector3.up * 5f, Vector3.down, 10f);
        bool foundGround = false;
        
        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("Ground") || hit.collider.gameObject.name.Contains("Ground") || hit.collider.gameObject.name.Contains("Plane"))
            {
                Debug.Log($"<color=white>✅ [Brain] 射线击中地面: {hit.point}，正在回收/提取 Puddle。</color>");
                
                if (PuddleManager.Instance != null)
                {
                    bool isSpecial = Random.value < 0.3f;
                    PuddleManager.Instance.GetPuddleFromPool(hit.point + Vector3.up * 0.01f, polarity, isSpecial);
                }
                else
                {
                    Debug.LogError("<color=red>🛑 [Brain] PuddleManager.Instance 为空，请确保场景中存在该管理器！</color>");
                }
                
                foundGround = true;
                break;
            }
        }

        if (!foundGround)
        {
            Debug.LogWarning($"<color=yellow>⚠️ [Brain] 射线未击中地面，无法放置污染区。spawnPos={spawnPos}</color>");
        }
    }

    private IEnumerator PurpleBluffRoutine()
    {
        CurrentState = EnemyState.Telegraphing;
        Debug.Log("<color=purple>【狂暴】Boss 进入紫光预警！绝对禁盾二连击！</color>");

        // 预警：紫色光效
        visualController.GlowForTelegraph(Polarity.Neutral, telegraphDuration);
        shapeMorpher.MorphForTelegraph(telegraphDuration, 3); // 紫色使用爆发形变
        
        if (tracker != null) tracker.StartTracking(15f); // 极速追踪
        yield return new WaitForSeconds(telegraphDuration);
        if (tracker != null) tracker.StopTracking();

        if (CurrentState == EnemyState.Stunned) yield break;

        // 第一击：红光极速横扫 (无视防御属性)
        CurrentState = EnemyState.Attacking;
        AttackData pd = new AttackData {
            damage = baseDamage * 0.7f,
            postureDamage = 0, // 紫光击不造成爆槽，只伤血
            polarity = Polarity.Neutral, // 绝对禁盾
            sourcePosition = transform.position,
            sourceObject = gameObject
        };
        
        redSweepHitbox.ActivateHitbox(pd);
        yield return new WaitForSeconds(0.15f);
        redSweepHitbox.DeactivateHitbox();

        // 瞬间衔接第二击：蓝光重砸
        blueSmashHitbox.ActivateHitbox(pd);
        yield return new WaitForSeconds(0.15f);
        blueSmashHitbox.DeactivateHitbox();

        visualController.ResetVisual();
        shapeMorpher.ResetShape(0.1f);
        CurrentState = EnemyState.Idle;
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
        StopAllCoroutines(); // 强行中断攻击流程
        visualController.SetStunnedVisual();
        shapeMorpher.ResetShape(0.1f);
        if (redSweepHitbox) redSweepHitbox.DeactivateHitbox();
        if (blueSmashHitbox) blueSmashHitbox.DeactivateHitbox();
    }
}
