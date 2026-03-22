using UnityEngine;
using System.Collections.Generic;

// ==========================================
// Title: 污染区全局管理器 (Puddle Manager)
// Description: 1. 提供全局事件通知全场净化；
//              2. (后期扩展) 提供对象池以减少 Instantiate 开销。
// ==========================================
public class PuddleManager : MonoBehaviour
{
    public static PuddleManager Instance { get; private set; }

    [Header("📦 对象池配置")]
    public GameObject puddlePrefab;
    public int initialPoolSize = 10;
    
    private Stack<ChaosPuddle> pool = new Stack<ChaosPuddle>();

    // 全局净化事件：供玩家完美弹刀时触发
    public static System.Action OnGlobalPurify;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }

        // 预载池
        if (puddlePrefab != null)
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreateNewPuddle();
            }
        }
    }

    private ChaosPuddle CreateNewPuddle()
    {
        GameObject obj = Instantiate(puddlePrefab, transform);
        ChaosPuddle puddle = obj.GetComponent<ChaosPuddle>();
        obj.SetActive(false);
        pool.Push(puddle);
        return puddle;
    }

    /// <summary>
    /// 从池中提取并激活污染区
    /// </summary>
    public ChaosPuddle GetPuddleFromPool(Vector3 position, Polarity polarity, bool isSpecial)
    {
        ChaosPuddle puddle = (pool.Count > 0) ? pool.Pop() : CreateNewPuddle();
        // 【核心修复】：必须在 SetActive(true) 之前设置位置！
        // 否则 Puddle 脚本会在 OnEnable 时记录对象池当前（通常是 0,0,0）的 Y 轴作为 originalY，
        // 导致后续动画将污染区强行拉回地下，造成“瞬间消失”的假象。
        puddle.transform.position = position; 
        puddle.gameObject.SetActive(true);
        puddle.Contaminate(polarity, isSpecial);
        return puddle;
    }

    /// <summary>
    /// 回收污染区到池中
    /// </summary>
    public void ReturnToPool(ChaosPuddle puddle)
    {
        puddle.gameObject.SetActive(false);
        pool.Push(puddle);
    }

    /// <summary>
    /// 全域净化指令
    /// </summary>
    public static void TriggerGlobalPurify()
    {
        Debug.Log("<color=green>✨ [PuddleManager] 收到全局净化指令，正在同步至所有节点...</color>");
        OnGlobalPurify?.Invoke();
    }
}
