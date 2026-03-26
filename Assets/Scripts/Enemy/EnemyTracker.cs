using UnityEngine;

// ==========================================
// Title: 敌方动态追踪控制器 (Smooth Lerp 方案)
// Description: 处理带有重量感的平滑追踪，以及硬核动作游戏必备的"攻击锁定帧(Lock-in)"机制
// ==========================================
public class EnemyTracker : MonoBehaviour
{
    private Transform playerTransform;
    private bool isTracking = false;
    private float currentTurnSpeed = 5f;

    private void Start()
    {
        // 9天速通方案：直接通过 Tag 找玩家
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    private void Update()
    {
        if (!isTracking || playerTransform == null) return;

        // 1. 计算目标朝向 (忽略 Y 轴的高度差，防止怪物低头或仰头)
        Vector3 directionToPlayer = playerTransform.position - transform.position;
        directionToPlayer.y = 0f; 
        
        if (directionToPlayer.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            
            // 2. 核心数学：平滑插值 (Smooth Lerp) 营造重量感
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, currentTurnSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 开始追踪 (由 EnemyAttackBrain 在进入 Telegraph 状态时调用)
    /// </summary>
    /// <param name="turnSpeed">转身速度：重砸给 2f(慢)，轻击给 10f(快)</param>
    public void StartTracking(float turnSpeed)
    {
        currentTurnSpeed = turnSpeed;
        isTracking = true;
    }

    /// <summary>
    /// 停止追踪 - 触发 Lock-in 锁定帧 (在攻击前摇结束前 0.15s 调用！)
    /// </summary>
    public void StopTracking()
    {
        isTracking = false;
    }
}
