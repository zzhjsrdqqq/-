using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class TransportBubble : MonoBehaviour
{
    public enum TransportDirection
    {
        Up,
        Right,
        Down,
        Left,
        UpRight,
        UpLeft,
        DownRight,
        DownLeft,
        Custom
    }

    [Header("固定移动方向（编辑者设置）")]
    public TransportDirection transportDirection = TransportDirection.Right;

    [Tooltip("当 Direction = Custom 时使用")]
    public Vector2 customDirection = Vector2.right;

    [Header("吸附设置")]
    [Tooltip("触发后是否先把玩家吸到泡泡中心")]
    public bool useAttract = true;

    [Tooltip("吸附速度")]
    public float attractSpeed = 18f;

    [Tooltip("距离泡泡多近时算吸附完成")]
    public float attractStopDistance = 0.05f;

    [Tooltip("吸附最长持续时间，防止卡住")]
    public float maxAttractTime = 0.2f;

    [Header("移动参数")]
    public float transportDistance = 4f;
    public float transportSpeed = 16f;

    [Tooltip("触发后是否刷新玩家跳跃/冲刺")]
    public bool refreshDashAndJump = true;

    [Header("跳跃脱出")]
    [Tooltip("开启后，运输过程中按跳跃键可以提前脱出")]
    public bool allowJumpEscape = false;

    [Tooltip("跳跃脱出时给予玩家的向上速度")]
    public float jumpEscapeForce = 12f;

    [Header("视觉结构")]
    [Tooltip("平时显示的常态物体。不填时会自动找名为“常态”的子物体")]
    public Transform idleVisual;

    [Tooltip("触发后显示并包裹玩家移动的触发态物体。不填时会自动找名为“触发态”的子物体")]
    public Transform activeVisual;

    [Tooltip("触发态跟随玩家时的偏移")]
    public Vector3 activeVisualOffset = Vector3.zero;

    [Tooltip("触发后触发态的额外倍率")]
    public Vector3 activeVisualScaleMultiplier = new Vector3(0.7f, 0.7f, 1f);

    [Tooltip("运输时是否临时把触发态从泡泡根物体下解绑。建议开启，更稳。")]
    public bool detachActiveVisualWhileTransporting = true;

    [Header("重生")]
    public bool respawnBubble = true;
    public float respawnTime = 2f;

    [Header("编辑器预览")]
    public bool showPreviewWhenNotSelected = false;
    public float previewArrowSize = 0.35f;

    [Header("调试")]
    public bool debugLog = false;

    private Collider2D triggerCol;
    private bool isUsed = false;

    private Coroutine followRoutine;

    private Transform activeOriginalParent;
    private int activeOriginalSiblingIndex;
    private Vector3 activeStartLocalPosition;
    private Quaternion activeStartLocalRotation;
    private Vector3 activeStartLocalScale;

    void Awake()
    {
        AutoBindVisuals();

        triggerCol = GetComponent<Collider2D>();
        if (triggerCol != null)
            triggerCol.isTrigger = true;

        CacheActiveVisualState();
        SetIdleState();
    }

    void OnValidate()
    {
        AutoBindVisuals();
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

        PlayerTransportResponder responder = other.GetComponentInParent<PlayerTransportResponder>();
        if (responder != null && responder.IsTransporting)
            return;

        isUsed = true;
        StartCoroutine(ActivateRoutine(other, player, responder));
    }

    private IEnumerator ActivateRoutine(Collider2D other, PlayerController player, PlayerTransportResponder responder)
    {
        if (player == null)
        {
            isUsed = false;
            yield break;
        }

        Vector2 direction = GetFixedDirection();
        if (direction.sqrMagnitude <= 0.001f)
        {
            isUsed = false;
            yield break;
        }

        if (useAttract)
            yield return StartCoroutine(AttractPlayerToBubble(player));

        if (refreshDashAndJump)
            player.RefreshDashAndJump();

        BubbleTimeBonusUtility.TryGiveTime(this, other);

        PlayerJuiceEffects juice = other.GetComponentInParent<PlayerJuiceEffects>();
        if (juice != null)
            juice.PlaySpecialBubbleJuice();

        SetTransportVisualState(player.transform);

        if (responder != null)
        {
            responder.TriggerTransport(
                direction,
                transportDistance,
                transportSpeed,
                allowJumpEscape,
                jumpEscapeForce
            );

            float waitEnterTimer = 0f;
            while (!responder.IsTransporting && waitEnterTimer < 0.25f)
            {
                waitEnterTimer += Time.deltaTime;
                yield return null;
            }

            while (responder != null && responder.IsTransporting)
                yield return null;
        }
        else
        {
            yield return StartCoroutine(FallbackTransportRoutine(
                player,
                direction,
                transportDistance,
                transportSpeed,
                allowJumpEscape,
                jumpEscapeForce
            ));
        }

        EndTransportVisualState();

        if (respawnBubble)
        {
            yield return new WaitForSeconds(respawnTime);
            SetIdleState();
            isUsed = false;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator AttractPlayerToBubble(PlayerController player)
    {
        if (player == null)
            yield break;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null)
            yield break;

        bool hadController = player.enabled;
        float savedGravity = rb.gravityScale;
        CollisionDetectionMode2D savedCollisionMode = rb.collisionDetectionMode;

        player.enabled = false;

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        Vector2 target = transform.position;
        float timer = 0f;

        attractSpeed = Mathf.Max(0.01f, attractSpeed);
        attractStopDistance = Mathf.Max(0.001f, attractStopDistance);
        maxAttractTime = Mathf.Max(0.01f, maxAttractTime);

        while (timer < maxAttractTime)
        {
            float distance = Vector2.Distance(rb.position, target);
            if (distance <= attractStopDistance)
                break;

            Vector2 next = Vector2.MoveTowards(
                rb.position,
                target,
                attractSpeed * Time.fixedDeltaTime
            );

            rb.MovePosition(next);
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = savedGravity;
        rb.collisionDetectionMode = savedCollisionMode;

        player.enabled = hadController;
    }

    private void AutoBindVisuals()
    {
        if (idleVisual == null)
            idleVisual = FindChildRecursiveByName(transform, "常态");

        if (activeVisual == null)
            activeVisual = FindChildRecursiveByName(transform, "触发态");
    }

    private Transform FindChildRecursiveByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name == targetName)
                return child;

            Transform result = FindChildRecursiveByName(child, targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    private void CacheActiveVisualState()
    {
        if (activeVisual == null)
            return;

        activeOriginalParent = activeVisual.parent;
        activeOriginalSiblingIndex = activeVisual.GetSiblingIndex();
        activeStartLocalPosition = activeVisual.localPosition;
        activeStartLocalRotation = activeVisual.localRotation;
        activeStartLocalScale = activeVisual.localScale;
    }

    private void SetIdleState()
    {
        if (triggerCol != null)
            triggerCol.enabled = true;

        if (idleVisual != null)
            idleVisual.gameObject.SetActive(true);

        if (activeVisual != null)
        {
            RestoreActiveVisualTransform();
            activeVisual.gameObject.SetActive(false);
        }
    }

    private void SetTransportVisualState(Transform playerTf)
    {
        if (triggerCol != null)
            triggerCol.enabled = false;

        if (idleVisual != null)
            idleVisual.gameObject.SetActive(false);

        if (activeVisual != null)
        {
            activeVisual.gameObject.SetActive(true);

            if (detachActiveVisualWhileTransporting)
            {
                CacheActiveVisualState();
                activeVisual.SetParent(null, true);
            }

            activeVisual.localScale = new Vector3(
                activeStartLocalScale.x * activeVisualScaleMultiplier.x,
                activeStartLocalScale.y * activeVisualScaleMultiplier.y,
                activeStartLocalScale.z * activeVisualScaleMultiplier.z
            );

            if (followRoutine != null)
                StopCoroutine(followRoutine);

            followRoutine = StartCoroutine(FollowActiveVisualRoutine(playerTf));
        }
    }

    private void EndTransportVisualState()
    {
        if (followRoutine != null)
        {
            StopCoroutine(followRoutine);
            followRoutine = null;
        }

        if (activeVisual != null)
            activeVisual.gameObject.SetActive(false);
    }

    private IEnumerator FollowActiveVisualRoutine(Transform playerTf)
    {
        while (playerTf != null)
        {
            if (activeVisual != null)
                activeVisual.position = playerTf.position + activeVisualOffset;

            yield return null;
        }
    }

    private void RestoreActiveVisualTransform()
    {
        if (activeVisual == null)
            return;

        if (detachActiveVisualWhileTransporting)
        {
            Transform parentToRestore = activeOriginalParent != null ? activeOriginalParent : transform;
            activeVisual.SetParent(parentToRestore, false);
            activeVisual.SetSiblingIndex(activeOriginalSiblingIndex);
        }

        activeVisual.localPosition = activeStartLocalPosition;
        activeVisual.localRotation = activeStartLocalRotation;
        activeVisual.localScale = activeStartLocalScale;
    }

    private Vector2 GetFixedDirection()
    {
        switch (transportDirection)
        {
            case TransportDirection.Up:
                return Vector2.up;
            case TransportDirection.Right:
                return Vector2.right;
            case TransportDirection.Down:
                return Vector2.down;
            case TransportDirection.Left:
                return Vector2.left;
            case TransportDirection.UpRight:
                return new Vector2(1f, 1f).normalized;
            case TransportDirection.UpLeft:
                return new Vector2(-1f, 1f).normalized;
            case TransportDirection.DownRight:
                return new Vector2(1f, -1f).normalized;
            case TransportDirection.DownLeft:
                return new Vector2(-1f, -1f).normalized;
            case TransportDirection.Custom:
                if (customDirection.sqrMagnitude <= 0.001f)
                    return Vector2.right;
                return customDirection.normalized;
        }

        return Vector2.right;
    }

    private IEnumerator FallbackTransportRoutine(
        PlayerController player,
        Vector2 direction,
        float distance,
        float speed,
        bool canJumpEscape,
        float escapeForce)
    {
        if (player == null)
            yield break;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null)
            yield break;

        direction = direction.normalized;
        speed = Mathf.Max(0.01f, speed);
        distance = Mathf.Max(0.01f, distance);

        float savedGravity = rb.gravityScale;
        CollisionDetectionMode2D savedCollisionMode = rb.collisionDetectionMode;

        player.enabled = false;

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        Vector2 start = rb.position;
        Vector2 target = start + direction * distance;
        bool escaped = false;

        while (Vector2.Distance(rb.position, target) > 0.02f)
        {
            if (canJumpEscape && IsJumpEscapePressed())
            {
                escaped = true;
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
        }

        if (!escaped)
        {
            rb.MovePosition(target);
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        rb.gravityScale = savedGravity;
        rb.collisionDetectionMode = savedCollisionMode;
        rb.angularVelocity = 0f;

        player.enabled = true;

        if (escaped)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Abs(escapeForce));
    }

    private bool IsJumpEscapePressed()
    {
        return Input.GetKeyDown(KeyCode.Space) ||
               Input.GetKeyDown(KeyCode.W) ||
               Input.GetKeyDown(KeyCode.UpArrow);
    }

    void OnDrawGizmos()
    {
        if (!showPreviewWhenNotSelected)
            return;

        DrawDirectionGizmo(new Color(0.3f, 1f, 1f, 0.35f), false);
    }

    void OnDrawGizmosSelected()
    {
        DrawDirectionGizmo(new Color(0.3f, 1f, 1f, 1f), true);
    }

    private void DrawDirectionGizmo(Color color, bool drawArrow)
    {
        Vector2 dir = GetFixedDirection();
        if (dir.sqrMagnitude <= 0.001f)
            return;

        float previewDistance = transportDistance > 0f ? transportDistance : 1f;

        Vector3 start = transform.position;
        Vector3 end = start + (Vector3)(dir.normalized * previewDistance);

        Gizmos.color = color;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, 0.12f);

        if (!drawArrow)
            return;

        Vector3 arrowDir = (end - start).normalized;
        Vector3 right = Quaternion.Euler(0f, 0f, 160f) * arrowDir;
        Vector3 left = Quaternion.Euler(0f, 0f, -160f) * arrowDir;

        float arrowSize = Mathf.Max(0.05f, previewArrowSize);
        Gizmos.DrawLine(end, end + right * arrowSize);
        Gizmos.DrawLine(end, end + left * arrowSize);
    }
}