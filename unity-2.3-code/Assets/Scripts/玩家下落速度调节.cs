using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BetterFall : MonoBehaviour
{
    [Header("下落加速倍率")]
    public float fallMultiplier = 2.5f;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        // 只有在下落时才额外加速
        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }
}