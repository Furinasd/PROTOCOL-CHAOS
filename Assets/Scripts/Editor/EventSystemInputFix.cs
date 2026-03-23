using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// ==========================================
// Title: EventSystem Input System 自动修复器
// Description: 修复 "You are trying to read Input using the UnityEngine.Input class,
//              but you have switched active Input handling to Input System package" 报错。
//
// 根本原因：场景中的 EventSystem 默认携带的是旧版 StandaloneInputModule，
//           但 Player Settings 已切换为 New Input System Package，两套系统互斥。
//
// 解决方案：运行时自动检测并把 StandaloneInputModule 替换为 InputSystemUIInputModule。
//
// 用法：将此组件挂载在场景中任意常驻对象（如 GameManager）上，
//       让它在 Awake 阶段完成热修复，无需手动改动场景中的 EventSystem Prefab。
// ==========================================
[DefaultExecutionOrder(-100)] // 确保在大多数其他脚本前执行
public class EventSystemInputFix : MonoBehaviour
{
    private void Awake()
    {
#if ENABLE_INPUT_SYSTEM
        EventSystem es = FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            Debug.LogWarning("[EventSystemInputFix] 场景中未找到 EventSystem，跳过修复。");
            return;
        }

        var oldModule = es.GetComponent<StandaloneInputModule>();
        if (oldModule != null)
        {
            Destroy(oldModule);

            // 确保没有重复添加
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                es.gameObject.AddComponent<InputSystemUIInputModule>();
                Debug.Log("<color=green>[EventSystemInputFix] ✅ StandaloneInputModule → InputSystemUIInputModule 替换完成。</color>");
            }
        }
        else
        {
            // 无需修复，提示确认
            Debug.Log("<color=grey>[EventSystemInputFix] EventSystem 已使用正确的 InputSystemUIInputModule，无需热修复。</color>");
        }
#else
        Debug.Log("<color=grey>[EventSystemInputFix] 当前项目使用旧版 Input Manager，不需要此修复器。</color>");
#endif
    }
}
