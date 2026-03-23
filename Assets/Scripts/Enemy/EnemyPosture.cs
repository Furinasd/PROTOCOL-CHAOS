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

    [Header("Type")]
    public bool isBoss = false;

    public float HealthPercentage => currentHP / maxHP;
    public float PosturePercentage => maxPosture <= 0f ? 0f : Mathf.Clamp01(currentPosture / maxPosture);

    private void Awake()
    {
        // 如果是普通小怪，保底挂载头顶双条 UI，避免场景重构后漏配组件。
        if (!isBoss && GetComponent<EnemyWorldHUD>() == null)
        {
            gameObject.AddComponent<EnemyWorldHUD>();
        }
    }

    // 当被打满时抛出事件，供处决系统监听
    public delegate void PostureBrokenHandler();
    public event PostureBrokenHandler OnPostureBroken;

    public void AddPosture(float amount)
    {
        if (IsBroken) return;
        currentPosture = Mathf.Min(currentPosture + amount, maxPosture);
        Debug.Log($"【系统】怪物积累被动熵值：{currentPosture} / {maxPosture}");
        NotifyUIImmediate();

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
        if (rend != null) rend.material.color = Color.gray;

        // 可选：让大脑处于 Stunned 状态
        EnemyAttackBrain brain = GetComponent<EnemyAttackBrain>();
        if (brain != null) brain.StopAllCoroutines();
    }

    /// <summary>
    /// 【极客处决】：触发高规格视觉终结。
    /// 步骤：1. 时间微顿；2. 替换 Unlit 材质；3. 缩放爆破；4. 彻底销毁。
    /// </summary>
    /// <summary>
    /// 【极客处决】：触发高规格视觉终结。
    /// 变更为：扣除怪物 50% HP，重置躯干值，开启新循环。
    /// </summary>
    public void Execute()
    {
        // 1. 瞬间伤害
        float damage = maxHP * 0.5f;
        TakeDamage(damage);

        if (currentHP > 0)
        {
            // 重置博弈状态
            currentPosture = 0;
            IsBroken = false;
            NotifyUIImmediate();

            // 恢复 AI
            EnemyAttackBrain brain = GetComponent<EnemyAttackBrain>();
            if (brain != null) brain.ResetAfterStun();

            // 恢复材质
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend != null) rend.material.color = Color.white;

            Debug.Log("<color=white>💠 [处决] 秩序回溯！目标受到重创并重启进入下一阶段。</color>");
        }
        else
        {
            // 彻底销毁逻辑
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack).SetUpdate(true).OnComplete(() =>
            {
                Destroy(gameObject);
            });
            Debug.Log("<color=red>💠 [处决] 秩序彻底肃清！</color>");
        }
    }

    public void TakeDamage(float damage)
    {
        currentHP = Mathf.Max(0, currentHP - damage);
        Debug.Log($"【系统】怪物受到伤害，剩余生命值: {currentHP} / {maxHP}");
        NotifyUIImmediate();

        // 【新规：UI 反馈】同步触发 Boss 血条抖动
        if (CombatHUDManager.Instance != null)
            CombatHUDManager.Instance.TriggerGlitchEffect(isPlayer: false);
    }

    public void TriggerAnomalyCoreParryUIFeedback()
    {
        EnemyVisualController visual = GetComponent<EnemyVisualController>();
        if (visual != null)
        {
            visual.TriggerAnomalyParryBurst();
        }

        EnemyWorldHUD hud = GetComponent<EnemyWorldHUD>();
        if (hud != null)
        {
            hud.TriggerAnomalyCoreParryPulse();
            hud.ForceSyncNow();
        }

        if (CombatHUDManager.Instance != null)
        {
            CombatHUDManager.Instance.TriggerGlitchEffect(isPlayer: false);
            CombatHUDManager.Instance.ForceRefreshBossUI();
        }
    }

    private void NotifyUIImmediate()
    {
        EnemyWorldHUD hud = GetComponent<EnemyWorldHUD>();
        if (hud != null) hud.ForceSyncNow();

        if (CombatHUDManager.Instance != null)
            CombatHUDManager.Instance.ForceRefreshBossUI();
    }
}
