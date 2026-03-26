using UnityEngine;
using DG.Tweening;

// ==========================================
// Title: 砸地冲击与污染区表现 (Tech-Design Simplified VFX)
// Description: 为策划提供的轻量化视觉反馈脚本。
// 通过 DOTween 控制缩放与透明度，代替重度粒子计算。
// ==========================================
public class GroundImpactVFX : MonoBehaviour
{
    [Header("🎨 视觉组件")]
    public Transform impactRing;    // 冲击环 (Quad/Mesh)
    public Transform puddleVisual;  // 污染区主体 (Quad/Decal)
    public ParticleSystem sparks;   // 飞溅粒子 (Burst mode)

    [Header("⚙️ 时间配置")]
    public float impactDuration = 0.3f;
    public float puddleDuration = 5f;
    public float fadeOutTime = 0.8f;

    private Material ringMat;
    private Material puddleMat;

    private void Awake()
    {
        if (impactRing) ringMat = impactRing.GetComponent<Renderer>().material;
        if (puddleVisual) puddleMat = puddleVisual.GetComponent<Renderer>().material;
    }

    /// <summary>
    /// 触发表现效果
    /// </summary>
    /// <param name="themeColor">污染区的主题颜色</param>
    public void PlayEffect(Color themeColor)
    {
        // --- 1. 冲击环动画 (快进快出) ---
        if (impactRing != null)
        {
            impactRing.gameObject.SetActive(true);
            impactRing.localScale = Vector3.zero;
            ringMat.color = themeColor;
            
            impactRing.DOScale(new Vector3(4, 4, 4), impactDuration).SetEase(Ease.OutQuart);
            ringMat.DOFade(0, impactDuration).SetEase(Ease.InQuad).OnComplete(() => {
                impactRing.gameObject.SetActive(false);
            });
        }

        // --- 2. 污染区生成动画 (回弹弹出) ---
        if (puddleVisual != null)
        {
            puddleVisual.gameObject.SetActive(true);
            puddleVisual.localScale = Vector3.zero;
            
            // 设置主题色（保持一点透明度，比如 Alpha = 0.8）
            Color targetColor = themeColor;
            targetColor.a = 0.8f;
            puddleMat.color = targetColor;

            puddleVisual.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        }

        // --- 3. 粒子喷射 (Burst一次性) ---
        if (sparks != null)
        {
            var main = sparks.main;
            main.startColor = themeColor;
            sparks.Play();
        }

        // --- 4. 自动销毁回收流程 ---
        Invoke(nameof(StartFadeOut), puddleDuration);
    }

    private void StartFadeOut()
    {
        if (puddleVisual != null)
        {
            puddleVisual.DOScale(Vector3.zero, fadeOutTime).SetEase(Ease.InBack);
            puddleMat.DOFade(0, fadeOutTime).OnComplete(() => {
                // 建议使用对象池 Recycle，Demo阶段直接销毁
                Destroy(gameObject);
            });
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
