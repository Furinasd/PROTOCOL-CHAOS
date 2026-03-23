using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using DG.Tweening;
using UnityEngine.InputSystem;

/// <summary>
/// 管理游戏结束 UI 表现与场景重启逻辑
/// </summary>
public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("UI References")]
    public CanvasGroup gameOverCanvasGroup;
    public Button restartButton;

    [Header("Fade Settings")]
    public float fadeDuration = 1.0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = 0;
            gameOverCanvasGroup.interactable = false;
            gameOverCanvasGroup.blocksRaycasts = false;
            // 初始缩放设为 0.8，用于后续弹跳展开感
            gameOverCanvasGroup.transform.localScale = Vector3.one * 0.8f;
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }
    }

    private void Update()
    {
        // 【死亡后快捷重启】：允许玩家在 UI 显示期间按 R 键快速开局
        if (gameOverCanvasGroup != null && gameOverCanvasGroup.alpha > 0.5f)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                RestartGame();
            }
        }
    }

    private void Start()
    {
        // 自动寻找玩家并尝试订阅（虽然现在是直接调用，但保留该逻辑作为多渠道触发的保障）
        PlayerCombatReceiver player = FindFirstObjectByType<PlayerCombatReceiver>();
        if (player != null)
        {
            Debug.Log("[GameOverManager] Successfully found PlayerCombatReceiver.");
        }
    }

    /// <summary>
    /// 触发游戏结束序列
    /// </summary>
    public void TriggerGameOver()
    {
        // 检查是否已经触发
        if (gameOverCanvasGroup != null && gameOverCanvasGroup.alpha > 0.5f) return;
        
        StopAllCoroutines();
        StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        Debug.Log("<color=red>[GameOver] 秩序坍缩中... 正在展开结局界面。</color>");

        if (gameOverCanvasGroup != null)
        {
            // 使用 DOTween 制作更具仪式感的展开：Alpha 渐变 + 弹跳缩放
            gameOverCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
            gameOverCanvasGroup.transform.DOScale(1f, fadeDuration).SetEase(Ease.OutBack).SetUpdate(true);
            
            yield return new WaitForSecondsRealtime(fadeDuration);
            
            // 【新规：重开体验】确保鼠标可见并解锁，方便玩家点击按钮
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            gameOverCanvasGroup.interactable = true;
            gameOverCanvasGroup.blocksRaycasts = true;
            Debug.Log("<color=yellow>[GameOver] UI 已就绪。按 R 或点击按钮重启。</color>");
        }
    }

    public void RestartGame()
    {
        Debug.Log("[GameOver] Restarting Scene...");
        // 彻底恢复状态，防止新场景继承死亡时的锁定状态
        Time.timeScale = 1.0f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
