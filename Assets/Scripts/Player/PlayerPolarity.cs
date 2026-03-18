using UnityEngine;

public enum PolarityColor
{
    Blue, // 绝对零度
    Red   // 绝对炽热
}

public class PlayerPolarity : MonoBehaviour
{
    public PolarityColor CurrentColor { get; private set; } = PolarityColor.Blue;
    
    [Header("Parry Settings")]
    public float parryWindowDuration = 0.2f;
    public bool IsParryWindow { get; private set; }
    private float parryTimer = 0f;

    private Renderer meshRenderer;

    private void Awake()
    {
        meshRenderer = GetComponent<Renderer>();
        UpdateVisuals();
    }

    private void Update()
    {
        // 监听右键切换形态
        if (Input.GetMouseButtonDown(1))
        {
            SwitchPolarity();
        }

        // 维护 Parry 窗口
        if (IsParryWindow)
        {
            parryTimer -= Time.deltaTime;
            if (parryTimer <= 0f)
            {
                IsParryWindow = false;
            }
        }
    }

    private void SwitchPolarity()
    {
        CurrentColor = CurrentColor == PolarityColor.Blue ? PolarityColor.Red : PolarityColor.Blue;
        IsParryWindow = true;
        parryTimer = parryWindowDuration;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (meshRenderer != null)
        {
            meshRenderer.material.color = CurrentColor == PolarityColor.Blue ? Color.blue : Color.red;
        }
    }
}
