using UnityEngine;

/// <summary>
/// 摄像机跟随 - 挂载到Main Camera上
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    [Tooltip("数值越大跟随越紧。10~20 比较跟手，5 偏拖")]
    public float smoothSpeed = 15f;
    public Vector3 offset = new Vector3(0, 1f, -10f);

    [Header("边界限制(可选)")]
    public bool useBounds = false;
    public float minX = -10f, maxX = 50f;
    public float minY = -5f, maxY = 10f;

    private CameraShake2D shake;

    void Start()
    {
        shake = GetComponent<CameraShake2D>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;

        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        Vector3 smoothed = Vector3.Lerp(transform.position, desired, t);

        if (useBounds)
        {
            smoothed.x = Mathf.Clamp(smoothed.x, minX, maxX);
            smoothed.y = Mathf.Clamp(smoothed.y, minY, maxY);
        }

        if (shake != null)
            smoothed += shake.GetOffset();

        smoothed.z = offset.z;
        transform.position = smoothed;
    }
}