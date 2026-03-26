using UnityEngine;
using System.Collections.Generic;

// ==========================================
// Title: 污染区全局管理器 (Puddle Manager)
// Description: 【主从架构版 v2】
//   1. 对象池：预载并复用 ChaosPuddle 实例
//   2. 活跃列表：记录当前场上所有已污染的 Puddle，
//      供 PlayerController 低频轮询，替代不稳定的 OnTriggerExit 机制
//   3. 全局净化事件：弹刀时广播 OnGlobalPurify 清场
// ==========================================
public class PuddleManager : MonoBehaviour
{
    public static PuddleManager Instance { get; private set; }

    [Header("📦 对象池配置")]
    public GameObject puddlePrefab;
    public int initialPoolSize = 10;

    private Stack<ChaosPuddle> pool = new Stack<ChaosPuddle>();

    // ── 活跃污染区列表（PlayerController 直接读取）────────────────────────────
    private List<ChaosPuddle> activePuddles = new List<ChaosPuddle>();
    /// <summary>当前场上所有已污染的污染区（只读快照）。</summary>
    public IReadOnlyList<ChaosPuddle> ActivePuddles => activePuddles;

    /// <summary>由 ChaosPuddle.Contaminate() 在激活时调用。</summary>
    public void RegisterActivePuddle(ChaosPuddle puddle)
    {
        if (!activePuddles.Contains(puddle))
            activePuddles.Add(puddle);
    }

    /// <summary>由 ChaosPuddle.Purify() 在净化时调用，O(1) 级别替换删减。</summary>
    public void UnregisterActivePuddle(ChaosPuddle puddle)
    {
        int idx = activePuddles.IndexOf(puddle);
        if (idx < 0) return;
        // Swap-and-pop：O(1) 无序移除，避免大列表逐元素位移
        int last = activePuddles.Count - 1;
        activePuddles[idx] = activePuddles[last];
        activePuddles.RemoveAt(last);
    }

    // 全局净化事件：供玩家完美弹刀时触发
    public static System.Action OnGlobalPurify;
    public static System.Action OnFirstSpecialPuddleSpawned;

    private bool hasRaisedFirstSpecialPuddleEvent;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }

        if (puddlePrefab != null)
        {
            for (int i = 0; i < initialPoolSize; i++)
                CreateNewPuddle();
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

    /// <summary>从池中提取并激活污染区。</summary>
    public ChaosPuddle GetPuddleFromPool(Vector3 position, Polarity polarity, bool isSpecial)
    {
        ChaosPuddle puddle = (pool.Count > 0) ? pool.Pop() : CreateNewPuddle();
        // 必须在 SetActive(true) 之前设置位置，防止 OnEnable 记录到错误的 originalY
        puddle.transform.position = position;
        puddle.gameObject.SetActive(true);
        puddle.Contaminate(polarity, isSpecial);

        if (isSpecial && !hasRaisedFirstSpecialPuddleEvent)
        {
            hasRaisedFirstSpecialPuddleEvent = true;
            OnFirstSpecialPuddleSpawned?.Invoke();
        }

        return puddle;
    }

    /// <summary>回收污染区到池中（由 ChaosPuddle.Purify 的 DOTween 回调调用）。</summary>
    public void ReturnToPool(ChaosPuddle puddle)
    {
        UnregisterActivePuddle(puddle); // 双保险：确保回收时不残留在活跃列表
        puddle.gameObject.SetActive(false);
        pool.Push(puddle);
    }

    /// <summary>全域净化指令（弹刀触发）。</summary>
    public static void TriggerGlobalPurify()
    {
        Debug.Log("<color=green>✨ [PuddleManager] 收到全局净化指令，正在同步至所有节点...</color>");
        OnGlobalPurify?.Invoke();
    }
}
