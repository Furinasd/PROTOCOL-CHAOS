using UnityEngine;
using DG.Tweening; // 引入 DOTween

// ==========================================
// Title: 敌方几何体形变控制器 (DOTween版)
// Description: 纯表现层，只负责接收 Brain 的指令执行缩放动画，不参与任何逻辑判定。
// ==========================================
public class EnemyShapeMorpher : MonoBehaviour
{
    private Vector3 originalScale;
    private Tween currentTween;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    /// <summary>
    /// 执行攻击前摇的夸张形变 (由 EnemyAttackBrain 在进入 Telegraph 状态时调用)
    /// </summary>
    /// <param name="duration">前摇时长</param>
    /// <param name="morphAxis">1: Y轴拉伸(重砸), 2: XZ轴扩宽(横扫), 3: 紫光爆发</param>
    public void MorphForTelegraph(float duration, int morphType)
    {
        currentTween?.Kill(); 

        Vector3 targetScale = originalScale;
        
        switch (morphType)
        {
            case 1: // 纵向重压 (Smash)
                targetScale = new Vector3(originalScale.x * 1.2f, originalScale.y * 3f, originalScale.z * 1.2f);
                break;
            case 2: // 横向蓄力 (Sweep)
                targetScale = new Vector3(originalScale.x * 3f, originalScale.y * 0.8f, originalScale.z * 1.5f);
                break;
            case 3: // 紫光爆发 (Full Scale)
                targetScale = originalScale * 2.5f;
                break;
        }

        currentTween = transform.DOScale(targetScale, duration).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// 恢复初始形状 (由 Brain 在攻击结束或被打断时调用)
    /// </summary>
    public void ResetShape(float duration = 0.2f)
    {
        currentTween?.Kill();
        currentTween = transform.DOScale(originalScale, duration).SetEase(Ease.OutQuad);
    }
}
