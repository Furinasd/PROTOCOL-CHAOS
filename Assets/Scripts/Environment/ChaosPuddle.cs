using UnityEngine;
using System.Collections;
using DG.Tweening;

// ==========================================
// Title: 混沌地板节点 (Grid Contamination 方案B)
// Author: 白糖 & 精灵小姐
// Description: 直接挂载在 PCG 地板上的污染组件。纯粹的风险区（强制规避），特殊区保留风险但提供极高弹刀收益。
// ==========================================
public class ChaosPuddle : MonoBehaviour
{
    [Header("🎨 污染属性")]
    public bool isContaminated = false;
    public bool isCoreAnomaly = false; // 是否为30%概率的"异常源核心区"
    public Polarity puddlePolarity;

    [Header("⚙️ 惩罚数值 (所有极性一致)")]
    public float damagePerSecond = 15f; 
    public float speedMultiplier = 0.5f; // 减速惩罚比例
    public int energyDrainPerSecond = 1; // 每秒抽取的秩序能量

    [Header("表现层 (需在 PCG 生成时或 Inspector 赋值)")]
    public Material normalFloorMat;
    public Material blueHazardMat;
    public Material redHazardMat;
    public Material anomalyCoreMat; // 核心高光材质

    private MeshRenderer meshRenderer;
    private PlayerCombatReceiver currentPlayerInside = null;
    private PlayerEnergySystem playerEnergy = null;
    private DemoPlayerController playerMovement = null;
    private float originalY;

    private void Awake()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer == null) Debug.LogWarning("<color=red>🛑 [Puddle] 子物体中未找到 MeshRenderer，渲染或将失败！</color>");
    }

    private void OnEnable()
    {
        PuddleManager.OnGlobalPurify += Purify;
        // 每次从对象池取出时，重新记录原始 Y 轴
        originalY = transform.position.y;
    }

    private void OnDisable()
    {
        PuddleManager.OnGlobalPurify -= Purify;
        StopAllCoroutines();
        transform.DOKill();
    }

    public void Contaminate(Polarity polarity, bool isSpecial, float duration = 8f)
    {
        if (isContaminated) return;

        isContaminated = true;
        puddlePolarity = polarity;
        isCoreAnomaly = isSpecial;

        Debug.Log($"<color=cyan>🌊 [Puddle] 污染区初始化: 极性={polarity}, 特殊={isSpecial}</color>");

        if (meshRenderer != null)
        {
            meshRenderer.material = isCoreAnomaly ? anomalyCoreMat : (polarity == Polarity.Blue ? blueHazardMat : redHazardMat);
        
            // 【视觉警报】：快速闪烁并位移
            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOMoveY(originalY - 0.3f, 0.1f).SetEase(Ease.OutFlash));
            seq.Join(meshRenderer.material.DOColor(Color.white, 0.1f).SetLoops(2, LoopType.Yoyo));
            seq.Append(transform.DOMoveY(originalY - 0.2f, 0.2f).SetEase(Ease.OutBounce));
        }
        else
        {
            Debug.LogWarning("<color=red>🛑 [Puddle] MeshRenderer 为空，污染区将不可见！</color>");
        }

        StartCoroutine(PurifyAfterTime(duration));
    }

    public void Purify()
    {
        if (!isContaminated) return;

        isContaminated = false;
        isCoreAnomaly = false;
        
        meshRenderer.material = normalFloorMat;

        // 执行回收表现
        transform.DOMoveY(originalY, 0.4f).SetEase(Ease.InOutQuad).OnComplete(() => {
            if (PuddleManager.Instance != null)
                PuddleManager.Instance.ReturnToPool(this);
            else
                gameObject.SetActive(false);
        });

        if (playerMovement != null)
        {
            playerMovement.environmentalSpeedMultiplier = 1f; 
        }
    }

    private IEnumerator PurifyAfterTime(float duration)
    {
        yield return new WaitForSeconds(duration);
        Purify();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isContaminated) return;
        
        if (other.CompareTag("Player"))
        {
            currentPlayerInside = other.GetComponent<PlayerCombatReceiver>();
            playerMovement = other.GetComponent<DemoPlayerController>();
            playerEnergy = other.GetComponent<PlayerEnergySystem>();
            
            if (currentPlayerInside != null && isCoreAnomaly)
            {
                currentPlayerInside.isStandingOnAnomalyCore = true; 
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (currentPlayerInside != null && isCoreAnomaly)
            {
                currentPlayerInside.isStandingOnAnomalyCore = false;
            }
            
            if (playerMovement != null) playerMovement.environmentalSpeedMultiplier = 1f;

            currentPlayerInside = null;
            playerEnergy = null;
            playerMovement = null;
        }
    }

    private void Update()
    {
        if (!isContaminated || currentPlayerInside == null || playerMovement == null) return;

        // 统一环境压制：扣血 + 减速 + 抽能
        currentPlayerInside.currentHP -= damagePerSecond * Time.deltaTime;
        playerMovement.environmentalSpeedMultiplier = speedMultiplier; 

        if (playerEnergy != null)
        {
            // 抽能逻辑：每秒损失对应格数
            if (Time.frameCount % 60 == 0) // 粗略每秒一次，避免太过频繁但保持压力
            {
                playerEnergy.TryConsumeEnergy(energyDrainPerSecond);
            }
        }
    }
}
