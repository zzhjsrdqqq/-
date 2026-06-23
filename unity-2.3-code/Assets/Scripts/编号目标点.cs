using UnityEngine;
using System.Collections.Generic;

public class TeleportPoint : MonoBehaviour
{
    [Header("目标点编号（不能重复）")]
    public int pointID = 1;

    [Header("实际落点，不填就用当前物体位置")]
    public Transform arrivalPoint;

    private static Dictionary<int, TeleportPoint> pointRegistry = new Dictionary<int, TeleportPoint>();

    void OnEnable()
    {
        if (pointRegistry.ContainsKey(pointID) && pointRegistry[pointID] != this)
        {
            Debug.LogWarning($"TeleportPoint 编号重复：{pointID}，物体名：{gameObject.name}");
        }

        pointRegistry[pointID] = this;
    }

    void OnDisable()
    {
        if (pointRegistry.ContainsKey(pointID) && pointRegistry[pointID] == this)
        {
            pointRegistry.Remove(pointID);
        }
    }

    public Vector3 GetArrivalPosition()
    {
        if (arrivalPoint != null)
            return arrivalPoint.position;

        return transform.position;
    }

    public static bool TryGetPoint(int id, out TeleportPoint point)
    {
        return pointRegistry.TryGetValue(id, out point);
    }
}