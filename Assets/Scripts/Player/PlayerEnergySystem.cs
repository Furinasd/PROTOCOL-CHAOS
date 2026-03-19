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
