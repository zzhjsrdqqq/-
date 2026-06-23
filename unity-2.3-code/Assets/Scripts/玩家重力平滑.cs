using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody2D))]
public class GravitySmoothing: MonoBehaviour
{
    [Header("下落更利落")]
    public float fallMultiplier = 2.4f;

    [Header("松开跳跃键后，上升更快结束")]
    public float lowJumpMultiplier = 1.9f;

    [Header("接近顶点时的速度阈值")]
    public float apexThreshold = 1.2f;

    [Header("顶点重力倍率（越小越柔）")]
    public float apexGravityMultiplier = 0.65f;

    [Header("最大下落速度")]
    public float maxFallSpeed = 18f;

    [Header("检测到起跳/二段跳后，多久内禁用顶点缓和")]
    public float jumpResetTime = 0.10f;

    [Header("检测到冲刺后，多久内禁用顶点缓和")]
    public float dashResetTime = 0.08f;

    [Header("判定为新一次起跳的最小竖直速度突增")]
    public float jumpVelocityBoostThreshold = 6f;

    [Header("判定为冲刺的最小水平速度")]
    public float dashDetectSpeed = 16f;

    private Rigidbody2D rb;
    private Vector2 prevVelocity;
    private float suppressApexTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        prevVelocity = rb.linearVelocity;
    }

    void FixedUpdate()
    {
        Vector2 rawVelocity = rb.linearVelocity;

        // 1) 侦测“新一次起跳 / 二段跳”
        float deltaY = rawVelocity.y - prevVelocity.y;
        if (rawVelocity.y > 0f && deltaY > jumpVelocityBoostThreshold)
        {
            suppressApexTimer = jumpResetTime;
        }

        // 2) 侦测“冲刺”
        if (Mathf.Abs(rawVelocity.x) >= dashDetectSpeed &&
            Mathf.Abs(prevVelocity.x) < dashDetectSpeed)
        {
            suppressApexTimer = dashResetTime;
        }

        if (suppressApexTimer > 0f)
            suppressApexTimer -= Time.fixedDeltaTime;

        Vector2 v = rawVelocity;

        bool jumpHeld =
            Input.GetKey(KeyCode.Space) ||
            Input.GetKey(KeyCode.W) ||
            Input.GetKey(KeyCode.UpArrow);

        // 下落：更利落
        if (v.y < 0f)
        {
            v.y += Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        // 上升中但已经松手：更快结束上升
        else if (v.y > 0f && !jumpHeld)
        {
            v.y += Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }
        // 接近顶点：做一点柔和，但刚二段跳 / 冲刺后先禁用
        else if (Mathf.Abs(v.y) < apexThreshold && suppressApexTimer <= 0f)
        {
            v.y += Physics2D.gravity.y * (apexGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }

        // 限制最大下落速度
        if (v.y < -maxFallSpeed)
            v.y = -maxFallSpeed;

        rb.linearVelocity = v;
        prevVelocity = rb.linearVelocity;
    }
}