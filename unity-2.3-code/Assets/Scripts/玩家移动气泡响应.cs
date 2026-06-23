using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerTransportResponder : MonoBehaviour
{
    [Header("默认重力")]
    public float defaultGravityScale = 1f;

    [Header("跳跃脱出缓冲")]
    [Tooltip("运输过程中按下跳跃后，这段时间内都会记住这次脱出输入，避免漏判")]
    public float jumpEscapeBufferTime = 0.12f;

    [Header("到达判定")]
    [Tooltip("距离终点小于这个值时，视为运输完成")]
    public float arriveDistance = 0.06f;

    [Header("强制结束保护")]
    [Tooltip("最大运输时间 = 距离 / 速度 + 这个额外时间")]
    public float maxExtraTransportTime = 0.25f;

    [Tooltip("如果连续几帧几乎没移动，就判定为卡住")]
    public float stuckDistanceThreshold = 0.001f;

    [Tooltip("卡住多久后强制结束运输")]
    public float stuckTime = 0.08f;

    [Tooltip("强制结束时，如果已经非常接近终点，就直接吸到终点")]
    public float snapToTargetDistance = 0.35f;

    [Header("调试")]
    public bool debugLog = false;

    public bool IsTransporting
    {
        get;
        private set;
    }

    private Rigidbody2D rb;
    private PlayerController playerController;

    private Coroutine transportRoutine;
    private CollisionDetectionMode2D savedCollisionMode;

    private bool currentAllowJumpEscape = false;
    private float currentJumpEscapeForce = 0f;
    private float jumpEscapeBufferTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController>();

        if (rb != null && rb.gravityScale > 0f)
            defaultGravityScale = rb.gravityScale;

        if (rb != null)
            savedCollisionMode = rb.collisionDetectionMode;
    }

    void Update()
    {
        if (!IsTransporting)
        {
            jumpEscapeBufferTimer = 0f;
            return;
        }

        if (currentAllowJumpEscape &&
            (Input.GetKeyDown(KeyCode.Space) ||
             Input.GetKeyDown(KeyCode.W) ||
             Input.GetKeyDown(KeyCode.UpArrow)))
        {
            jumpEscapeBufferTimer = jumpEscapeBufferTime;
        }

        if (jumpEscapeBufferTimer > 0f)
            jumpEscapeBufferTimer -= Time.deltaTime;
    }

    public void TriggerTransport(Vector2 direction, float distance, float speed)
    {
        StartFixedDirectionTransport(direction, distance, speed, false, 0f);
    }

    public void TriggerTransport(Vector2 direction, float distance, float speed, bool allowJumpEscape, float jumpEscapeForce)
    {
        StartFixedDirectionTransport(direction, distance, speed, allowJumpEscape, jumpEscapeForce);
    }

    public void StartTransport(Vector2 direction, float distance, float speed)
    {
        StartFixedDirectionTransport(direction, distance, speed, false, 0f);
    }

    public void StartTransport(Vector2 direction, float distance, float speed, bool allowJumpEscape, float jumpEscapeForce)
    {
        StartFixedDirectionTransport(direction, distance, speed, allowJumpEscape, jumpEscapeForce);
    }

    public void Transport(Vector2 direction, float distance, float speed)
    {
        StartFixedDirectionTransport(direction, distance, speed, false, 0f);
    }

    public void Transport(Vector2 direction, float distance, float speed, bool allowJumpEscape, float jumpEscapeForce)
    {
        StartFixedDirectionTransport(direction, distance, speed, allowJumpEscape, jumpEscapeForce);
    }

    private void StartFixedDirectionTransport(Vector2 direction, float distance, float speed, bool allowJumpEscape, float jumpEscapeForce)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        if (transportRoutine != null)
            StopCoroutine(transportRoutine);

        currentAllowJumpEscape = allowJumpEscape;
        currentJumpEscapeForce = Mathf.Abs(jumpEscapeForce);
        jumpEscapeBufferTimer = 0f;

        IsTransporting = true;
        transportRoutine = StartCoroutine(TransportRoutine(direction.normalized, distance, speed));
    }

    private IEnumerator TransportRoutine(Vector2 direction, float distance, float speed)
    {
        if (rb == null)
        {
            IsTransporting = false;
            yield break;
        }

        distance = Mathf.Max(0.01f, distance);
        speed = Mathf.Max(0.01f, speed);

        savedCollisionMode = rb.collisionDetectionMode;

        if (playerController != null)
        {
            playerController.RefreshDashAndJump();
            playerController.enabled = false;
        }

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        Vector2 start = rb.position;
        Vector2 target = start + direction * distance;

        bool escaped = false;
        float elapsed = 0f;
        float maxDuration = (distance / speed) + Mathf.Max(0f, maxExtraTransportTime);

        Vector2 prevPos = rb.position;
        float stuckTimer = 0f;

        while (true)
        {
            if (CanConsumeJumpEscape())
            {
                escaped = true;
                break;
            }

            float remaining = Vector2.Distance(rb.position, target);
            if (remaining <= arriveDistance)
                break;

            if (elapsed >= maxDuration)
            {
                if (debugLog)
                    Debug.Log($"运输超时，强制结束：{gameObject.name}");
                break;
            }

            Vector2 next = Vector2.MoveTowards(
                rb.position,
                target,
                speed * Time.fixedDeltaTime
            );

            rb.MovePosition(next);
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            yield return new WaitForFixedUpdate();

            elapsed += Time.fixedDeltaTime;

            float moved = Vector2.Distance(rb.position, prevPos);
            if (moved <= stuckDistanceThreshold)
                stuckTimer += Time.fixedDeltaTime;
            else
                stuckTimer = 0f;

            if (stuckTimer >= stuckTime)
            {
                if (debugLog)
                    Debug.Log($"运输卡住，强制结束：{gameObject.name}");
                break;
            }

            prevPos = rb.position;
        }

        float remainingToTarget = Vector2.Distance(rb.position, target);
        float escapeForceToApply = currentJumpEscapeForce;

        // 非跳跃脱出时，如果已经足够接近终点，就直接吸到终点
        if (!escaped && remainingToTarget <= snapToTargetDistance)
        {
            rb.position = target;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        RestoreState();

        if (escaped && rb != null)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, escapeForceToApply);
    }

    private bool CanConsumeJumpEscape()
    {
        if (!currentAllowJumpEscape)
            return false;

        if (jumpEscapeBufferTimer <= 0f)
            return false;

        jumpEscapeBufferTimer = 0f;
        return true;
    }

    public void CancelTransport()
    {
        if (transportRoutine != null)
        {
            StopCoroutine(transportRoutine);
            transportRoutine = null;
        }

        RestoreState();
    }

    private void RestoreState()
    {
        if (rb != null)
        {
            rb.gravityScale = defaultGravityScale;
            rb.collisionDetectionMode = savedCollisionMode;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (playerController != null)
            playerController.enabled = true;

        currentAllowJumpEscape = false;
        currentJumpEscapeForce = 0f;
        jumpEscapeBufferTimer = 0f;

        IsTransporting = false;
        transportRoutine = null;
    }
}