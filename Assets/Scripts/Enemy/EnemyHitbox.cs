using UnityEngine;

public class EnemyHitbox : MonoBehaviour
{
    [Header("Attack Payload Details")]
    public PolarityColor AttackColor;
    
    // 标记空间高度属性
    public bool IsLowAttack; // 下段（横扫），需要玩家跳跃
    public bool IsHighAttack; // 上段（重砸），需要玩家下蹲

    [Header("References")]
    public EnemyPosture OwnerPosture; // 对应怪物的躯干系统
}
