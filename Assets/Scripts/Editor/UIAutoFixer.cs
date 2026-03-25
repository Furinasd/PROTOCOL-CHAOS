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

        // 3. 修复 Quest 系统层级与绑定
        GameObject FindObj(string name) {
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach(var o in all) {
                if(o.name == name && !EditorUtility.IsPersistent(o)) return o;
            }
            return null;
        }

        GameObject qfm = FindObj("QuestFlowManager");
        GameObject canvas = FindObj("TutorialCanvas");
        GameObject qum = FindObj("QuestUIManager");
        GameObject qt = FindObj("QuestText");
        GameObject player = FindObj("Player");

        Debug.Log($"[QuestFix] QFM:{qfm}, Canvas:{canvas}, QUM:{qum}, Player:{player}");

        if (qfm != null && canvas != null && qum != null)
        {
            // 修正层级
            qum.transform.SetParent(canvas.transform, false);
            qum.SetActive(true); // 确保 UI 开启
            canvas.SetActive(true);
            
            GameObject textRoot = FindObj("TextRoot");
            if (textRoot == null || textRoot.transform.parent != qum.transform)
            {
                textRoot = new GameObject("TextRoot", typeof(RectTransform));
                textRoot.transform.SetParent(qum.transform, false);
            }
            
            RectTransform trRect = textRoot.GetComponent<RectTransform>();
            trRect.anchorMin = new Vector2(0.5f, 0.5f);
            trRect.anchorMax = new Vector2(0.5f, 0.5f);
            trRect.anchoredPosition = new Vector2(0, 400);

            if (qt != null)
            {
                qt.transform.SetParent(textRoot.transform, false);
                
                // 绑定字体
                var tmp = qt.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Arts/MiSans VF SDF.asset");
                    if (font != null) tmp.font = font;
                }
            }

            // 绑定 QuestUIManager 字段
            var qumComp = qum.GetComponent<QuestUIManager>();
            if (qumComp != null)
            {
                var serializedQum = new SerializedObject(qumComp);
                serializedQum.FindProperty("questFlowManager").objectReferenceValue = qfm.GetComponent<QuestFlowManager>();
                serializedQum.FindProperty("playerEnergySystem").objectReferenceValue = player?.GetComponent<PlayerEnergySystem>();
                serializedQum.FindProperty("questText").objectReferenceValue = qt?.GetComponent<TextMeshProUGUI>();
                serializedQum.FindProperty("canvasGroup").objectReferenceValue = qum.GetComponent<CanvasGroup>();
                serializedQum.FindProperty("textRoot").objectReferenceValue = trRect;
                serializedQum.ApplyModifiedProperties();
                Debug.Log("<color=green>✅ QuestUIManager 引用已自动绑定。</color>");
            }

            // 修正 QuestFlowManager 标签与引用
            if (qfm.CompareTag("Player"))
            {
                qfm.tag = "Untagged";
                Debug.Log("<color=yellow>⚠️ QuestFlowManager 标签已从 Player 修正为 Untagged。</color>");
            }

            var qfmComp = qfm.GetComponent<QuestFlowManager>();
            if (qfmComp != null)
            {
                var serializedQfm = new SerializedObject(qfmComp);
                serializedQfm.FindProperty("playerEnergySystem").objectReferenceValue = player?.GetComponent<PlayerEnergySystem>();
                serializedQfm.FindProperty("playerCombatReceiver").objectReferenceValue = player?.GetComponent<PlayerCombatReceiver>();
                serializedQfm.ApplyModifiedProperties();
                Debug.Log("<color=green>✅ QuestFlowManager 引用已自动绑定。</color>");
            }
        }
        else
        {
            Debug.LogWarning($"[QuestFix] 无法完成 Quest 修复。缺失对象: QFM?{qfm == null}, Canvas?{canvas == null}, QUM?{qum == null}");
        }

        Debug.Log("<color=cyan>[UIFixer] 修复流程完成。请在场景中保存并尝试运行。</color>");
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
