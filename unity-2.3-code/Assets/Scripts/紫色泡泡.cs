using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class PurpleBubbleSwitch : MonoBehaviour
{
    public enum TriggerTarget
    {
        OneWayDoor,
        SceneFlip,
        OneWayDoorAndSceneFlip
    }

    public enum ChangeMode
    {
        CycleClockwise,
        CycleCounterClockwise,
        Rotate180,
        SetDirection
    }

    [Header("触发目标")]
    public TriggerTarget triggerTarget = TriggerTarget.OneWayDoor;

    [Header("单向门链接方式")]
    public OneWayDoor linkedDoor;
    public OneWayDoor[] linkedDoors;
    public int targetDoorId = 0;

    [Header("单向门改变方式")]
    public ChangeMode changeMode = ChangeMode.Rotate180;

    [Header("如果是 SetDirection，则使用这个目标方向")]
    public OneWayDoor.PassDirection targetDirection = OneWayDoor.PassDirection.Up;

    [Header("场景翻转链接方式")]
    public SceneFlipManager linkedSceneFlipManager;
    public int targetSceneFlipId = 0;

    [Header("场景翻转开关")]
    [Tooltip("勾上则触发水平翻转")]
    public bool flipHorizontal = false;

    [Tooltip("勾上则触发垂直翻转")]
    public bool flipVertical = true;

    [Header("表现")]
    public bool playJuice = true;

    [Header("是否使用后重生")]
    public bool respawnBubble = true;
    public float respawnTime = 2f;

    [Header("调试")]
    public bool debugLog = false;

    private Collider2D[] allColliders;
    private SpriteRenderer[] allRenderers;
    private bool isUsed = false;

    void Awake()
    {
        allColliders = GetComponentsInChildren<Collider2D>(true);
        allRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryActivate(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryActivate(other);
    }

    private void TryActivate(Collider2D other)
    {
        if (isUsed)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        PlayerTransportResponder transport = other.GetComponentInParent<PlayerTransportResponder>();
        if (transport != null && transport.IsTransporting)
            return;

        bool needDoor =
            triggerTarget == TriggerTarget.OneWayDoor ||
            triggerTarget == TriggerTarget.OneWayDoorAndSceneFlip;

        bool needSceneFlip =
            triggerTarget == TriggerTarget.SceneFlip ||
            triggerTarget == TriggerTarget.OneWayDoorAndSceneFlip;

        List<OneWayDoor> doors = new List<OneWayDoor>();
        SceneFlipManager sceneFlipManager = null;

        if (needDoor)
        {
            doors = ResolveDoors();

            if (doors == null || doors.Count <= 0)
            {
                Debug.LogWarning($"紫色泡泡找不到单向门：{gameObject.name}，Target Door Id = {targetDoorId}");
                return;
            }
        }

        if (needSceneFlip)
        {
            sceneFlipManager = ResolveSceneFlipManager();

            if (sceneFlipManager == null)
            {
                Debug.LogWarning($"紫色泡泡找不到 SceneFlipManager：{gameObject.name}");
                return;
            }
        }

        isUsed = true;

        if (needDoor)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i] != null)
                    ApplyDoorChange(doors[i]);
            }
        }

        if (sceneFlipManager != null)
        {
            if (debugLog)
            {
                Debug.Log(
                    $"PurpleBubbleSwitch 触发翻转: bubble={gameObject.name}, " +
                    $"flipHorizontal={flipHorizontal}, flipVertical={flipVertical}"
                );
            }

            if (flipHorizontal)
                sceneFlipManager.FlipHorizontal();

            if (flipVertical)
                sceneFlipManager.FlipVertical();
        }

        BubbleTimeBonusUtility.TryGiveTime(this, other);

        if (playJuice)
        {
            PlayerJuiceEffects juice = other.GetComponentInParent<PlayerJuiceEffects>();
            if (juice != null)
                juice.PlaySpecialBubbleJuice();
        }

        if (respawnBubble)
            StartCoroutine(RespawnRoutine());
        else
            Destroy(gameObject);
    }

    private List<OneWayDoor> ResolveDoors()
    {
        List<OneWayDoor> result = new List<OneWayDoor>();

        if (linkedDoor != null)
            AddDoorIfValid(result, linkedDoor);

        if (linkedDoors != null)
        {
            for (int i = 0; i < linkedDoors.Length; i++)
                AddDoorIfValid(result, linkedDoors[i]);
        }

        if (result.Count > 0)
            return result;

        return OneWayDoor.FindAllById(targetDoorId);
    }

    private void AddDoorIfValid(List<OneWayDoor> list, OneWayDoor door)
    {
        if (list == null || door == null || !door.isActiveAndEnabled)
            return;

        if (!list.Contains(door))
            list.Add(door);
    }

    private void ApplyDoorChange(OneWayDoor door)
    {
        if (door == null)
            return;

        switch (changeMode)
        {
            case ChangeMode.CycleClockwise:
                door.RotateClockwise();
                break;
            case ChangeMode.CycleCounterClockwise:
                door.RotateCounterClockwise();
                break;
            case ChangeMode.Rotate180:
                door.Rotate180();
                break;
            case ChangeMode.SetDirection:
                door.SetDirection(targetDirection);
                break;
        }
    }

    private SceneFlipManager ResolveSceneFlipManager()
    {
        if (linkedSceneFlipManager != null)
            return linkedSceneFlipManager;

        return SceneFlipManager.FindById(targetSceneFlipId);
    }

    IEnumerator RespawnRoutine()
    {
        SetBubbleVisible(false);

        yield return new WaitForSeconds(respawnTime);

        SetBubbleVisible(true);
        isUsed = false;
    }

    private void SetBubbleVisible(bool visible)
    {
        if (allColliders != null)
        {
            for (int i = 0; i < allColliders.Length; i++)
            {
                if (allColliders[i] != null)
                    allColliders[i].enabled = visible;
            }
        }

        if (allRenderers != null)
        {
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null)
                    allRenderers[i].enabled = visible;
            }
        }
    }
}