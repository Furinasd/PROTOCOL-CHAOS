using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 新手任务流程控制器：Phase1~4 串联、事件计数、无黑屏一镜到底过渡。
/// </summary>
public class QuestFlowManager : MonoBehaviour
{
    public enum QuestPhase
    {
        None = 0,
        Phase1 = 1,
        Phase2 = 2,
        Phase3 = 3,
        Phase4 = 4,
        Completed = 5
    }

    [Header("References")]
    [SerializeField] private PlayerCombatReceiver playerCombatReceiver;
    [SerializeField] private PlayerEnergySystem playerEnergySystem;
    [SerializeField] private ArenaTransitionManager arenaTransitionManager;

    [Header("Flow")]
    [SerializeField] private bool autoStartOnSceneLoad = true;
    [SerializeField] private bool enableAutoSave = true;
    [SerializeField] private bool clearSaveOnFlowCompleted = true;
    [SerializeField] private int phase1AbsorbTarget = 3;
    [SerializeField] private int phase2PerfectParryTarget = 2;
    [SerializeField] private int phase3EnergyFullTarget = 2;
    [SerializeField] private int phase3ExecuteTarget = 1;
    [SerializeField] private float phaseTransitionDelay = 0.4f;

    [Header("Spawn")]
    [SerializeField] private Transform phase1SpawnRoot;
    [SerializeField] private Transform phase2SpawnRoot;
    [SerializeField] private Transform phase3SpawnRoot;
    [SerializeField] private Transform phase4SpawnRoot;
    [SerializeField] private GameObject[] phase1EnemyPrefabs;
    [SerializeField] private GameObject[] phase2EnemyPrefabs;
    [SerializeField] private GameObject[] phase3EnemyPrefabs;
    [SerializeField] private GameObject phase4BossPrefab;
    [SerializeField] private bool useSpawnRootChildrenAsTemplateFallback = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    public QuestPhase CurrentPhase { get; private set; } = QuestPhase.None;

    public event Action<QuestPhase> OnPhaseChanged;
    public event Action<QuestPhase, int, int> OnPhaseProgressChanged;
    public event Action OnFlowCompleted;

    private int absorbCounter;
    private int perfectParryCounter;
    private int energyFullCounter;
    private int executeCounter;
    private bool flowRunning;
    private QuestPhase loadedStartPhase = QuestPhase.Phase1;
    private bool loadedFromSave;
    private readonly List<GameObject> activePhaseEntities = new List<GameObject>();

    private const string SavePrefix = "QuestFlow_";
    private const string SaveValidKey = SavePrefix + "Valid";
    private const string SavePhaseKey = SavePrefix + "Phase";
    private const string SaveAbsorbKey = SavePrefix + "Absorb";
    private const string SaveParryKey = SavePrefix + "Parry";
    private const string SaveEnergyKey = SavePrefix + "Energy";
    private const string SaveExecuteKey = SavePrefix + "Execute";

    private void Awake()
    {
        if (playerCombatReceiver == null)
        {
            playerCombatReceiver = FindFirstObjectByType<PlayerCombatReceiver>();
        }

        if (playerEnergySystem == null)
        {
            playerEnergySystem = FindFirstObjectByType<PlayerEnergySystem>();
        }

        if (arenaTransitionManager == null)
        {
            arenaTransitionManager = FindFirstObjectByType<ArenaTransitionManager>();
        }
    }

    private void OnEnable()
    {
        if (playerCombatReceiver != null)
        {
            playerCombatReceiver.OnSamePolarityAbsorbed += HandleSamePolarityAbsorbed;
            playerCombatReceiver.OnPerfectParrySucceeded += HandlePerfectParrySucceeded;
            playerCombatReceiver.OnEnemyExecuted += HandleEnemyExecuted;
        }

        if (playerEnergySystem != null)
        {
            playerEnergySystem.OnEnergyFilled += HandleEnergyFilled;
        }

        EnemyAttackBrain.OnTelegraphStarted += HandleEnemyTelegraphStarted;
    }

    private void Start()
    {
        if (autoStartOnSceneLoad)
        {
            StartFlow();
        }
    }

    private void OnDisable()
    {
        if (playerCombatReceiver != null)
        {
            playerCombatReceiver.OnSamePolarityAbsorbed -= HandleSamePolarityAbsorbed;
            playerCombatReceiver.OnPerfectParrySucceeded -= HandlePerfectParrySucceeded;
            playerCombatReceiver.OnEnemyExecuted -= HandleEnemyExecuted;
        }

        if (playerEnergySystem != null)
        {
            playerEnergySystem.OnEnergyFilled -= HandleEnergyFilled;
        }

        EnemyAttackBrain.OnTelegraphStarted -= HandleEnemyTelegraphStarted;
    }

    public void StartFlow()
    {
        if (flowRunning)
        {
            return;
        }

        if (playerCombatReceiver == null || playerEnergySystem == null)
        {
            Debug.LogWarning("[QuestFlow] 缺少玩家战斗组件引用，流程无法启动。");
            return;
        }

        LoadProgressIfNeeded();

        flowRunning = true;
        StartCoroutine(FlowRoutine());
    }

    private IEnumerator FlowRoutine()
    {
        if (loadedStartPhase == QuestPhase.Completed)
        {
            EnterPhase(QuestPhase.Completed);
            OnFlowCompleted?.Invoke();
            flowRunning = false;
            yield break;
        }

        if (loadedStartPhase == QuestPhase.Phase1)
        {
            EnterPhase(QuestPhase.Phase1);
            SetupPhase1(!loadedFromSave);
            yield return new WaitUntil(() => absorbCounter >= phase1AbsorbTarget);

            yield return TransitionToNextPhase();
        }

        if (loadedStartPhase == QuestPhase.Phase2 || loadedStartPhase == QuestPhase.Phase1)
        {
            EnterPhase(QuestPhase.Phase2);
            SetupPhase2(!(loadedFromSave && loadedStartPhase == QuestPhase.Phase2));
            yield return new WaitUntil(() => perfectParryCounter >= phase2PerfectParryTarget);

            yield return TransitionToNextPhase();
        }

        if (loadedStartPhase == QuestPhase.Phase3 || loadedStartPhase == QuestPhase.Phase2 || loadedStartPhase == QuestPhase.Phase1)
        {
            EnterPhase(QuestPhase.Phase3);
            SetupPhase3(!(loadedFromSave && loadedStartPhase == QuestPhase.Phase3));
            yield return new WaitUntil(() => energyFullCounter >= phase3EnergyFullTarget && executeCounter >= phase3ExecuteTarget);

            yield return TransitionToNextPhase();
        }

        EnterPhase(QuestPhase.Phase4);
        SetupPhase4();
        yield return new WaitUntil(() => !AnyActiveEntityAlive());

        EnterPhase(QuestPhase.Completed);
        OnFlowCompleted?.Invoke();
        if (enableAutoSave && clearSaveOnFlowCompleted)
        {
            ClearProgressSave();
        }
        flowRunning = false;
    }

    private void SetupPhase1(bool resetCounter)
    {
        if (resetCounter)
        {
            absorbCounter = 0;
        }

        perfectParryCounter = 0;
        energyFullCounter = 0;
        executeCounter = 0;
        absorbCounter = Mathf.Clamp(absorbCounter, 0, phase1AbsorbTarget);

        if (playerCombatReceiver != null)
        {
            // Phase2 只训练弹刀：关闭其它充能路径，避免目标干扰。
            playerCombatReceiver.ConfigureTutorialRules(canGainAbsorbEnergy: false, canGainDodgeEnergy: false);
        }

        SpawnForCurrentPhase(phase1EnemyPrefabs, phase1SpawnRoot);
        ApplyPhaseLight(1);
        EmitCurrentPhaseProgress();
        SaveProgress();
        Log("Phase1 开始：同色吸收教学（目标 3 次），关闭闪避充能奖励。");
    }

    private void SetupPhase2(bool resetCounter)
    {
        if (resetCounter)
        {
            perfectParryCounter = 0;
        }

        perfectParryCounter = Mathf.Clamp(perfectParryCounter, 0, phase2PerfectParryTarget);

        if (playerCombatReceiver != null)
        {
            playerCombatReceiver.ConfigureTutorialRules(canGainAbsorbEnergy: true, canGainDodgeEnergy: false);
        }

        SpawnForCurrentPhase(phase2EnemyPrefabs, phase2SpawnRoot);
        ApplyPhaseLight(2);
        EmitCurrentPhaseProgress();
        SaveProgress();
        Log("Phase2 开始：仅判定弹刀成功次数（目标 2 次），前摇前自动充能用于弹刀训练。");
    }

    private void SetupPhase3(bool resetCounter)
    {
        if (resetCounter)
        {
            energyFullCounter = 0;
            executeCounter = 0;
        }

        energyFullCounter = Mathf.Clamp(energyFullCounter, 0, phase3EnergyFullTarget);
        executeCounter = Mathf.Clamp(executeCounter, 0, phase3ExecuteTarget);

        if (playerCombatReceiver != null)
        {
            playerCombatReceiver.ConfigureTutorialRules(canGainAbsorbEnergy: true, canGainDodgeEnergy: true);
        }

        SpawnForCurrentPhase(phase3EnemyPrefabs, phase3SpawnRoot);
        ApplyPhaseLight(3);
        EmitCurrentPhaseProgress();
        SaveProgress();
        Log("Phase3 开始：开启完整积攒机制（目标：能量回满后，通过 F 处决敌人进入决战）。");
    }

    private void SetupPhase4()
    {
        CleanupDestroyedEntries();
        ApplyPhaseLight(4);
        SaveProgress();

        if (phase4BossPrefab != null)
        {
            Transform spawnRoot = phase4SpawnRoot != null ? phase4SpawnRoot : transform;
            GameObject boss = Instantiate(phase4BossPrefab, spawnRoot.position, spawnRoot.rotation);
            boss.SetActive(true);
            activePhaseEntities.Add(boss);
            Log("Phase4 开始：Boss 登场，灯光切换为血红。\n");
            return;
        }

        if (useSpawnRootChildrenAsTemplateFallback && TrySpawnFromRootChildren(phase4SpawnRoot, "Phase4-Boss"))
        {
            Log("Phase4 开始：未配置 Boss Prefab，已使用 Phase4 SpawnRoot 子物体模板刷出。\n");
            return;
        }

        Log("Phase4 开始：未配置 Boss 预制体，且 SpawnRoot 下无可用模板，当前阶段将无法刷怪。");
    }

    private IEnumerator TransitionToNextPhase()
    {
        CleanupDestroyedEntries();

        if (arenaTransitionManager != null)
        {
            yield return arenaTransitionManager.PlayArenaLiftTransition();
        }

        CleanupActiveEntitiesImmediate();
        if (phaseTransitionDelay > 0f)
        {
            yield return new WaitForSeconds(phaseTransitionDelay);
        }
    }

    private void EnterPhase(QuestPhase phase)
    {
        CurrentPhase = phase;
        OnPhaseChanged?.Invoke(phase);
        SaveProgress();
        Log($"进入阶段: {phase}");
    }

    private void HandleSamePolarityAbsorbed()
    {
        if (CurrentPhase != QuestPhase.Phase1)
        {
            return;
        }

        absorbCounter++;
        EmitCurrentPhaseProgress();
        SaveProgress();
        Log($"Phase1 吸收计数: {absorbCounter}/{phase1AbsorbTarget}");
    }

    private void HandlePerfectParrySucceeded()
    {
        if (CurrentPhase != QuestPhase.Phase2)
        {
            return;
        }

        perfectParryCounter++;
        EmitCurrentPhaseProgress();
        SaveProgress();
        Log($"Phase2 完美弹刀计数: {perfectParryCounter}/{phase2PerfectParryTarget}");
    }

    private void HandleEnergyFilled()
    {
        if (CurrentPhase != QuestPhase.Phase3)
        {
            return;
        }

        energyFullCounter++;
        EmitCurrentPhaseProgress();
        SaveProgress();
        Log($"Phase3 满能计数: {energyFullCounter}/{phase3EnergyFullTarget}");
    }

    private void HandleEnemyExecuted()
    {
        if (CurrentPhase != QuestPhase.Phase3)
        {
            return;
        }

        executeCounter = Mathf.Min(executeCounter + 1, phase3ExecuteTarget);
        SaveProgress();
        Log($"Phase3 处决计数: {executeCounter}/{phase3ExecuteTarget}");
    }

    public void ClearProgressSave()
    {
        PlayerPrefs.DeleteKey(SaveValidKey);
        PlayerPrefs.DeleteKey(SavePhaseKey);
        PlayerPrefs.DeleteKey(SaveAbsorbKey);
        PlayerPrefs.DeleteKey(SaveParryKey);
        PlayerPrefs.DeleteKey(SaveEnergyKey);
        PlayerPrefs.DeleteKey(SaveExecuteKey);
        PlayerPrefs.Save();
    }

    private void SaveProgress()
    {
        if (!enableAutoSave)
        {
            return;
        }

        PlayerPrefs.SetInt(SaveValidKey, 1);
        PlayerPrefs.SetInt(SavePhaseKey, (int)CurrentPhase);
        PlayerPrefs.SetInt(SaveAbsorbKey, absorbCounter);
        PlayerPrefs.SetInt(SaveParryKey, perfectParryCounter);
        PlayerPrefs.SetInt(SaveEnergyKey, energyFullCounter);
        PlayerPrefs.SetInt(SaveExecuteKey, executeCounter);
        PlayerPrefs.Save();
    }

    private void LoadProgressIfNeeded()
    {
        loadedFromSave = false;
        loadedStartPhase = QuestPhase.Phase1;

        if (!enableAutoSave)
        {
            return;
        }

        if (PlayerPrefs.GetInt(SaveValidKey, 0) != 1)
        {
            return;
        }

        int phaseRaw = PlayerPrefs.GetInt(SavePhaseKey, (int)QuestPhase.Phase1);
        QuestPhase parsed = (QuestPhase)Mathf.Clamp(phaseRaw, (int)QuestPhase.Phase1, (int)QuestPhase.Completed);
        loadedStartPhase = parsed;
        loadedFromSave = true;

        absorbCounter = Mathf.Clamp(PlayerPrefs.GetInt(SaveAbsorbKey, 0), 0, phase1AbsorbTarget);
        perfectParryCounter = Mathf.Clamp(PlayerPrefs.GetInt(SaveParryKey, 0), 0, phase2PerfectParryTarget);
        energyFullCounter = Mathf.Clamp(PlayerPrefs.GetInt(SaveEnergyKey, 0), 0, phase3EnergyFullTarget);
        executeCounter = Mathf.Clamp(PlayerPrefs.GetInt(SaveExecuteKey, 0), 0, phase3ExecuteTarget);

        Log($"读取自动存档：Phase={loadedStartPhase} / P1={absorbCounter} / P2={perfectParryCounter} / P3能量={energyFullCounter} / P3处决={executeCounter}");
    }

    private void EmitCurrentPhaseProgress()
    {
        switch (CurrentPhase)
        {
            case QuestPhase.Phase1:
                OnPhaseProgressChanged?.Invoke(CurrentPhase, absorbCounter, phase1AbsorbTarget);
                break;
            case QuestPhase.Phase2:
                OnPhaseProgressChanged?.Invoke(CurrentPhase, perfectParryCounter, phase2PerfectParryTarget);
                break;
            case QuestPhase.Phase3:
                OnPhaseProgressChanged?.Invoke(CurrentPhase, energyFullCounter, phase3EnergyFullTarget);
                break;
        }
    }

    private void HandleEnemyTelegraphStarted(EnemyAttackBrain brain, Polarity polarity)
    {
        if (CurrentPhase != QuestPhase.Phase2)
        {
            return;
        }

        if (playerEnergySystem != null)
        {
            playerEnergySystem.CheatFillEnergyForTutorial();
        }
    }

    private void ApplyPhaseLight(int phaseIndex)
    {
        if (arenaTransitionManager == null)
        {
            return;
        }

        arenaTransitionManager.ApplyPhaseLight(phaseIndex);
    }

    private void SpawnForCurrentPhase(GameObject[] prefabs, Transform spawnRoot)
    {
        CleanupActiveEntitiesImmediate();

        Transform root = spawnRoot != null ? spawnRoot : transform;
        bool spawnedAny = false;

        if (prefabs != null && prefabs.Length > 0)
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null)
                {
                    continue;
                }

                Vector3 offset = new Vector3(i * 1.8f, 0f, 0f);
                GameObject instance = Instantiate(prefabs[i], root.position + offset, root.rotation);
                instance.SetActive(true);
                activePhaseEntities.Add(instance);
                spawnedAny = true;
            }
        }

        if (!spawnedAny && useSpawnRootChildrenAsTemplateFallback)
        {
            spawnedAny = TrySpawnFromRootChildren(spawnRoot, CurrentPhase.ToString());
        }

        if (!spawnedAny)
        {
            Debug.LogWarning($"[QuestFlow] {CurrentPhase} 未刷出敌人：请检查 Prefab 数组或在 SpawnRoot 下放置(可禁用)敌人模板子物体。", this);
        }
    }

    private bool TrySpawnFromRootChildren(Transform spawnRoot, string debugPhase)
    {
        if (spawnRoot == null)
        {
            return false;
        }

        bool spawned = false;
        int spawnedIndex = 0;
        for (int i = 0; i < spawnRoot.childCount; i++)
        {
            Transform child = spawnRoot.GetChild(i);
            if (child == null || !IsEnemyTemplateCandidate(child.gameObject))
            {
                continue;
            }

            Vector3 offset = new Vector3(spawnedIndex * 1.8f, 0f, 0f);
            GameObject instance = Instantiate(child.gameObject, spawnRoot.position + offset, spawnRoot.rotation);
            instance.name = child.gameObject.name.Replace("(Clone)", string.Empty);
            instance.SetActive(true);
            activePhaseEntities.Add(instance);
            spawned = true;
            spawnedIndex++;
        }

        if (spawned)
        {
            Log($"{debugPhase} 使用 SpawnRoot 子物体模板刷怪成功，共 {spawnedIndex} 个。");
        }

        return spawned;
    }

    private static bool IsEnemyTemplateCandidate(GameObject go)
    {
        if (go == null)
        {
            return false;
        }

        return go.GetComponentInChildren<EnemyAttackBrain>(true) != null ||
               go.GetComponentInChildren<EnemyPosture>(true) != null;
    }

    private bool AnyActiveEntityAlive()
    {
        CleanupDestroyedEntries();
        return activePhaseEntities.Count > 0;
    }

    private void CleanupDestroyedEntries()
    {
        for (int i = activePhaseEntities.Count - 1; i >= 0; i--)
        {
            if (activePhaseEntities[i] == null)
            {
                activePhaseEntities.RemoveAt(i);
            }
        }
    }

    private void CleanupActiveEntitiesImmediate()
    {
        for (int i = 0; i < activePhaseEntities.Count; i++)
        {
            if (activePhaseEntities[i] != null)
            {
                Destroy(activePhaseEntities[i]);
            }
        }

        activePhaseEntities.Clear();
    }

    private void Log(string msg)
    {
        if (!verboseLog)
        {
            return;
        }

        Debug.Log($"[QuestFlow] {msg}");
    }
}
