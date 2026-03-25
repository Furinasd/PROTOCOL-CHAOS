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
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CombatHUDManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("CombatHUDManager_Auto");
                    _instance = go.AddComponent<CombatHUDManager>();
                }
            }
            return _instance;
        }
    }

    [Header("玩家组件")]
    public Image playerHPFill;
    public Image playerHPEaseFill;
    public RectTransform playerHPContainer;

    [Header("玩家血条布局")]
    public bool lockPlayerHudTopLeft = true;
    public Vector2 playerHudTopLeftOffset = new Vector2(170f, -70f);
    public Vector2 playerHudSize = new Vector2(220f, 24f);

    [Header("Boss 组件")]
    public GameObject bossHUDParent;
    public Image bossHPFill;
    public Image bossHPEaseFill;
    public Image bossPostureFill;
    public Image bossPostureEaseFill;

    private PlayerCombatReceiver playerReceiver;
    private EnemyPosture bossPosture;
    private PlayerCombatReceiver subscribedPlayerReceiver;
    private EnemyPosture subscribedBossPosture;
    private bool autoHudBuilt;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this) Destroy(gameObject);

        EnsureCanvasScaleValid();
        EnsureHudBindings();
    }

    private void Start()
    {
        RefreshReferences();
        if (bossHUDParent != null) bossHUDParent.SetActive(bossPosture != null); // 初始隐藏，直到发现 Boss
    }

    public void RefreshReferences()
    {
        UnbindHUDEvents();

        playerReceiver = FindFirstObjectByType<PlayerCombatReceiver>();
        if (playerReceiver == null)
        {
            GameObject playerObj = TryFindByTag("Player");
            if (playerObj != null) playerReceiver = playerObj.GetComponent<PlayerCombatReceiver>();
        }

        EnemyPosture[] allEnemies = FindObjectsByType<EnemyPosture>(FindObjectsSortMode.None);
        bossPosture = System.Array.Find(allEnemies, e => e.isBoss);

        if (bossHUDParent != null) bossHUDParent.SetActive(bossPosture != null);

        BindHUDEvents();

        EnsureHudBindings();
    }

    private void Update()
    {
        // 如果引用丢失（如场景重载），尝试重新获取
        if (playerReceiver == null) RefreshReferences();
        if (bossPosture == null && Time.frameCount % 10 == 0) RefreshReferences(); // 每10帧检查Boss

        EnsureCanvasScaleValid();
        EnsureHudBindings();
        EnforcePlayerHudTopLeft();

        UpdatePlayerHUD();
        UpdateBossHUD();
    }

    private void UpdatePlayerHUD()
    {
        if (playerReceiver == null || playerHPFill == null) return;

        float target = Mathf.Clamp01(playerReceiver.currentHP / Mathf.Max(1f, playerReceiver.maxHP));
        playerHPFill.fillAmount = target;

        if (playerHPEaseFill != null)
        {
            if (playerHPEaseFill.fillAmount > target)
            {
                playerHPEaseFill.fillAmount = Mathf.Lerp(playerHPEaseFill.fillAmount, target, Time.unscaledDeltaTime * 5f);
            }
            else if (Mathf.Abs(playerHPEaseFill.fillAmount - target) > 0.001f)
            {
                // 上升更快
                playerHPEaseFill.fillAmount = Mathf.Lerp(playerHPEaseFill.fillAmount, target, Time.unscaledDeltaTime * 8f);
            }
        }
    }

    private void UpdateBossHUD()
    {
        if (bossPosture == null || bossHPFill == null) 
        {
            if (bossHUDParent != null && bossHUDParent.activeSelf) 
            {
                bossHUDParent.SetActive(false);
            }
            return;
        }

        if (bossHUDParent != null && !bossHUDParent.activeSelf && bossPosture.currentHP > 0)
        {
            bossHUDParent.SetActive(true);
        }

        if (bossPosture.currentHP <= 0 && bossHUDParent != null && bossHUDParent.activeSelf)
        {
            bossHUDParent.SetActive(false);
            return;
        }

        float target = Mathf.Clamp01(bossPosture.currentHP / Mathf.Max(1f, bossPosture.maxHP));
        bossHPFill.fillAmount = target;

        if (bossHPEaseFill != null)
        {
            if (bossHPEaseFill.fillAmount > target)
            {
                bossHPEaseFill.fillAmount = Mathf.Lerp(bossHPEaseFill.fillAmount, target, Time.unscaledDeltaTime * 3f);
            }
            else if (Mathf.Abs(bossHPEaseFill.fillAmount - target) > 0.001f)
            {
                bossHPEaseFill.fillAmount = Mathf.Lerp(bossHPEaseFill.fillAmount, target, Time.unscaledDeltaTime * 6f);
            }
        }

        if (bossPostureFill != null)
        {
            float postureTarget = bossPosture.PosturePercentage;
            bossPostureFill.fillAmount = postureTarget;

            if (bossPostureEaseFill != null)
            {
                if (bossPostureEaseFill.fillAmount > postureTarget)
                {
                    bossPostureEaseFill.fillAmount = Mathf.Lerp(bossPostureEaseFill.fillAmount, postureTarget, Time.unscaledDeltaTime * 5f);
                }
                else if (Mathf.Abs(bossPostureEaseFill.fillAmount - postureTarget) > 0.001f)
                {
                    bossPostureEaseFill.fillAmount = Mathf.Lerp(bossPostureEaseFill.fillAmount, postureTarget, Time.unscaledDeltaTime * 8f);
                }
            }
        }
    }

    public void ForceRefreshBossUI()
    {
        if (bossPosture == null) RefreshReferences();
        if (bossPosture == null) return;

        if (bossHPFill != null)
            bossHPFill.fillAmount = Mathf.Clamp01(bossPosture.currentHP / bossPosture.maxHP);

        if (bossPostureFill != null)
            bossPostureFill.fillAmount = bossPosture.PosturePercentage;
    }

    private void BindHUDEvents()
    {
        if (playerReceiver != null && playerReceiver != subscribedPlayerReceiver)
        {
            if (subscribedPlayerReceiver != null)
            {
                subscribedPlayerReceiver.OnHealthChanged -= HandlePlayerHealthChanged;
            }

            subscribedPlayerReceiver = playerReceiver;
            subscribedPlayerReceiver.OnHealthChanged += HandlePlayerHealthChanged;
            HandlePlayerHealthChanged(subscribedPlayerReceiver.currentHP, subscribedPlayerReceiver.maxHP);
        }

        if (bossPosture != null && bossPosture != subscribedBossPosture)
        {
            if (subscribedBossPosture != null)
            {
                subscribedBossPosture.OnHealthChanged -= HandleBossStatsChanged;
                subscribedBossPosture.OnPostureChanged -= HandleBossStatsChanged;
            }

            subscribedBossPosture = bossPosture;
            subscribedBossPosture.OnHealthChanged += HandleBossStatsChanged;
            subscribedBossPosture.OnPostureChanged += HandleBossStatsChanged;
            HandleBossStatsChanged(subscribedBossPosture.currentHP, subscribedBossPosture.maxHP);
        }
    }

    private void UnbindHUDEvents()
    {
        if (subscribedPlayerReceiver != null)
        {
            subscribedPlayerReceiver.OnHealthChanged -= HandlePlayerHealthChanged;
        }

        if (subscribedBossPosture != null)
        {
            subscribedBossPosture.OnHealthChanged -= HandleBossStatsChanged;
            subscribedBossPosture.OnPostureChanged -= HandleBossStatsChanged;
        }
    }

    private void HandlePlayerHealthChanged(float current, float max)
    {
        float target = Mathf.Clamp01(current / Mathf.Max(1f, max));

        if (playerHPFill != null)
            playerHPFill.fillAmount = target;

        if (playerHPEaseFill != null && playerHPEaseFill.fillAmount > target)
            playerHPEaseFill.fillAmount = Mathf.Lerp(playerHPEaseFill.fillAmount, target, Time.unscaledDeltaTime * 5f);
    }

    private void HandleBossStatsChanged(float current, float max)
    {
        if (bossPosture == null)
            return;

        if (bossHUDParent != null)
            bossHUDParent.SetActive(current > 0f);

        if (bossHPFill != null)
            bossHPFill.fillAmount = Mathf.Clamp01(current / Mathf.Max(1f, max));

        if (bossPostureFill != null)
            bossPostureFill.fillAmount = bossPosture.PosturePercentage;
    }

    public void TriggerGlitchEffect(bool isPlayer)
    {
        RectTransform target = isPlayer ? playerHPContainer : (RectTransform)bossHUDParent.transform;
        if (target != null)
        {
            target.DOShakeAnchorPos(0.2f, 8f, 25).SetUpdate(true);
        }
    }

    private void EnsureCanvasScaleValid()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c == null) continue;
            if (c.renderMode != RenderMode.ScreenSpaceOverlay && c.renderMode != RenderMode.ScreenSpaceCamera) continue;

            Vector3 s = c.transform.localScale;
            if (Mathf.Abs(s.x) < 0.001f || Mathf.Abs(s.y) < 0.001f || Mathf.Abs(s.z) < 0.001f)
            {
                c.transform.localScale = Vector3.one;
            }
        }
    }

    private void EnsureHudBindings()
    {
        if (playerHPFill != null && playerHPContainer != null && bossHPFill != null && bossPostureFill != null)
            return;

        if (!autoHudBuilt)
        {
            if (playerHPFill == null && bossHPFill == null && bossPostureFill == null)
            {
                Debug.Log("[CombatHUDManager] All HUD bindings empty, building Auto Fallback HUD.");
                BuildFallbackHUD();
            }
            else
            {
                Debug.LogError("[CombatHUDManager] HUD bindings are incorrectly assigned! Some fields are assigned while others are null! Please assign ALL fields in CombatHUDManager inspector. (PlayerHPFill, BossHPFill, BossPostureFill). UI will not update properly until you fix this!");
            }
            autoHudBuilt = true;
        }
    }

    private void BuildFallbackHUD()
    {
        GameObject canvasGo = new GameObject("CombatHUD_Auto", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        RectTransform root = canvasGo.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        playerHPContainer = CreatePanel("PlayerHP_Container", root, new Vector2(0f, 1f), new Vector2(0f, 1f), playerHudSize, playerHudTopLeftOffset);
        CreateBarSet(playerHPContainer, "PlayerHP", out playerHPFill, out playerHPEaseFill, new Color(0.9f, 0.2f, 0.2f, 1f));

        RectTransform bossContainer = CreatePanel("BossHUD_Container", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(420f, 56f), new Vector2(0f, -56f));
        bossHUDParent = bossContainer.gameObject;
        CreateBarSet(bossContainer, "BossHP", out bossHPFill, out bossHPEaseFill, new Color(0.95f, 0.25f, 0.25f, 1f), new Vector2(0f, 10f), new Vector2(360f, 14f));
        CreateBarSet(bossContainer, "BossPosture", out bossPostureFill, out bossPostureEaseFill, new Color(0.3f, 0.8f, 1f, 1f), new Vector2(0f, -12f), new Vector2(360f, 10f));

        bossHUDParent.SetActive(false);
    }

    private void EnforcePlayerHudTopLeft()
    {
        if (!lockPlayerHudTopLeft || playerHPContainer == null) return;

        playerHPContainer.anchorMin = new Vector2(0f, 1f);
        playerHPContainer.anchorMax = new Vector2(0f, 1f);
        playerHPContainer.pivot = new Vector2(0.5f, 0.5f);
        playerHPContainer.anchoredPosition = playerHudTopLeftOffset;
        playerHPContainer.sizeDelta = playerHudSize;
    }

    private GameObject TryFindByTag(string tag)
    {
        try
        {
            return GameObject.FindGameObjectWithTag(tag);
        }
        catch (UnityException)
        {
            return null;
        }
    }

    private RectTransform CreatePanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return rt;
    }

    private void CreateBarSet(RectTransform parent, string prefix, out Image fill, out Image ease, Color fillColor)
    {
        CreateBarSet(parent, prefix, out fill, out ease, fillColor, Vector2.zero, new Vector2(200f, 12f));
    }

    private void CreateBarSet(RectTransform parent, string prefix, out Image fill, out Image ease, Color fillColor, Vector2 anchoredPos, Vector2 size)
    {
        RectTransform bg = CreateImageRect(prefix + "_BG", parent, new Color(0f, 0f, 0f, 0.55f), anchoredPos, size);
        ease = CreateFillRect(prefix + "_Ease", bg, new Color(1f, 0.85f, 0.4f, 1f));
        fill = CreateFillRect(prefix + "_Fill", bg, fillColor);
    }

    private RectTransform CreateImageRect(string name, RectTransform parent, Color color, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = color;
        return rt;
    }

    private Image CreateFillRect(string name, RectTransform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(2f, 2f);
        rt.offsetMax = new Vector2(-2f, -2f);

        Image image = go.GetComponent<Image>();
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = 0;
        image.fillAmount = 1f;
        image.color = color;
        return image;
    }
}
