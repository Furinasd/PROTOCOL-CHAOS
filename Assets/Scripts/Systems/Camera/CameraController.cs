using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;

// ==========================================
// Title: 镜头控制中枢 (Camera Controller)
// Description: 统管 FOV 动态爆发（Dash 空间压缩）与战斗区机位切换。
//              所有镜头表现逻辑在此集中，外部只调接口。
//
// Inspector 配置说明：
//  - freeLookCam : 拖入场景中的 CM FreeLook1
//  - battleCam   : 拖入专为首领战设计的静态广角 Cinemachine Virtual Camera
//                  （如没有，脚本会优雅降级，不会报错）
// ==========================================
public class CameraController : MonoBehaviour
{
    // 单例
    public static CameraController Instance { get; private set; }

    [Header("📷 Cinemachine 引用")]
    [Tooltip("主游戏摄像机 CM FreeLook1")]
    public CinemachineCamera freeLookCam;

    [Tooltip("Boss 战专用静态广角虚拟相机（可选）")]
    public CinemachineCamera battleCam;

    // ──────────────────────────────────────────
    [Header("💨 Dash FOV 爆发参数")]
    [Tooltip("基础 FOV（编辑器内 CM 相机设置的默认值）")]
    public float baseFOV = 60f;

    [Tooltip("Dash 瞬间爆发到的 FOV 峰值")]
    public float dashFOVPeak = 80f;

    [Tooltip("达到峰值所需时间（极短，造成「瞬间击穿」感）")]
    public float dashFOVRiseTime = 0.05f;

    [Tooltip("从峰值回弹到 baseFOV 的时间（越长，粘滞感越强）")]
    public float dashFOVFallTime = 0.3f;

    // ──────────────────────────────────────────
    [Header("🏟️ 战斗区机位参数")]
    [Tooltip("普通探索时 freeLookCam 的优先级")]
    public int explorationPriority = 10;

    [Tooltip("进入战斗区时 battleCam 的优先级（需高于 explorationPriority）")]
    public int battlePriority = 20;

    // ──────────────────────────────────────────
    private Tween fovTween;
    private bool inBattleMode = false;

    // ──────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        // 安全初始化：确保机位处于探索模式
        if (battleCam != null)
            battleCam.Priority = explorationPriority - 1;
    }

    // ══════════════════════════════════════════
    // 公开接口
    // ══════════════════════════════════════════

    /// <summary>
    /// 【Dash 瞬间调用】触发 FOV 非线性爆发 + 回弹。
    /// 曲线：0.05s 瞬间拉大（OutExpo）→ 0.3s 极缓回弹（OutExpo）
    /// 产生「突破音障」的空间压缩粘滞感。
    /// </summary>
    public void TriggerDashFOV()
    {
        if (freeLookCam == null) return;

        // 杀掉之前还在进行的回弹，防止 Tween 叠加导致 FOV 漂移
        fovTween?.Kill();

        // 【瞬间爆发阶段】0.05s 冲到峰值
        fovTween = DOTween.To(
            getter: () => freeLookCam.Lens.FieldOfView,
            setter: v  => { var lens = freeLookCam.Lens; lens.FieldOfView = v; freeLookCam.Lens = lens; },
            endValue: dashFOVPeak,
            duration: dashFOVRiseTime
        )
        .SetEase(Ease.Linear)           // 上升要"暴力"，用线性感觉最冲
        .SetUpdate(UpdateType.Normal, isIndependentUpdate: true)  // 不受 timeScale 影响
        .OnComplete(() =>
        {
            // 【缓慢回弹阶段】0.3s OutExpo，慢慢松弛
            fovTween = DOTween.To(
                getter: () => freeLookCam.Lens.FieldOfView,
                setter: v  => { var lens = freeLookCam.Lens; lens.FieldOfView = v; freeLookCam.Lens = lens; },
                endValue: baseFOV,
                duration: dashFOVFallTime
            )
            .SetEase(Ease.OutExpo)
            .SetUpdate(UpdateType.Normal, isIndependentUpdate: true);
        });
    }

    /// <summary>
    /// 【进入 Boss 战场时调用】切换到广角静态机位。
    /// 如果 battleCam 未配置，会优雅忽略（无错误）。
    /// </summary>
    public void EnterBattleMode()
    {
        if (battleCam == null || inBattleMode) return;
        inBattleMode = true;
        battleCam.Priority = battlePriority;
        Debug.Log("<color=yellow>🎥 [Camera] 进入战斗机位 — 广角大构图</color>");
    }

    /// <summary>
    /// 【战斗结束时调用】恢复探索机位。
    /// </summary>
    public void ExitBattleMode()
    {
        if (battleCam == null || !inBattleMode) return;
        inBattleMode = false;
        battleCam.Priority = explorationPriority - 1;
        Debug.Log("<color=cyan>🎥 [Camera] 退出战斗机位 — 恢复探索视角</color>");
    }

    /// <summary>
    /// 将 FOV 强制重置（场景重置时调用）。
    /// </summary>
    public void ResetFOV()
    {
        fovTween?.Kill();
        if (freeLookCam != null)
        {
            var lens = freeLookCam.Lens;
            lens.FieldOfView = baseFOV;
            freeLookCam.Lens = lens;
        }
    }
}
