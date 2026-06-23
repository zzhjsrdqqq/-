using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerBubbleResponder : MonoBehaviour
{
    [Header("玩家默认重力")]
    public float defaultGravityScale = 1f;

    private Rigidbody2D rb;
    private PlayerController playerController;

    private CollisionDetectionMode2D savedCollisionMode;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController>();

        if (rb != null && rb.gravityScale > 0f)
            defaultGravityScale = rb.gravityScale;

        if (rb != null)
            savedCollisionMode = rb.collisionDetectionMode;
    }

    public IEnumerator AttractToPoint(
        Transform target,
        float attractSpeed,
        float stopDistance,
        float maxTime,
        bool disablePlayerControl = true)
    {
        if (target == null) yield break;
        if (rb == null) yield break;

        savedCollisionMode = rb.collisionDetectionMode;

        if (disablePlayerControl && playerController != null)
            playerController.enabled = false;

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        float timer = 0f;
        attractSpeed = Mathf.Max(0.01f, attractSpeed);
        stopDistance = Mathf.Max(0.001f, stopDistance);
        maxTime = Mathf.Max(0.01f, maxTime);

        while (target != null && timer < maxTime)
        {
            Vector2 next = Vector2.MoveTowards(
                rb.position,
                target.position,
                attractSpeed * Time.fixedDeltaTime
            );

            rb.MovePosition(next);
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            if (Vector2.Distance(rb.position, target.position) <= stopDistance)
                break;

            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        ForceRestorePhysics();

        if (disablePlayerControl && playerController != null)
            playerController.enabled = true;
    }

    public void TriggerLaunch(Vector2 direction, float force, float controlLockTime = 0.12f)
    {
        StartCoroutine(LaunchRoutine(direction, force, controlLockTime));
    }

    private IEnumerator LaunchRoutine(Vector2 direction, float force, float controlLockTime)
    {
        if (rb == null)
            yield break;

        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;

        direction.Normalize();

        savedCollisionMode = rb.collisionDetectionMode;

        if (playerController != null)
        {
            // 这里提前刷新能力，并清掉旧跳跃缓存。
            // 不再放到弹射结束后刷新，避免斜向弹射结束时触发额外上冲。
            playerController.RefreshDashAndJump();
            playerController.enabled = false;
        }

        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.angularVelocity = 0f;

        Vector2 launchVelocity = direction * force;

        float timer = 0f;
        controlLockTime = Mathf.Max(0f, controlLockTime);

        if (controlLockTime <= 0f)
        {
            rb.linearVelocity = launchVelocity;
            yield return null;
        }
        else
        {
            while (timer < controlLockTime)
            {
                // 弹射锁定期间固定速度，确保是真正的“斜冲”，不是斜抛。
                rb.linearVelocity = launchVelocity;
                rb.angularVelocity = 0f;

                timer += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        ForceRestorePhysics();

        // 只处理“斜上弹射”的残余上升速度。
        // 纯向上弹射不受影响。
        RemoveUpwardCarryAfterDiagonalLaunch(direction);

        if (playerController != null)
            playerController.enabled = true;
    }

    private void RemoveUpwardCarryAfterDiagonalLaunch(Vector2 direction)
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

    public void ForceRestorePhysics()
    {
        if (rb == null)
            return;

        rb.gravityScale = defaultGravityScale;
        rb.collisionDetectionMode = savedCollisionMode;
        rb.angularVelocity = 0f;
    }
}