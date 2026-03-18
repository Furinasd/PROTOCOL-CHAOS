using System.Collections;
using UnityEngine;

public class EnemyShapeMorpher : MonoBehaviour
{
    [Header("Telegraph & Attack Timings")]
    public float telegraphDuration = 0.5f;
    public float attackDuration = 0.2f;

    [Header("Prefabs or Child Objs")]
    public GameObject redSweepBoxPrefab; 
    public GameObject blueSmashCylinderPrefab;
    
    public void StartRedSweepAttack(System.Action onComplete)
    {
        StartCoroutine(AttackRoutine(true, onComplete));
    }

    public void StartBlueSmashAttack(System.Action onComplete)
    {
        StartCoroutine(AttackRoutine(false, onComplete));
    }

    private IEnumerator AttackRoutine(bool isRedSweep, System.Action onComplete)
    {
        // 1. Telegraph (前摇)
        Debug.Log(isRedSweep ? "【怪物前摇】核心变红，生成红色长方体" : "【怪物前摇】核心变蓝，头顶生成蓝色圆柱");
        // [未来接入] DOTween 或 AnimationCurve 实现形变动画
        
        yield return new WaitForSeconds(telegraphDuration);

        // 2. Attack Hitbox active (生成实体判定区)
        Debug.Log(isRedSweep ? "【怪物攻击判定期间】红色攻击生效！" : "【怪物攻击判定期间】蓝色攻击生效！");
        
        GameObject hitboxObj = new GameObject("TemporaryHitbox");
        hitboxObj.transform.position = transform.position;
        var col = hitboxObj.AddComponent<BoxCollider>(); 
        col.isTrigger = true;

        var hitboxData = hitboxObj.AddComponent<EnemyHitbox>();
        hitboxData.AttackColor = isRedSweep ? PolarityColor.Red : PolarityColor.Blue;
        hitboxData.IsLowAttack = isRedSweep;
        hitboxData.IsHighAttack = !isRedSweep;
        hitboxData.OwnerPosture = GetComponent<EnemyPosture>();

        // 模拟攻击存在的一小段时间
        yield return new WaitForSeconds(attackDuration);

        // 3. 销毁判定区
        Destroy(hitboxObj);

        // 结束回调返回状态机
        onComplete?.Invoke();
    }
}
