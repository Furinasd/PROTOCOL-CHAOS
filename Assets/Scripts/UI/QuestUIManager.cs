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
    [SerializeField] private TMP_FontAsset chineseFontAsset;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform textRoot;

    [Header("Typewriter")]
    [SerializeField] private float charInterval = 0.03f;
    [SerializeField] private float autoFadeDelay = 1.2f;

    [Header("Juice")]
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private float shakeStrength = 14f;

    private Coroutine typingRoutine;

    /// <summary>
    /// [编辑器工具] 将层级锚点一键修复为“全撑满”，确保所见即所得。
    /// 右键点击组件标题即可看到此选项。
    /// </summary>
    [ContextMenu("Fix Layout Hierarchy")]
    public void FixLayoutHierarchy()
    {
        if (textRoot != null)
        {
            textRoot.anchorMin = Vector2.zero; // (0, 0)
            textRoot.anchorMax = Vector2.one;  // (1, 1)
            textRoot.pivot = Vector2.up;      // (0, 1)
            textRoot.offsetMin = Vector2.zero;
            textRoot.offsetMax = Vector2.zero;
        }

        if (questText != null)
        {
            // 确保文本组件始终左上对齐
            questText.alignment = TextAlignmentOptions.TopLeft;

            // 强制文本 RectTransform 填满容器
            RectTransform tRect = questText.rectTransform;
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.pivot = Vector2.up;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;
        }

        Debug.Log("[QuestUI] 层级锚点已修复。现在你可以自由调整 QuestUIManager 的框体大小，文字将自动填充并左对齐。", this);
    }

    private void Awake()
    {
        if (questFlowManager == null)
            questFlowManager = FindFirstObjectByType<QuestFlowManager>();

        if (playerEnergySystem == null)
            playerEnergySystem = FindFirstObjectByType<PlayerEnergySystem>();

        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>();

        // 仅处理字体和核心对齐，不再触碰位置和宽度
        EnsureQuestFontReady();
    }

    private void EnsureQuestFontReady()
    {
        if (questText == null)
        {
            return;
        }

        TMP_FontAsset resolved = ResolveChineseFontAsset();
        if (resolved == null)
        {
            Debug.LogWarning("[QuestUI] 未找到可用中文 TMP 字体，请在 QuestUIManager.chineseFontAsset 手动指定字体。", this);
            return;
        }

        if (questText.font != resolved)
        {
            questText.font = resolved;
        }

        questText.alignment = TextAlignmentOptions.TopLeft; // 强制左上对齐
        questText.isRightToLeftText = false;
        questText.textWrappingMode = TextWrappingModes.Normal;
        questText.overflowMode = TextOverflowModes.Overflow;
    }

    private TMP_FontAsset ResolveChineseFontAsset()
    {
        if (chineseFontAsset != null)
        {
            return chineseFontAsset;
        }

        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loadedFonts.Length; i++)
        {
            TMP_FontAsset font = loadedFonts[i];
            if (font == null)
            {
                continue;
            }

            string lowerName = font.name.ToLowerInvariant();
            if (lowerName.Contains("misans") || lowerName.Contains("sourcehan") || lowerName.Contains("noto"))
            {
                chineseFontAsset = font;
                return chineseFontAsset;
            }
        }

        if (TMP_Settings.defaultFontAsset != null)
        {
            return TMP_Settings.defaultFontAsset;
        }

        return null;
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
                $"检测到环境混沌。通过【鼠标右键】切换极性至[同色]以吸收秩序能量。" +
                $"进度：({current}/{Mathf.Max(1, target)})",

            // 【Phase 2】：熵减测试 - 教导玩家完美弹刀
            QuestFlowManager.QuestPhase.Phase2 =>
                $"【熵减测试】\n" +
                $"遭遇高维畸变体。在攻击瞬间切换[异色]使用【鼠标左键】触发[极性湮灭]\n" +
                $"通过[极性湮灭]造成伤害!\n" +
                $"进度：({current}/{Mathf.Max(1, target)})",

            // 【Phase 3】：能量循环 - 教导玩家积累和反击
            QuestFlowManager.QuestPhase.Phase3 =>
                $"【能量循环】\n" +
                $"实战测试开启。使用[Shift]或[Space]触发【极限闪避】获取能量，\n" +
                $"累积 3 格能量后通过[极性湮灭]反击，压低敌方躯干后按[F]处决!\n" +
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
                "你已掌握秩序之力,力量与你同在!",

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
