using UnityEngine;
using DG.Tweening;

// ==========================================
// Title: 敌方光效与材质控制器 (SRP Batcher 兼容版)
// Key Change: 弃用 `.material` 实例化，全面改用 MaterialPropertyBlock (MPB)。
//             MPB 可在不打破 SRP Batcher 批次的前提下，对单个 Renderer 修改颜色属性。
// ==========================================
public class EnemyVisualController : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock mpb;

    // 缓存 Shader 属性 Hash，避免每次调用时内部都执行一次字符串哈希运算
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    [Header("材质引用 (需带Emission)")]
    public Material neutralMat;
    public Material blueGlowMat;
    public Material redGlowMat;
    public Material purpleGlowMat;
    public Material stunnedMat;

    // 跟踪当前正在播放的 DOTween Tween，用于在切换状态时安全中断
    private Tween glowTween;
    private Tween anomalyBurstTween;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        mpb = new MaterialPropertyBlock();

        // 使用 sharedMaterial 设置初始材质，不产生实例化
        if (neutralMat != null)
            meshRenderer.sharedMaterial = neutralMat;
    }

    private void OnDisable()
    {
        // 必须在对象被禁用（如对象池回收）时清理，防止复用时带着残余光效
        glowTween?.Kill();
        anomalyBurstTween?.Kill();
        // 清除 MPB 覆盖，恢复 sharedMaterial 的默认属性
        meshRenderer.SetPropertyBlock(null);
    }

    /// <summary>
    /// 预警光效渐变：通过 MPB 驱动，不破坏 SRP Batcher
    /// </summary>
    public void GlowForTelegraph(Polarity polarity, float duration)
    {
        Material targetMat = GetMaterialForPolarity(polarity);
        if (targetMat == null)
        {
            Debug.LogError($"[EnemyVisual] {polarity}GlowMat 材质未在 Inspector 中赋值！");
            return;
        }

        // 切换到目标共享材质（必要时），这里用 sharedMaterial 不产生实例
        meshRenderer.sharedMaterial = targetMat;

        // 中断上一个光效 Tween
        glowTween?.Kill();

        if (!targetMat.HasProperty(EmissionColorID))
        {
            Debug.LogWarning($"[EnemyVisual] 材质 {targetMat.name} 不包含 _EmissionColor 属性，无法执行渐变动画。");
            return;
        }

        Color peakColor = targetMat.GetColor(EmissionColorID);

        // 先把 MPB 颜色重置为黑色（关灯），再渐变到峰值
        meshRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(EmissionColorID, Color.black);
        meshRenderer.SetPropertyBlock(mpb);

        // 用 DOVirtual.Color 驱动插值，通过 MPB 写入颜色——不产生任何材质实例！
        glowTween = DOVirtual.Color(Color.black, peakColor, duration, (c) =>
        {
            meshRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColorID, c);
            meshRenderer.SetPropertyBlock(mpb);
        }).SetEase(Ease.InQuad);
    }

    public void ResetVisual()
    {
        glowTween?.Kill();
        anomalyBurstTween?.Kill();

        if (neutralMat != null)
            meshRenderer.sharedMaterial = neutralMat;

        // 清除所有 MPB 覆盖，让材质恢复默认属性
        meshRenderer.SetPropertyBlock(null);
    }

    public void SetStunnedVisual()
    {
        glowTween?.Kill();
        anomalyBurstTween?.Kill();

        if (stunnedMat != null)
        {
            meshRenderer.sharedMaterial = stunnedMat;
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

        meshRenderer.sharedMaterial = burstMat;

        if (burstMat.HasProperty(EmissionColorID))
        {
            Color peak = new Color(1f, 0.2f, 1f, 1f) * 3.5f;

            // 立刻写入峰值，然后延迟复原
            meshRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColorID, peak);
            meshRenderer.SetPropertyBlock(mpb);

            anomalyBurstTween = DOVirtual.DelayedCall(duration, () =>
            {
                if (meshRenderer != null)
                {
                    meshRenderer.sharedMaterial = neutralMat;
                    meshRenderer.SetPropertyBlock(null); // 清除 MPB 覆盖
                }
            }).SetUpdate(true);
        }
        else
        {
            anomalyBurstTween = DOVirtual.DelayedCall(duration, () =>
            {
                if (meshRenderer != null)
                {
                    meshRenderer.sharedMaterial = neutralMat;
                }
            }).SetUpdate(true);
        }

        transform.DOPunchScale(new Vector3(0.08f, 0.08f, 0.08f), duration, 14, 0.6f).SetUpdate(true);
    }

    private Material GetMaterialForPolarity(Polarity polarity)
    {
        if (polarity == Polarity.Red) return redGlowMat;
        if (polarity == Polarity.Blue) return blueGlowMat;
        if (polarity == Polarity.Neutral) return purpleGlowMat;
        return null;
    }
}
