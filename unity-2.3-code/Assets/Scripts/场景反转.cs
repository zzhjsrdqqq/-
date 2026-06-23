using UnityEngine;
using System.Collections.Generic;

public class SceneFlipManager : MonoBehaviour
{
    [Header("编号，用于紫色泡泡按编号触发")]
    public int flipId = 0;

    [Header("要翻转的场景根物体")]
    public Transform sceneRoot;

    [Header("启动时归正")]
    public bool normalizeRootScaleOnAwake = true;

    [Header("原地翻转修正")]
    public bool keepSceneCenterInPlace = true;

    [Header("逻辑方向同步")]
    public bool mirrorLaunchBubbleDirections = true;
    public bool mirrorOneWayDoorDirections = true;

    [Header("携带玩家")]
    public bool enableFlipCarryBlocks = true;

    [Header("相机")]
    public bool requestCameraSnapAfterFlip = true;

    [Header("调试")]
    public bool debugLog = false;

    public bool IsHorizontallyFlipped => horizontalFlipped;
    public bool IsVerticallyFlipped => verticalFlipped;

    private bool horizontalFlipped;
    private bool verticalFlipped;
    private Vector3 basePositiveScale = Vector3.one;

    private static Dictionary<int, SceneFlipManager> registry = new Dictionary<int, SceneFlipManager>();

    void Awake()
    {
        if (sceneRoot == null)
            sceneRoot = transform;

        if (sceneRoot != null)
        {
            Vector3 initialScale = sceneRoot.localScale;

            horizontalFlipped = initialScale.x < 0f;
            verticalFlipped = initialScale.y < 0f;

            basePositiveScale = new Vector3(
                Mathf.Abs(initialScale.x) <= 0.0001f ? 1f : Mathf.Abs(initialScale.x),
                Mathf.Abs(initialScale.y) <= 0.0001f ? 1f : Mathf.Abs(initialScale.y),
                Mathf.Abs(initialScale.z) <= 0.0001f ? 1f : Mathf.Abs(initialScale.z)
            );

            if (normalizeRootScaleOnAwake)
                ApplyCurrentFlipScale();
        }

        Register();
    }

    void OnEnable()
    {
        Register();
    }

    void OnDisable()
    {
        Unregister();
    }

    void OnDestroy()
    {
        Unregister();
    }

    private void Register()
    {
        registry[flipId] = this;
    }

    private void Unregister()
    {
        if (registry.TryGetValue(flipId, out SceneFlipManager current) && current == this)
            registry.Remove(flipId);
    }

    public static SceneFlipManager FindById(int id)
    {
        if (registry.TryGetValue(id, out SceneFlipManager manager))
            return manager;

        return null;
    }

    public void FlipHorizontal()
    {
        if (sceneRoot == null)
        {
            Debug.LogWarning($"SceneFlipManager 没有设置 Scene Root：{gameObject.name}");
            return;
        }

        if (debugLog)
            Debug.Log($"SceneFlipManager FlipHorizontal 前：H={horizontalFlipped}, V={verticalFlipped}, Scale={sceneRoot.localScale}");

        ApplyFlipChange(true, false);

        if (debugLog)
            Debug.Log($"SceneFlipManager FlipHorizontal 后：H={horizontalFlipped}, V={verticalFlipped}, Scale={sceneRoot.localScale}");
    }

    public void FlipVertical()
    {
        if (sceneRoot == null)
        {
            Debug.LogWarning($"SceneFlipManager 没有设置 Scene Root：{gameObject.name}");
            return;
        }

        if (debugLog)
            Debug.Log($"SceneFlipManager FlipVertical 前：H={horizontalFlipped}, V={verticalFlipped}, Scale={sceneRoot.localScale}");

        ApplyFlipChange(false, true);

        if (debugLog)
            Debug.Log($"SceneFlipManager FlipVertical 后：H={horizontalFlipped}, V={verticalFlipped}, Scale={sceneRoot.localScale}");
    }

    public void FlipBoth()
    {
        if (sceneRoot == null)
        {
            Debug.LogWarning($"SceneFlipManager 没有设置 Scene Root：{gameObject.name}");
            return;
        }

        if (debugLog)
            Debug.Log($"SceneFlipManager FlipBoth 前：H={horizontalFlipped}, V={verticalFlipped}, Scale={sceneRoot.localScale}");

        ApplyFlipChange(true, true);

        if (debugLog)
            Debug.Log($"SceneFlipManager FlipBoth 后：H={horizontalFlipped}, V={verticalFlipped}, Scale={sceneRoot.localScale}");
    }

    private void ApplyFlipChange(bool toggleHorizontal, bool toggleVertical)
    {
        FlipCarryBlock[] carryBlocks = GetCarryBlocks();
        PrepareCarryBlocks(carryBlocks);

        Bounds beforeBounds;
        bool hasBounds = TryGetSceneBounds(out beforeBounds);

        if (toggleHorizontal)
            horizontalFlipped = !horizontalFlipped;

        if (toggleVertical)
            verticalFlipped = !verticalFlipped;

        ApplyCurrentFlipScale();

        Physics2D.SyncTransforms();

        if (keepSceneCenterInPlace && hasBounds)
            KeepBoundsCenterInPlace(beforeBounds);

        Physics2D.SyncTransforms();

        if (toggleHorizontal)
            MirrorKnownLogicDirections(true);

        if (toggleVertical)
            MirrorKnownLogicDirections(false);

        ApplyCarryBlocks(carryBlocks);
        RequestCameraSnapIfNeeded();
    }

    private void ApplyCurrentFlipScale()
    {
        if (sceneRoot == null)
            return;

        Vector3 scale = basePositiveScale;
        scale.x *= horizontalFlipped ? -1f : 1f;
        scale.y *= verticalFlipped ? -1f : 1f;
        sceneRoot.localScale = scale;
    }

    private bool TryGetSceneBounds(out Bounds bounds)
    {
        bounds = new Bounds(sceneRoot.position, Vector3.zero);

        bool hasBounds = false;

        Renderer[] renderers = sceneRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        Collider2D[] colliders = sceneRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c == null)
                continue;

            Bounds cb = c.bounds;
            if (cb.size.sqrMagnitude <= 0.000001f)
                continue;

            if (!hasBounds)
            {
                bounds = cb;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(cb);
            }
        }

        return hasBounds;
    }

    private void KeepBoundsCenterInPlace(Bounds beforeBounds)
    {
        Bounds afterBounds;
        bool hasAfterBounds = TryGetSceneBounds(out afterBounds);

        if (!hasAfterBounds)
            return;

        Vector3 offset = beforeBounds.center - afterBounds.center;
        sceneRoot.position += offset;
    }

    private FlipCarryBlock[] GetCarryBlocks()
    {
        if (!enableFlipCarryBlocks || sceneRoot == null)
            return new FlipCarryBlock[0];

        return sceneRoot.GetComponentsInChildren<FlipCarryBlock>(true);
    }

    private void PrepareCarryBlocks(FlipCarryBlock[] carryBlocks)
    {
        if (!enableFlipCarryBlocks)
            return;

        FlipCarryBlock.ResetCarriedPlayersThisFlip();

        if (carryBlocks == null)
            return;

        for (int i = 0; i < carryBlocks.Length; i++)
        {
            if (carryBlocks[i] != null)
                carryBlocks[i].BeforeSceneFlip();
        }
    }

    private void ApplyCarryBlocks(FlipCarryBlock[] carryBlocks)
    {
        if (!enableFlipCarryBlocks || carryBlocks == null)
            return;

        for (int i = 0; i < carryBlocks.Length; i++)
        {
            if (carryBlocks[i] != null)
                carryBlocks[i].AfterSceneFlip();
        }

        Physics2D.SyncTransforms();
    }

    private void MirrorKnownLogicDirections(bool horizontal)
    {
        MirrorLaunchBubbleDirections(horizontal);
        MirrorOneWayDoorDirections(horizontal);
    }

    private void MirrorLaunchBubbleDirections(bool horizontal)
    {
        if (!mirrorLaunchBubbleDirections || sceneRoot == null)
            return;

        LaunchBubble[] launchBubbles = sceneRoot.GetComponentsInChildren<LaunchBubble>(true);

        for (int i = 0; i < launchBubbles.Length; i++)
        {
            LaunchBubble bubble = launchBubbles[i];
            if (bubble == null)
                continue;

            Vector2 dir = bubble.launchDirection;

            if (horizontal)
                dir.x *= -1f;
            else
                dir.y *= -1f;

            bubble.launchDirection = dir;
        }
    }

    private void MirrorOneWayDoorDirections(bool horizontal)
    {
        if (!mirrorOneWayDoorDirections || sceneRoot == null)
            return;

        OneWayDoor[] doors = sceneRoot.GetComponentsInChildren<OneWayDoor>(true);

        for (int i = 0; i < doors.Length; i++)
        {
            OneWayDoor door = doors[i];
            if (door == null)
                continue;

            OneWayDoor.PassDirection newDirection = GetMirroredDoorDirection(
                door.currentDirection,
                horizontal
            );

            door.SetDirection(newDirection);
        }
    }

    private OneWayDoor.PassDirection GetMirroredDoorDirection(OneWayDoor.PassDirection direction, bool horizontal)
    {
        if (horizontal)
        {
            switch (direction)
            {
                case OneWayDoor.PassDirection.Left:
                    return OneWayDoor.PassDirection.Right;
                case OneWayDoor.PassDirection.Right:
                    return OneWayDoor.PassDirection.Left;
                case OneWayDoor.PassDirection.Up:
                    return OneWayDoor.PassDirection.Up;
                case OneWayDoor.PassDirection.Down:
                    return OneWayDoor.PassDirection.Down;
            }
        }
        else
        {
            switch (direction)
            {
                case OneWayDoor.PassDirection.Up:
                    return OneWayDoor.PassDirection.Down;
                case OneWayDoor.PassDirection.Down:
                    return OneWayDoor.PassDirection.Up;
                case OneWayDoor.PassDirection.Left:
                    return OneWayDoor.PassDirection.Left;
                case OneWayDoor.PassDirection.Right:
                    return OneWayDoor.PassDirection.Right;
            }
        }

        return direction;
    }

    private void RequestCameraSnapIfNeeded()
    {
        if (!requestCameraSnapAfterFlip)
            return;

        if (Camera.main == null)
            return;

        CameraTeleportSnap snap = Camera.main.GetComponent<CameraTeleportSnap>();
        if (snap != null)
            snap.RequestSnap();
    }
}