using System.Collections;
using UnityEngine;

// ==========================================
// Title: 敌人视觉控制器 (Enemy Visual Controller)
// Description: 承接所有敌人的视觉表现变化（材质切换光效）。
//              取代原版基于"物理几何体生成"的 EnemyShapeMorpher，
//              改用 Emission 材质光效来实现前摇预警，需配合 Bloom 后处理使用。
//              同时仍然保留对外部攻击结算回调的接口，供 EnemyAttackBrain 使用。
// ==========================================

[RequireComponent(typeof(MeshRenderer))]
public class EnemyVisualController : MonoBehaviour
{
    [Header("⚔️ 攻击节奏参数")]
    public float telegraphDuration = 0.6f;
    public float attackActiveDuration = 0.25f;

    [Header("🎨 Emission 材质 (拖入 Inspector)")]
    [Tooltip("平时的中性材质（无光效）")]
    public Material neutralMat;
    [Tooltip("红色横扫攻击前摇材质（需开启强力 Emission）")]
    public Material redGlowMat;
    [Tooltip("蓝色重砸攻击前摇材质（需开启强力 Emission）")]
    public Material blueGlowMat;
    [Tooltip("熵值爆满、宕机状态（灰败）材质")]
    public Material stunnedMat;

    [Header("🎯 判定盒 Prefab")]
    [Tooltip("挂有 EnemyHitbox 组件的攻击判定预制体，由 AttackBrain 填充并在此激活")]
    public GameObject redSweepHitboxPrefab;
    public GameObject blueSmashHitboxPrefab;

    // 内部引用
    private MeshRenderer meshRenderer;
    private EnemyPosture posture;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        posture = GetComponent<EnemyPosture>();

        // 挂载宕机监听：敌人受伤宕机时自动切材质
        if (posture != null)
        {
            posture.OnPostureBroken += OnPostureBroken;
        }
    }

    private void OnDestroy()
    {
        if (posture != null)
        {
            posture.OnPostureBroken -= OnPostureBroken;
        }
    }

    // ──────────────────────────────────
    // 对外接口：供 EnemyAttackBrain 调用
    // ──────────────────────────────────

    /// <summary>
    /// 触发红色横扫攻击的完整流程（前摇光效 → 判定期 → 后摇归位）
    /// </summary>
    public void StartRedSweepAttack(System.Action onComplete)
    {
        StartCoroutine(AttackRoutine(Polarity.Red, redSweepHitboxPrefab, onComplete));
    }

    /// <summary>
    /// 触发蓝色重砸攻击的完整流程
    /// </summary>
    public void StartBlueSmashAttack(System.Action onComplete)
    {
        StartCoroutine(AttackRoutine(Polarity.Blue, blueSmashHitboxPrefab, onComplete));
    }

    // ──────────────────────────────────
    // 内部协程：光效预警 → 判定盒激活 → 归位
    // ──────────────────────────────────

    private IEnumerator AttackRoutine(Polarity attackPolarity, GameObject hitboxPrefab, System.Action onComplete)
    {
        // ── 1. 前摇：切材质，全身发光预警 ──
        meshRenderer.material = attackPolarity == Polarity.Red ? redGlowMat : blueGlowMat;
        Debug.Log($"[EnemyVisual] 前摇预警 - {attackPolarity} 光效激活！");
        // TODO: 调用 CinemachineImpulseSource 产生微弱镜头抖动预警（未来拓展点）

        yield return new WaitForSeconds(telegraphDuration);

        // ── 2. 攻击判定期：实例化判定盒 ──
        GameObject hitboxObj = null;
        if (hitboxPrefab != null)
        {
            hitboxObj = Instantiate(hitboxPrefab, transform.position, transform.rotation, transform);
            Debug.Log($"[EnemyVisual] 攻击判定期开始！{attackPolarity} 判定盒激活！");
        }
        else
        {
            // 无 Prefab 时回退：动态生成简单判定盒
            hitboxObj = CreateFallbackHitbox(attackPolarity);
            Debug.Log($"[EnemyVisual] （回退）动态生成 {attackPolarity} 判定盒！");
        }

        yield return new WaitForSeconds(attackActiveDuration);

        // ── 3. 后摇：销毁判定盒，光效熄灭 ──
        if (hitboxObj != null) Destroy(hitboxObj);
        if (!posture.IsBroken) meshRenderer.material = neutralMat;
        Debug.Log("[EnemyVisual] 攻击结束，光效归位。");

        onComplete?.Invoke();
    }

    // ──────────────────────────────────
    // 辅助：宕机时自动切换材质
    // ──────────────────────────────────

    private void OnPostureBroken()
    {
        if (stunnedMat != null)
        {
            meshRenderer.material = stunnedMat;
        }
        Debug.Log("[EnemyVisual] 宕机！切换至灰败材质，等待处决。");
    }

    // ──────────────────────────────────
    // 辅助：无 Prefab 时生成回退判定盒
    // ──────────────────────────────────

    private GameObject CreateFallbackHitbox(Polarity attackPolarity)
    {
        GameObject obj = new GameObject("FallbackHitbox");
        obj.transform.position = transform.position;
        obj.transform.parent = transform;

        var col = obj.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(2f, 1f, 2f); // 默认判定范围

        var hitboxData = obj.AddComponent<EnemyHitbox>();
        hitboxData.attackPolarity = attackPolarity;
        hitboxData.damage = 10f;
        hitboxData.postureDamage = 20f;
        hitboxData.IsLowAttack = (attackPolarity == Polarity.Red);
        hitboxData.IsHighAttack = (attackPolarity == Polarity.Blue);
        hitboxData.OwnerPosture = posture;

        return obj;
    }
}
