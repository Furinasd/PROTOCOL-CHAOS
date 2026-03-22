using UnityEngine;

public class InspectHitbox : MonoBehaviour
{
    void Start()
    {
        EnemyHitbox[] hitboxes = FindObjectsByType<EnemyHitbox>(FindObjectsSortMode.None);
        foreach (var hb in hitboxes)
        {
            Debug.Log($"[Inspect] Hitbox: {hb.gameObject.name}, LayerMask: {hb.targetLayer.value}, Size: {hb.hitboxSize}");
        }
        
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            Debug.Log($"[Inspect] Player Layer: {player.layer} ({LayerMask.LayerToName(player.layer)})");
        }
    }
}
