using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

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
        else Destroy(gameObject);

        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = 0;
            gameOverCanvasGroup.interactable = false;
            gameOverCanvasGroup.blocksRaycasts = false;
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
            // 初始隐藏按钮交互（通过 CanvasGroup 控制）
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
        Debug.Log("[GameOver] Starting UI Sequence...");

        if (gameOverCanvasGroup != null)
        {
            float elapsed = 0;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                gameOverCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            
            gameOverCanvasGroup.alpha = 1;
            gameOverCanvasGroup.interactable = true;
            gameOverCanvasGroup.blocksRaycasts = true;
        }

        Debug.Log("[GameOver] UI Ready for Restart.");
    }

    public void RestartGame()
    {
        Debug.Log("[GameOver] Restarting Scene...");
        // 恢复时间缩放（防止之前有慢动作效果）
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
