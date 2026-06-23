using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class SpringPad : MonoBehaviour
{
    public enum TriggerSide
    {
        TopOnly,        // 只从上方触发
        BottomOnly,     // 只从下方触发
        LeftOnly,       // 只从左侧触发
        RightOnly,      // 只从右侧触发
        TopAndBottom,   // 上下触发
        LeftAndRight,   // 左右触发
        AnySide         // 任意方向触发
    }

    public enum BounceDirectionMode
    {
        Up,
        Down,
        Left,
        Right,
        AwayFromPad,    // 按玩家接触方向，远离弹跳板弹出
        Custom          // 使用自定义方向
    }

    [Header("触发方向")]
    public TriggerSide triggerSide = TriggerSide.TopOnly;

    [Header("弹射方向")]
    [Tooltip("玩家触发后往哪个方向弹")]
    public BounceDirectionMode bounceDirectionMode = BounceDirectionMode.Up;

    [Tooltip("Bounce Direction Mode = Custom 时使用")]
    public Vector2 customBounceDirection = Vector2.up;

    [Header("弹跳功能")]
    public float bounceForce = 18f;

    [Tooltip("是否保留玩家原来的水平速度。侧面弹时一般建议关闭")]
    public bool keepHorizontalSpeed = false;

    [Tooltip("是否保留玩家原来的竖直速度。上下弹时一般建议关闭")]
    public bool keepVerticalSpeed = false;

    [Tooltip("只允许玩家下落时触发。想让下方/侧面触发，建议关闭")]
    public bool onlyBounceWhenFalling = false;

    public bool refreshDashAndJump = true;
    public bool playJuice = true;

    [Header("触发冷却")]
    public float triggerCooldown = 0.08f;

    [Header("主体显示")]
    public Transform padVisual;

    [Header("白闪层（可选）")]
    public SpriteRenderer flashRenderer;
    public float flashMaxAlpha = 0.8f;
    public float flashDuration = 0.08f;

    [Header("压缩动画")]
    public float pressDepth = 0.12f;
    public float squashY = 0.55f;
    public float stretchX = 1.18f;
    public float pressTime = 0.035f;

    [Header("回弹动画")]
    public float reboundHeight = 0.06f;
    public float reboundStretchY = 1.12f;
    public float reboundSquashX = 0.94f;
    public float reboundTime = 0.07f;
    public float settleTime = 0.06f;

    [Header("调试")]
    public bool debugLog = false;

    private Vector3 originalScale;
    private Vector3 originalLocalPos;
    private Coroutine animRoutine;
    private Coroutine flashRoutine;
    private float lastTriggerTime = -999f;

    void Awake()
    {
        if (padVisual == null)
            padVisual = transform;

        originalScale = padVisual.localScale;
        originalLocalPos = padVisual.localPosition;

        if (flashRenderer != null)
        {
            Color c = flashRenderer.color;
            flashRenderer.color = new Color(c.r, c.g, c.b, 0f);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryBounce(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryBounce(collision);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryBounce(other, GetFallbackSide(other));
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryBounce(other, GetFallbackSide(other));
    }

    private void TryBounce(Collision2D collision)
    {
        if (collision == null)
            return;

        ContactSide contactSide = GetContactSide(collision);

        if (!IsAllowedSide(contactSide))
            return;

        TryBounce(collision.collider, contactSide);
    }

    private void TryBounce(Collider2D hitCollider, ContactSide contactSide)
    {
        if (Time.time < lastTriggerTime + triggerCooldown)
            return;

        if (hitCollider == null)
            return;

        PlayerController player = hitCollider.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null)
            return;

        if (onlyBounceWhenFalling && rb.linearVelocity.y > 0f)
            return;

        lastTriggerTime = Time.time;

        if (refreshDashAndJump)
            player.RefreshDashAndJump();

        Vector2 bounceDir = GetBounceDirection(contactSide);
        if (bounceDir.sqrMagnitude <= 0.001f)
            bounceDir = Vector2.up;

        bounceDir.Normalize();

        Vector2 oldVelocity = rb.linearVelocity;
        Vector2 newVelocity = bounceDir * bounceForce;

        if (keepHorizontalSpeed)
            newVelocity.x = oldVelocity.x;

        if (keepVerticalSpeed)
            newVelocity.y = oldVelocity.y;

        rb.linearVelocity = newVelocity;

        if (playJuice)
        {
            PlayerJuiceEffects juice = hitCollider.GetComponentInParent<PlayerJuiceEffects>();
            if (juice != null)
                juice.PlaySpringPadJuice();
        }

        PlayPadAnimation(contactSide);
        PlayFlash();

        if (debugLog)
            Debug.Log($"SpringPad 触发：{gameObject.name}, 接触方向 = {contactSide}, 弹射速度 = {newVelocity}");
    }

    private enum ContactSide
    {
        Top,
        Bottom,
        Left,
        Right,
        Unknown
    }

    private ContactSide GetContactSide(Collision2D collision)
    {
        ContactSide bestSide = ContactSide.Unknown;
        float bestDot = -999f;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            Vector2 n = contact.normal;

            float topDot = Vector2.Dot(n, Vector2.down);
            if (topDot > bestDot)
            {
                bestDot = topDot;
                bestSide = ContactSide.Top;
            }

            float bottomDot = Vector2.Dot(n, Vector2.up);
            if (bottomDot > bestDot)
            {
                bestDot = bottomDot;
                bestSide = ContactSide.Bottom;
            }

            float leftDot = Vector2.Dot(n, Vector2.right);
            if (leftDot > bestDot)
            {
                bestDot = leftDot;
                bestSide = ContactSide.Left;
            }

            float rightDot = Vector2.Dot(n, Vector2.left);
            if (rightDot > bestDot)
            {
                bestDot = rightDot;
                bestSide = ContactSide.Right;
            }
        }

        return bestSide;
    }

    private ContactSide GetFallbackSide(Collider2D other)
    {
        if (other == null)
            return ContactSide.Unknown;

        Vector2 playerPos = other.bounds.center;
        Vector2 padPos = transform.position;
        Vector2 delta = playerPos - padPos;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            if (delta.x < 0f)
                return ContactSide.Left;

            return ContactSide.Right;
        }
        else
        {
            if (delta.y > 0f)
                return ContactSide.Top;

            return ContactSide.Bottom;
        }
    }

    private bool IsAllowedSide(ContactSide side)
    {
        switch (triggerSide)
        {
            case TriggerSide.TopOnly:
                return side == ContactSide.Top;

            case TriggerSide.BottomOnly:
                return side == ContactSide.Bottom;

            case TriggerSide.LeftOnly:
                return side == ContactSide.Left;

            case TriggerSide.RightOnly:
                return side == ContactSide.Right;

            case TriggerSide.TopAndBottom:
                return side == ContactSide.Top || side == ContactSide.Bottom;

            case TriggerSide.LeftAndRight:
                return side == ContactSide.Left || side == ContactSide.Right;

            case TriggerSide.AnySide:
                return true;
        }

        return false;
    }

    private Vector2 GetBounceDirection(ContactSide contactSide)
    {
        switch (bounceDirectionMode)
        {
            case BounceDirectionMode.Up:
                return Vector2.up;

            case BounceDirectionMode.Down:
                return Vector2.down;

            case BounceDirectionMode.Left:
                return Vector2.left;

            case BounceDirectionMode.Right:
                return Vector2.right;

            case BounceDirectionMode.AwayFromPad:
                return GetAwayDirection(contactSide);

            case BounceDirectionMode.Custom:
                return customBounceDirection;
        }

        return Vector2.up;
    }

    private Vector2 GetAwayDirection(ContactSide contactSide)
    {
        switch (contactSide)
        {
            case ContactSide.Top:
                return Vector2.up;

            case ContactSide.Bottom:
                return Vector2.down;

            case ContactSide.Left:
                return Vector2.left;

            case ContactSide.Right:
                return Vector2.right;
        }

        return Vector2.up;
    }

    private void PlayPadAnimation(ContactSide contactSide)
    {
        if (padVisual == null)
            return;

        if (animRoutine != null)
            StopCoroutine(animRoutine);

        padVisual.localScale = originalScale;
        padVisual.localPosition = originalLocalPos;
        animRoutine = StartCoroutine(PadAnimationRoutine(contactSide));
    }

    private IEnumerator PadAnimationRoutine(ContactSide contactSide)
    {
        Vector3 pressDir = GetPressDirection(contactSide);

        Vector3 pressedScale = GetPressedScale(contactSide);
        Vector3 pressedPos = originalLocalPos + pressDir * pressDepth;

        Vector3 reboundScale = GetReboundScale(contactSide);
        Vector3 reboundPos = originalLocalPos - pressDir * reboundHeight;

        float t = 0f;
        while (t < pressTime)
        {
            t += Time.deltaTime;
            float k = pressTime > 0f ? t / pressTime : 1f;
            padVisual.localScale = Vector3.Lerp(originalScale, pressedScale, k);
            padVisual.localPosition = Vector3.Lerp(originalLocalPos, pressedPos, k);
            yield return null;
        }

        t = 0f;
        while (t < reboundTime)
        {
            t += Time.deltaTime;
            float k = reboundTime > 0f ? t / reboundTime : 1f;
            padVisual.localScale = Vector3.Lerp(pressedScale, reboundScale, k);
            padVisual.localPosition = Vector3.Lerp(pressedPos, reboundPos, k);
            yield return null;
        }

        t = 0f;
        while (t < settleTime)
        {
            t += Time.deltaTime;
            float k = settleTime > 0f ? t / settleTime : 1f;
            padVisual.localScale = Vector3.Lerp(reboundScale, originalScale, k);
            padVisual.localPosition = Vector3.Lerp(reboundPos, originalLocalPos, k);
            yield return null;
        }

        padVisual.localScale = originalScale;
        padVisual.localPosition = originalLocalPos;
        animRoutine = null;
    }

    private Vector3 GetPressDirection(ContactSide contactSide)
    {
        switch (contactSide)
        {
            case ContactSide.Top:
                return Vector3.down;

            case ContactSide.Bottom:
                return Vector3.up;

            case ContactSide.Left:
                return Vector3.right;

            case ContactSide.Right:
                return Vector3.left;
        }

        return Vector3.down;
    }

    private Vector3 GetPressedScale(ContactSide contactSide)
    {
        bool horizontalPress =
            contactSide == ContactSide.Left ||
            contactSide == ContactSide.Right;

        if (horizontalPress)
        {
            return new Vector3(
                originalScale.x * squashY,
                originalScale.y * stretchX,
                originalScale.z
            );
        }

        return new Vector3(
            originalScale.x * stretchX,
            originalScale.y * squashY,
            originalScale.z
        );
    }

    private Vector3 GetReboundScale(ContactSide contactSide)
    {
        bool horizontalPress =
            contactSide == ContactSide.Left ||
            contactSide == ContactSide.Right;

        if (horizontalPress)
        {
            return new Vector3(
                originalScale.x * reboundStretchY,
                originalScale.y * reboundSquashX,
                originalScale.z
            );
        }

        return new Vector3(
            originalScale.x * reboundSquashX,
            originalScale.y * reboundStretchY,
            originalScale.z
        );
    }

    private void PlayFlash()
    {
        if (flashRenderer == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        Color baseColor = flashRenderer.color;

        float t = 0f;
        while (t < flashDuration)
        {
            t += Time.deltaTime;
            float k = flashDuration > 0f ? t / flashDuration : 1f;
            float a = Mathf.Lerp(flashMaxAlpha, 0f, k);
            flashRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            yield return null;
        }

        flashRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        flashRoutine = null;
    }

    void OnDisable()
    {
        if (padVisual != null)
        {
            padVisual.localScale = originalScale;
            padVisual.localPosition = originalLocalPos;
        }

        if (flashRenderer != null)
        {
            Color c = flashRenderer.color;
            flashRenderer.color = new Color(c.r, c.g, c.b, 0f);
        }
    }
}