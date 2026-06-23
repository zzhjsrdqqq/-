using UnityEngine;

/// <summary>
/// 移动平台 - 在两个点之间来回移动
/// 挂载到平台对象上
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;

    private bool goingToB = true;

    void Update()
    {
        Vector3 target = goingToB ? pointB.position : pointA.position;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.05f)
            goingToB = !goingToB;
    }

    // 让玩家跟随平台移动
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            collision.transform.SetParent(transform);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            collision.transform.SetParent(null);
    }
}
