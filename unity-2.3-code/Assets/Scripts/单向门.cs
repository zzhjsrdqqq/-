using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(PlatformEffector2D))]
public class OneWayDoor : MonoBehaviour
{
    public enum PassDirection
    {
        Up,
        Right,
        Down,
        Left
    }

    [Header("门编号（用于给紫泡泡链接）")]
    public int doorId = 0;

    [Header("当前可通过方向")]
    public PassDirection currentDirection = PassDirection.Up;

    [Header("是否在开始时自动配置组件")]
    public bool autoSetupEffector = true;

    private static Dictionary<int, List<OneWayDoor>> registry = new Dictionary<int, List<OneWayDoor>>();

    void Awake()
    {
        if (autoSetupEffector)
            SetupEffector();

        RegisterDoor();
        ApplyDirection();
    }

    void OnEnable()
    {
        RegisterDoor();
        ApplyDirection();
    }

    void OnDisable()
    {
        UnregisterDoor();
    }

    void OnDestroy()
    {
        UnregisterDoor();
    }

    void OnValidate()
    {
        ApplyDirectionInEditor();
    }

    public static OneWayDoor FindById(int id)
    {
        List<OneWayDoor> doors = FindAllById(id);

        if (doors == null || doors.Count <= 0)
            return null;

        return doors[0];
    }

    public static List<OneWayDoor> FindAllById(int id)
    {
        CleanupRegistry();

        if (!registry.TryGetValue(id, out List<OneWayDoor> doors))
            return new List<OneWayDoor>();

        List<OneWayDoor> result = new List<OneWayDoor>();

        for (int i = 0; i < doors.Count; i++)
        {
            if (doors[i] != null && doors[i].isActiveAndEnabled)
                result.Add(doors[i]);
        }

        return result;
    }

    public void SetDirection(PassDirection newDirection)
    {
        currentDirection = newDirection;
        ApplyDirection();
    }

    public void RotateClockwise()
    {
        currentDirection = (PassDirection)(((int)currentDirection + 1) % 4);
        ApplyDirection();
    }

    public void RotateCounterClockwise()
    {
        int next = (int)currentDirection - 1;
        if (next < 0) next = 3;

        currentDirection = (PassDirection)next;
        ApplyDirection();
    }

    public void Rotate180()
    {
        currentDirection = (PassDirection)(((int)currentDirection + 2) % 4);
        ApplyDirection();
    }

    private void RegisterDoor()
    {
        if (!registry.TryGetValue(doorId, out List<OneWayDoor> doors))
        {
            doors = new List<OneWayDoor>();
            registry.Add(doorId, doors);
        }

        if (!doors.Contains(this))
            doors.Add(this);
    }

    private void UnregisterDoor()
    {
        if (!registry.TryGetValue(doorId, out List<OneWayDoor> doors))
            return;

        doors.Remove(this);

        if (doors.Count <= 0)
            registry.Remove(doorId);
    }

    private static void CleanupRegistry()
    {
        List<int> emptyKeys = new List<int>();

        foreach (KeyValuePair<int, List<OneWayDoor>> pair in registry)
        {
            List<OneWayDoor> doors = pair.Value;

            for (int i = doors.Count - 1; i >= 0; i--)
            {
                if (doors[i] == null)
                    doors.RemoveAt(i);
            }

            if (doors.Count <= 0)
                emptyKeys.Add(pair.Key);
        }

        for (int i = 0; i < emptyKeys.Count; i++)
            registry.Remove(emptyKeys[i]);
    }

    private void SetupEffector()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        PlatformEffector2D effector = GetComponent<PlatformEffector2D>();

        box.usedByEffector = true;
        box.isTrigger = false;

        effector.useOneWay = true;
        effector.useOneWayGrouping = true;
        effector.surfaceArc = 180f;
    }

    private void ApplyDirection()
    {
        transform.rotation = Quaternion.Euler(0f, 0f, DirectionToAngle(currentDirection));
    }

    private void ApplyDirectionInEditor()
    {
        if (!Application.isPlaying)
            transform.rotation = Quaternion.Euler(0f, 0f, DirectionToAngle(currentDirection));
    }

    private float DirectionToAngle(PassDirection direction)
    {
        switch (direction)
        {
            case PassDirection.Up:
                return 0f;

            case PassDirection.Right:
                return -90f;

            case PassDirection.Down:
                return 180f;

            case PassDirection.Left:
                return 90f;
        }

        return 0f;
    }
}