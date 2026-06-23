using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class TeleportSwitch : MonoBehaviour
{
    [Header("吸附设置")]
    public bool useAttract = true;
    public float attractSpeed = 18f;
    public float attractStopDistance = 0.05f;
    public float maxAttractTime = 0.2f;

    [Header("要传送到的目标编号")]
    public int targetID = 1;

    [Header("是否只能触发一次")]
    public bool oneTimeOnly = false;

    [Header("机关自己的冷却时间")]
    public float localCooldown = 0.1f;

    [Header("是否进入就传送")]
    public bool teleportOnEnter = true;

    [Header("是否需要按上键才传送")]
    public bool requireUpKey = false;

    [Header("传送时是否清空速度")]
    public bool resetVelocity = true;

    private bool hasTriggered = false;
    private bool isCoolingDown = false;
    private bool isActivating = false;

    private PlayerTeleportState playerInsideState;
    private Rigidbody2D playerInsideRb;
    private Transform playerInsideTf;
    private Collider2D playerInsideCol;
    private PlayerTransportResponder playerInsideTransport;
    private PlayerBubbleResponder playerInsideBubbleResponder;

    void Start()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void Update()
    {
        if (!requireUpKey) return;
        if (playerInsideState == null) return;
        if (isActivating) return;
        if (playerInsideTransport != null && playerInsideTransport.IsTransporting) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            StartCoroutine(TryTeleportRoutine());
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        CachePlayer(other);

        if (teleportOnEnter && !requireUpKey)
        {
            if (isActivating) return;
            if (playerInsideTransport != null && playerInsideTransport.IsTransporting) return;

            StartCoroutine(TryTeleportRoutine());
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        CachePlayer(other);

        if (teleportOnEnter && !requireUpKey)
        {
            if (isActivating) return;
            if (playerInsideTransport != null && playerInsideTransport.IsTransporting) return;

            StartCoroutine(TryTeleportRoutine());
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        PlayerTeleportState state = other.GetComponentInParent<PlayerTeleportState>();
        if (state == null) return;

        if (state == playerInsideState)
            ClearCachedPlayer();
    }

    private void CachePlayer(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        PlayerTeleportState state = other.GetComponentInParent<PlayerTeleportState>();
        if (state == null) return;

        playerInsideState = state;
        playerInsideRb = other.GetComponentInParent<Rigidbody2D>();
        playerInsideTf = state.transform;
        playerInsideCol = other;
        playerInsideTransport = other.GetComponentInParent<PlayerTransportResponder>();
        playerInsideBubbleResponder = other.GetComponentInParent<PlayerBubbleResponder>();
    }

    private IEnumerator TryTeleportRoutine()
    {
        if (playerInsideState == null || playerInsideTf == null) yield break;
        if (oneTimeOnly && hasTriggered) yield break;
        if (isCoolingDown) yield break;
        if (isActivating) yield break;
        if (!playerInsideState.CanTeleport()) yield break;
        if (playerInsideTransport != null && playerInsideTransport.IsTransporting) yield break;

        isActivating = true;

        if (useAttract && playerInsideBubbleResponder != null)
        {
            yield return playerInsideBubbleResponder.AttractToPoint(
                transform,
                attractSpeed,
                attractStopDistance,
                maxAttractTime,
                true
            );
        }

        if (!TeleportPoint.TryGetPoint(targetID, out TeleportPoint point))
        {
            Debug.LogWarning($"找不到目标编号：{targetID}，机关名：{gameObject.name}");
            isActivating = false;
            yield break;
        }

        BubbleTimeBonusUtility.TryGiveTime(this, playerInsideCol);

        if (resetVelocity && playerInsideRb != null)
            playerInsideRb.linearVelocity = Vector2.zero;

        playerInsideTf.position = point.GetArrivalPosition();

        CameraTeleportSnap camSnap = Camera.main != null ? Camera.main.GetComponent<CameraTeleportSnap>() : null;
        if (camSnap != null)
            camSnap.RequestSnap();

        playerInsideState.MarkTeleported();
        hasTriggered = true;

        ClearCachedPlayer();

        isActivating = false;
        StartCoroutine(CooldownRoutine());
    }

    private void ClearCachedPlayer()
    {
        playerInsideState = null;
        playerInsideRb = null;
        playerInsideTf = null;
        playerInsideCol = null;
        playerInsideTransport = null;
        playerInsideBubbleResponder = null;
    }

    private IEnumerator CooldownRoutine()
    {
        isCoolingDown = true;
        yield return new WaitForSeconds(localCooldown);
        isCoolingDown = false;
    }
}