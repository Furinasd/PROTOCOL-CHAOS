using System.Collections;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 教学 Boss：基于状态机的多段处决循环与终幕演出。
/// </summary>
[RequireComponent(typeof(EnemyPosture), typeof(EnemyAttackBrain))]
public class TutorialBoss : MonoBehaviour
{
    public enum BossState
    {
        Phase1,
        Phase2,
        Vulnerable,
        Executing,
        Dead
    }

    public static event System.Action<TutorialBoss> OnBossDefeated;

    [Header("Execute Loop")]
    [SerializeField] private float executeDamagePercent = 0.35f;
    [SerializeField] private float executeFlatDamage = 0f;
    [SerializeField] private int estimatedExecuteLoops = 3;

    [Header("Finale Juice")]
    [SerializeField] private float finalSlowMotionDuration = 1.2f;
    [SerializeField] private float finalSlowTimeScale = 0.1f;
    [SerializeField] private float hoverDuration = 2f;
    [SerializeField] private float hoverHeight = 1.2f;
    [SerializeField] private AudioClip finalExplosionSfx;
    [SerializeField] private Material wireframeMaterial;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    public BossState CurrentState { get; private set; } = BossState.Phase1;

    private EnemyPosture posture;
    private EnemyAttackBrain brain;
    private MeshRenderer[] renderers;
    private Vector3 spawnPosition;
    private bool finalSequenceStarted;
    private int executeCount;

    private void Awake()
    {
        posture = GetComponent<EnemyPosture>();
        brain = GetComponent<EnemyAttackBrain>();
        renderers = GetComponentsInChildren<MeshRenderer>(true);
        spawnPosition = transform.position;

        posture.isBoss = true;
    }

    private void OnEnable()
    {
        if (posture != null)
        {
            posture.OnPostureBroken += HandlePostureBroken;
            posture.OnEnemyDefeated += HandleEnemyDefeated;
        }
    }

    private void OnDisable()
    {
        if (posture != null)
        {
            posture.OnPostureBroken -= HandlePostureBroken;
            posture.OnEnemyDefeated -= HandleEnemyDefeated;
        }
    }

    public bool TryExecuteFromPlayer()
    {
        if (posture == null || CurrentState != BossState.Vulnerable)
        {
            return false;
        }

        executeCount++;
        CurrentState = BossState.Executing;

        float executeDamage = Mathf.Max(posture.maxHP * executeDamagePercent + executeFlatDamage, 1f);
        posture.TakeDamage(executeDamage);

        if (posture.currentHP <= 0f)
        {
            if (!finalSequenceStarted)
            {
                StartCoroutine(FinalExecuteRoutine());
            }
            return true;
        }

        posture.ResetPostureToNeutral();
        if (brain != null)
        {
            brain.ResetAfterStun();
        }

        CurrentState = posture.HealthPercentage <= 0.5f ? BossState.Phase2 : BossState.Phase1;
        Log($"Boss 被处决 {executeCount} 次，估计总循环 {estimatedExecuteLoops} 次。");
        return true;
    }

    private void HandlePostureBroken()
    {
        if (CurrentState == BossState.Dead || finalSequenceStarted)
        {
            return;
        }

        CurrentState = BossState.Vulnerable;
        Log("Boss 躯干值归零，进入可处决状态。");
    }

    private void HandleEnemyDefeated()
    {
        if (finalSequenceStarted || CurrentState == BossState.Dead)
        {
            return;
        }

        // 兜底：如果外部逻辑直接把 HP 打空，也走终幕序列。
        StartCoroutine(FinalExecuteRoutine());
    }

    private IEnumerator FinalExecuteRoutine()
    {
        finalSequenceStarted = true;
        CurrentState = BossState.Executing;

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.DoHitstop(finalSlowMotionDuration, finalSlowTimeScale);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.TriggerVacuumEffect(1.4f, 0.08f);
        }

        ApplyWireframeMaterial();

        Vector3 hoverTarget = spawnPosition + Vector3.up * hoverHeight;
        yield return transform.DOMove(hoverTarget, 0.4f).SetEase(Ease.OutSine).SetUpdate(true).WaitForCompletion();
        yield return new WaitForSecondsRealtime(hoverDuration);

        if (AudioManager.Instance != null)
        {
            AudioClip clip = finalExplosionSfx != null ? finalExplosionSfx : AudioManager.Instance.sfxExecutionHit;
            AudioManager.Instance.PlayHighlightSFX(clip, 1f);
        }

        CurrentState = BossState.Dead;
        OnBossDefeated?.Invoke(this);
        Destroy(gameObject);
    }

    private void ApplyWireframeMaterial()
    {
        if (wireframeMaterial == null || renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            renderers[i].material = wireframeMaterial;
        }
    }

    private void Log(string msg)
    {
        if (!verboseLog)
        {
            return;
        }

        Debug.Log($"[TutorialBoss] {msg}");
    }
}
