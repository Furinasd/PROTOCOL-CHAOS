using UnityEngine;
using System;

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

    [Header("✨ 教程充能反馈")]
    public bool triggerTutorialChargeGlitch = true;

    public event Action<int, int> OnEnergyChanged;
    public event Action OnEnergyFilled;
    public event Action OnTutorialCheatFilled;

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

        RaiseEnergyEvents(oldEnergy);
    }

    /// <summary>
    /// 尝试消耗能量，成功返回true
    /// </summary>
    public bool TryConsumeEnergy(int amount)
    {
        int oldEnergy = currentEnergyGrids;
        if (currentEnergyGrids >= amount)
        {
            currentEnergyGrids -= amount;
            Debug.Log($"<color=red>【消耗】消耗 {amount} 格秩序能量！剩余: {currentEnergyGrids} / {maxEnergyGrids} 格</color>");
            RaiseEnergyEvents(oldEnergy);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 教程用作弊接口：不改动消耗规则，只在敌人前摇前“白送满能”。
    /// </summary>
    public void CheatFillEnergyForTutorial()
    {
        int oldEnergy = currentEnergyGrids;
        currentEnergyGrids = maxEnergyGrids;

        if (currentEnergyGrids > oldEnergy)
        {
            Debug.Log("<color=cyan>【教程】战前注能：能量已强制回满，维持真实耗能弹刀习惯。</color>");
            OnTutorialCheatFilled?.Invoke();

            if (triggerTutorialChargeGlitch && CombatHUDManager.Instance != null)
            {
                CombatHUDManager.Instance.TriggerGlitchEffect(isPlayer: true);
            }

            if (CameraController.Instance != null)
            {
                CameraController.Instance.TriggerDashFOV();
            }
        }

        RaiseEnergyEvents(oldEnergy);
    }

    private void RaiseEnergyEvents(int oldEnergy)
    {
        OnEnergyChanged?.Invoke(currentEnergyGrids, maxEnergyGrids);

        if (oldEnergy < maxEnergyGrids && currentEnergyGrids == maxEnergyGrids)
        {
            OnEnergyFilled?.Invoke();
        }
    }
}
