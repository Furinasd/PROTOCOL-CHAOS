using UnityEngine;
using System.Collections;
using DG.Tweening;

// ==========================================
// Title: 混沌地板节点 (Grid Contamination 方案B)
// Author: 白糖 & 精灵小姐 (Redesigned by Antigravity)
// Description: 直接挂载在 PCG 地板上的污染组件。
//              【重构版】：增加 LineRenderer 和 Point Light 以在 URP 中实现 100% 可见度。
// ==========================================
public class ChaosPuddle : MonoBehaviour
{
    [Header("🎨 污染属性")]
    public bool isContaminated = false;
    public bool isCoreAnomaly = false; 
    public Polarity puddlePolarity;

    [Header("⚙️ 惩罚数值")]
    public float damagePerSecond = 15f; 
    public float speedMultiplier = 0.5f; 
    public int energyDrainPerSecond = 1; 

    [Header("表现层 (需在 Prefab 或 Inspector 赋值)")]
    public Material normalFloorMat;
    public Material purpleHazardMat; 
    public Material anomalyCoreMat; 

    // 增强组件
    private LineRenderer lineRenderer;
    private Light glowLight;
    private ParticleSystem particles;
    private MeshRenderer meshRenderer;

    private PlayerCombatReceiver currentPlayerInside = null;
    private PlayerEnergySystem playerEnergy = null;
    private PlayerController playerMovement = null;
    private float originalY;

    private void Awake()
    {
        // 1. 获取基础渲染器
        Transform visualT = transform.Find("PuddleVisual");
        if (visualT != null)
        {
            meshRenderer = visualT.GetComponent<MeshRenderer>();
            visualT.localRotation = Quaternion.Euler(-90f, 0f, 0f); 
        }
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();

        // 2. 动态获取/添加增强组件
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
            lineRenderer.SetPosition(i, new Vector3(Mathf.Sin(angle) * (radius * 1.05f), 0.05f, Mathf.Cos(angle) * (radius * 1.05f)));
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
        StopAllCoroutines();
        transform.DOKill();
    }

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
            
            // 动画反馈
            lineRenderer.enabled = true;
            glowLight.enabled = true;
            glowLight.color = highlightColor;
            
            lineRenderer.material.DOColor(highlightColor, "_BaseColor", 0.2f);
            DOTween.To(() => glowLight.intensity, x => glowLight.intensity = x, 2.5f, 0.2f);

            if (meshRenderer.material.HasProperty("_BaseColor"))
            {
                seq.Join(meshRenderer.material.DOColor(highlightColor, "_BaseColor", 0.1f).SetLoops(2, LoopType.Yoyo));
            }

            seq.Append(transform.DOMoveY(originalY + 0.05f, 0.2f).SetEase(Ease.OutBounce)); 
        }

        if (particles != null)
        {
            var main = particles.main;
            main.startColor = isCoreAnomaly ? Color.white : (Color)highlightColor;
            particles.Play();
        }
    }

    public void Purify()
    {
        if (!isContaminated) return;

        isContaminated = false;
        isCoreAnomaly = false;
        
        if (meshRenderer != null) meshRenderer.material = normalFloorMat;
        if (lineRenderer != null) lineRenderer.enabled = false;
        if (glowLight != null) glowLight.enabled = false;
        if (particles != null) particles.Stop();

        transform.DOMoveY(originalY - 0.5f, 0.4f).SetEase(Ease.InQuad).OnComplete(() => {
            if (PuddleManager.Instance != null)
                PuddleManager.Instance.ReturnToPool(this);
            else
                gameObject.SetActive(false);
        });

        if (playerMovement != null) playerMovement.environmentalSpeedMultiplier = 1f; 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isContaminated) return;
        if (other.CompareTag("Player"))
        {
            currentPlayerInside = other.GetComponent<PlayerCombatReceiver>();
            playerMovement = other.GetComponent<PlayerController>();
            playerEnergy = other.GetComponent<PlayerEnergySystem>();
            if (currentPlayerInside != null && isCoreAnomaly) currentPlayerInside.isStandingOnAnomalyCore = true; 
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (currentPlayerInside != null && isCoreAnomaly) currentPlayerInside.isStandingOnAnomalyCore = false;
            if (playerMovement != null) playerMovement.environmentalSpeedMultiplier = 1f;
            currentPlayerInside = null;
            playerEnergy = null;
            playerMovement = null;
        }
    }

    public void ApplySizeMultiplier(float multiplier)
    {
        transform.localScale = new Vector3(5.0f * multiplier, 1f, 5.0f * multiplier);
    }

    private void Update()
    {
        if (!isContaminated || currentPlayerInside == null || playerMovement == null) return;
        currentPlayerInside.currentHP -= damagePerSecond * Time.deltaTime;
        playerMovement.environmentalSpeedMultiplier = speedMultiplier; 
        if (playerEnergy != null && Time.frameCount % 60 == 0) playerEnergy.TryConsumeEnergy(energyDrainPerSecond);
    }
}
