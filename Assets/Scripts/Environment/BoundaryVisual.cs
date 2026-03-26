using UnityEngine;

/// <summary>
/// 边界可视化桥接器。
/// 保持旧场景引用在脚本重组后仍然有效。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class BoundaryVisual : MonoBehaviour
{
    private LineRenderer lineRenderer;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer != null)
        {
            // Preserve previous behavior expectation: world-space ring line.
            lineRenderer.useWorldSpace = true;
        }
    }
}
