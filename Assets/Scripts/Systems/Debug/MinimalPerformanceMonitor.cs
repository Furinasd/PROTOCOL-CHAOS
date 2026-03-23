using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 极简性能监测器，用于在游戏左上角实时显示 FPS 和内存占用，不依赖庞大的 UGUI 系统。
/// 建议挂载在常驻内存的服务节点上（例如 GameManager/Loader）。
/// </summary>
[DisallowMultipleComponent]
public class MinimalPerformanceMonitor : MonoBehaviour
{
    [Header("Behavior")]
    [SerializeField] private bool visibleOnStart = true;
    [SerializeField] private KeyCode legacyToggleKey = KeyCode.F8;
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private string inputSystemToggleBinding = "<Keyboard>/f8";
    private InputAction toggleAction;
#endif
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.25f;

    [Header("Layout")]
    [SerializeField] private Vector2 anchorOffset = new Vector2(16f, 16f);
    [SerializeField, Min(10)] private int baseFontSize = 20;
    [SerializeField] private float fontScaleByHeight = 0.025f;

    private static MinimalPerformanceMonitor instance;

    private readonly StringBuilder sb = new StringBuilder(256);
    private GUIStyle style;
    private FrameTiming[] frameTimings = new FrameTiming[1];

    private bool isVisible;
    private string cachedLabel = string.Empty;
    private float timer;
    private int frameCount;
    private float frameTimeSum;
    private float minFrameMs = float.MaxValue;
    private float maxFrameMs;
    private int lastScreenHeight = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (FindAnyObjectByType<MinimalPerformanceMonitor>() != null)
        {
            return;
        }

        var go = new GameObject("[Debug] MinimalPerformanceMonitor");
        go.AddComponent<MinimalPerformanceMonitor>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        isVisible = visibleOnStart;
        style = new GUIStyle
        {
            alignment = TextAnchor.UpperLeft
        };

        RecalculateStyle();
    }

    private void Update()
    {
        if (IsTogglePressed())
        {
            isVisible = !isVisible;
        }

        float frameMs = Time.unscaledDeltaTime * 1000f;
        frameCount++;
        timer += Time.unscaledDeltaTime;
        frameTimeSum += frameMs;
        minFrameMs = Mathf.Min(minFrameMs, frameMs);
        maxFrameMs = Mathf.Max(maxFrameMs, frameMs);

        if (timer >= refreshInterval)
        {
            RebuildLabel();
        }
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleAction == null)
        {
            toggleAction = new InputAction("PerfMonitorToggle", InputActionType.Button, inputSystemToggleBinding);
        }

        if (!toggleAction.enabled)
        {
            toggleAction.Enable();
        }
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleAction != null && toggleAction.enabled)
        {
            toggleAction.Disable();
        }
#endif
    }

    private bool IsTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleAction != null && toggleAction.WasPressedThisFrame())
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(legacyToggleKey))
        {
            return true;
        }
#endif

        return false;
    }

    private void OnGUI()
    {
        if (!isVisible)
        {
            return;
        }

        if (Screen.height != lastScreenHeight)
        {
            RecalculateStyle();
        }

        Rect rect = new Rect(anchorOffset.x, anchorOffset.y, Screen.width * 0.9f, Screen.height * 0.25f);
        GUI.Label(rect, cachedLabel, style);
    }

    private void RebuildLabel()
    {
        float safeTimer = Mathf.Max(timer, 0.0001f);
        int safeFrameCount = Mathf.Max(frameCount, 1);

        float avgFps = safeFrameCount / safeTimer;
        float avgFrameMs = frameTimeSum / safeFrameCount;
        long gcMemoryMb = System.GC.GetTotalMemory(false) / (1024 * 1024);
        long totalAllocatedMb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);

        double cpuFrameMs = -1;
        double gpuFrameMs = -1;
        FrameTimingManager.CaptureFrameTimings();
        uint timingCount = FrameTimingManager.GetLatestTimings((uint)frameTimings.Length, frameTimings);
        if (timingCount > 0)
        {
            cpuFrameMs = frameTimings[0].cpuFrameTime;
            gpuFrameMs = frameTimings[0].gpuFrameTime;
        }

        if (avgFps < 30f)
        {
            style.normal.textColor = Color.red;
        }
        else if (avgFps < 55f)
        {
            style.normal.textColor = Color.yellow;
        }
        else
        {
            style.normal.textColor = Color.green;
        }

        sb.Clear();
        sb.AppendFormat("FPS {0:0.0} | Avg {1:0.00} ms | Min {2:0.00} | Max {3:0.00}\n", avgFps, avgFrameMs, minFrameMs, maxFrameMs);
        sb.AppendFormat("GC {0} MB | Total Alloc {1} MB", gcMemoryMb, totalAllocatedMb);

        if (cpuFrameMs >= 0d)
        {
            sb.AppendFormat("\nCPU Frame {0:0.00} ms", cpuFrameMs);
        }

        if (gpuFrameMs >= 0d)
        {
            sb.AppendFormat(" | GPU Frame {0:0.00} ms", gpuFrameMs);
        }

        cachedLabel = sb.ToString();

        timer = 0f;
        frameCount = 0;
        frameTimeSum = 0f;
        minFrameMs = float.MaxValue;
        maxFrameMs = 0f;
    }

    private void RecalculateStyle()
    {
        lastScreenHeight = Screen.height;
        style.fontSize = Mathf.Max(baseFontSize, Mathf.RoundToInt(lastScreenHeight * fontScaleByHeight));
    }
}
