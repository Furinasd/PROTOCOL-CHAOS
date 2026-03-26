using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(LineRenderer))]
public class BoundaryVisualCircle : MonoBehaviour
{
    [Header("🔗 Data Link")]
    public PlayerController playerController;

    [Header("🎨 Visual Settings")]
    public float circleLineWidth = 0.3f;
    public int circleSegments = 64;
    public float heightOffset = 0.15f;

    private float lastRadius;

    void Start()
    {
        TryFindPlayerController();
        RefreshCirclePositions();
    }

    void Update()
    {
        if (playerController == null && !Application.isPlaying)
        {
            TryFindPlayerController();
        }

        if (playerController != null)
        {
            float currentRadius = playerController.arenaRadius;
            if (!Mathf.Approximately(currentRadius, lastRadius))
            {
                lastRadius = currentRadius;
                RefreshCirclePositions();
            }
        }
        
        // 确保在编辑器中即使脚本重新加载，视觉效果也始终正确
        if (!Application.isPlaying && Time.frameCount % 30 == 0) 
        {
            RefreshCirclePositions();
        }
    }

    private void TryFindPlayerController()
    {
        playerController = Object.FindFirstObjectByType<PlayerController>();
    }

    public void RefreshCirclePositions()
    {
        LineRenderer line = GetComponent<LineRenderer>();
        if (line == null) return;

        float radius = (playerController != null) ? playerController.arenaRadius : 20f;

        // 强制在运行时渲染可读的环形：避免地面Z轴冲突和局部变换失真。
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = Mathf.Max(8, circleSegments);
        line.startWidth = circleLineWidth;
        line.endWidth = circleLineWidth;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.View;

        int segments = line.positionCount;
        Vector3 center = transform.position + Vector3.up * heightOffset;
        Vector3[] points = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            points[i] = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }
        line.SetPositions(points);
        lastRadius = radius;
    }
}
