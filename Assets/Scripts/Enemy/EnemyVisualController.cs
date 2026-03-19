using UnityEngine;
using DG.Tweening;

// ==========================================
// Title: 敌方光效与材质控制器 (DOTween版)
// ==========================================
public class EnemyVisualController : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    
    [Header("材质引用 (需带Emission)")]
    public Material neutralMat;
    public Material blueGlowMat;
    public Material redGlowMat;
    public Material stunnedMat;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = neutralMat;
    }

    /// <summary>
    /// 预警光效渐变
    /// </summary>
    public void GlowForTelegraph(Polarity polarity, float duration)
    {
        Material targetMat = polarity == Polarity.Red ? redGlowMat : blueGlowMat;
        
        if (targetMat == null)
        {
            Debug.LogError($"[EnemyVisual] {polarity}GlowMat 材质未在 Inspector 中赋值！");
            return;
        }

        meshRenderer.material = targetMat;
        
        // 安全检查：由于某些 Shader 可能没有 _EmissionColor 属性，先校验
        if (meshRenderer.material.HasProperty("_EmissionColor"))
        {
            Color targetColor = targetMat.GetColor("_EmissionColor");
            meshRenderer.material.SetColor("_EmissionColor", Color.black);
            meshRenderer.material.DOColor(targetColor, "_EmissionColor", duration).SetEase(Ease.InQuad);
        }
        else
        {
            Debug.LogWarning($"[EnemyVisual] 材质 {targetMat.name} 不包含 _EmissionColor 属性，无法执行渐变动画。");
        }
    }

    public void ResetVisual()
    {
        meshRenderer.material = neutralMat;
    }

    public void SetStunnedVisual()
    {
        if (stunnedMat != null)
        {
            meshRenderer.material = stunnedMat;
        }
        else
        {
            Debug.LogError("[EnemyVisual] stunnedMat 材质未在 Inspector 中赋值！");
        }

        // 宕机时让几何体灰暗且轻微颤抖
        transform.DOShakePosition(1.0f, 0.1f, 10, 90f, false, true).SetLoops(-1); 
    }
}
