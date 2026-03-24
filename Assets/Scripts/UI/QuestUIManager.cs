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
        }

        if (playerEnergySystem != null)
        {
            playerEnergySystem.OnTutorialCheatFilled -= HandleTutorialCheatFilled;
            playerEnergySystem.OnEnergyFilled -= HandleEnergyFilled;
        }
    }

    private void HandlePhaseChanged(QuestFlowManager.QuestPhase phase)
    {
        string text = phase switch
        {
            QuestFlowManager.QuestPhase.Phase1 => "Phase 1 - 同色吸收 3 次",
            QuestFlowManager.QuestPhase.Phase2 => "Phase 2 - 异色弹刀 2 次（能量真实消耗）",
            QuestFlowManager.QuestPhase.Phase3 => "Phase 3 - 开放完整秩序系统",
            QuestFlowManager.QuestPhase.Phase4 => "Phase 4 - 处决 Tutorial Boss",
            QuestFlowManager.QuestPhase.Completed => "Clear - 秩序已重建",
            _ => string.Empty
        };

        if (!string.IsNullOrEmpty(text))
        {
            PlayTypewriter(text);
        }
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
