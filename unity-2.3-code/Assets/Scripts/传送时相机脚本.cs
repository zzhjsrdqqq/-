using UnityEngine;

[DefaultExecutionOrder(10000)]
public class CameraTeleportSnap : MonoBehaviour
{
    public CameraFollow follow;

    private bool snapRequested = false;

    void Awake()
    {
        if (follow == null)
            follow = GetComponent<CameraFollow>();
    }

    public void RequestSnap()
    {
        snapRequested = true;
    }

    void LateUpdate()
    {
        if (!snapRequested) return;
        snapRequested = false;

        if (follow == null || follow.target == null)
            return;

        Vector3 snapped = follow.target.position + follow.offset;

        if (follow.useBounds)
        {
            snapped.x = Mathf.Clamp(snapped.x, follow.minX, follow.maxX);
            snapped.y = Mathf.Clamp(snapped.y, follow.minY, follow.maxY);
        }

        snapped.z = follow.offset.z;
        transform.position = snapped;
    }
}