using UnityEngine;

public class PlayerTeleportState : MonoBehaviour
{
    [Header("传送后免疫时间，避免来回连传")]
    public float teleportImmunityTime = 0.2f;

    private float nextTeleportAllowedTime = 0f;

    public bool CanTeleport()
    {
        return Time.time >= nextTeleportAllowedTime;
    }

    public void MarkTeleported()
    {
        nextTeleportAllowedTime = Time.time + teleportImmunityTime;
    }
}