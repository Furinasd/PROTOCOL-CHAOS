using System.Collections;
using TMPro;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 顶部任务 UI：阶段文本打字机与充能爆闪反馈。
/// </summary>
public class QuestUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private QuestFlowManager questFlowManager;
    [SerializeField] private PlayerEnergySystem playerEnergySystem;
    [SerializeField] private TMP_Text questText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform textRoot;

    [Header("Typewriter")]
    [SerializeField] private float charInterval = 0.03f;
    [SerializeField] private float autoFadeDelay = 1.2f;

    [Header("Juice")]
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private float shakeStrength = 14f;

    private Coroutine typingRoutine;

    private void Awake()
    {
        if (questFlowManager == null)
        {
            questFlowManager = FindFirstObjectByType<QuestFlowManager>();
        }

        if (playerEnergySystem == null)
        {
            playerEnergySystem = FindFirstObjectByType<PlayerEnergySystem>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponentInChildren<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        if (questFlowManager != null)
        {
            questFlowManager.OnPhaseChanged += HandlePhaseChanged;
            questFlowManager.OnPhaseProgressChanged += HandlePhaseProgressChanged;
        }

        if (playerEnergySystem != null)
        {
            playerEnergySystem.OnTutorialCheatFilled += HandleTutorialCheatFilled;
            playerEnergySystem.OnEnergyFilled += HandleEnergyFilled;
        }
    }

    private void OnDisable()
    {
        if (questFlowManager != null)
        {
            questFlowManager.OnPhaseChanged -= HandlePhaseChanged;
            questFlowManager.OnPhaseProgressChanged -= HandlePhaseProgressChanged;
        }

        if (playerEnergySystem != null)
        {
            playerEnergySystem.OnTutorialCheatFilled -= HandleTutorialCheatFilled;
            playerEnergySystem.OnEnergyFilled -= HandleEnergyFilled;
        }
    }

    private void HandlePhaseChanged(QuestFlowManager.QuestPhase phase)
    {
        string text = BuildPhaseText(phase, 0, 0);

        if (!string.IsNullOrEmpty(text))
        {
            PlayTypewriter(text);
        }
    }

    private void HandlePhaseProgressChanged(QuestFlowManager.QuestPhase phase, int current, int target)
    {
        if (target <= 0 || current <= 0)
        {
            return;
        }

        string text = BuildPhaseText(phase, current, target);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (questText != null)
        {
            questText.text = text;
        }

        TriggerChargeJuice();
    }

    private string BuildPhaseText(QuestFlowManager.QuestPhase phase, int current, int target)
    {
        return phase switch
        {
            // 【Phase 1】：协议初始化 - 教导玩家极性切换和吸收机制
            QuestFlowManager.QuestPhase.Phase1 => 
                $"【协议初始化】\n" +
                $"检测到环境混沌。请通过【鼠标右键】切换极性，\n" +
                $"以吸收同频能量。\n" +
                $"进度：({current}/{Mathf.Max(1, target)})",

            // 【Phase 2】：熵减测试 - 教导玩家完美弹刀
            QuestFlowManager.QuestPhase.Phase2 => 
                $"【熵减测试】\n" +
                $"遭遇高维畸变体。请在攻击瞬间【鼠标左键】触发，\n" +
                $"通过【异色湮灭】击碎敌方外壳。\n" +
                $"进度：({current}/{Mathf.Max(1, target)})",

            // 【Phase 3】：能量循环 - 教导玩家积累和反击
            QuestFlowManager.QuestPhase.Phase3 => 
                $"【能量循环】\n" +
                $"实战测试开启。使用【极限闪避】或【滑行】获取能量，\n" +
                $"累积 3 格能量后通过【异色湮灭】反击。\n" +
                $"进度：({current}/{Mathf.Max(1, target)})",

            // 【Phase 4】：绝对失序 - Boss 战
            QuestFlowManager.QuestPhase.Phase4 => 
                "【绝对失序】\n" +
                "不稳定变量已过载。\n" +
                "歼灭【终极混沌核心】！",

            // 完成状态
            QuestFlowManager.QuestPhase.Completed => 
                "【秩序已重建】\n" +
                "训练闭环完成。\n" +
                "你已掌握秩序之力。",

            _ => string.Empty
        };
    }

    private void HandleTutorialCheatFilled()
    {
        TriggerChargeJuice();
    }

    private void HandleEnergyFilled()
    {
        TriggerChargeJuice();
    }

    private void PlayTypewriter(string msg)
    {
        if (questText == null)
        {
            return;
        }

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
        }

        typingRoutine = StartCoroutine(TypewriterRoutine(msg));
    }

    private IEnumerator TypewriterRoutine(string msg)
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOFade(1f, 0.12f).SetUpdate(true);
        }

        questText.text = string.Empty;
        for (int i = 0; i < msg.Length; i++)
        {
            questText.text += msg[i];
            yield return new WaitForSecondsRealtime(charInterval);
        }

        if (canvasGroup != null)
        {
            yield return new WaitForSecondsRealtime(autoFadeDelay);
            canvasGroup.DOFade(0.65f, 0.25f).SetUpdate(true);
        }
    }

    private void TriggerChargeJuice()
    {
        if (textRoot != null)
        {
            textRoot.DOComplete();
            textRoot.DOShakeAnchorPos(shakeDuration, shakeStrength, 18).SetUpdate(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.DOFade(0.75f, 0.2f).SetUpdate(true);
        }
    }
}
