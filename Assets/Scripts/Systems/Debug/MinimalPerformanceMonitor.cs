using UnityEngine;
using System.Text;

/// <summary>
/// 极简性能监测器，用于在游戏左上角实时显示 FPS 和内存占用，不依赖庞大的 UGUI 系统。
/// 建议挂载在常驻内存的服务节点上（例如 GameManager/Loader）。
/// </summary>
public class MinimalPerformanceMonitor : MonoBehaviour
{
    private float deltaTime = 0.0f;
    private StringBuilder sb = new StringBuilder();
    private GUIStyle style = new GUIStyle();

    private void Awake()
    {
        // 保证跨场景不被销毁
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // 平滑计算帧间隔
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    private void OnGUI()
    {
        int w = Screen.width, h = Screen.height;

        // 统一缩放字体大小
        Rect rect = new Rect(20, 20, w, h * 2 / 100);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = h * 3 / 100;

        float msec = deltaTime * 1000.0f;
        float fps = 1.0f / deltaTime;
        
        // 获取当前 C# 托管内存使用量 (MB)
        long memory = System.GC.GetTotalMemory(false) / (1024 * 1024);

        // 颜色动态预警
        if (fps < 30) 
            style.normal.textColor = Color.red;       // 烂帧
        else if (fps < 55) 
            style.normal.textColor = Color.yellow;    // 掉帧
        else 
            style.normal.textColor = Color.green;     // 丝滑

        // 避免垃圾回收的字符串拼接
        sb.Clear();
        sb.AppendFormat("{0:0.0} ms ({1:0.} fps) | GC Mem: {2} MB", msec, fps, memory);
        
        // 渲染文本
        GUI.Label(rect, sb.ToString(), style);
    }
}
