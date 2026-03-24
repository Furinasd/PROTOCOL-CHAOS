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
    [SerializeField] private Material lightBeamMaterial; // 如果为空将自动创建一个基础的 Additive 材质

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

    private void Awake()
    {
        CachePlatformPosition();
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
        // 动态生成一个光柱圆柱体
        GameObject beamObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(beamObj.GetComponent<Collider>()); // 移除碰撞体

        // 设置光柱位置（玩家正中心或场地中心）
        PlayerController player = FindFirstObjectByType<PlayerController>();
        Vector3 spawnPos = player != null ? player.transform.position : Vector3.zero;
        beamObj.transform.position = spawnPos + Vector3.up * 50f; // 从高空开始
        beamObj.transform.localScale = new Vector3(8f, 50f, 8f); // 初始很细很长

        // 配置材质 (如果没有赋予，创建一个简单的半透明发光材质)
        MeshRenderer renderer = beamObj.GetComponent<MeshRenderer>();
        if (lightBeamMaterial != null)
        {
            renderer.material = lightBeamMaterial;
        }
        else
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetColor("_BaseColor", new Color(0.2f, 0.8f, 1f, 0.6f));
            // 启用 Transparent
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.renderQueue = 3000;
            renderer.material = mat;
        }

        // Juice 动画：瞬间砸向地面并且变宽，然后渐渐消散
        Sequence seq = DOTween.Sequence();
        seq.Append(beamObj.transform.DOMoveY(0f, 0.15f).SetEase(Ease.OutExpo));
        seq.Join(beamObj.transform.DOScale(new Vector3(30f, 50f, 30f), 0.3f).SetEase(Ease.OutQuint));

        // 材质阿尔法消散
        if (renderer.material.HasProperty("_BaseColor"))
        {
            Color startColor = renderer.material.GetColor("_BaseColor");
            seq.Join(renderer.material.DOColor(new Color(startColor.r, startColor.g, startColor.b, 0f), "_BaseColor", 0.5f).SetDelay(0.1f));
        }

        seq.OnComplete(() => Destroy(beamObj));
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
