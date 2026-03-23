using UnityEngine;

/// <summary>
/// Legacy boundary visual bridge.
/// Keeps old scene references valid after script reorganization.
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
