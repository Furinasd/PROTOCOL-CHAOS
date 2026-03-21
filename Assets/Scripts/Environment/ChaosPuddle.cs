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
    [UnityEngine.Serialization.FormerlySerializedAs("blueHazardMat")]
    public Material purpleHazardMat; // 唯一的普通污染区紫色材质
    public Material anomalyCoreMat; // 核心高光材质

    private MeshRenderer meshRenderer;
    private PlayerCombatReceiver currentPlayerInside = null;
    private PlayerEnergySystem playerEnergy = null;
    private DemoPlayerController playerMovement = null;
    private float originalY;

    private void Awake()
    {
        // 【优先搜索】显式命名的视觉子物体，避免获取到根节点的冗余渲染器
        Transform visualT = transform.Find("PuddleVisual");
        if (visualT != null)
        {
            meshRenderer = visualT.GetComponent<MeshRenderer>();
            // 【重要修复】将 Quad 翻转至正面朝上，解决因为 270 度导致的背面剔除 (Cull Back) 透明问题
            visualT.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();
        
        if (meshRenderer == null) Debug.LogWarning("<color=red>🛑 [Puddle] 未找到 MeshRenderer，污染区将不可见！</color>");
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
        // 极性仅作记录，视觉统一为紫色
        puddlePolarity = polarity;
        isCoreAnomaly = isSpecial;

        Debug.Log($"<color=cyan>🌊 [Puddle] 污染区初始化: 极性={polarity}, 特殊={isSpecial}</color>");

        if (meshRenderer != null)
        {
            meshRenderer.material = isCoreAnomaly ? anomalyCoreMat : purpleHazardMat;
        
            // 【重要修复】：将动画坐标修正为+0.3f向上漂浮，防止面片陷入 Ground 层导致不可见
            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOMoveY(originalY + 0.3f, 0.1f).SetEase(Ease.OutFlash));
            seq.Join(meshRenderer.material.DOColor(Color.white, 0.1f).SetLoops(2, LoopType.Yoyo));
            seq.Append(transform.DOMoveY(originalY, 0.2f).SetEase(Ease.OutBounce));
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
            Debug.Log($"<color=orange>🎯 [Puddle] 玩家进入污染区范围！</color>");
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
            Debug.Log($"<color=gray>🍃 [Puddle] 玩家离开污染区范围。</color>");
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

    /// <summary>
    /// 外部调用以动态调整污染区大小
    /// </summary>
    public void ApplySizeMultiplier(float multiplier)
    {
        // 保持 Y 轴缩放为 1，仅调整 XZ 平面
        transform.localScale = new Vector3(5.0f * multiplier, 1f, 5.0f * multiplier);
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
