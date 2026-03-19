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

    [Header("⚙️ 惩罚数值 (普通与特殊区均保留此基础风险)")]
    public float damagePerSecond = 15f; 
    public float speedMultiplier = 0.5f; // 减速惩罚比例

    [Header("表现层 (需在 PCG 生成时或 Inspector 赋值)")]
    public Material normalFloorMat;
    public Material blueHazardMat;
    public Material redHazardMat;
    public Material anomalyCoreMat; // 核心高光材质

    private MeshRenderer meshRenderer;
    private PlayerCombatReceiver currentPlayerInside = null;
    private DemoPlayerController playerMovement = null;
    private float originalY;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        originalY = transform.position.y;
    }

    public void Contaminate(Polarity polarity, bool isSpecial, float duration = 8f)
    {
        if (isContaminated) return;

        isContaminated = true;
        puddlePolarity = polarity;
        isCoreAnomaly = isSpecial;

        meshRenderer.material = isCoreAnomaly ? anomalyCoreMat : (polarity == Polarity.Blue ? blueHazardMat : redHazardMat);
        transform.DOMoveY(originalY - 0.2f, 0.2f).SetEase(Ease.OutBounce);

        StartCoroutine(PurifyAfterTime(duration));
    }

    public void Purify()
    {
        if (!isContaminated) return;

        isContaminated = false;
        isCoreAnomaly = false;
        
        meshRenderer.material = normalFloorMat;
        transform.DOMoveY(originalY, 0.4f).SetEase(Ease.InOutQuad);

        if (playerMovement != null)
        {
            playerMovement.moveSpeed = 8f; 
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
            
            currentPlayerInside = null;
            if (playerMovement != null) playerMovement.moveSpeed = 8f;
            playerMovement = null;
        }
    }

    private void Update()
    {
        if (!isContaminated || currentPlayerInside == null || playerMovement == null) return;

        currentPlayerInside.currentHP -= damagePerSecond * Time.deltaTime;
        playerMovement.moveSpeed = 8f * speedMultiplier; 
    }
}
