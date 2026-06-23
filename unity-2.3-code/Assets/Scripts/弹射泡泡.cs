using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class LaunchBubble : MonoBehaviour
{
    [Header("吸附设置")]
    public bool useAttract = true;
    public float attractSpeed = 18f;
    public float attractStopDistance = 0.05f;
    public float maxAttractTime = 0.2f;

    [Header("发射方向")]
    public Vector2 launchDirection = new Vector2(1f, 1f);

    [Header("发射力度")]
    public float launchForce = 16f;

    [Header("控制锁定时间")]
    public float controlLockTime = 0.12f;

    [Header("是否使用后重生")]
    public bool respawnBubble = true;

    [Header("重生时间")]
    public float respawnTime = 2f;

    [Header("编辑器预览")]
    [Tooltip("未选中时是否也显示淡色方向线")]
    public bool showPreviewWhenNotSelected = false;

    [Tooltip("方向线的预览长度。小于等于 0 时自动按发射力度估算")]
    public float previewLength = 0f;

    [Tooltip("预览箭头大小")]
    public float previewArrowSize = 0.35f;

    private Collider2D col;
    private SpriteRenderer sr;
    private bool isUsed = false;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

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
        if (isUsed) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        PlayerTransportResponder transport = other.GetComponentInParent<PlayerTransportResponder>();
        if (transport != null && transport.IsTransporting) return;

        isUsed = true;
        StartCoroutine(ActivateRoutine(other, player));
    }

    private IEnumerator ActivateRoutine(Collider2D other, PlayerController player)
    {
        PlayerBubbleResponder responder = other.GetComponentInParent<PlayerBubbleResponder>();

        if (useAttract && responder != null)
        {
            yield return responder.AttractToPoint(
                transform,
                attractSpeed,
                attractStopDistance,
                maxAttractTime,
                true
            );
        }

        BubbleTimeBonusUtility.TryGiveTime(this, other);

        PlayerJuiceEffects juice = other.GetComponentInParent<PlayerJuiceEffects>();
        if (juice != null)
            juice.PlaySpecialBubbleJuice();

        Vector2 dir = launchDirection.sqrMagnitude > 0.001f
            ? launchDirection.normalized
            : Vector2.up;

        if (responder != null)
        {
            responder.TriggerLaunch(dir, launchForce, controlLockTime);
        }
        else
        {
            StartCoroutine(FallbackLaunchRoutine(player, dir, launchForce, controlLockTime));
        }

        if (respawnBubble)
            StartCoroutine(RespawnRoutine());
        else
            Destroy(gameObject);
    }

    private IEnumerator FallbackLaunchRoutine(
        PlayerController player,
        Vector2 direction,
        float force,
        float lockTime)
    {
        if (player == null)
            yield break;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null)
            yield break;

        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;

        direction.Normalize();

        float savedGravity = rb.gravityScale;
        CollisionDetectionMode2D savedCollisionMode = rb.collisionDetectionMode;

        player.RefreshDashAndJump();
        player.enabled = false;

        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.angularVelocity = 0f;

        Vector2 launchVelocity = direction * force;

        float timer = 0f;
        lockTime = Mathf.Max(0f, lockTime);

        if (lockTime <= 0f)
        {
            rb.linearVelocity = launchVelocity;
            yield return null;
        }
        else
        {
            while (timer < lockTime)
            {
                rb.linearVelocity = launchVelocity;
                rb.angularVelocity = 0f;

                timer += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        rb.gravityScale = savedGravity;
        rb.collisionDetectionMode = savedCollisionMode;
        rb.angularVelocity = 0f;

        RemoveUpwardCarryAfterDiagonalLaunch(rb, direction);

        player.enabled = true;
    }

    private void RemoveUpwardCarryAfterDiagonalLaunch(Rigidbody2D rb, Vector2 direction)
    {
        if (rb == null)
            return;

        bool isUpDiagonal =
            Mathf.Abs(direction.x) > 0.05f &&
            direction.y > 0.05f;

        if (!isUpDiagonal)
            return;

        if (rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
    }

    IEnumerator RespawnRoutine()
    {
        if (col != null) col.enabled = false;
        if (sr != null) sr.enabled = false;

        yield return new WaitForSeconds(respawnTime);

        if (col != null) col.enabled = true;
        if (sr != null) sr.enabled = true;

        isUsed = false;
    }

    void OnDrawGizmos()
    {
        if (!showPreviewWhenNotSelected)
            return;

        DrawLaunchGizmo(new Color(1f, 0.92f, 0.2f, 0.35f), false);
    }

    void OnDrawGizmosSelected()
    {
        DrawLaunchGizmo(new Color(1f, 0.92f, 0.2f, 1f), true);
    }

    private void DrawLaunchGizmo(Color color, bool drawArrow)
    {
        Vector2 dir = launchDirection.sqrMagnitude > 0.001f
            ? launchDirection.normalized
            : Vector2.up;

        float length = previewLength > 0f
            ? previewLength
            : Mathf.Clamp(launchForce * 0.12f, 1f, 3f);

        Vector3 start = transform.position;
        Vector3 end = start + (Vector3)(dir * length);

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