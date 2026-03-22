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
    private float lastEnvironmentalDrainTime = -10f;

    /// <summary>
    /// 环境持续扣能接口：带有内置冷却（3.0秒），防止重叠污染区导致瞬间被吸干
    /// </summary>
    public void TryEnvironmentalDrain(int amount, float interval = 3.0f)
    {
        if (Time.time - lastEnvironmentalDrainTime >= interval)
        {
            if (TryConsumeEnergy(amount))
            {
                lastEnvironmentalDrainTime = Time.time;
            }
        }
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
            Debug.Log($"<color=red>【消耗】消耗 {amount} 格秩序能量！剩余: {currentEnergyGrids} / {maxEnergyGrids} 格</color>\n调用源: {System.Environment.StackTrace}");
            return true;
        }
        return false;
    }
}
