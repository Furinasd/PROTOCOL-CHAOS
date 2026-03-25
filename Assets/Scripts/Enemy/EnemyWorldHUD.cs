using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

/// <summary>
/// Runtime enemy overhead UI with dual bars (HP + posture).
/// </summary>
[RequireComponent(typeof(EnemyPosture))]
public class EnemyWorldHUD : MonoBehaviour
{
    [Header("Anchor")]
    public Vector3 worldOffset = new Vector3(0f, 2.6f, 0f);

    [Header("Size")]
    public Vector2 rootSize = new Vector2(96f, 24f);
    public Vector2 barSize = new Vector2(86f, 7f);
    [Tooltip("World Space Canvas 的整体缩放，按场景比例调整。")]
    public float uiWorldScale = 0.01f;

    [Header("Smoothing")]
    public float hpLerpSpeed = 12f;
    public float postureLerpSpeed = 14f;

    [Header("Damage Popup")]
    public Vector2 damagePopupStart = new Vector2(0f, 18f);
    public float damagePopupRise = 20f;
    public float damagePopupDuration = 0.35f;
    public float damagePopupFontSize = 24f;
    public Color damagePopupColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("Colors")]
    public Color hpColor = new Color(0.95f, 0.2f, 0.2f, 1f);
    public Color postureColor = new Color(0.3f, 0.8f, 1f, 1f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);

    private EnemyPosture posture;
    private Camera mainCam;

    private Canvas canvas;
    private RectTransform root;
    private Image hpFill;
    private Image postureFill;
    private TMP_FontAsset popupFont;

    private float hpDisplayed = 1f;
    private float postureDisplayed = 0f;
    private float hpTarget = 1f;
    private float postureTarget = 0f;
    private float lastHpRaw;
    private float lastPostureRaw;
    private Tween pulseTween;

    private void Awake()
    {
        posture = GetComponent<EnemyPosture>();

        // 确保 posture 存在
        if (posture == null)
        {
            Debug.LogWarning($"[EnemyWorldHUD] {gameObject.name} 缺少 EnemyPosture 组件，销毁该HUD。");
            Destroy(this);
            return;
        }

        // Boss 敌人使用 CombatHUDManager 管理血条，不需要头顶HUD
        if (posture.isBoss)
        {
            Debug.Log($"[EnemyWorldHUD] {gameObject.name} 是Boss，使用中央血条管理，销毁头顶HUD。");
            Destroy(this);
            return;
        }

        mainCam = Camera.main;
        BuildUI();
        BindPostureEvents();
        ForceSyncNow();
        Debug.Log($"[EnemyWorldHUD] 为 {gameObject.name} 创建了头顶血条UI。");
    }

    private void OnDestroy()
    {
        UnbindPostureEvents();
    }

    private void LateUpdate()
    {
        if (posture == null || canvas == null) return;

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        Vector3 anchor = transform.position + worldOffset;
        root.position = anchor;
        root.forward = mainCam.transform.forward;

        hpDisplayed = Mathf.Lerp(hpDisplayed, hpTarget, Time.unscaledDeltaTime * hpLerpSpeed);
        postureDisplayed = Mathf.Lerp(postureDisplayed, postureTarget, Time.unscaledDeltaTime * postureLerpSpeed);

        hpFill.fillAmount = hpDisplayed;
        postureFill.fillAmount = postureDisplayed;

        canvas.enabled = posture.currentHP > 0f;
    }

    public void ForceSyncNow()
    {
        if (posture == null || hpFill == null || postureFill == null) return;

        hpDisplayed = posture.maxHP <= 0f ? 0f : Mathf.Clamp01(posture.currentHP / posture.maxHP);
        postureDisplayed = posture.PosturePercentage;
        hpTarget = hpDisplayed;
        postureTarget = postureDisplayed;
        lastHpRaw = posture.currentHP;
        lastPostureRaw = posture.currentPosture;
        hpFill.fillAmount = hpDisplayed;
        postureFill.fillAmount = postureDisplayed;
    }

    private void BindPostureEvents()
    {
        if (posture == null)
        {
            return;
        }

        posture.OnHealthChanged += HandleHealthChanged;
        posture.OnPostureChanged += HandlePostureChanged;
    }

    private void UnbindPostureEvents()
    {
        if (posture == null)
        {
            return;
        }

        posture.OnHealthChanged -= HandleHealthChanged;
        posture.OnPostureChanged -= HandlePostureChanged;
    }

    private void HandleHealthChanged(float current, float max)
    {
        hpTarget = Mathf.Clamp01(current / Mathf.Max(1f, max));

        float damage = Mathf.Max(0f, lastHpRaw - current);
        if (damage > 0.01f)
        {
            PulseBar(hpFill);
            SpawnDamagePopup(Mathf.RoundToInt(damage));
        }

        lastHpRaw = current;
    }

    private void HandlePostureChanged(float current, float max)
    {
        postureTarget = Mathf.Clamp01(current / Mathf.Max(1f, max));

        if (current > lastPostureRaw + 0.01f)
        {
            PulseBar(postureFill);
        }

        lastPostureRaw = current;
    }

    private void PulseBar(Image bar)
    {
        if (bar == null)
        {
            return;
        }

        RectTransform barRect = bar.rectTransform;
        barRect.DOKill();
        barRect.localScale = Vector3.one;
        barRect.DOPunchScale(new Vector3(0.18f, 0.18f, 0f), 0.2f, 12, 0.7f).SetUpdate(true);
    }

    private void SpawnDamagePopup(int damage)
    {
        if (root == null || damage <= 0)
        {
            return;
        }

        GameObject go = new GameObject("DamagePopup", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = damagePopupStart;
        rt.sizeDelta = new Vector2(120f, 36f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = $"-{damage}";
        tmp.fontSize = damagePopupFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = damagePopupColor;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        if (popupFont == null)
        {
            popupFont = TMP_Settings.defaultFontAsset;
        }
        if (popupFont != null)
        {
            tmp.font = popupFont;
        }

        Sequence seq = DOTween.Sequence();
        seq.Append(rt.DOAnchorPosY(damagePopupStart.y + damagePopupRise, damagePopupDuration).SetEase(Ease.OutQuad));
        seq.Join(tmp.DOFade(0f, damagePopupDuration).SetEase(Ease.InQuad));
        seq.OnComplete(() => Destroy(go));
        seq.SetUpdate(true);
    }

    public void TriggerAnomalyCoreParryPulse()
    {
        if (root == null) return;

        pulseTween?.Kill();
        root.localScale = Vector3.one * Mathf.Max(0.001f, uiWorldScale);
        pulseTween = root.DOPunchScale(Vector3.one * (uiWorldScale * 0.8f), 0.35f, 16, 0.4f).SetUpdate(true);

        if (postureFill != null)
        {
            postureFill.color = new Color(1f, 0.2f, 1f, 1f);
            DOVirtual.DelayedCall(0.25f, () =>
            {
                if (postureFill != null) postureFill.color = postureColor;
            }).SetUpdate(true);
        }
    }

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("EnemyWorldHUD");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500;
        canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 16f;
        canvasGO.AddComponent<GraphicRaycaster>();

        root = canvas.GetComponent<RectTransform>();
        root.sizeDelta = rootSize;
        root.localScale = Vector3.one * Mathf.Max(0.001f, uiWorldScale);

        RectTransform hpBg = CreateBar("HP_BG", root, new Vector2(0f, 6f), barSize, backgroundColor);
        hpFill = CreateFill("HP_Fill", hpBg, hpColor);

        RectTransform postureBg = CreateBar("Posture_BG", root, new Vector2(0f, -6f), barSize, backgroundColor);
        postureFill = CreateFill("Posture_Fill", postureBg, postureColor);

        hpFill.fillAmount = 1f;
        postureFill.fillAmount = 0f;
    }

    private RectTransform CreateBar(string name, RectTransform parent, Vector2 anchoredPos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.type = Image.Type.Sliced;

        return rt;
    }

    private Image CreateFill(string name, RectTransform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;

        return img;
    }
}
