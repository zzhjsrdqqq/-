using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class CameraZoomTrigger : MonoBehaviour
{
    public enum ZoomMode
    {
        ZoomIn,
        ZoomOut,
        SetCustomSize
    }

    [Header("镜头控制器")]
    [Tooltip("可以手动拖 Main Camera 上的 CameraZoomController。不拖也会自动找 Main Camera")]
    public CameraZoomController zoomController;

    [Header("优先级")]
    [Tooltip("多个镜头区重叠时，优先级高的生效。相同优先级时，后进入的生效。")]
    public int priority = 0;

    [Header("缩放方式")]
    public ZoomMode zoomMode = ZoomMode.SetCustomSize;

    [Tooltip("拉近时的相机大小。数值越小镜头越近")]
    public float zoomInSize = 3.5f;

    [Tooltip("拉远时的相机大小。数值越大镜头越远")]
    public float zoomOutSize = 7f;

    [Tooltip("自定义相机大小")]
    public float customSize = 5f;

    [Header("过渡时间")]
    public float zoomDuration = 0.35f;

    [Header("离开触发区")]
    [Tooltip("玩家离开触发区后，是否允许恢复到其他镜头区或默认镜头。做区域镜头时建议开启。")]
    public bool restoreOnExit = true;

    [Tooltip("离开触发区后恢复默认镜头大小的时间")]
    public float restoreDuration = 0.35f;

    [Header("触发设置")]
    [Tooltip("是否只触发一次。区域镜头一般关闭。永久改变镜头才开启。")]
    public bool oneTimeOnly = false;

    [Tooltip("触发一次后是否禁用碰撞体。区域镜头不要开启，否则不会正常退出区域。")]
    public bool disableColliderAfterUse = false;

    [Header("调试")]
    public bool debugLog = false;

    private bool hasTriggered = false;
    private bool isRegisteredActive = false;

    private Collider2D col;

    private readonly HashSet<Collider2D> playerCollidersInside = new HashSet<Collider2D>();

    public bool IsRegisteredActive
    {
        get { return isRegisteredActive; }
    }

    public int LastEnterOrder
    {
        get;
        private set;
    }

    void Awake()
    {
        col = GetComponent<Collider2D>();

        if (col != null)
            col.isTrigger = true;
    }

    void Start()
    {
        ResolveZoomController();
    }

    void OnDisable()
    {
        ForceUnregister();
    }

    void OnDestroy()
    {
        ForceUnregister();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryEnter(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryEnter(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        TryExit(other);
    }

    private void TryEnter(Collider2D other)
    {
        if (other == null)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        if (oneTimeOnly && hasTriggered && !isRegisteredActive)
            return;

        playerCollidersInside.Add(other);

        ResolveZoomController();

        if (zoomController == null)
        {
            Debug.LogWarning($"找不到 CameraZoomController：{gameObject.name}");
            return;
        }

        if (!isRegisteredActive)
        {
            isRegisteredActive = true;
            hasTriggered = true;

            zoomController.RegisterZoomTrigger(this);

            if (debugLog)
                Debug.Log($"进入镜头区：{gameObject.name}, Size = {GetTargetSize()}");

            if (disableColliderAfterUse && col != null)
                col.enabled = false;
        }
    }

    private void TryExit(Collider2D other)
    {
        if (other == null)
            return;

        if (!playerCollidersInside.Contains(other))
            return;

        playerCollidersInside.Remove(other);

        if (playerCollidersInside.Count > 0)
            return;

        if (!restoreOnExit)
            return;

        ForceUnregister();
    }

    private void ForceUnregister()
    {
        if (!isRegisteredActive)
            return;

        isRegisteredActive = false;
        playerCollidersInside.Clear();

        ResolveZoomController();

        if (zoomController != null)
            zoomController.UnregisterZoomTrigger(this, restoreDuration);

        if (debugLog)
            Debug.Log($"退出镜头区：{gameObject.name}");
    }

    public float GetTargetSize()
    {
        switch (zoomMode)
        {
            case ZoomMode.ZoomIn:
                return zoomInSize;

            case ZoomMode.ZoomOut:
                return zoomOutSize;

            case ZoomMode.SetCustomSize:
                return customSize;
        }

        return customSize;
    }

    public void InternalSetLastEnterOrder(int order)
    {
        LastEnterOrder = order;
    }

    private void ResolveZoomController()
    {
        if (zoomController != null)
            return;

        if (Camera.main == null)
            return;

        zoomController = Camera.main.GetComponent<CameraZoomController>();

        if (zoomController == null)
            zoomController = Camera.main.gameObject.AddComponent<CameraZoomController>();
    }
}