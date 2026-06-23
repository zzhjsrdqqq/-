using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("能力开关")]
    [Tooltip("是否允许二段跳。关闭后玩家只能进行普通跳跃。")]
    public bool enableDoubleJump = true;

    [Tooltip("是否允许冲刺。关闭后 LeftShift 不会触发冲刺。")]
    public bool enableDash = true;

    [Tooltip("是否允许斜上冲刺。关闭后冲刺过程中按跳跃不会转成45度斜上冲。")]
    public bool enableDiagonalUpDash = true;

    [Header("移动设置")]
    public float moveSpeed = 7f;
    public float jumpForce = 14f;
    public float crouchSpeedMultiplier = 0.5f;

    [Header("跳跃设置")]
    [Tooltip("总跳跃次数。1=只能跳一次，2=可二段跳。是否真的能二段跳还受 Enable Double Jump 控制。")]
    public int maxJumps = 2;

    [Header("跳跃手感")]
    [Tooltip("离开地面后还能起跳的容错时间")]
    public float coyoteTime = 0.12f;

    [Tooltip("提前按下跳跃时的输入缓存时间")]
    public float jumpBufferTime = 0.12f;

    [Header("地面检测")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundLayer;

    [Header("重生设置")]
    public Transform spawnPoint;

    [Header("掉出屏幕判定")]
    public bool killWhenBelowScreen = false;
    public float belowScreenOffset = 1f;

    [Header("冲刺设置")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 1f;

    [Tooltip("开始冲刺后，这段时间内按空格/W/↑可转成45度斜上冲")]
    public float dashDiagonalInputWindow = 0.12f;

    [Header("特效")]
    public GameObject jumpDustPrefab;
    public GameObject deathBubbleBurstPrefab;
    public TrailRenderer trail;

    [Header("渲染层级")]
    [Tooltip("玩家及其所有子物体的 Order in Layer")]
    public int renderOrder = 1000;

    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public bool FacingRight => facingRight;

    public bool CanDoubleJump => enableDoubleJump;
    public bool CanDash => enableDash;
    public bool CanDiagonalUpDash => enableDiagonalUpDash;

    private Rigidbody2D rb;
    private Animator anim;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isCrouching;
    private float moveInput;
    private bool facingRight = true;

    private int jumpCount = 0;
    private float coyoteTimer = 0f;
    private float jumpBufferTimer = 0f;
    private bool refreshedInAir = false;

    private bool isDashing = false;
    private float dashTimer = 0f;
    private float dashCooldownTimer = 0f;
    private float dashDiagonalTimer = 0f;
    private bool dashChangedToDiagonal = false;
    private Vector2 currentDashVelocity = Vector2.zero;

    private PlayerTeleportState teleportState;
    private PlayerCheckpointState checkpointState;
    private PlayerJuiceEffects juice;
    private BubblePlayerVisual bubbleVisual;

    private Vector3 spawnPosition;

    void OnEnable()
    {
        wasGrounded = false;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        teleportState = GetComponent<PlayerTeleportState>();
        checkpointState = GetComponent<PlayerCheckpointState>();
        juice = GetComponent<PlayerJuiceEffects>();
        bubbleVisual = GetComponent<BubblePlayerVisual>();

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.sortingOrder = renderOrder;

        foreach (TrailRenderer tr in GetComponentsInChildren<TrailRenderer>(true))
            tr.sortingOrder = renderOrder;

        if (spawnPoint == null)
            spawnPoint = transform;

        spawnPosition = spawnPoint.position;
    }

    public void Respawn()
    {
        Vector3 deathPos = transform.position;

        if (deathBubbleBurstPrefab != null)
        {
            GameObject fx = Instantiate(deathBubbleBurstPrefab, deathPos, Quaternion.identity);
            Destroy(fx, 2f);
        }

        isDashing = false;
        rb.linearVelocity = Vector2.zero;

        jumpCount = 0;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        refreshedInAir = false;

        Vector3 targetPos = spawnPosition;

        if (checkpointState != null)
            targetPos = checkpointState.GetRespawnPosition();

        transform.position = targetPos;

        CameraTeleportSnap camSnap = Camera.main != null ? Camera.main.GetComponent<CameraTeleportSnap>() : null;
        if (camSnap != null)
            camSnap.RequestSnap();

        PlayerCountdownTimer timer = GetComponent<PlayerCountdownTimer>();
        if (timer != null && timer.resetTimerWhenRespawn)
            timer.ResetTimer();
    }


    public void RefreshDashAndJump()
    {
        jumpCount = 0;
        dashCooldownTimer = 0f;
        jumpBufferTimer = 0f;

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
            refreshedInAir = false;
        }
        else
        {
            coyoteTimer = 0f;
            refreshedInAir = true;
        }
    }

    public void SetDoubleJumpEnabled(bool enabled)
    {
        enableDoubleJump = enabled;

        if (!enableDoubleJump && jumpCount > 1)
            jumpCount = 1;
    }

    public void SetDashEnabled(bool enabled)
    {
        enableDash = enabled;

        if (!enableDash)
        {
            isDashing = false;
            dashTimer = 0f;
            dashDiagonalTimer = 0f;
            dashChangedToDiagonal = false;
            currentDashVelocity = Vector2.zero;
        }
    }

    public void SetDiagonalUpDashEnabled(bool enabled)
    {
        enableDiagonalUpDash = enabled;

        if (!enableDiagonalUpDash)
        {
            dashDiagonalTimer = 0f;
            dashChangedToDiagonal = false;
        }
    }

    public void UnlockDoubleJump()
    {
        SetDoubleJumpEnabled(true);
    }

    public void LockDoubleJump()
    {
        SetDoubleJumpEnabled(false);
    }

    public void UnlockDash()
    {
        SetDashEnabled(true);
    }

    public void LockDash()
    {
        SetDashEnabled(false);
    }

    public void UnlockDiagonalUpDash()
    {
        SetDiagonalUpDashEnabled(true);
    }

    public void LockDiagonalUpDash()
    {
        SetDiagonalUpDashEnabled(false);
    }

    public void UnlockAllAbilities()
    {
        SetDoubleJumpEnabled(true);
        SetDashEnabled(true);
        SetDiagonalUpDashEnabled(true);
    }

    public void LockAllAdvancedAbilities()
    {
        SetDoubleJumpEnabled(false);
        SetDashEnabled(false);
        SetDiagonalUpDashEnabled(false);
    }

    void Update()
    {
        bool justTeleported = teleportState != null && !teleportState.CanTeleport();

        if (killWhenBelowScreen && !justTeleported && Camera.main != null)
        {
            float screenBottom = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, 0)).y;

            if (transform.position.y < screenBottom - belowScreenOffset)
                Respawn();
        }

        moveInput = Input.GetAxisRaw("Horizontal");
        isCrouching = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

        bool jumpPressedThisFrame =
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.W) ||
            Input.GetKeyDown(KeyCode.UpArrow);

        if (jumpPressedThisFrame)
            jumpBufferTimer = jumpBufferTime;
        else if (jumpBufferTimer > 0f)
            jumpBufferTimer -= Time.deltaTime;

        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            dashDiagonalTimer -= Time.deltaTime;

            if (enableDiagonalUpDash &&
                !dashChangedToDiagonal &&
                dashDiagonalTimer > 0f &&
                jumpPressedThisFrame)
            {
                float comp = dashSpeed / Mathf.Sqrt(2f);
                float dirX = facingRight ? 1f : -1f;

                currentDashVelocity = new Vector2(dirX * comp, comp);
                dashChangedToDiagonal = true;
                jumpBufferTimer = 0f;
            }

            if (dashTimer <= 0f)
                isDashing = false;
        }

        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
            refreshedInAir = false;

            if (!wasGrounded)
                jumpCount = 0;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }

        wasGrounded = isGrounded;

        if (enableDash &&
            Input.GetKeyDown(KeyCode.LeftShift) &&
            !isDashing &&
            dashCooldownTimer <= 0f)
        {
            StartDash();
        }

        if (!isDashing)
        {
            bool canJumpFromGroundWindow = isGrounded || coyoteTimer > 0f;
            bool canStartJumpNormally = jumpCount == 0 && canJumpFromGroundWindow;
            bool canStartJumpFromRefresh = jumpCount == 0 && refreshedInAir;

            bool canExtraJump =
                enableDoubleJump &&
                jumpCount > 0 &&
                jumpCount < maxJumps;

            if (jumpBufferTimer > 0f &&
                (canStartJumpNormally || canStartJumpFromRefresh || canExtraJump))
            {
                bool isExtraJump = !canStartJumpNormally && !canStartJumpFromRefresh;

                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

                jumpCount++;
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;

                if (canStartJumpFromRefresh)
                    refreshedInAir = false;

                if (jumpDustPrefab != null && groundCheck != null)
                    Instantiate(jumpDustPrefab, groundCheck.position, Quaternion.identity);

                if (isExtraJump && juice != null)
                    juice.PlayDoubleJumpJuice();

                if (bubbleVisual != null)
                {
                    if (isExtraJump)
                        bubbleVisual.PlayDoubleJumpPop();
                    else
                        bubbleVisual.PlayNormalJumpPop();
                }
            }
        }

        if (moveInput > 0 && !facingRight)
            Flip();
        else if (moveInput < 0 && facingRight)
            Flip();

        if (anim != null)
        {
            anim.SetFloat("Speed", Mathf.Abs(moveInput));
            anim.SetBool("IsGrounded", isGrounded);
            anim.SetBool("IsCrouching", isCrouching);
            anim.SetFloat("VerticalSpeed", rb.linearVelocity.y);
            anim.SetBool("IsDashing", isDashing);
        }

        if (trail != null)
        {
            trail.emitting =
                isDashing ||
                Mathf.Abs(rb.linearVelocity.x) > 0.5f ||
                Mathf.Abs(rb.linearVelocity.y) > 1f;
        }
    }

    void FixedUpdate()
    {
        if (isDashing)
        {
            rb.linearVelocity = currentDashVelocity;
            return;
        }

        float speed = moveSpeed * (isCrouching ? crouchSpeedMultiplier : 1f);
        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
    }

    void StartDash()
    {
        if (!enableDash)
            return;

        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        dashDiagonalTimer = enableDiagonalUpDash ? dashDiagonalInputWindow : 0f;
        dashChangedToDiagonal = false;

        float dirX = facingRight ? 1f : -1f;
        currentDashVelocity = new Vector2(dirX * dashSpeed, 0f);

        rb.linearVelocity = currentDashVelocity;

        if (juice != null)
            juice.PlayDashJuice();

        if (bubbleVisual != null)
            bubbleVisual.PlayDashPop(currentDashVelocity);
    }

    void Flip()
    {
        facingRight = !facingRight;

        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (facingRight ? 1f : -1f);
        transform.localScale = s;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}