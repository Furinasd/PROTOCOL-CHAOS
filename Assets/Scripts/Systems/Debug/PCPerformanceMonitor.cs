using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 【TD 级重构】零 GC 性能监控仪。
/// 使用 TextMeshProUGUI.SetText(StringBuilder) 原生零拷贝赋值，
/// 手写 AppendInt/AppendLong 彻底消除数值拼接产生的装箱 (boxing)。
/// 替代 MinimalPerformanceMonitor（OnGUI 方案）。
/// 挂载：Canvas > 任意 TextMeshProUGUI 对象，将该 TMP 拖入 statsText 槽位。
/// </summary>
[DisallowMultipleComponent]
public class PCPerformanceMonitor : MonoBehaviour
{
    [Header("绑定")]
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("行为")]
    [SerializeField] private bool visibleOnStart = true;
    [SerializeField] private KeyCode legacyToggleKey = KeyCode.F8;
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private string inputSystemToggleBinding = "<Keyboard>/f8";
    private InputAction toggleAction;
#endif
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.5f;

    // ──────────────────────────────────────────────
    // 私有状态
    // ──────────────────────────────────────────────
    private static PCPerformanceMonitor instance;

    private bool isVisible;
    private float timer;
    private int frameCount;
    private float frameTimeSum;
    private float minFrameMs = float.MaxValue;
    private float maxFrameMs;

    private readonly FrameTiming[] frameTimings = new FrameTiming[1];

    // 预分配 256 char，永不扩容（监控文本远不到此长度）
    private readonly StringBuilder sb = new StringBuilder(256);

    // stackalloc 辅助缓冲：用于 AppendInt/AppendLong，放在类层面以供复用
    // 注意：Span<char> 本身不产生堆分配，但声明为字段需要用 char[] 代替
    private readonly char[] digitBuf = new char[20];

    // ──────────────────────────────────────────────
    // 生命周期
    // ──────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (FindAnyObjectByType<PCPerformanceMonitor>() != null) return;

        // 自动创建：Canvas > TMP > Monitor
        var canvasGo = new GameObject("[Debug] PCPerformanceMonitor");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var textGo = new GameObject("StatsText", typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);
        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(16f, -16f);
        rt.sizeDelta = new Vector2(520f, 200f);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 18f;
        tmp.color = Color.white;

        var monitor = canvasGo.AddComponent<PCPerformanceMonitor>();
        monitor.statsText = tmp;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        isVisible = visibleOnStart;
        if (statsText != null) statsText.gameObject.SetActive(isVisible);
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleAction == null)
            toggleAction = new InputAction("PCPerfToggle", InputActionType.Button, inputSystemToggleBinding);
        if (!toggleAction.enabled) toggleAction.Enable();
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleAction != null && toggleAction.enabled) toggleAction.Disable();
#endif
    }

    private void Update()
    {
        if (IsTogglePressed())
        {
            isVisible = !isVisible;
            if (statsText != null) statsText.gameObject.SetActive(isVisible);
        }

        if (!isVisible) return;

        float frameMs = Time.unscaledDeltaTime * 1000f;
        frameCount++;
        timer        += Time.unscaledDeltaTime;
        frameTimeSum += frameMs;

        if (frameMs < minFrameMs) minFrameMs = frameMs;
        if (frameMs > maxFrameMs) maxFrameMs = frameMs;

        if (timer >= refreshInterval)
            RebuildStats();
    }

    // ──────────────────────────────────────────────
    // 核心：零 GC 统计刷新
    // ──────────────────────────────────────────────

    private void RebuildStats()
    {
        if (statsText == null) return;

        float safeTimer  = timer      > 0.0001f ? timer      : 0.0001f;
        int   safeFrames = frameCount > 0        ? frameCount : 1;

        float avgFps     = safeFrames / safeTimer;
        float avgFrameMs = frameTimeSum / safeFrames;
        long  gcMb       = System.GC.GetTotalMemory(false) / (1024 * 1024);
        long  totalAllocMb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);

        // CPU/GPU Frame Timing
        double cpuMs = -1d, gpuMs = -1d;
        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, frameTimings) > 0)
        {
            cpuMs = frameTimings[0].cpuFrameTime;
            gpuMs = frameTimings[0].gpuFrameTime;
        }

        // FPS 颜色分级
        string fpsColor = avgFps >= 55f ? "#00FF00" : avgFps >= 30f ? "#FFDD00" : "#FF4444";

        sb.Clear();

        // 行1：FPS
        sb.Append("<color=").Append(fpsColor).Append(">FPS ");
        AppendInt(sb, Mathf.RoundToInt(avgFps));
        sb.Append("</color>  Avg ");
        AppendFloat1(sb, avgFrameMs);
        sb.Append("ms  Min ");
        AppendFloat1(sb, minFrameMs == float.MaxValue ? 0f : minFrameMs);
        sb.Append("ms  Max ");
        AppendFloat1(sb, maxFrameMs);
        sb.Append("ms\n");

        // 行2：GC & Alloc
        sb.Append("<color=#FF7777>GC ");
        AppendLong(sb, gcMb);
        sb.Append(" MB</color>  Total Alloc ");
        AppendLong(sb, totalAllocMb);
        sb.Append(" MB");

        // 行3（可选）：CPU/GPU 帧时间
        if (cpuMs >= 0d)
        {
            sb.Append("\nCPU ");
            AppendFloat2(sb, (float)cpuMs);
            sb.Append("ms");
        }
        if (gpuMs >= 0d)
        {
            sb.Append("  GPU ");
            AppendFloat2(sb, (float)gpuMs);
            sb.Append("ms");
        }

        // ★ TMP 原生 StringBuilder 赋值：0 GC，0 string 分配
        statsText.SetText(sb);

        // 重置计数器
        timer        = 0f;
        frameCount   = 0;
        frameTimeSum = 0f;
        minFrameMs   = float.MaxValue;
        maxFrameMs   = 0f;
    }

    // ──────────────────────────────────────────────
    // 手写零装箱数字拼接工具
    // ──────────────────────────────────────────────

    /// <summary>将 int 逐位写入 StringBuilder，无任何堆分配。</summary>
    private void AppendInt(StringBuilder sb, int value)
    {
        if (value == 0) { sb.Append('0'); return; }
        if (value < 0)  { sb.Append('-'); value = -value; }

        int idx = 0;
        while (value > 0)
        {
            digitBuf[idx++] = (char)('0' + value % 10);
            value /= 10;
        }
        for (int i = idx - 1; i >= 0; i--) sb.Append(digitBuf[i]);
    }

    /// <summary>将 long 逐位写入 StringBuilder，无任何堆分配。</summary>
    private void AppendLong(StringBuilder sb, long value)
    {
        if (value == 0) { sb.Append('0'); return; }
        if (value < 0)  { sb.Append('-'); value = -value; }

        int idx = 0;
        while (value > 0)
        {
            digitBuf[idx++] = (char)('0' + value % 10);
            value /= 10;
        }
        for (int i = idx - 1; i >= 0; i--) sb.Append(digitBuf[i]);
    }

    /// <summary>1 位小数的 float 拼接，例如 16.7。</summary>
    private void AppendFloat1(StringBuilder sb, float value)
    {
        int intPart  = (int)value;
        int fracPart = Mathf.Abs(Mathf.RoundToInt((value - intPart) * 10f));
        AppendInt(sb, intPart);
        sb.Append('.');
        sb.Append((char)('0' + fracPart % 10));
    }

    /// <summary>2 位小数的 float 拼接，例如 8.33。</summary>
    private void AppendFloat2(StringBuilder sb, float value)
    {
        int intPart  = (int)value;
        int frac     = Mathf.Abs(Mathf.RoundToInt((value - intPart) * 100f));
        AppendInt(sb, intPart);
        sb.Append('.');
        sb.Append((char)('0' + (frac / 10) % 10));
        sb.Append((char)('0' + frac % 10));
    }

    // ──────────────────────────────────────────────
    // 输入检测
    // ──────────────────────────────────────────────

    private bool IsTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleAction != null && toggleAction.WasPressedThisFrame()) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(legacyToggleKey)) return true;
#endif
        return false;
    }
}
