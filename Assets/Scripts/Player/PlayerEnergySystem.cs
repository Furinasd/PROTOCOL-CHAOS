using UnityEngine;

// ==========================================
// Title: 玩家秩序能量系统 (Player Energy System)
// Description: 管理极性空间博弈中的核心资源——秩序能量(Order Energy)。
//              能量以"格"为单位，用于驱动异色弹刀等高阶流派。
// ==========================================
public class PlayerEnergySystem : MonoBehaviour
{
    [Header("⚡ 秩序能量 (Order Energy)")]
    public int maxEnergyGrids = 3;
    public int currentEnergyGrids = 0;

    [Header("🛡️ 环境保护 (Environmental Drain Protection)")]
    private float environmentalTimer = 0f;
    private bool isInPuddle = false;

    private void Update()
    {
        // 判定玩家在污染区内时从 0 开始记时，每 3 秒扣除一格能量
        if (isInPuddle)
        {
            environmentalTimer += Time.deltaTime;
            if (environmentalTimer >= 3.0f)
            {
                if (TryConsumeEnergy(1))
                {
                    Debug.Log("<color=red>【环境】在污染区停留满 3 秒，扣除 1 格能量。</color>");
                }
                environmentalTimer = 0f; // 重新计息
            }
        }
    }

    /// <summary>
    /// 设置是否在污染区内，进入时重置计时器
    /// </summary>
    public void SetInPuddle(bool inPuddle)
    {
        if (inPuddle && !isInPuddle)
        {
            environmentalTimer = 0f; // 进入瞬间从 0 开始计时
            Debug.Log("<color=cyan>【环境】进入污染区，计时开始。</color>");
        }
        else if (!inPuddle && isInPuddle)
        {
            environmentalTimer = 0f; // 离开瞬间重置
            Debug.Log("<color=white>【环境】离开污染区，计时归零。</color>");
        }
        isInPuddle = inPuddle;
    }

    /// <summary>
    /// 增加指定格数的能量
    /// </summary>
    public void AddEnergy(int amount)
    {
        int oldEnergy = currentEnergyGrids;
        currentEnergyGrids = Mathf.Clamp(currentEnergyGrids + amount, 0, maxEnergyGrids);
        
        if (currentEnergyGrids > oldEnergy)
        {
            Debug.Log($"<color=yellow>【充能】获取秩序能量！目前总量: {currentEnergyGrids} / {maxEnergyGrids} 格</color>");
        }
    }

    /// <summary>
    /// 尝试消耗能量，成功返回true
    /// </summary>
    public bool TryConsumeEnergy(int amount)
    {
        if (currentEnergyGrids >= amount)
        {
            currentEnergyGrids -= amount;
            Debug.Log($"<color=red>【消耗】消耗 {amount} 格秩序能量！剩余: {currentEnergyGrids} / {maxEnergyGrids} 格</color>");
            return true;
        }
        return false;
    }
}
