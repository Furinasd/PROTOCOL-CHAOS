using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEditor;
using UnityEngine.InputSystem.UI; // 必须引入新版 UI 输入模块命名空间
using System.Linq;

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

        // 2. 尝试为所有 TMP 文本补齐中文字体资产，避免方块字/乱码
        TMP_Text[] allTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        TMP_FontAsset miSans = FindMiSansFont();
        foreach (var txt in allTexts)
        {
            if (miSans != null && txt.font != miSans)
            {
                txt.font = miSans;
                Debug.Log($"<color=white>🔤 已绑定中文字体: {txt.name}</color>");
            }
        }

        // 3. 【已废弃】该部分逻辑由于硬编码坐标与新的 Quest 布局冲突，已彻底移除。
        /*
        GameObject FindObj(string name) { ... }
        GameObject qfm = FindObj("QuestFlowManager");
        ... (此处略去 80 行冲突代码) ...
        */
        Debug.Log("<color=green>✅ UIAutoFixer: 已跳过过时的 Quest 系统硬编码布局修复。</color>");
    }

    private static TMP_FontAsset FindMiSansFont()
    {
        string[] guids = AssetDatabase.FindAssets("MiSans t:TMP_FontAsset");
        if (guids == null || guids.Length == 0)
        {
            return null;
        }

        string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
    }
}
