using UnityEngine;

public class EnemyPosture : MonoBehaviour
{
    [Header("Posture Settings")]
    public float maxPosture = 100f;
    public float currentPosture = 0f;
    public bool IsBroken { get; private set; }

    // 当被打满时抛出事件，供处决系统监听
    public delegate void PostureBrokenHandler();
    public event PostureBrokenHandler OnPostureBroken;

    public void AddPosture(float amount)
    {
        if (IsBroken) return;

        currentPosture += amount;
        Debug.Log($"【系统】怪物积累被动熵值：{currentPosture} / {maxPosture}");

        if (currentPosture >= maxPosture)
        {
            TriggerBrokenState();
        }
    }

    private void TriggerBrokenState()
    {
        IsBroken = true;
        Debug.Log("<color=grey>【宕机】怪物熵值爆满，陷入彻底瘫痪，等待 F 键处决</color>");
        OnPostureBroken?.Invoke();
        
        // 简单的视觉反馈
        Renderer rend = GetComponent<Renderer>();
        if(rend != null) rend.material.color = Color.grey;
    }
}
