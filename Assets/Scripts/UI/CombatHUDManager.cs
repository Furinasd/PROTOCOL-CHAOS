using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CombatHUDManager : MonoBehaviour
{
    private static CombatHUDManager _instance;
    public static CombatHUDManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<CombatHUDManager>();
            return _instance;
        }
    }

    [Header("玩家组件")]
    public Image playerHPFill;
    public Image playerHPEaseFill;
    public RectTransform playerHPContainer;

    [Header("Boss 组件")]
    public GameObject bossHUDParent;
    public Image bossHPFill;
    public Image bossHPEaseFill;

    private PlayerCombatReceiver playerReceiver;
    private EnemyPosture bossPosture;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        RefreshReferences();
        if (bossHUDParent != null) bossHUDParent.SetActive(false); // 初始隐藏，直到发现 Boss
    }

    public void RefreshReferences()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerReceiver = playerObj.GetComponent<PlayerCombatReceiver>();

        GameObject bossObj = GameObject.FindGameObjectWithTag("Enemy");
        if (bossObj != null) bossPosture = bossObj.GetComponent<EnemyPosture>();
        
        if (bossHUDParent != null) bossHUDParent.SetActive(bossObj != null);
    }

    private void Update()
    {
        // 如果引用丢失（如场景重载），尝试重新获取
        if (playerReceiver == null) RefreshReferences();

        UpdatePlayerHUD();
        UpdateBossHUD();
    }

    private void UpdatePlayerHUD()
    {
        if (playerReceiver == null || playerHPFill == null) return;

        float target = Mathf.Clamp01(playerReceiver.currentHP / playerReceiver.maxHP);
        playerHPFill.fillAmount = target;

        if (playerHPEaseFill != null && playerHPEaseFill.fillAmount > target)
        {
            playerHPEaseFill.fillAmount = Mathf.Lerp(playerHPEaseFill.fillAmount, target, Time.deltaTime * 5f);
        }
    }

    private void UpdateBossHUD()
    {
        if (bossPosture == null || bossHPFill == null) return;

        float target = Mathf.Clamp01(bossPosture.currentHP / bossPosture.maxHP);
        bossHPFill.fillAmount = target;

        if (bossHPEaseFill != null && bossHPEaseFill.fillAmount > target)
        {
            bossHPEaseFill.fillAmount = Mathf.Lerp(bossHPEaseFill.fillAmount, target, Time.deltaTime * 3f);
        }
    }

    public void TriggerGlitchEffect(bool isPlayer)
    {
        RectTransform target = isPlayer ? playerHPContainer : (RectTransform)bossHUDParent.transform;
        if (target != null)
        {
            target.DOShakeAnchorPos(0.2f, 8f, 25).SetUpdate(true);
        }
    }
}
