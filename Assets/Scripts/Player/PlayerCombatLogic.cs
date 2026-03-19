using UnityEngine;

// ==========================================
// 前置占位：由 TD 创建，防止 ChaosPuddle 报编译错误
// ==========================================
public class PlayerCombatLogic : MonoBehaviour, IDamageable
{
    public float currentHP = 100f;
    public bool isStandingOnAnomalyCore = false;

    public bool TakeDamage(AttackData attackData)
    {
        return false;
    }
}
