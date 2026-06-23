using UnityEngine;

/// <summary>
/// 自动来回移动平台，无需外部路径点。
/// 挂载到平台 GameObject 上，在 Inspector 设置移动方向、距离和速度即可。
/// </summary>
public class AutoMovingPlatform : MonoBehaviour
{
    public enum MoveAxis { Horizontal, Vertical }

    [Header("移动设置")]
    [Tooltip("移动方向：Horizontal = 左右，Vertical = 上下")]
    public MoveAxis axis = MoveAxis.Horizontal;

    [Tooltip("从初始位置向正方向移动的距离（单位 Unity unit）")]
    public float distance = 3f;

    [Tooltip("移动速度（unit/s）")]
    public float speed = 2f;

    [Tooltip("启动延迟（秒），错开多平台同步")]
    public float startDelay = 0f;

    private Vector3 posA;
    private Vector3 posB;
    private bool    goingToB = true;
    private bool    started  = false;
    private float   timer    = 0f;

    void Start()
    {
        posA = transform.position;
        posB = posA + (axis == MoveAxis.Horizontal ? Vector3.right : Vector3.up) * distance;
        timer = 0f;
        started = (startDelay <= 0f);
    }

    void Update()
    {
        if (!started)
        {
            timer += Time.deltaTime;
            if (timer >= startDelay) started = true;
            return;
        }

        Vector3 target = goingToB ? posB : posA;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.01f)
            goingToB = !goingToB;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
            col.transform.SetParent(transform);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
            col.transform.SetParent(null);
    }

    // 在编辑器中可视化移动范围
    void OnDrawGizmosSelected()
    {
        Vector3 a = Application.isPlaying ? posA : transform.position;
        Vector3 dir = axis == MoveAxis.Horizontal ? Vector3.right : Vector3.up;
        Vector3 b = a + dir * distance;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.15f);
        Gizmos.DrawWireSphere(b, 0.15f);
    }
}
