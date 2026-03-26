using UnityEngine;
using System.Collections;
using DG.Tweening;

// ==========================================
// Title: 混沌地板节点 (Grid Contamination)
// Author: 白糖 & 精灵小姐 (Redesigned by Antigravity)
// Description: 【主从架构版 v2】
//   本节点已完全退化为"被动数据层"：
//   - 仅持有极性属性与视觉组件（LineRenderer / Light / MeshRenderer）
//   - 彻底移除 Update 主动扣血、主动追踪玩家等"侵入式"逻辑
//   - 玩家状态感知（扣血、减速、充能）全部由 PlayerController 中心化管控
//   - OnTrigger 仅保留作为"初次感应哨兵"，但不作为扣血事实依据
// ==========================================
public class ChaosPuddle : MonoBehaviour
{
    [Header("🎨 污染属性")]
    public bool isContaminated = false;
    public bool isCoreAnomaly = false;
    public Polarity puddlePolarity;

    [Header("⚙️ 惩罚数值（由 PlayerController 读取使用）")]
    public float damagePerSecond = 15f;
    public float speedMultiplier = 0.5f;
    public int energyDrainPerSecond = 1;

    [Header("表现层 (需在 Prefab 或 Inspector 赋值)")]
    public Material normalFloorMat;
    public Material purpleHazardMat;
    public Material anomalyCoreMat;

    // 视觉组件（被动持有，只由自身 Contaminate/Purify 驱动）
    private LineRenderer lineRenderer;
    private Light glowLight;
    private ParticleSystem particles;
    private MeshRenderer meshRenderer;

    private float originalY;

    // ── 供 PlayerController 轮询使用的公共数据 ──────────────────────────────
    /// <summary>污染区圆形半径（世界单位），用于 PlayerController 的距离判定。</summary>
    public float Radius => transform.localScale.x * 0.5f; // scale.x=5 → radius=2.5

    private void Awake()
    {
        Transform visualT = transform.Find("PuddleVisual");
        if (visualT != null)
        {
            meshRenderer = visualT.GetComponent<MeshRenderer>();
            visualT.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();

        glowLight = GetComponent<Light>();
        if (glowLight == null) glowLight = gameObject.AddComponent<Light>();

        particles = GetComponentInChildren<ParticleSystem>();

        SetupVisualComponents();
    }

    private void SetupVisualComponents()
    {
        lineRenderer.useWorldSpace = false;
        lineRenderer.startWidth = 0.08f;
        lineRenderer.endWidth = 0.08f;
        lineRenderer.positionCount = 37;
        lineRenderer.loop = true;
        lineRenderer.material = purpleHazardMat;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        glowLight.type = LightType.Point;
        glowLight.range = 4f;
        glowLight.intensity = 0f;
        glowLight.shadows = LightShadows.None;

        float radius = 1.0f;
        for (int i = 0; i < 37; i++)
        {
            float angle = i * 10f * Mathf.Deg2Rad;
            lineRenderer.SetPosition(i, new Vector3(
                Mathf.Sin(angle) * (radius * 1.05f),
                0.05f,
                Mathf.Cos(angle) * (radius * 1.05f)));
        }

        lineRenderer.enabled = false;
        glowLight.enabled = false;
    }

    private void OnEnable()
    {
        PuddleManager.OnGlobalPurify += Purify;
        originalY = transform.position.y;
    }

    private void OnDisable()
    {
        PuddleManager.OnGlobalPurify -= Purify;
        // 【TD 级安全保险】如果物件被直接禁用或销毁，确保从活跃列表剔除，防止 PlayerController 列表膨胀
        if (PuddleManager.Instance != null)
            PuddleManager.Instance.UnregisterActivePuddle(this);

        StopAllCoroutines();
        transform.DOKill();
    }

    // ── 污染激活 ────────────────────────────────────────────────────────────
    public void Contaminate(Polarity polarity, bool isSpecial)
    {
        if (isContaminated) return;

        isContaminated = true;
        puddlePolarity = polarity;
        isCoreAnomaly = isSpecial;
        originalY = transform.position.y;

        Debug.Log($"<color=cyan>🌊 [Puddle] 污染区初始化: 极性={polarity}, 特殊={isSpecial} at Y={originalY}</color>");

        Color highlightColor = isCoreAnomaly ? Color.white : new Color(0.8f, 0.4f, 1.0f);

        if (meshRenderer != null)
        {
            meshRenderer.material = isCoreAnomaly ? anomalyCoreMat : purpleHazardMat;

            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOMoveY(originalY + 0.4f, 0.1f).SetEase(Ease.OutFlash));

            lineRenderer.enabled = true;
            glowLight.enabled = true;
            glowLight.color = highlightColor;

            lineRenderer.material.DOColor(highlightColor, "_BaseColor", 0.2f);
            DOTween.To(() => glowLight.intensity, x => glowLight.intensity = x, 2.5f, 0.2f);

            if (meshRenderer.material.HasProperty("_BaseColor"))
                seq.Join(meshRenderer.material.DOColor(highlightColor, "_BaseColor", 0.1f)
                    .SetLoops(2, LoopType.Yoyo));

            seq.Append(transform.DOMoveY(originalY + 0.05f, 0.2f).SetEase(Ease.OutBounce));
        }

        if (particles != null)
        {
            var main = particles.main;
            main.startColor = isCoreAnomaly ? Color.white : (Color)highlightColor;
            particles.Play();
        }

        // 激活后，通知 PuddleManager 将自己加入活跃列表
        PuddleManager.Instance?.RegisterActivePuddle(this);
    }

    // ── 净化（回收） ─────────────────────────────────────────────────────────
    public void Purify()
    {
        if (!isContaminated) return;

        // 先从活跃列表中移除，让 PlayerController 立刻停止感知
        PuddleManager.Instance?.UnregisterActivePuddle(this);

        isContaminated = false;
        isCoreAnomaly = false;

        if (meshRenderer != null) meshRenderer.material = normalFloorMat;
        if (lineRenderer != null) lineRenderer.enabled = false;
        if (glowLight != null) glowLight.enabled = false;
        if (particles != null) particles.Stop();

        transform.DOMoveY(originalY - 0.5f, 0.4f).SetEase(Ease.InQuad).OnComplete(() =>
        {
            if (PuddleManager.Instance != null)
                PuddleManager.Instance.ReturnToPool(this);
            else
                gameObject.SetActive(false);
        });
    }

    public void ApplySizeMultiplier(float multiplier)
    {
        transform.localScale = new Vector3(5.0f * multiplier, 1f, 5.0f * multiplier);
    }

    // ── 保留 OnTrigger 作为"首次感应哨兵"便于调试，但不作为扣血依据 ─────────
    // PlayerController.UpdateEnvironmentalConditions 通过距离判定驱动全部效果
    // （这两个方法目前为空体，保留以便后续添加音效等初次感应事件）
    private void OnTriggerEnter(Collider other) { }
    private void OnTriggerExit(Collider other) { }
}
