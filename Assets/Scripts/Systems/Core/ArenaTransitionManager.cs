using System.Collections;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 管理一镜到底的竞技场过渡：地板升降、微震、灯光变色。
/// </summary>
public class ArenaTransitionManager : MonoBehaviour
{
    [Header("Arena Platform")]
    [SerializeField] private Transform arenaPlatform;
    [SerializeField] private float platformDropOffset = 0.6f;
    [SerializeField] private float dropDuration = 0.22f;
    [SerializeField] private float riseDuration = 0.35f;
    [SerializeField] private Ease dropEase = Ease.InQuad;
    [SerializeField] private Ease riseEase = Ease.OutBack;

    [Header("Juice VFX")]
    [SerializeField] private GameObject lightBeamPrefab;
    [SerializeField] private Material lightBeamMaterial;

    [Header("Lighting")]
    [SerializeField] private Light keyLight;
    [SerializeField] private float lightBlendDuration = 0.45f;
    [SerializeField] private Color phase1LightColor = new Color(0.45f, 0.7f, 1f, 1f);
    [SerializeField] private Color phase2LightColor = new Color(0.55f, 0.85f, 1f, 1f);
    [SerializeField] private Color phase3LightColor = new Color(0.95f, 0.95f, 1f, 1f);
    [SerializeField] private Color phase4LightColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("Feedback")]
    [SerializeField] private bool useCameraShake = true;
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeStrength = 0.1f;

    private Vector3 platformInitialLocalPos;
    private bool platformCached;
    private Transform beamTransform;
    private MeshRenderer beamRenderer;
    private Material beamSharedMaterial;
    private MaterialPropertyBlock beamMpb;
    private Tween beamAlphaTween;

    private void Awake()
    {
        CachePlatformPosition();
        CacheBeamResources();
    }

    private void OnDestroy()
    {
        beamAlphaTween?.Kill();
    }

    public void ApplyPhaseLight(int phaseIndex)
    {
        Color targetColor = phaseIndex switch
        {
            1 => phase1LightColor,
            2 => phase2LightColor,
            3 => phase3LightColor,
            4 => phase4LightColor,
            _ => phase1LightColor,
        };

        if (keyLight == null)
        {
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetColor * 0.4f, 0.8f);
            return;
        }

        DOTween.Kill(keyLight);
        keyLight.DOColor(targetColor, lightBlendDuration).SetEase(Ease.OutSine);
    }

    public IEnumerator PlayArenaLiftTransition()
    {
        CachePlatformPosition();

        // 1. 播放屏幕震动
        if (useCameraShake && Camera.main != null)
        {
            Camera.main.transform.DOShakePosition(shakeDuration, shakeStrength, 14, 90f, false, true)
                .SetUpdate(true);
        }

        // 2. 生成从天而降的光束 Juice
        SpawnLightBeamJuice();

        if (arenaPlatform == null)
        {
            yield break; // 如果没有绑定 Ground，只播放震动和光束即可
        }

        // 3. 地板下沉与升起 (由于有重力，玩家和怪物会自然跟随掉落，产生失重感)
        Vector3 downPos = platformInitialLocalPos + Vector3.down * platformDropOffset;
        yield return arenaPlatform.DOLocalMove(downPos, dropDuration).SetEase(dropEase).WaitForCompletion();
        yield return arenaPlatform.DOLocalMove(platformInitialLocalPos, riseDuration).SetEase(riseEase).WaitForCompletion();
    }

    private void SpawnLightBeamJuice()
    {
        CacheBeamResources();
        if (beamTransform == null || beamRenderer == null)
        {
            return;
        }

        beamAlphaTween?.Kill();

        // 设置光柱位置（玩家正中心或场地中心）
        PlayerController player = FindFirstObjectByType<PlayerController>();
        Vector3 spawnPos = player != null ? player.transform.position : Vector3.zero;
        beamTransform.position = spawnPos + Vector3.up * 50f; // 从高空开始
        beamTransform.localScale = new Vector3(8f, 50f, 8f); // 初始很细很长
        beamRenderer.enabled = true;
        SetBeamAlpha(0.6f);

        // Juice 动画：瞬间砸向地面并且变宽，然后渐渐消散
        Sequence seq = DOTween.Sequence();
        seq.Append(beamTransform.DOMoveY(0f, 0.15f).SetEase(Ease.OutExpo));
        seq.Join(beamTransform.DOScale(new Vector3(30f, 50f, 30f), 0.3f).SetEase(Ease.OutQuint));

        beamAlphaTween = DOVirtual.Float(0.6f, 0f, 0.5f, SetBeamAlpha).SetDelay(0.1f);
        seq.Join(beamAlphaTween);
        seq.OnComplete(() =>
        {
            if (beamRenderer != null)
            {
                beamRenderer.enabled = false;
            }
        });
    }

    private void CacheBeamResources()
    {
        if (beamTransform != null && beamRenderer != null)
        {
            return;
        }

        GameObject beamObj;
        if (lightBeamPrefab != null)
        {
            beamObj = Instantiate(lightBeamPrefab, transform);
            beamObj.name = "ArenaLightBeam_Pooled";
        }
        else
        {
            beamObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beamObj.name = "ArenaLightBeam_Pooled";
            Destroy(beamObj.GetComponent<Collider>());
            beamObj.transform.SetParent(transform, true);
        }

        beamTransform = beamObj.transform;
        beamRenderer = beamObj.GetComponentInChildren<MeshRenderer>(true);
        if (beamRenderer == null)
        {
            Debug.LogWarning("[ArenaTransition] Light beam prefab 未找到 MeshRenderer，已跳过光柱演出。", this);
            Destroy(beamObj);
            beamTransform = null;
            return;
        }

        beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beamRenderer.receiveShadows = false;

        if (lightBeamMaterial != null)
        {
            beamSharedMaterial = lightBeamMaterial;
        }

        if (beamSharedMaterial != null)
        {
            beamRenderer.sharedMaterial = beamSharedMaterial;
        }
        else if (beamRenderer.sharedMaterial == null)
        {
            Debug.LogWarning("[ArenaTransition] 未配置 lightBeamMaterial 且 prefab 无内置材质，光柱可能不可见。", this);
        }

        beamMpb = new MaterialPropertyBlock();
        beamRenderer.enabled = false;
    }

    private void SetBeamAlpha(float alpha)
    {
        if (beamRenderer == null)
        {
            return;
        }

        Color c = new Color(0.2f, 0.8f, 1f, Mathf.Clamp01(alpha));
        beamRenderer.GetPropertyBlock(beamMpb);
        beamMpb.SetColor("_BaseColor", c);
        beamRenderer.SetPropertyBlock(beamMpb);
    }

    private void CachePlatformPosition()
    {
        if (platformCached || arenaPlatform == null)
        {
            return;
        }

        platformInitialLocalPos = arenaPlatform.localPosition;
        platformCached = true;
    }
}
