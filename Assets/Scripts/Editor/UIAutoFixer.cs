using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEditor;
using UnityEngine.InputSystem.UI; // 必须引入新版 UI 输入模块命名空间

/// <summary>
/// 自动修复 UI 交互与乱码问题的工具脚本
/// </summary>
[InitializeOnLoad]
public class UIAutoFixer
{
    static UIAutoFixer()
    {
        // 延迟执行，确保场景加载完毕
        EditorApplication.delayCall += PerformFix;
    }

    [MenuItem("Tools/3C Tuning/Final UI Fix")]
    public static void PerformFix()
    {
        Debug.Log("<color=cyan>[UIFixer] 正在扫描并修复 UI 系统...</color>");

        // 1. 修复 EventSystem 缺失与新版 Input System 冲突问题
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            // 【重要】项目使用的是 New Input System，必须使用 InputSystemUIInputModule 
            // 否则会报 InvalidOperationException: You are trying to read Input using the UnityEngine.Input class...
            es.AddComponent<InputSystemUIInputModule>(); 
            
            Debug.Log("<color=green>✅ 已自动补偿基于 InputSystem 的 EventSystem 组件，交互冲突已修复。</color>");
        }

        // 2. 修复文字乱码（将中文替换为英文以便默认字体显示）
        TMP_Text[] allTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var txt in allTexts)
        {
            // 针对性替换界面关键文字
            if (txt.text.Contains("重新") || txt.name == "Text" && txt.transform.parent.name == "RestartButton")
            {
                txt.text = "RESTART [R]";
                Debug.Log($"<color=white>🔤 已修复文本乱码: {txt.name} -> RESTART [R]</color>");
            }
            
            if (txt.text.Contains("游戏") || txt.text.Contains("死亡") || txt.name.Contains("Title"))
            {
                txt.text = "GAME OVER";
                Debug.Log($"<color=white>🔤 已修复文本乱码: {txt.name} -> GAME OVER</color>");
            }
        }

        Debug.Log("<color=cyan>[UIFixer] 修复流程完成。请在场景中保存并尝试运行。</color>");
    }
}
