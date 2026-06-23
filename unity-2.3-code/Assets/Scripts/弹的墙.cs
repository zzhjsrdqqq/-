using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BounceWall : MonoBehaviour
{
    [Header("这面墙自己的水平弹力")]
    public float bounceSpeed = 14f;

    [Header("这面墙自己的斜上弹力")]
    [Tooltip("小于等于 0 时，使用玩家默认的 diagonalBounceSpeed")]
    public float diagonalBounceSpeed = 0f;

    private Collider2D wallCol;

    void Start()
    {
        wallCol = GetComponent<Collider2D>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryBounce(collision);
    }

    private void TryBounce(Collision2D collision)
    {
        if (collision == null || wallCol == null)
            return;

        PlayerWallBounceControl control = collision.collider.GetComponentInParent<PlayerWallBounceControl>();
        if (control == null)
            return;

        float dx = collision.collider.transform.position.x - wallCol.bounds.center.x;

        Vector2 wallNormal;
        if (dx >= 0f)
            wallNormal = Vector2.right;   // 玩家在墙右边，往右弹
        else
            wallNormal = Vector2.left;    // 玩家在墙左边，往左弹

        if (diagonalBounceSpeed > 0f)
            control.TryBounceHorizontal(wallNormal, bounceSpeed, diagonalBounceSpeed);
        else
            control.TryBounceHorizontal(wallNormal, bounceSpeed);
    }
}