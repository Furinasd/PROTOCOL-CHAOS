using UnityEngine;
using DG.Tweening;

// ==========================================
// Title: 敌方光效与材质控制器 (DOTween版)
// ==========================================
public class EnemyVisualController : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private Tween anomalyBurstTween;

    [Header("材质引用 (需带Emission)")]
    public Material neutralMat;
    public Material blueGlowMat;
    public Material redGlowMat;
    public Material purpleGlowMat; // 新增：紫色二连击预警
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
        Material targetMat = null;
        if (polarity == Polarity.Red) targetMat = redGlowMat;
        else if (polarity == Polarity.Blue) targetMat = blueGlowMat;
        else if (polarity == Polarity.Neutral) targetMat = purpleGlowMat;

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
        anomalyBurstTween?.Kill();
        meshRenderer.material = neutralMat;
    }

    public void SetStunnedVisual()
    {
        anomalyBurstTween?.Kill();
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

    /// <summary>
    /// 特殊污染区弹刀成功时触发：紫色发光爆闪 + 轻微模型冲击。
    /// </summary>
    public void TriggerAnomalyParryBurst(float duration = 0.3f)
    {
        if (meshRenderer == null) return;

        anomalyBurstTween?.Kill();

        Material burstMat = purpleGlowMat != null ? purpleGlowMat : redGlowMat;
        if (burstMat == null)
        {
            Debug.LogWarning("[EnemyVisual] 缺少可用爆闪材质，跳过异常源弹刀爆闪。");
            return;
        }

        meshRenderer.material = burstMat;

        if (meshRenderer.material.HasProperty("_EmissionColor"))
        {
            Color peak = new Color(1f, 0.2f, 1f, 1f) * 3.5f;
            meshRenderer.material.SetColor("_EmissionColor", peak);
            anomalyBurstTween = DOVirtual.DelayedCall(duration, () =>
            {
                if (meshRenderer != null)
                {
                    meshRenderer.material = neutralMat;
                }
            }).SetUpdate(true);
        }
        else
        {
            anomalyBurstTween = DOVirtual.DelayedCall(duration, () =>
            {
                if (meshRenderer != null)
                {
                    meshRenderer.material = neutralMat;
                }
            }).SetUpdate(true);
        }

        transform.DOPunchScale(new Vector3(0.08f, 0.08f, 0.08f), duration, 14, 0.6f).SetUpdate(true);
    }
}
