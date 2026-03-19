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
    public void MorphForTelegraph(float duration)
    {
        // 杀掉上一个可能没播完的动画，防止冲突
        currentTween?.Kill(); 
        
        // 沿 Y 轴拉伸 3 倍，SetEase(Ease.OutBack) 会自带极其高级的"果冻蓄力回弹感"！
        currentTween = transform.DOScale(
            new Vector3(originalScale.x, originalScale.y * 3f, originalScale.z), 
            duration
        ).SetEase(Ease.OutBack);
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
