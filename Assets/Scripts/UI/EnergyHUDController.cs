using UnityEngine;
using UnityEngine.UI;

// ==========================================
// Title: 秩序能量 HUD 控制器 (Energy HUD Controller)
// Description: 在屏幕底部正中心显示玩家当前秩序能量格数。
//              使用极简方格图标阵列，每帧检测变化时刷新。
// ==========================================
public class EnergyHUDController : MonoBehaviour
{
    [Header("数据源")]
    public PlayerEnergySystem energySystem;

    [Header("UI 元素 - 按顺序挂入能量格 Image")]
    public Image[] energyGridImages;

    [Header("视觉颜色")]
    public Color activeColor = new Color(0.97f, 0.9f, 0.3f, 1f);   // 充能：金黄
    public Color emptyColor  = new Color(0.25f, 0.25f, 0.25f, 0.5f); // 空格：暗灰

    private int lastKnownEnergy = -1;

    private void Start()
    {
        if (energySystem == null)
            energySystem = FindFirstObjectByType<PlayerEnergySystem>();

        if (energyGridImages == null || energyGridImages.Length == 0)
        {
            energyGridImages = GetComponentsInChildren<Image>();
            Debug.Log($"[EnergyHUD] 自动获取了 {energyGridImages.Length} 个能量格 UI 组件。");
        }

        if (energySystem == null)
        {
            Debug.LogWarning("[EnergyHUD] PlayerEnergySystem not found in scene.");
            return;
        }
        ApplyDisplay();
    }

    private void Update()
    {
        if (energySystem == null) return;
        int cur = energySystem.currentEnergyGrids;
        if (cur == lastKnownEnergy) return;
        lastKnownEnergy = cur;
        ApplyDisplay();
    }

    private void ApplyDisplay()
    {
        if (energyGridImages == null) return;
        int cur = energySystem != null ? energySystem.currentEnergyGrids : 0;
        for (int i = 0; i < energyGridImages.Length; i++)
        {
            if (energyGridImages[i] != null)
                energyGridImages[i].color = (i < cur) ? activeColor : emptyColor;
        }
    }
}
