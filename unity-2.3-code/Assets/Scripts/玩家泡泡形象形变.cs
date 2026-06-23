using UnityEngine;

public class BubblePlayerVisual : MonoBehaviour
{
    [Header("引用")]
    public Rigidbody2D rb;
    public PlayerController playerController;

    public Transform bubbleVisualRoot;
    public Transform bubbleBody;
    public Transform bubbleCore;
    public Transform bubbleHighlight;

    public SpriteRenderer bodyRenderer;
    public SpriteRenderer coreRenderer;
    public SpriteRenderer highlightRenderer;

    [Header("排序")]
    public int bodyOrderOffset = 0;
    public int coreOrderOffset = 1;
    public int highlightOrderOffset = 2;

    [Header("平滑")]
    public float smoothSpeed = 8f;

    [Header("速度参考")]
    public float horizontalSpeedForMax = 7f;
    public float verticalSpeedForMax = 14f;
    public float dashSpeedForMax = 20f;

    [Header("待机呼吸")]
    public float idleBreathAmount = 0.05f;
    public float idleBreathSpeed = 2.8f;

    [Header("移动形变")]
    public float moveStretchX = 0.22f;
    public float moveSquashY = 0.10f;

    [Header("上升形变")]
    public float riseStretchY = 0.22f;
    public float riseSquashX = 0.10f;

    [Header("下落形变")]
    public float fallSquashY = 0.16f;
    public float fallStretchX = 0.12f;

    [Header("冲刺持续形变")]
    public float dashStretch = 0.28f;
    public float dashSquash = 0.12f;

    [Header("内核领先 / 高光滞后")]
    public float coreLagDistance = 0.03f;
    public float highlightLagDistance = 0.08f;
    public float highlightUpBias = 0.03f;

    [Header("内核回摆")]
    public float coreSpringStrength = 45f;
    public float coreSpringDamping = 10f;
    public float coreDirectionChangeKick = 0.05f;
    public float coreKickMinSpeed = 0.15f;

    [Header("落地压缩")]
    public float landMinImpact = 1.2f;
    public float landSquashX = 0.50f;
    public float landSquashY = 0.38f;
    public float landPunchDuration = 0.14f;
    public float minAirTimeForLandEffect = 0.05f;
    public float landEffectCooldown = 0.08f;

    [Header("普通起跳爆发")]
    public float jumpBurstY = 0.10f;
    public float jumpBurstX = 0.04f;
    public float jumpBurstDuration = 0.10f;

    [Header("二段跳爆发")]
    public float doubleJumpBurstY = 0.18f;
    public float doubleJumpBurstX = 0.08f;
    public float doubleJumpBurstDuration = 0.14f;

    [Header("冲刺爆发")]
    public float dashBurstStretch = 0.20f;
    public float dashBurstDuration = 0.12f;

    private Vector3 bodyBaseScale;
    private Vector3 coreBaseScale;
    private Vector3 highlightBaseScale;

    private Vector3 coreBasePos;
    private Vector3 highlightBasePos;

    private Vector3 currentBodyScale;
    private Vector3 currentCorePos;
    private Vector3 currentHighlightPos;

    private Vector3 currentCoreVelocity;
    private Vector2 lastMotionDirLocal = Vector2.right;

    private bool lastGrounded;
    private float airborneTime = 0f;
    private float maxFallSpeedThisAir = 0f;
    private float landCooldownTimer = 0f;

    private float landPunchTimer = 0f;
    private float landPunchStrength = 0f;

    private float jumpBurstTimer = 0f;
    private float jumpBurstDur = 0f;
    private float jumpBurstYAmount = 0f;
    private float jumpBurstXAmount = 0f;

    private float dashBurstTimer = 0f;
    private Vector2 dashBurstDirection = Vector2.right;

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (bubbleVisualRoot == null)
            bubbleVisualRoot = transform;

        if (bubbleBody != null)
            bodyBaseScale = bubbleBody.localScale;

        if (bubbleCore != null)
        {
            coreBaseScale = bubbleCore.localScale;
            coreBasePos = bubbleCore.localPosition;
        }

        if (bubbleHighlight != null)
        {
            highlightBaseScale = bubbleHighlight.localScale;
            highlightBasePos = bubbleHighlight.localPosition;
        }

        currentBodyScale = bodyBaseScale;
        currentCorePos = coreBasePos;
        currentHighlightPos = highlightBasePos;
        currentCoreVelocity = Vector3.zero;
    }

    void LateUpdate()
    {
        if (rb == null || bubbleBody == null)
            return;

        UpdateSorting();

        Vector2 velocity = rb.linearVelocity;

        bool grounded = playerController != null ? playerController.IsGrounded : Mathf.Abs(velocity.y) < 0.01f;
        bool dashing = playerController != null ? playerController.IsDashing : velocity.magnitude > dashSpeedForMax * 0.75f;

        if (landCooldownTimer > 0f)
            landCooldownTimer -= Time.deltaTime;

        if (!grounded)
        {
            airborneTime += Time.deltaTime;

            float fallSpeed = Mathf.Max(0f, -velocity.y);
            if (fallSpeed > maxFallSpeedThisAir)
                maxFallSpeedThisAir = fallSpeed;
        }

        if (grounded && !lastGrounded)
        {
            if (airborneTime >= minAirTimeForLandEffect &&
                maxFallSpeedThisAir > landMinImpact &&
                landCooldownTimer <= 0f)
            {
                landPunchStrength = Mathf.Clamp01(
                    (maxFallSpeedThisAir - landMinImpact) /
                    Mathf.Max(0.01f, verticalSpeedForMax - landMinImpact)
                );

                landPunchTimer = landPunchDuration;
                landCooldownTimer = landEffectCooldown;

                currentBodyScale = new Vector3(
                    bodyBaseScale.x * (1f + landSquashX * 0.55f * landPunchStrength),
                    bodyBaseScale.y * (1f - landSquashY * 0.55f * landPunchStrength),
                    bodyBaseScale.z
                );
            }

            airborneTime = 0f;
            maxFallSpeedThisAir = 0f;
        }
        else if (grounded)
        {
            airborneTime = 0f;
            maxFallSpeedThisAir = 0f;
        }

        Vector3 targetBodyScale = bodyBaseScale;

        float absX = Mathf.Abs(velocity.x);
        float absY = Mathf.Abs(velocity.y);

        if (grounded && absX < 0.35f && absY < 0.35f && !dashing)
        {
            float breathe = 1f + Mathf.Sin(Time.time * idleBreathSpeed) * idleBreathAmount;
            targetBodyScale *= breathe;
        }

        if (absX > 0.05f)
        {
            float moveT = Mathf.Clamp01(absX / Mathf.Max(0.01f, horizontalSpeedForMax));
            targetBodyScale.x *= 1f + moveStretchX * moveT;
            targetBodyScale.y *= 1f - moveSquashY * moveT;
        }

        if (velocity.y > 0.05f)
        {
            float riseT = Mathf.Clamp01(velocity.y / Mathf.Max(0.01f, verticalSpeedForMax));
            targetBodyScale.y *= 1f + riseStretchY * riseT;
            targetBodyScale.x *= 1f - riseSquashX * riseT;
        }

        if (velocity.y < -0.05f)
        {
            float fallT = Mathf.Clamp01((-velocity.y) / Mathf.Max(0.01f, verticalSpeedForMax));
            targetBodyScale.y *= 1f - fallSquashY * fallT;
            targetBodyScale.x *= 1f + fallStretchX * fallT;
        }

        if (dashing)
        {
            Vector2 dashDir = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.right;

            targetBodyScale.x *= 1f + dashStretch * Mathf.Abs(dashDir.x);
            targetBodyScale.y *= 1f + dashStretch * Mathf.Abs(dashDir.y);

            targetBodyScale.x *= 1f - dashSquash * Mathf.Abs(dashDir.y);
            targetBodyScale.y *= 1f - dashSquash * Mathf.Abs(dashDir.x);
        }

        if (landPunchTimer > 0f)
        {
            float t = 1f - (landPunchTimer / Mathf.Max(0.0001f, landPunchDuration));
            float punch = Mathf.Sin(t * Mathf.PI) * landPunchStrength;

            targetBodyScale.x *= 1f + landSquashX * punch;
            targetBodyScale.y *= 1f - landSquashY * punch;

            landPunchTimer -= Time.deltaTime;
        }

        if (jumpBurstTimer > 0f)
        {
            float t = jumpBurstTimer / Mathf.Max(0.0001f, jumpBurstDur);
            targetBodyScale.y *= 1f + jumpBurstYAmount * t;
            targetBodyScale.x *= 1f - jumpBurstXAmount * t;
            jumpBurstTimer -= Time.deltaTime;
        }

        if (dashBurstTimer > 0f)
        {
            float t = dashBurstTimer / Mathf.Max(0.0001f, dashBurstDuration);
            Vector2 dir = dashBurstDirection;

            targetBodyScale.x *= 1f + dashBurstStretch * Mathf.Abs(dir.x) * t;
            targetBodyScale.y *= 1f + dashBurstStretch * Mathf.Abs(dir.y) * t;

            targetBodyScale.x *= 1f - dashSquash * Mathf.Abs(dir.y) * t;
            targetBodyScale.y *= 1f - dashSquash * Mathf.Abs(dir.x) * t;

            dashBurstTimer -= Time.deltaTime;
        }

        Vector2 motionDirWorld;
        if (velocity.sqrMagnitude > 0.001f)
            motionDirWorld = velocity.normalized;
        else
            motionDirWorld = (playerController != null && !playerController.FacingRight) ? Vector2.left : Vector2.right;

        float lagT = Mathf.Clamp01(velocity.magnitude / Mathf.Max(0.01f, horizontalSpeedForMax));

        // 把世界方向换成当前显示层级的本地方向
        float visualFlipX = 1f;
        if (bubbleVisualRoot != null)
            visualFlipX = Mathf.Sign(bubbleVisualRoot.lossyScale.x);

        Vector2 motionDirLocal = new Vector2(motionDirWorld.x * visualFlipX, motionDirWorld.y);
        if (motionDirLocal.sqrMagnitude > 0.0001f)
            motionDirLocal.Normalize();

        // 目标位置：内核领先
        Vector3 targetCorePos = coreBasePos + (Vector3)(motionDirLocal * coreLagDistance * lagT);

        // 如果方向变化比较明显，给内核一个轻微冲量，制造回摆感
        if (lagT > coreKickMinSpeed)
        {
            float dirDot = Vector2.Dot(lastMotionDirLocal, motionDirLocal);
            if (dirDot < 0.55f)
            {
                currentCoreVelocity += (Vector3)(motionDirLocal * coreDirectionChangeKick);
            }
        }

        // 用弹簧方式更新内核位置
        Vector3 coreDelta = targetCorePos - currentCorePos;
        currentCoreVelocity += coreDelta * coreSpringStrength * Time.deltaTime;
        currentCoreVelocity *= Mathf.Exp(-coreSpringDamping * Time.deltaTime);
        currentCorePos += currentCoreVelocity * Time.deltaTime;

        // 高光继续普通滞后
        Vector3 targetHighlightPos = highlightBasePos - (Vector3)(motionDirLocal * highlightLagDistance * lagT);
        targetHighlightPos += Vector3.up * highlightUpBias * (grounded ? 0.25f : 1f);

        float lerpT = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);

        currentBodyScale = Vector3.Lerp(currentBodyScale, targetBodyScale, lerpT);
        currentHighlightPos = Vector3.Lerp(currentHighlightPos, targetHighlightPos, lerpT);

        bubbleBody.localScale = currentBodyScale;

        if (bubbleCore != null)
            bubbleCore.localPosition = currentCorePos;

        if (bubbleHighlight != null)
            bubbleHighlight.localPosition = currentHighlightPos;

        lastMotionDirLocal = motionDirLocal;
        lastGrounded = grounded;
    }

    private void UpdateSorting()
    {
        int baseOrder = 1000;
        if (playerController != null)
            baseOrder = playerController.renderOrder;

        if (bodyRenderer != null)
            bodyRenderer.sortingOrder = baseOrder + bodyOrderOffset;

        if (coreRenderer != null)
            coreRenderer.sortingOrder = baseOrder + coreOrderOffset;

        if (highlightRenderer != null)
            highlightRenderer.sortingOrder = baseOrder + highlightOrderOffset;
    }

    public void PlayNormalJumpPop()
    {
        jumpBurstTimer = jumpBurstDuration;
        jumpBurstDur = jumpBurstDuration;
        jumpBurstYAmount = jumpBurstY;
        jumpBurstXAmount = jumpBurstX;
    }

    public void PlayDoubleJumpPop()
    {
        jumpBurstTimer = doubleJumpBurstDuration;
        jumpBurstDur = doubleJumpBurstDuration;
        jumpBurstYAmount = doubleJumpBurstY;
        jumpBurstXAmount = doubleJumpBurstX;
    }

    public void PlayDashPop(Vector2 dashVelocity)
    {
        dashBurstDirection = dashVelocity.sqrMagnitude > 0.001f ? dashVelocity.normalized : Vector2.right;
        dashBurstTimer = dashBurstDuration;
    }
}