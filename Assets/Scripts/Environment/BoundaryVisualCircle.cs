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
        
        // Ensure visual is always correct in editor even if script reloads
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
        
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = circleSegments;
        line.startWidth = circleLineWidth;
        line.endWidth = circleLineWidth;

        Vector3[] points = new Vector3[circleSegments];
        for (int i = 0; i < circleSegments; i++)
        {
            float angle = i * 2f * Mathf.PI / circleSegments;
            points[i] = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
        }
        line.SetPositions(points);
        lastRadius = radius;
    }
}
