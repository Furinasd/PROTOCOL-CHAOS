using UnityEngine;
using DG.Tweening;

public class EnemyPosture : MonoBehaviour
{
    [Header("Posture Settings")]
    public float maxPosture = 100f;
    public float currentPosture = 0f;
    public bool IsBroken { get; private set; }

    [Header("Health Settings")]
    public float maxHP = 200f;
    public float currentHP = 200f;

    public float HealthPercentage => currentHP / maxHP;

    // 当被打满时抛出事件，供处决系统监听
    public delegate void PostureBrokenHandler();
    public event PostureBrokenHandler OnPostureBroken;

    public void AddPosture(float amount)
    {
        if (IsBroken) return;
        currentPosture = Mathf.Min(currentPosture + amount, maxPosture);
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
        
        // 视觉提示：进入呆滞态
        Renderer rend = GetComponentInChildren<Renderer>();
        if(rend != null) rend.material.color = Color.gray;
        
        // 可选：让大脑处于 Stunned 状态
        EnemyAttackBrain brain = GetComponent<EnemyAttackBrain>();
        if(brain != null) brain.StopAllCoroutines();
    }

    /// <summary>
    /// 【极客处决】：触发高规格视觉终结。
    /// 步骤：1. 时间微顿；2. 替换 Unlit 材质；3. 缩放爆破；4. 彻底销毁。
    /// </summary>
    public void Execute()
    {
        // 1. 冻结 AI 和碰撞
        EnemyAttackBrain brain = GetComponent<EnemyAttackBrain>();
        if(brain != null) brain.enabled = false;
        
        Collider col = GetComponent<Collider>();
        if(col != null) col.enabled = false;

        // 2. 视觉转换：替换为高亮切碎效果 (Rim/White)
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            // 给到一个清脆的白白发光材质感
            rend.material.color = Color.white;
            rend.material.SetColor("_EmissionColor", Color.white * 2f);
            rend.material.EnableKeyword("_EMISSION");
        }

        // 3. 动力学反馈
        transform.DOShakePosition(0.3f, 0.3f, 30, 90f, false, true).SetUpdate(true);
        transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack).SetUpdate(true).OnComplete(() => {
            Destroy(gameObject);
        });

        Debug.Log("<color=white>💠 [处决] 秩序重归！目标已被彻底切割。</color>");
    }

    public void TakeDamage(float damage)
    {
        currentHP = Mathf.Max(0, currentHP - damage);
        Debug.Log($"【系统】怪物受到伤害，剩余生命值: {currentHP} / {maxHP}");
    }
}
