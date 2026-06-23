using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerWallBounceControl : MonoBehaviour
{
    [Header("默认弹墙后的水平速度")]
    public float bounceSpeed = 14f;

    [Header("默认按空格后的45度斜上弹射速度")]
    public float diagonalBounceSpeed = 14f;

    [Header("锁控制时间")]
    public float controlLockTime = 0.12f;

    private Rigidbody2D rb;
    private PlayerController playerController;
    private PlayerJuiceEffects juice;

    private Vector2 lastVelocity;
    private bool isLocked = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController>();
        juice = GetComponent<PlayerJuiceEffects>();
    }

    void FixedUpdate()
    {
        if (rb != null)
            lastVelocity = rb.linearVelocity;
    }

    // 保留原来的 API，不破坏旧调用
    public void TryBounceHorizontal(Vector2 wallNormal)
    {
        TryBounceHorizontal(wallNormal, bounceSpeed, diagonalBounceSpeed);
    }

    // 新增：允许墙体传入自己的水平弹力
    public void TryBounceHorizontal(Vector2 wallNormal, float customBounceSpeed)
    {
        TryBounceHorizontal(wallNormal, customBounceSpeed, diagonalBounceSpeed);
    }

    // 新增：允许墙体同时传入自己的水平弹力和斜上弹力
    public void TryBounceHorizontal(Vector2 wallNormal, float customBounceSpeed, float customDiagonalBounceSpeed)
    {
        if (isLocked) return;
        if (playerController == null) return;
        if (rb == null) return;

        float currentSpeed = lastVelocity.magnitude;
        if (currentSpeed <= playerController.moveSpeed)
            return;

        if (Vector2.Dot(lastVelocity, -wallNormal) <= 0f)
            return;

        float finalBounceSpeed = Mathf.Abs(customBounceSpeed);
        float finalDiagonalBounceSpeed = Mathf.Abs(customDiagonalBounceSpeed);

        Vector2 bounceVelocity = new Vector2(wallNormal.x * finalBounceSpeed, lastVelocity.y);
        StartCoroutine(BounceRoutine(bounceVelocity, wallNormal.x, finalDiagonalBounceSpeed));
    }

    private IEnumerator BounceRoutine(Vector2 bounceVelocity, float awayX, float finalDiagonalBounceSpeed)
    {
        isLocked = true;

        playerController.RefreshDashAndJump();
        playerController.enabled = false;

        rb.linearVelocity = bounceVelocity;

        if (juice != null)
            juice.PlayWallBounceJuice();

        float timer = 0f;
        bool changedToDiagonal = false;

        while (timer < controlLockTime)
        {
            if (!changedToDiagonal &&
                (Input.GetKeyDown(KeyCode.Space) ||
                 Input.GetKeyDown(KeyCode.W) ||
                 Input.GetKeyDown(KeyCode.UpArrow)))
            {
                float comp = finalDiagonalBounceSpeed / Mathf.Sqrt(2f);
                rb.linearVelocity = new Vector2(awayX * comp, comp);
                changedToDiagonal = true;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        playerController.enabled = true;
        isLocked = false;
    }
}